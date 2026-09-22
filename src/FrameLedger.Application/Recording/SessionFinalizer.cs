using FrameLedger.Application.Persistence;
using FrameLedger.Application.Telemetry;
using FrameLedger.Domain.Metrics;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.Application.Recording;

/// <summary>
/// <c>04_CAPTURE</c> §Finalizing, steps 2–5: segments and aggregates (<see cref="SessionAggregator"/>), the
/// FULL <c>frame_blobs</c> set and the sensor series through the codec, then one transaction — and the
/// discard rule in front of all of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Step 1 is the caller's</b>: the loop already asked the Overlay to stop and drained once more before
/// it let go of the ring; what arrives here is final. <b>Step 5's delete of the <c>.partial</c> is the
/// caller's too</b>, because only the caller holds the file, and it deletes only on
/// <see cref="FinalizeStatus.Saved"/>, <see cref="FinalizeStatus.Discarded"/> or <see cref="FinalizeStatus.GameRemoved"/>.
/// </para>
/// <para>
/// <b>Every series in <c>06_DATA_MODEL</c> is written, or is null because nothing measured it</b> — never
/// skipped for convenience. <c>latency_us</c> was once omitted from this step while the schema declared a
/// column for it; <c>SessionFinalizerTests</c> now lists the columns against the codec's output.
/// </para>
/// </remarks>
public sealed class SessionFinalizer
{
    public const int DefaultRetentionKeep = 20;

    /// <summary>Sensor series: a tick with no reading is stored as −1, never 0 (temperatures, loads, power and memory are never negative).</summary>
    public const float SensorMissing = -1f;

    public static readonly TimeSpan MinimumSessionLength = TimeSpan.FromSeconds(30);

    private readonly ISessionRepository _sessions;
    private readonly ISeriesCodec _codec;

