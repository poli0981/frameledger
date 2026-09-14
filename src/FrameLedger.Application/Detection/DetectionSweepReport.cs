namespace FrameLedger.Application.Detection;

/// <summary>What one <see cref="DetectionSweep.SweepOnceAsync"/> did.</summary>
public sealed record DetectionSweepReport
{
    /// <summary>Games detected and written this pass.</summary>
    public int Scanned { get; init; }

    /// <summary>Games whose cache key matched, left alone.</summary>
    public int Current { get; init; }

    /// <summary>Games whose executable could not be read, skipped.</summary>
    public int Unreadable { get; init; }

    /// <summary>The rules file could not be loaded; nothing was scanned.</summary>
    public bool RulesUnusable { get; init; }
}
