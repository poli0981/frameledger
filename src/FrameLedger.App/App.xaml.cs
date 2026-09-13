using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace FrameLedger.App;

/// <summary>
/// The application (P3 PR-2): a .NET Generic Host owns everything, per <c>docs/16_WPFUI_SYNTAX.md</c> §App
/// bootstrap. Startup opens the ledger the Agent owns (<c>06_DATA_MODEL</c> §Writer ownership: the App reads it
/// all and writes only its own tables), reads the UI culture from <c>settings</c> BEFORE any window exists,
/// then starts the host; <see cref="ApplicationHostService"/> shows the shell.
/// </summary>
/// <remarks>
/// FULLY QUALIFIED base type, deliberately: this project's namespace is <c>FrameLedger.App</c> and there is a
/// <c>FrameLedger.Application</c> namespace beside it, so the bare name <c>Application</c> resolves to that
/// NAMESPACE rather than to the WPF type (CS0118) — sibling namespaces win over a <c>using</c>.
/// </remarks>
public partial class App : System.Windows.Application
{
    private IHost? _host;
    private LedgerDatabase? _db;
    private Task? _run;

    /// <summary>The composition, for the few places XAML cannot reach DI (the shell rebuild, the crash path).</summary>
    public static IServiceProvider Services =>
        ((App)Current)._host?.Services ?? throw new InvalidOperationException("the host is not started");

    /// <summary>
    /// WPF's startup is synchronous and the host's is not, so the whole run is one task on the dispatcher:
    /// open, start, wait for the host's shutdown (the shell host requests it when the live window closes, the
    /// Exit command requests it directly), tear down, and only then <see cref="System.Windows.Application.Shutdown()"/>.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        UiPaths.EnsureDirectories();
        ConfigureLogging();
        HookCrashHandlers();
        _run = RunAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Log.Information("ui: exit {Code} (run {Status})", e.ApplicationExitCode, _run?.Status);
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private async Task RunAsync()
    {
        int exitCode = 0;
        try
        {
            _db = await LedgerDatabase.OpenAsync(UiPaths.Database).ConfigureAwait(true);
            var appearance = new AppearanceSettings(new SqliteSettingsStore(_db));
            await appearance.LoadAsync().ConfigureAwait(true);
            ApplyCulture(appearance.Language);

            _host = BuildHost(_db, appearance);
            await _host.StartAsync().ConfigureAwait(true);
            Log.Information("ui: started ({Version}), ledger {Ledger}", UiIdentity.Version, UiPaths.Database);
            await _host.WaitForShutdownAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Fatal(ex, "ui: the run ended in an exception");
            exitCode = 1;
        }
        finally
        {
            await TearDownAsync().ConfigureAwait(true);
            Shutdown(exitCode);
        }
    }

    private async Task TearDownAsync()
    {
        try
        {
            if (_host is not null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
                _host.Dispose();
                _host = null;
            }

            if (_db is not null)
            {
                await _db.DisposeAsync().ConfigureAwait(true);
                _db = null;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "ui: shutdown was not clean");
        }
    }

    /// <summary>The UI culture, once, before the first window: resources are read at XAML load.</summary>
    public static void ApplyCulture(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Strings.Culture = culture;
    }

    private static IHost BuildHost(LedgerDatabase db, AppearanceSettings appearance)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddSerilog();

        // WPF UI: the page provider resolves pages from DI, the three services are the shell's (16 §Main window skeleton).
        builder.Services.AddNavigationViewPageProvider();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<ISnackbarService, SnackbarService>();
        builder.Services.AddSingleton<IContentDialogService, ContentDialogService>();

        // The ledger and the App's own settings (06_DATA_MODEL §Writer ownership: the App writes `settings`).
        builder.Services.AddSingleton(db);
        builder.Services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
        builder.Services.AddSingleton(appearance);
        builder.Services.AddSingleton<IThemeApplier, WpfThemeApplier>();

        // The Agent over the pipe (07_IPC §Client behavior): connect, start it when it is not there, tell the shell.
        builder.Services.AddSingleton<IAgentLauncher, AgentLauncher>();
        builder.Services.AddSingleton(static sp => new AgentConnection(
            sp.GetRequiredService<IAgentLauncher>(),
            static () => new PipeClient(),
            new AgentConnectionOptions(),
            UiIdentity.Version));
        builder.Services.AddHostedService<AgentConnectionHostedService>();
        builder.Services.AddSingleton<IAgentRequests>(static sp => sp.GetRequiredService<AgentConnection>());

        // FR-2.1 (P3 PR-4): the consent dialog and the request it ends in; the toggle that opens it is the game page's (PR-5).
        builder.Services.AddSingleton<IConsentPrompt, ConsentPrompt>();
        builder.Services.AddSingleton<HookingConsent>();

        // The shell: the window is TRANSIENT because a language change rebuilds it (09_I18N §Mechanics); the
        // shell host tracks the live one. Pages and their view models are transient (16 §Navigation).
        builder.Services.AddSingleton<ShellHost>();
        builder.Services.AddTransient<MainWindow>();
        builder.Services.AddSingleton<MainWindowViewModel>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<GamesPage>();
        builder.Services.AddTransient<GamesViewModel>();
        builder.Services.AddTransient<ComparePage>();
        builder.Services.AddTransient<CompareViewModel>();
        builder.Services.AddTransient<LogsPage>();
        builder.Services.AddTransient<LogsViewModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddHostedService<ApplicationHostService>();
        return builder.Build();
    }

    /// <summary><c>10_LOGGING</c> §Serilog configuration: <c>logs/ui-.log</c>, daily, 7 kept, 10 MB, the one template.</summary>
    private static void ConfigureLogging() =>
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("Process", "ui")
            .WriteTo.File(
                Path.Combine(UiPaths.Logs, "ui-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}")
            .CreateLogger();

    /// <summary><c>10_LOGGING</c> §Crash handling: every unhandled path is logged as Fatal. The dialog and the minidump are P4's.</summary>
    private void HookCrashHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += static (_, args) => Log.Fatal(args.ExceptionObject as Exception, "ui: unhandled exception (AppDomain)");
        TaskScheduler.UnobservedTaskException += static (_, args) => Log.Error(args.Exception, "ui: unobserved task exception");
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) =>
        Log.Fatal(e.Exception, "ui: unhandled exception (Dispatcher)");
}
