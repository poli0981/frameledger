using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Services;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.ViewModels;

/// <summary>One session in Compare's picker: what it is, its tier, its one FPS line, its FR-6.4 warning, and whether it is ticked.</summary>
public sealed partial class CompareCandidateViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public CompareCandidateViewModel(RecentSession recent)
    {
        ArgumentNullException.ThrowIfNull(recent);
        Row = recent.Row;
        Id = recent.Row.Id;
        GameName = recent.GameName;
        DateText = Formats.Date(recent.Row.StartedAt);
        IsHooked = recent.Row.Tier == CaptureTier.Hooked;
        TierText = IsHooked ? Strings.Tier_Hooked : Strings.Tier_NotHooked;
        ReadoutLine = FpsPresentation.FromRow(recent.Row).Line;
        SettingsChangedMidSession = recent.Row.SettingsChangedMidSession;
    }

    public Application.Persistence.SessionRow Row { get; }

    public long Id { get; }

    public string GameName { get; }

    public string DateText { get; }

    public bool IsHooked { get; }

    public string TierText { get; }

    public string ReadoutLine { get; }

    public bool SettingsChangedMidSession { get; }

    public string Label => GameName + " · " + DateText + " (" + TierText + ")";
}
