using System.Globalization;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Metrics;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Telemetry;
using FrameLedger.Domain.Metrics;
using FrameLedger.Shared;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Ipc;

/// <summary>
/// One <c>SessionProgress</c> from one drain tick: the last <see cref="WindowSeconds"/> of the ring's records
/// through the SAME calculators the stored row uses (<c>FrameTimeSeries</c>, <c>FgWindow</c>, the FG ladder,
/// <c>UpscaleExtent</c>) — a live number that disagreed with the row afterwards would be a second implementation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 6 at the wire.</b> Native / Displayed / factor are set only when the ladder's verdict is one the row
/// would publish (<c>SessionAggregator.ApplyFg</c>'s <c>usable</c>); otherwise the presented rate stands alone
/// with its census qualifier, which is <see cref="FgLadder.PresentedQualifier"/> — the row's, not a copy.
/// </para>
/// <para>
/// Pure and allocation-tolerant: it runs once a second on the loop's task, over a few thousand records at most.
/// </para>
/// </remarks>
public static class SessionProgressCalculator
{
    public const double WindowSeconds = 5;

    public static SessionProgressEvent Compute(Guid sessionGuid, CaptureProgress progress, long qpcFrequency, double elapsedSeconds, GpuSample? gpu)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qpcFrequency);

        Window w = Window.Of(progress, qpcFrequency);
        FgWindow? fg = w.All.Count > 0 ? FgWindow.From(w.All, qpcFrequency) : null;
        string? withheld = fg?.IsNone == true
            ? FgLadder.WithholdNone((FlRuntimeCensus)progress.WriterState.RuntimeCensus, progress.RuntimeModules, progress.WriterState)
            : null;
        FlFgMode? identity = FgLadder.Identity(w.DominantRecords);
        FgVerdict verdict = FgLadder.Resolve(identity, fg, withheld);
        bool usable = fg is { Refusal: null } && verdict is FgVerdict.Named or FgVerdict.ActiveUnidentified or FgVerdict.None or FgVerdict.NoneInputsTagged;

        UpscaleExtent? extent = UpscaleExtent.From(w.Dominant);
        FlUpscaler? upscaler = FgLadder.UpscalerIdentity(progress.Records);
        IReadOnlyList<FrameSample> withParams = [.. w.Dominant.Where(static s => s.Claims(MeasuredFields.UpscalerParams))];

        return new SessionProgressEvent
        {
            SessionGuid = sessionGuid,
            ElapsedS = elapsedSeconds,
            Presents5s = w.Records.Count,
            PresentedFps5s = w.Series?.AverageFps,
            PresentedQualifier = FgLadder.PresentedQualifier(progress.WriterState, withheld),
            NativeFps5s = usable ? fg!.NativeFps : null,
            DisplayedFps5s = usable ? fg!.DisplayedFps : null,
            FgFactor = usable ? fg!.Factor : null,
            FgMode = Vocabulary.FgMode(verdict, identity),
            Upscaler = upscaler is { } u ? Vocabulary.Upscaler(u) : FgLadder.UpscalerHookRan(progress.Records) ? "unknown" : null,
            UpscalerQuality = Modal(withParams)?.ToString(CultureInfo.InvariantCulture),
            RenderW = extent?.RenderW,
            RenderH = extent?.RenderH,
            OutputW = extent?.OutputW,
            OutputH = extent?.OutputH,
            RtActive = RtActive(w.All),
            GpuTempC = gpu?.TempCoreC,
            CpuTempC = null,
            VramProcMb = Newest(w.All, MeasuredFields.Vram, static s => s.VramUsedMb > 0 ? (int)s.VramUsedMb : null),
            LatencyUs = Newest(w.All, MeasuredFields.Latency, static s => s.ReflexLatencyUs > 0 ? (int)s.ReflexLatencyUs : null),
        };
    }

    /// <summary>Null when nothing in the window claimed a ray-tracing measurement; otherwise whether any evidence bit is set.</summary>
    private static bool? RtActive(IReadOnlyList<FrameSample> samples)
    {
        bool claimed = false;
        foreach (FrameSample s in samples)
        {
            if (!s.Claims(MeasuredFields.Rt))
            {
                continue;
            }

            claimed = true;
            if (s.Rt != RtEvidenceBits.None)
            {
                return true;
            }
        }

        return claimed ? false : null;
    }

    /// <summary>The newest sample claiming <paramref name="field"/> whose value reads as measured.</summary>
    private static int? Newest(IReadOnlyList<FrameSample> samples, MeasuredFields field, Func<FrameSample, int?> read)
    {
        for (int i = samples.Count - 1; i >= 0; i--)
        {
            if (samples[i].Claims(field) && read(samples[i]) is { } value)
            {
                return value;
            }
        }

        return null;
    }

    private static int? Modal(IReadOnlyList<FrameSample> samples) =>
        samples.Count == 0
            ? null
            : samples.GroupBy(static s => s.UpscalerQuality).OrderByDescending(static g => g.Count()).ThenBy(static g => g.Key).First().Key;

    /// <summary>The last <see cref="WindowSeconds"/> of records, and the dominant stream within them with its gaps.</summary>
    private sealed class Window
    {
        private Window(IReadOnlyList<FlFrameRecord> records, IReadOnlyList<FlFrameRecord> dominantRecords, HashSet<int> dominantGaps, long qpcFrequency)
        {
            Records = records;
            DominantRecords = dominantRecords;
            All = FrameSampleMapper.Map(records);
            Dominant = FrameSampleMapper.Map(dominantRecords);
            Series = Dominant.Count > 0 ? FrameTimeSeries.From(Dominant, dominantGaps, qpcFrequency) : null;
        }

        public IReadOnlyList<FlFrameRecord> Records { get; }

        public IReadOnlyList<FlFrameRecord> DominantRecords { get; }

        public IReadOnlyList<FrameSample> All { get; }

        public IReadOnlyList<FrameSample> Dominant { get; }

        public FrameTimeSeries? Series { get; }

        public static Window Of(CaptureProgress progress, long qpcFrequency)
        {
            IReadOnlyList<FlFrameRecord> all = progress.Records;
            if (all.Count == 0)
            {
                return new Window([], [], [], qpcFrequency);
            }

            ulong span = (ulong)(WindowSeconds * qpcFrequency);
            ulong last = all[^1].Qpc;
            ulong cutoff = last > span ? last - span : 0;
            int start = all.Count - 1;
            while (start > 0 && all[start - 1].Qpc >= cutoff)
            {
                start--;
            }

            var records = new FlFrameRecord[all.Count - start];
            for (int i = 0; i < records.Length; i++)
            {
                records[i] = all[start + i];
            }

            HashSet<int> gaps = [];
            foreach (int g in progress.GapBefore)
            {
                if (g >= start)
                {
                    gaps.Add(g - start);
                }
            }

            uint dominantId = DominantSwapchain(records);
            var dominant = new List<FlFrameRecord>(records.Length);
            HashSet<int> dominantGaps = [];
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].SwapchainId != dominantId)
                {
                    continue;
                }

                if (gaps.Contains(i))
                {
                    dominantGaps.Add(dominant.Count);
                }

                dominant.Add(records[i]);
            }

            return new Window(records, dominant, dominantGaps, qpcFrequency);
        }

        /// <summary>The swapchain with the most records in the window; ties go to the one seen first.</summary>
        private static uint DominantSwapchain(FlFrameRecord[] records)
        {
            Dictionary<uint, int> counts = [];
            uint best = records[0].SwapchainId;
            int bestCount = 0;
            foreach (FlFrameRecord r in records)
            {
                int n = counts.GetValueOrDefault(r.SwapchainId) + 1;
                counts[r.SwapchainId] = n;
                if (n > bestCount)
                {
                    bestCount = n;
                    best = r.SwapchainId;
                }
            }

            return best;
        }
    }
}