    public SessionFinalizer(ISessionRepository sessions, ISeriesCodec codec)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    /// <summary>Whether the discard rule applies.</summary>
    public static bool IsTooShort(SessionRow row, TimeSpan? minimum = null)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.EndedAt - row.StartedAt < (minimum ?? MinimumSessionLength);
    }

    /// <summary>Steps 2–4, pure: what the transaction will write.</summary>
    public FinalizedSession Build(FinalizeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        SessionRow row = input.Skeleton;
        if (input.Hooked is null)
        {
            // A Tier-2 row has no frames and every sensor the machine provided (2026-09-22): the same averages a
            // hooked row gets, so the sessions list can show its GPU temperature and the trend charts can use it.
            return new FinalizedSession
            {
                Row = SessionAggregator.WithSensorAggregates(row with { Tier = CaptureTier.NotHooked }, input.Sensors),
                Sensors = EncodeSensors(input.Sensors, row.QpcEpoch, row.QpcFrequency),
            };
        }

        AggregationResult r = SessionAggregator.Aggregate(row with { Tier = CaptureTier.Hooked },
            input.Hooked with { Sensors = input.Sensors });
        return new FinalizedSession
        {
            Row = r.Row,
            Segments = r.Segments,
            Frames = r.Samples.Count == 0 ? null : EncodeFrames(r.Samples, input.Hooked.GapBefore, row.QpcFrequency),
            Sensors = EncodeSensors(input.Sensors, row.QpcEpoch, row.QpcFrequency),
        };
    }

    /// <summary>
    /// The discard rule, the owner, then step 5: one transaction, then the retention sweep.
    /// </summary>
    /// <remarks>
    /// <b>The owner is looked up, not assumed (2026-09-23).</b> The skeleton carries the id the session started under,
    /// and the entry can be removed — or removed and re-imported — while the game runs: two GIRLS' FRONTLINE 2 sessions
    /// died on the foreign key that way and left their <c>.partial</c> files for a recovery that would have died the same
    /// way. The session lands on the row that owns it now (<see cref="ISessionRepository.ResolveOwnerAsync"/>), and with no
    /// owner it is <see cref="FinalizeStatus.GameRemoved"/>: the user removed the game with its sessions, and this was one.
    /// </remarks>
    public async ValueTask<FinalizeOutcome> FinalizeAsync(FinalizeInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (IsTooShort(input.Skeleton, input.MinimumSessionLength))
        {
            return new FinalizeOutcome(FinalizeStatus.Discarded, null, 0);
        }

        if (await _sessions.ExistsAsync(input.Skeleton.SessionGuid, ct).ConfigureAwait(false))
        {
            return new FinalizeOutcome(FinalizeStatus.AlreadyStored, null, 0);
        }

        long? owner = await _sessions.ResolveOwnerAsync(input.Skeleton.GameId, input.ExePath, input.Skeleton.StartedAt, ct).ConfigureAwait(false);
        if (owner is not { } gameId)
        {
            return new FinalizeOutcome(FinalizeStatus.GameRemoved, null, 0);
        }

        FinalizedSession session = Build(gameId == input.Skeleton.GameId ? input : input with { Skeleton = input.Skeleton with { GameId = gameId } });
        long id;
        try
        {
            id = await _sessions.InsertFinalizedAsync(session, ct).ConfigureAwait(false);
        }
        catch (SessionOwnerMissingException)
        {
            // Removed between the lookup and the write: the same answer, reached one statement later.
            return new FinalizeOutcome(FinalizeStatus.GameRemoved, null, 0);
        }

        int swept = await _sessions.SweepRetentionAsync(gameId, input.RetentionKeep, ct).ConfigureAwait(false);
        return new FinalizeOutcome(FinalizeStatus.Saved, id, swept, gameId);
    }

    /// <summary>The full <c>frame_blobs</c> row over every record in drained order.</summary>
    private FrameBlobs EncodeFrames(IReadOnlyList<FrameSample> samples, IReadOnlyList<int> gapBefore, long qpcFrequency)
    {
        int n = samples.Count;
        var gaps = new HashSet<int>(gapBefore);
        var frametimes = new float[n];
        var flags = new byte[n];
        var frameIndex = new uint[n];
        var swapchains = new uint[n];
        var lastQpc = new Dictionary<uint, ulong>();
        for (int i = 0; i < n; i++)
        {
            FrameSample s = samples[i];
            frameIndex[i] = s.FrameIndex;
            swapchains[i] = s.SwapchainId;
            var f = FrameFlagBits.None;
            if (lastQpc.TryGetValue(s.SwapchainId, out ulong prev) && !gaps.Contains(i) && s.Qpc > prev)
            {
                frametimes[i] = (float)((s.Qpc - prev) * 1000.0 / qpcFrequency);
            }
            else
            {
                f |= FrameFlagBits.Gap;
            }

            if (s.Claims(MeasuredFields.FgCounts) && s.FgEvaluations == 0)
            {
                f |= FrameFlagBits.Generated;
            }

            flags[i] = (byte)f;
            lastQpc[s.SwapchainId] = s.Qpc;
        }

        bool multiStream = swapchains.Distinct().Count() > 1;
        return new FrameBlobs
        {
            Codec = _codec.Tag,
            SampleCount = n,
            FrameTimes = _codec.EncodeFloat32(frametimes),
            FrameFlags = _codec.EncodeBytes(flags),
            FrameIndex = _codec.EncodeUInt32(frameIndex),
            // The explicit cast matters: `cond ? byte[] : null` converts the null ARRAY to an empty memory.
            SwapchainIds = multiStream ? new ReadOnlyMemory<byte>(_codec.EncodeUInt32(swapchains)) : (ReadOnlyMemory<byte>?)null,
            RtFlags = Series(samples, MeasuredFields.Rt, static s => (byte)s.Rt, v => _codec.EncodeBytes(v)),
            RenderRes = RenderRes(samples),
            DispatchRays = Series(samples, MeasuredFields.Rt, static s => s.DispatchRaysVolume, v => _codec.EncodeUInt32(v)),
            PsoCreated = Series(samples, MeasuredFields.Pso, static s => s.PsoCreated, v => _codec.EncodeUInt16(v)),
            VramProc = Series(samples, MeasuredFields.Vram, static s => s.VramUsedMb, v => _codec.EncodeUInt32(v)),
            LatencyUs = Series(samples, MeasuredFields.Latency, static s => s.ReflexLatencyUs, v => _codec.EncodeUInt32(v)),
        };
    }

    /// <summary>A per-frame series, present only when some record claimed the measurement; unclaimed frames carry the record's zero.</summary>
    private static ReadOnlyMemory<byte>? Series<T>(IReadOnlyList<FrameSample> samples, MeasuredFields claim, Func<FrameSample, T> value,
        Func<T[], byte[]> encode)
    {
        if (!samples.Any(s => s.Claims(claim)))
        {
            return null;
        }

        var values = new T[samples.Count];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = value(samples[i]);
        }

        return encode(values);
    }

    /// <summary>Two pairs per frame, only when the parameters were measured and either pair varies.</summary>
    private ReadOnlyMemory<byte>? RenderRes(IReadOnlyList<FrameSample> samples)
    {
        if (!samples.Any(static s => s.Claims(MeasuredFields.UpscalerParams)))
        {
            return null;
        }

        var pairs = new ushort[samples.Count * 4];
        bool varies = false;
        (ushort, ushort, ushort, ushort)? first = null;
        for (int i = 0; i < samples.Count; i++)
        {
            FrameSample s = samples[i];
            pairs[i * 4] = s.RenderW;
            pairs[i * 4 + 1] = s.RenderH;
            pairs[i * 4 + 2] = s.OutputW;
            pairs[i * 4 + 3] = s.OutputH;
            if (!s.Claims(MeasuredFields.UpscalerParams))
            {
                continue;
            }

            (ushort, ushort, ushort, ushort) tuple = (s.RenderW, s.RenderH, s.OutputW, s.OutputH);
            first ??= tuple;
            varies |= tuple != first.Value;
        }

        return varies ? new ReadOnlyMemory<byte>(_codec.EncodeUInt16(pairs)) : (ReadOnlyMemory<byte>?)null;
    }

    /// <summary><c>t_ms</c> plus one series per field any sample carried, aligned to <c>t_ms</c>, −1 where a tick had no reading.</summary>
    private List<SensorBlob> EncodeSensors(IReadOnlyList<TelemetrySample> sensors, ulong qpcEpoch, long qpcFrequency)
    {
        if (sensors.Count == 0)
        {
            return [];
        }

        var blobs = new List<SensorBlob>
        {
            Blob("t_ms", sensors, s => (float)((s.QpcTicks - (long)qpcEpoch) * 1000.0 / qpcFrequency)),
        };
        AddIfAny(blobs, "gpu_temp", sensors, static s => s.Sample.TempCoreC);
        AddIfAny(blobs, "gpu_hotspot", sensors, static s => s.Sample.TempHotspotC);
        AddIfAny(blobs, "gpu_load", sensors, static s => s.Sample.LoadPct);
        AddIfAny(blobs, "gpu_power", sensors, static s => s.Sample.PowerW);
        AddIfAny(blobs, "vram_adapter", sensors, static s => s.Sample.VramAdapterMb);
        AddIfAny(blobs, "cpu_load", sensors, static s => s.System.CpuLoadPct);
        AddIfAny(blobs, "cpu_temp", sensors, static s => s.System.CpuTempC);
        AddIfAny(blobs, "ram_mb", sensors, static s => s.System.RamUsedMb);
        return blobs;
    }

    private void AddIfAny(List<SensorBlob> into, string series, IReadOnlyList<TelemetrySample> sensors, Func<TelemetrySample, double?> field)
    {
        if (sensors.Any(s => field(s) is not null))
        {
            into.Add(Blob(series, sensors, s => field(s) is { } v ? (float)v : SensorMissing));
        }
    }

    private SensorBlob Blob(string series, IReadOnlyList<TelemetrySample> sensors, Func<TelemetrySample, float> value) => new()
    {
        Series = series,
        Hz = 1,
        Codec = _codec.Tag,
        Data = _codec.EncodeFloat32([.. sensors.Select(value)]),
    };
}
