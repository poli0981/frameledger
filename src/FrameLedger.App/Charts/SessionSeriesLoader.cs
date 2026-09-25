using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Metrics;
using FrameLedger.Infrastructure.Blobs;

namespace FrameLedger.App.Charts;

/// <summary>
/// Decodes one session's blobs into a <see cref="SessionSeries"/>: the charted stream's frametime series with the gap
/// and generated bits, the application-frame view with <c>StutterDetector</c> over it (the rule the stored
/// <c>stutter_count</c> came from), the segments mapped from frame index to seconds, and the sensors. Null when the
/// session has no frame blob (Tier 2, or swept by retention); <see cref="LoadSensorsAsync"/> reads a Tier-2 session's
/// sensors on their own.
/// </summary>
public sealed class SessionSeriesLoader
{
    private readonly ISessionRepository _sessions;

    public SessionSeriesLoader(ISessionRepository sessions) => _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));

    public async Task<SessionSeries?> LoadAsync(long sessionId, CancellationToken ct = default)
    {
        FrameBlobs? frames = await _sessions.FindFramesAsync(sessionId, ct).ConfigureAwait(false);
        if (frames is null || !string.Equals(frames.Codec, SeriesCodec.Tag, StringComparison.Ordinal))
        {
            return null;
        }

        IReadOnlyList<SegmentRow> segments = await _sessions.FindSegmentsAsync(sessionId, ct).ConfigureAwait(false);
        IReadOnlyList<SensorBlob> sensors = await _sessions.FindSensorsAsync(sessionId, ct).ConfigureAwait(false);
        return Decode(frames, segments, sensors);
    }

    /// <summary>
    /// The sensor series alone, timed from the session's start — what a session that was not hooked has to chart (beta.8: the
    /// Tier-2 row has kept its sensors since 2026-09-22, and no chart read them). Empty when none were recorded.
    /// </summary>
    public async Task<IReadOnlyList<SensorSeries>> LoadSensorsAsync(long sessionId, CancellationToken ct = default) =>
        DecodeSensors(await _sessions.FindSensorsAsync(sessionId, ct).ConfigureAwait(false), offsetS: 0);

    public static SessionSeries Decode(FrameBlobs frames, IReadOnlyList<SegmentRow> segments, IReadOnlyList<SensorBlob> sensors)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(sensors);
        float[] allFrametimes = SeriesCodec.DecodeFloat32(frames.FrameTimes.ToArray());
        byte[] allFlags = SeriesCodec.DecodeBytes(frames.FrameFlags.ToArray());
        int total = Math.Min(allFrametimes.Length, allFlags.Length);
        uint[]? swapchains = frames.SwapchainIds is { } ids ? SeriesCodec.DecodeUInt32(ids.ToArray()) : null;
        (int[] keep, uint? swapchain) = ChartedStream(total, swapchains);

        float[] frametimes = Pick(allFrametimes, keep);
        byte[] flags = Pick(allFlags, keep);
        int n = keep.Length;
        var times = new double[n];
        var generated = new bool[n];
        var gap = new bool[n];
        double t = 0;
        for (int i = 0; i < n; i++)
        {
            t += frametimes[i] / 1000.0;
            times[i] = t;
            generated[i] = (flags[i] & (byte)FrameFlagBits.Generated) != 0;
            gap[i] = (flags[i] & (byte)FrameFlagBits.Gap) != 0;
        }

        (List<int> appIndex, List<double> appFrametimes, bool noApplicationFrames) = ApplicationFrames(frametimes, generated, gap);
        StutterResult? stutter = StutterDetector.Detect(appFrametimes);
        uint[]? frameIndex = frames.FrameIndex is { } fi ? PickOptional(SeriesCodec.DecodeUInt32(fi.ToArray()), keep) : null;
        double? offsetS = frames.FirstPresentMs / 1000.0;
        return new SessionSeries
        {
            TimesS = times,
            FrameTimesMs = frametimes,
            Generated = generated,
            Gap = gap,
            FrameIndex = frameIndex,
            RtFlags = frames.RtFlags is { } rt ? PickOptional(SeriesCodec.DecodeBytes(rt.ToArray()), keep) : null,
            DispatchRays = frames.DispatchRays is { } dr ? PickOptional(SeriesCodec.DecodeUInt32(dr.ToArray()), keep) : null,
            PsoCreated = frames.PsoCreated is { } pso ? PickOptional(SeriesCodec.DecodeUInt16(pso.ToArray()), keep) : null,
            VramProcMb = frames.VramProc is { } vram ? PickOptional(SeriesCodec.DecodeUInt32(vram.ToArray()), keep) : null,
            LatencyUs = frames.LatencyUs is { } lat ? PickOptional(SeriesCodec.DecodeUInt32(lat.ToArray()), keep) : null,
            RenderRes = frames.RenderRes is { } rr ? PickQuads(SeriesCodec.DecodeUInt16(rr.ToArray()), keep) : null,
            AppTimesS = [.. appIndex.Select(i => times[i])],
            AppFrameTimesMs = [.. appFrametimes],
            AppPresentIndex = [.. appIndex],
            NoApplicationFrames = noApplicationFrames,
            Stutter = stutter is null ? null : [.. stutter.IsStutter],
            Segments = MapSegments([.. segments.Where(s => swapchain is null || s.SwapchainId == swapchain.Value)], times, frameIndex),
            Sensors = DecodeSensors(sensors, offsetS ?? 0),
            SensorsAligned = offsetS is not null,
        };
    }

    /// <summary>
    /// The presents of the stream the charts draw — the one the session's statistics are over (<c>SegmentBuilder.DominantStream</c>:
    /// identified first, then the most presents) — as indices into the blob, and that stream's id; every present, and no id,
    /// when the blob held one stream.
    /// </summary>
    internal static (int[] Keep, uint? Swapchain) ChartedStream(int count, uint[]? swapchains)
    {
        if (swapchains is null || swapchains.Length < count)
        {
            return ([.. Enumerable.Range(0, count)], null);
        }

        IReadOnlyList<int> keep = SegmentBuilder.DominantStream([.. Enumerable.Range(0, count)], i => swapchains[i]);
        return ([.. keep], keep.Count > 0 ? swapchains[keep[0]] : null);
    }

    /// <summary>
    /// The application-frame view: each application frame's time is from the previous application frame's present to its own —
    /// the generated presents between them add their intervals to it, never an interval of their own — and a gap breaks the
    /// chain. Where every present was generated, every present's interval, and the flag that says so.
    /// </summary>
    internal static (List<int> Index, List<double> FrametimesMs, bool NoApplicationFrames) ApplicationFrames(float[] frametimes, bool[] generated, bool[] gap)
    {
        ArgumentNullException.ThrowIfNull(frametimes);
        ArgumentNullException.ThrowIfNull(generated);
        ArgumentNullException.ThrowIfNull(gap);
        bool noApplicationFrames = generated.Length > 0 && generated.All(static g => g);
        var index = new List<int>(frametimes.Length);
        var ms = new List<double>(frametimes.Length);
        bool chain = false;
        double sinceApplicationFrame = 0;
        for (int i = 0; i < frametimes.Length; i++)
        {
            if (gap[i])
            {
                chain = false;
                sinceApplicationFrame = 0;
            }
            else
            {
                sinceApplicationFrame += frametimes[i];
            }

            if (generated[i] && !noApplicationFrames)
            {
                continue;
            }

            if (chain && sinceApplicationFrame > 0)
            {
                index.Add(i);
                ms.Add(sinceApplicationFrame);
            }

            chain = true;
            sinceApplicationFrame = 0;
        }

        return (index, ms, noApplicationFrames);
    }

    private static T[] Pick<T>(T[] values, int[] keep)
    {
        if (keep.Length == values.Length)
        {
            return values;
        }

        var picked = new T[keep.Length];
        for (int i = 0; i < keep.Length; i++)
        {
            picked[i] = values[keep[i]];
        }

        return picked;
    }

    /// <summary>A per-present series the blob may have written shorter than the frames: only the kept presents it covers.</summary>
    private static T[] PickOptional<T>(T[] values, int[] keep) => keep.Length == values.Length ? values : [.. keep.Where(i => i < values.Length).Select(i => values[i])];

    private static ushort[] PickQuads(ushort[] values, int[] keep)
    {
        if (keep.Length * 4 == values.Length)
        {
            return values;
        }

        var picked = new List<ushort>(keep.Length * 4);
        foreach (int i in keep.Where(i => (i * 4) + 3 < values.Length))
        {
            picked.AddRange(values.AsSpan(i * 4, 4).ToArray());
        }

        return [.. picked];
    }

    /// <summary>A segment's <c>start_frame</c>/<c>end_frame</c> are frame indices; the present at that index (or the nearest) gives the seconds.</summary>
    private static List<SegmentSpan> MapSegments(IReadOnlyList<SegmentRow> segments, double[] times, uint[]? frameIndex)
    {
        if (times.Length == 0)
        {
            return [];
        }

        double At(long frame)
        {
            int i;
            if (frameIndex is null)
            {
                i = (int)Math.Clamp(frame, 0, times.Length - 1);
            }
            else
            {
                i = Array.BinarySearch(frameIndex, (uint)Math.Max(0, frame));
                if (i < 0)
                {
                    i = ~i;
                }

                i = Math.Clamp(i, 0, times.Length - 1);
            }

            return times[i];
        }

        return [.. segments.Select(s => new SegmentSpan(At(s.StartFrame), At(s.EndFrame), SegmentLabel(s)))];
    }

    private static string SegmentLabel(SegmentRow s)
    {
        // A segment carries no driver word: the driver's bookkeeping is per process, not per settings change.
        string upscaler = Services.Formats.Upscaler(s.Upscaler, s.UpscalerQuality, driverReported: null);

        string resolution = Services.Formats.Resolution(s.RenderW, s.RenderH, s.OutputW, s.OutputH);
        string fg = Services.Formats.FrameGeneration(s.FgMode);
        return string.Join(" · ", new[] { upscaler, resolution, fg }.Where(static x => x.Length > 0));
    }

    /// <summary>
    /// Every sensor series against <c>t_ms</c> less <paramref name="offsetS"/> — the first present's place on the sensors'
    /// clock, so they share the frames' axis (schema 0013) — and −1 dropped: a tick with no reading, never a zero.
    /// </summary>
    private static List<SensorSeries> DecodeSensors(IReadOnlyList<SensorBlob> sensors, double offsetS)
    {
        SensorBlob? clock = sensors.FirstOrDefault(static s => string.Equals(s.Series, "t_ms", StringComparison.Ordinal));
        float[]? tMs = clock is null ? null : SeriesCodec.DecodeFloat32(clock.Data.ToArray());
        var result = new List<SensorSeries>();
        foreach (SensorBlob blob in sensors)
        {
            if (string.Equals(blob.Series, "t_ms", StringComparison.Ordinal) || !string.Equals(blob.Codec, SeriesCodec.Tag, StringComparison.Ordinal))
            {
                continue;
            }

            float[] values = SeriesCodec.DecodeFloat32(blob.Data.ToArray());
            var xs = new List<double>(values.Length);
            var ys = new List<double>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] < 0)
                {
                    continue;   // SessionFinalizer.SensorMissing: a tick with no reading, never a zero
                }

                xs.Add((tMs is not null && i < tMs.Length ? tMs[i] / 1000.0 : i / Math.Max(blob.Hz, 0.001)) - offsetS);
                ys.Add(values[i]);
            }

            result.Add(new SensorSeries(blob.Series, [.. xs], [.. ys]));
        }

        return result;
    }
}
