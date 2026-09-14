using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>How the pill and the banner show <see cref="AgentConnectionState"/> — one table, so the view models agree.</summary>
public static class AgentStatusPresentation
{
    /// <summary>The pill's text and colour.</summary>
    public static (string Text, ControlAppearance Appearance) Pill(AgentConnectionState state) => state switch
    {
        AgentConnectionState.Connected => (Strings.Agent_State_Connected, ControlAppearance.Success),
        AgentConnectionState.Connecting => (Strings.Agent_State_Connecting, ControlAppearance.Info),
        AgentConnectionState.Starting => (Strings.Agent_State_Starting, ControlAppearance.Info),
        AgentConnectionState.Offline => (Strings.Agent_State_Offline, ControlAppearance.Caution),
        AgentConnectionState.Missing => (Strings.Agent_State_Missing, ControlAppearance.Danger),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "not a connection state"),
    };

    /// <summary>The persistent <c>InfoBar</c>'s body (08_UI §Notifications: "Agent offline" is in-app, persistent), or null when no banner is due.</summary>
    public static string? Banner(AgentConnectionState state) => state switch
    {
        AgentConnectionState.Offline => Strings.Agent_Banner_Offline_Body,
        AgentConnectionState.Missing => Strings.Agent_Banner_Missing_Body,
        _ => null,
    };
}
