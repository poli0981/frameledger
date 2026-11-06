// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

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
        GameId = recent.Row.GameId;
        StartedAt = recent.Row.StartedAt;
        GameName = recent.GameName;
        DateText = Formats.Date(recent.Row.StartedAt);
        IsHooked = recent.Row.Tier == CaptureTier.Hooked;
        TierText = IsHooked ? Strings.Tier_Hooked : Strings.Tier_NotHooked;
        ReadoutLine = FpsPresentation.FromRow(recent.Row).Line;
        SettingsChangedMidSession = recent.Row.SettingsChangedMidSession;
        SettingsChangedText = SettingsChangedMidSession ? Strings.Compare_MidSession_Changed : "—";
        HasFrameGeneration = recent.Row.FgMode switch
        {
            "none" => false,
            "na" or null => null,
            _ => true,
        };
    }

    public Application.Persistence.SessionRow Row { get; }

    public long Id { get; }

    public long GameId { get; }

    /// <summary>The date column sorts by this, not by its text (beta.34).</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>beta.34 (D85): frame generation in the session — true, false (measured none), or null (not measured: Tier 2).</summary>
    public bool? HasFrameGeneration { get; }

    /// <summary>The Settings-changed cell's words beside its icon, or a dash — the column was blank for every unchanged session.</summary>
    public string SettingsChangedText { get; }

    public string GameName { get; }

    public string DateText { get; }

    public bool IsHooked { get; }

    public string TierText { get; }

    public string ReadoutLine { get; }

    public bool SettingsChangedMidSession { get; }

    public string Label => GameName + " · " + DateText + " (" + TierText + ")";
}
