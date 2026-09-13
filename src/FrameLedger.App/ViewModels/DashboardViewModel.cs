using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Services;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// The Dashboard's Agent status card (08_UI §Dashboard): what <c>HelloAck</c> and <c>StatusAck</c> said, every
/// value <c>N/A</c> until the Agent answered — never a placeholder that reads as a measurement.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly AgentConnection _agent;
    private readonly UiThread _ui = new();

    [ObservableProperty]
    private string _agentState = Strings.Agent_State_Connecting;

    [ObservableProperty]
    private string _agentVersion = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _telemetrySource = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _overlayBuildId = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _elevated = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _runningSessions = Strings.Common_NotAvailable;

    public DashboardViewModel(AgentConnection agent)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _agent.Changed += OnAgentChanged;
        Refresh();
    }

    public static string Header => Strings.Dashboard_Header;

    public static string AgentHeader => Strings.Dashboard_Agent_Header;

    public static string EmptyText => Strings.Dashboard_Empty;

    public void Dispose() => _agent.Changed -= OnAgentChanged;

    private void OnAgentChanged(object? sender, EventArgs e) => _ui.Post(Refresh);

    private void Refresh()
    {
        AgentState = AgentStatusPresentation.Pill(_agent.State).Text;
        HelloAck? hello = _agent.Hello;
        StatusAck? status = _agent.Status;
        AgentVersion = hello?.AgentVersion ?? Strings.Common_NotAvailable;
        TelemetrySource = hello?.TelemetrySource ?? Strings.Common_NotAvailable;
        OverlayBuildId = hello?.OverlayBuildId ?? Strings.Common_NotAvailable;
        Elevated = hello is null ? Strings.Common_NotAvailable : hello.Elevated ? Strings.Common_Yes : Strings.Common_No;
        RunningSessions = status?.ActiveSessions.Count.ToString(CultureInfo.CurrentCulture) ?? Strings.Common_NotAvailable;
    }
}
