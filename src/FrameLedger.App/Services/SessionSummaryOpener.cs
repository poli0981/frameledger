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
/// <c>Show()</c> so the window appears at once and fills in.
/// </summary>
public sealed class SessionSummaryOpener : ISessionSummaryOpener
{
    private readonly IServiceProvider _services;
    private readonly ShellHost _shell;

    public SessionSummaryOpener(IServiceProvider services, ShellHost shell)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
    }

    public void Open(long sessionId)
    {
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
        window.Show();
        _ = LoadAsync(viewModel, sessionId);
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
