// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.App.Update;

namespace FrameLedger.App.Services;

/// <summary>
/// What the shell's banners say to a screen reader (beta.35, D89): the capture agent going away and coming back, and an
/// update's steps. Only a change is said — the first state the window shows is read with the window, a download's percent
/// ticks are one step, and a rebuilt window (a language change) re-reads the same state and says nothing.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed class ShellAnnouncements(IAnnouncer announcer)
{
    private readonly IAnnouncer _announcer = announcer ?? throw new ArgumentNullException(nameof(announcer));
    private bool _agentSeen;
    private bool _agentBanner;
    private UpdateStage? _update;

    /// <summary>The agent's state changed; <paramref name="banner"/> is the banner's text, or null when there is none.</summary>
    public void Agent(AgentConnectionState state, string? banner)
    {
        bool shown = banner is not null;
        if (!_agentSeen)
        {
            (_agentSeen, _agentBanner) = (true, shown);
            return;
        }

        if (shown && !_agentBanner)
        {
            _announcer.Announce(SpokenText.TitleAndBody(Strings.Agent_Banner_Offline_Title, banner), AnnouncementKind.Alert, "Agent");
        }
        else if (!shown && _agentBanner && state == AgentConnectionState.Connected)
        {
            _announcer.Announce(Strings.A11y_Agent_Connected, AnnouncementKind.Status, "Agent");
        }

        _agentBanner = shown;
    }

    /// <summary>The updater's step changed or ticked; <paramref name="banner"/> is the banner's text for that step.</summary>
    public void Update(UpdateStage stage, string? version, string banner)
    {
        UpdateStage? before = _update;
        _update = stage;
        if (before is null || before == stage)
        {
            return;
        }

        string? said = stage switch
        {
            UpdateStage.Downloading => string.Format(CultureInfo.CurrentCulture, Strings.A11y_Update_Downloading_Format, version ?? string.Empty),
            UpdateStage.Ready or UpdateStage.Deferred or UpdateStage.Applying => SpokenText.TitleAndBody(Strings.Update_Banner_Title, banner),
            _ => null,
        };
        if (said is not null)
        {
            _announcer.Announce(said, AnnouncementKind.Status, "Update");
        }
    }
}
