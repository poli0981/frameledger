using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// The tray (08_UI §Shell, FR-14) without a pixel of WPF in it: the four states from the pipe's session events
/// and the pause flag, the menu's commands (Open, Pause/Resume over <c>PauseCapture</c>/<c>ResumeCapture</c>,
/// Agent status, Exit), and FR-3.8's "session saved" balloon — raised only while the window is not on screen,
/// because the Dashboard's snackbar carries the same event when it is (08_UI §Notifications policy: one event,
/// exactly one channel). A safety event is never a balloon; the notices are the shell's.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class TrayViewModel : ObservableObject, IDisposable
{
    private readonly IAgentLink _agent;
    private readonly IShellPresence _shell;
    private readonly ISessionSummaryOpener _summaries;
    private readonly IPageNavigator _navigator;
    private readonly UiThread _ui = new();
    private readonly Dictionary<Guid, (int Tier, string Name)> _running = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tooltip))]
    private TrayState _state = TrayState.Idle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseText))]
    [NotifyPropertyChangedFor(nameof(Tooltip))]
    private bool _isPaused;

    public TrayViewModel(IAgentLink agent, IShellPresence shell, ISessionSummaryOpener summaries, IPageNavigator navigator)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _summaries = summaries ?? throw new ArgumentNullException(nameof(summaries));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _agent.Changed += OnAgentChanged;
        _agent.EventReceived += OnAgentEvent;
        IsPaused = _agent.Status?.Paused ?? false;
    }

    /// <summary>A balloon to show (the host decides how); never raised while the window is on screen.</summary>
    public event EventHandler<TrayToastEventArgs>? ToastRequested;

    public string PauseText => IsPaused ? Strings.Tray_Resume : Strings.Tray_Pause;

    /// <summary>The last saved session, for the balloon's click.</summary>
    public long? LastSavedSessionId { get; private set; }

    public string Tooltip => State switch
    {
        TrayState.Paused => Strings.Tray_State_Paused,
        TrayState.Capturing => string.Format(CultureInfo.CurrentCulture, Strings.Tray_State_Capturing_Format, RunningName()),
        TrayState.RecordingOnly => string.Format(CultureInfo.CurrentCulture, Strings.Tray_State_RecordingOnly_Format, RunningName()),
        _ => Strings.Tray_State_Idle,
    };

    /// <summary>A pending pause/resume, for a test to await.</summary>
    public Task Pending { get; private set; } = Task.CompletedTask;

    public void Dispose()
    {
        _agent.Changed -= OnAgentChanged;
        _agent.EventReceived -= OnAgentEvent;
    }

    [RelayCommand]
    private void Open() => _shell.Reveal();

    [RelayCommand]
    private void AgentStatus()
    {
        _shell.Reveal();
        _navigator.Navigate<DashboardPage>();
    }

    [RelayCommand]
    private void Exit() => _shell.Quit();

    /// <summary>FR-3.9: one request, and the state the Agent answers with — never assumed.</summary>
    [RelayCommand]
    private Task TogglePauseAsync() => Pending = TogglePauseCoreAsync();

    [RelayCommand]
    private void OpenLastSaved()
    {
        if (LastSavedSessionId is long id)
        {
            _shell.Reveal();
            _summaries.Open(id);
        }
    }

    private async Task TogglePauseCoreAsync()
    {
        if (!_agent.IsConnected)
        {
            return;
        }

        try
        {
            IpcEnvelope ack = IsPaused
                ? await _agent.RequestAsync(IpcMessageType.ResumeCapture, new ResumeCaptureRequest()).ConfigureAwait(true)
                : await _agent.RequestAsync(IpcMessageType.PauseCapture, new PauseCaptureRequest()).ConfigureAwait(true);
            if (IpcCodec.Payload<PauseAck>(ack) is { } paused)
            {
                IsPaused = paused.Paused;
                Recompute();
            }
        }
        catch (Exception ex) when (ex is IpcRequestException or TimeoutException or System.IO.IOException or InvalidOperationException)
        {
            Serilog.Log.Warning(ex, "tray: pause/resume did not complete");
        }
    }

    private void OnAgentChanged(object? sender, EventArgs e) => _ui.Post(() =>
    {
        if (_agent.Status is { } status)
        {
            IsPaused = status.Paused;
        }

        if (!_agent.IsConnected)
        {
            _running.Clear();
        }

        Recompute();
    });

    private void OnAgentEvent(object? sender, AgentEventArgs e) => _ui.Post(() => Dispatch(e.Envelope));

    private void Dispatch(IpcEnvelope envelope)
    {
        switch (envelope.Type)
        {
            case IpcMessageType.SessionStarted when IpcCodec.Payload<SessionStartedEvent>(envelope) is { } started:
                _running[started.SessionGuid] = (started.Tier, started.GameName ?? Strings.Common_NotAvailable);
                Recompute();
                break;
            case IpcMessageType.SessionCompleted when IpcCodec.Payload<SessionCompletedEvent>(envelope) is { } completed:
                string name = _running.TryGetValue(completed.SessionGuid, out (int Tier, string Name) run) ? run.Name : Strings.Common_NotAvailable;
                _running.Remove(completed.SessionGuid);
                Recompute();
                if (completed.SessionId is long id)
                {
                    LastSavedSessionId = id;
                    if (!_shell.IsShown)
                    {
                        ToastRequested?.Invoke(this, new TrayToastEventArgs(Strings.Tray_SessionSaved_Title, string.Format(CultureInfo.CurrentCulture, Strings.Tray_SessionSaved_Body_Format, name), id));
                    }
                }

                break;
            default:
                break;
        }
    }

    private void Recompute() => State = IsPaused
        ? TrayState.Paused
        : _running.Values.Any(static r => r.Tier == 1)
            ? TrayState.Capturing
            : _running.Count > 0 ? TrayState.RecordingOnly : TrayState.Idle;

    private string RunningName() => _running.Values.Select(static r => r.Name).FirstOrDefault() ?? Strings.Common_NotAvailable;
}
