using FrameLedger.App.Charts;
using FrameLedger.App.ViewModels;
using FrameLedger.App.Windows;
using FrameLedger.Application.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// Builds a summary window per open: the view model's override prompt is bound to THAT window's dialog host,
/// which is why the window is composed here rather than resolved whole from DI. The load runs after
/// <c>Show()</c> so the window appears at once and fills in. It keeps the windows it opened, so that Delete all sessions
/// can close the ones whose session is gone (<see cref="ISessionWindows"/>, beta.11).
/// </summary>
public sealed class SessionSummaryOpener : ISessionSummaryOpener, ISessionWindows
{
    private readonly IServiceProvider _services;
    private readonly ShellHost _shell;

    private readonly SessionSelection _selection;

    private readonly List<(long SessionId, SessionSummaryWindow Window)> _open = [];

    public SessionSummaryOpener(IServiceProvider services, ShellHost shell, SessionSelection selection)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
    }

    public void Open(long sessionId)
    {
        _selection.Set(sessionId);    // File ▸ Export follows the window the user opened last (P4 PR-3)
        SessionSummaryWindow? window = null;
        var viewModel = new SessionSummaryViewModel(
            _services.GetRequiredService<ISessionRepository>(),
            _services.GetRequiredService<IGameRepository>(),
            _services.GetRequiredService<ISessionAnnotationRepository>(),
            _services.GetRequiredService<IHardwareSnapshotRepository>(),
            _services.GetRequiredService<SessionSeriesLoader>(),
            new TriStateOverridePrompt(() => window!.Dialogs),
            _services.GetRequiredService<IFileSaver>(),
            _services.GetRequiredService<IMessageStrip>());
        window = new SessionSummaryWindow(viewModel, _services.GetRequiredService<IThemeApplier>(), _services.GetRequiredService<AppearanceSettings>())
        {
            Owner = _shell.Current,
        };
        (long, SessionSummaryWindow) open = (sessionId, window);
        _open.Add(open);
        window.Closed += (_, _) => _open.Remove(open);
        window.Show();
        _ = LoadAsync(viewModel, sessionId);
    }

    public async Task CloseDeletedAsync(CancellationToken ct = default)
    {
        ISessionRepository sessions = _services.GetRequiredService<ISessionRepository>();
        foreach ((long SessionId, SessionSummaryWindow Window) open in _open.ToArray())
        {
            // Still open after the read: the user may have closed it meanwhile, and a closed window is not closed twice.
            if (await sessions.FindByIdAsync(open.SessionId, ct).ConfigureAwait(true) is null && _open.Contains(open))
            {
                open.Window.Close();
            }
        }
    }

    private static async Task LoadAsync(SessionSummaryViewModel viewModel, long sessionId)
    {
        try
        {
            await viewModel.LoadAsync(sessionId).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Error(ex, "ui: the session summary for {SessionId} did not load", sessionId);
        }
    }
}
