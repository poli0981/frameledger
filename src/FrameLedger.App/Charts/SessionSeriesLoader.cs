using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Metrics;
using FrameLedger.Infrastructure.Blobs;

namespace FrameLedger.App.Charts;

/// <summary>
/// Decodes one session's blobs into a <see cref="SessionSeries"/>: the frametime series with the gap and
/// generated bits, the application-frame view with <c>StutterDetector</c> over it (the same rule the stored
/// <c>stutter_count</c> came from), the segments mapped from frame index to seconds, and the sensors aligned to
/// their <c>t_ms</c>. Null when the session has no frame blob (Tier 2, or swept by retention).
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

    public static SessionSeries Decode(FrameBlobs frames, IReadOnlyList<SegmentRow> segments, IReadOnlyList<SensorBlob> sensors)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(sensors);
        float[] frametimes = SeriesCodec.DecodeFloat32(frames.FrameTimes.ToArray());
        byte[] flags = SeriesCodec.DecodeBytes(frames.FrameFlags.ToArray());
        int n = Math.Min(frametimes.Length, flags.Length);
        var times = new double[n];
        var generated = new bool[n];
        var gap = new bool[n];
        double t = 0;
        var appTimes = new List<double>(n);
        var appFrametimes = new List<double>(n);
        var appIndex = new List<int>(n);
        for (int i = 0; i < n; i++)
        {
            t += frametimes[i] / 1000.0;
            times[i] = t;
            generated[i] = (flags[i] & (byte)FrameFlagBits.Generated) != 0;
            gap[i] = (flags[i] & (byte)FrameFlagBits.Gap) != 0;
            if (i > 0 && !generated[i] && !gap[i] && frametimes[i] > 0)
            {
                appTimes.Add(t);
                appFrametimes.Add(frametimes[i]);
                appIndex.Add(i);
            }
        }

        StutterResult? stutter = StutterDetector.Detect(appFrametimes);
        uint[]? frameIndex = frames.FrameIndex is { } fi ? SeriesCodec.DecodeUInt32(fi.ToArray()) : null;
        return new SessionSeries
        {
            TimesS = times,
            FrameTimesMs = frametimes.Length == n ? frametimes : frametimes[..n],
            Generated = generated,
            Gap = gap,
            FrameIndex = frameIndex,
            RtFlags = frames.RtFlags is { } rt ? SeriesCodec.DecodeBytes(rt.ToArray()) : null,
            DispatchRays = frames.DispatchRays is { } dr ? SeriesCodec.DecodeUInt32(dr.ToArray()) : null,
            PsoCreated = frames.PsoCreated is { } pso ? SeriesCodec.DecodeUInt16(pso.ToArray()) : null,
            VramProcMb = frames.VramProc is { } vram ? SeriesCodec.DecodeUInt32(vram.ToArray()) : null,
            LatencyUs = frames.LatencyUs is { } lat ? SeriesCodec.DecodeUInt32(lat.ToArray()) : null,
            RenderRes = frames.RenderRes is { } rr ? SeriesCodec.DecodeUInt16(rr.ToArray()) : null,
            AppTimesS = [.. appTimes],
            AppFrameTimesMs = [.. appFrametimes],
            AppPresentIndex = [.. appIndex],
            Stutter = stutter is null ? null : [.. stutter.IsStutter],
            Segments = MapSegments(segments, times, frameIndex),
            Sensors = DecodeSensors(sensors),
        };
    }

    /// <summary>A segment's <c>start_frame</c>/<c>end_frame</c> are frame indices; the present at that index (or the nearest) gives the seconds.</summary>
    private static IReadOnlyList<SegmentSpan> MapSegments(IReadOnlyList<SegmentRow> segments, double[] times, uint[]? frameIndex)
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

    private static List<SensorSeries> DecodeSensors(IReadOnlyList<SensorBlob> sensors)
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

                xs.Add(tMs is not null && i < tMs.Length ? tMs[i] / 1000.0 : i / Math.Max(blob.Hz, 0.001));
                ys.Add(values[i]);
            }

            result.Add(new SensorSeries(blob.Series, [.. xs], [.. ys]));
        }

        return result;
    }
}
