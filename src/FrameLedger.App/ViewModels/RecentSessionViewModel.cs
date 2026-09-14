using FrameLedger.App.Services;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.ViewModels;

/// <summary>One line of the Dashboard's recent list: game, when, how long, the tier, the one FPS line under the display rule, a crash mark.</summary>
public sealed class RecentSessionViewModel
{
    public RecentSessionViewModel(RecentSession recent)
    {
        ArgumentNullException.ThrowIfNull(recent);
        Id = recent.Row.Id;
        GameId = recent.Row.GameId;
        GameName = recent.GameName;
        DateText = Formats.Date(recent.Row.StartedAt);
        DurationText = Formats.Duration(recent.Row.DurationSeconds);
        IsHooked = recent.Row.Tier == CaptureTier.Hooked;
        TierText = IsHooked ? Strings.Tier_Hooked : Strings.Tier_NotHooked;
        Readout = FpsPresentation.FromRow(recent.Row);
        IsCrashed = recent.Row.ExitStatus == ExitStatus.Crashed;
    }

    public long Id { get; }

    public long GameId { get; }

    public string GameName { get; }

    public string DateText { get; }

    public string DurationText { get; }

    public bool IsHooked { get; }

    public string TierText { get; }

    public FpsReadoutModel Readout { get; }

    public bool IsCrashed { get; }
}
