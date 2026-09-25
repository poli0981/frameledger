namespace FrameLedger.Application.Capture;

/// <summary>What <see cref="IDriverProfileSource"/> read for one executable.</summary>
public sealed record DriverProfileReading
{
    /// <summary>Nothing was asked: no source composed.</summary>
    public static readonly DriverProfileReading NotRead = new() { Outcome = DriverProfileOutcome.NotRead };

    public required DriverProfileOutcome Outcome { get; init; }

    /// <summary>The profile the driver applies, as the driver names it; null when none was read.</summary>
    public string? ProfileName { get; init; }

    /// <summary>The NvAPI status behind a degraded answer (or the lookup's, for the global branch).</summary>
    public int? NvapiStatus { get; init; }

    /// <summary>One entry per id asked, in that order; empty unless the outcome is <see cref="DriverProfileOutcome.Application"/> or <see cref="DriverProfileOutcome.Global"/>.</summary>
    public IReadOnlyList<DriverSettingReading> Settings { get; init; } = [];
}
