using FrameLedger.Shared;

namespace FrameLedger.Application.Capture;

/// <summary>One drain tick's view of the session so far (<see cref="ICaptureObserver.Tick"/>).</summary>
public sealed record CaptureProgress
{
    public required IReadOnlyList<FlFrameRecord> Records { get; init; }

    /// <summary>Indices into <see cref="Records"/> whose record follows a torn slot or a drop.</summary>
    public required IReadOnlyList<int> GapBefore { get; init; }

    public required FlWriterState WriterState { get; init; }

    public required long TotalDropped { get; init; }

    public required long TotalGaps { get; init; }

    public required long DrainTicks { get; init; }

    public required long ForegroundTicks { get; init; }

    public required uint GuardTicksPublished { get; init; }

    public required IReadOnlyList<long> TouchQpc { get; init; }

    public required RuntimeModuleSet RuntimeModules { get; init; }

    public required NgxDriverState NgxDriver { get; init; }

    /// <summary>The tick of a session that never attached (Tier 2, 2026-09-22): no records, no ring, nothing measured.</summary>
    public static CaptureProgress Unhooked { get; } = new()
    {
        Records = [],
        GapBefore = [],
        WriterState = default,
        TotalDropped = 0,
        TotalGaps = 0,
        DrainTicks = 0,
        ForegroundTicks = 0,
        GuardTicksPublished = 0,
        TouchQpc = [],
        RuntimeModules = RuntimeModuleSet.Empty,
        NgxDriver = NgxDriverState.NotRun,
    };
}
