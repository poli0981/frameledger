using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.App.Update;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Import;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Settings;
using FrameLedger.Application.Watch;
using FrameLedger.Infrastructure.Diagnostics;
using FrameLedger.Infrastructure.Import;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Infrastructure.Watch;
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
    /// <summary><c>10_LOGGING</c> §Crash handling (P4 PR-9): Fatal, the minidump, the crash dialog offering the bug report.</summary>
    private readonly CrashReporter _crashes = new(
        static () => CrashDumpWriter.TryWrite(UiPaths.CrashDumps, "ui", DateTimeOffset.UtcNow, static line => Log.Warning("{Line}", line)),
        CrashDialog.AskToReport,
        static () =>
        {
            // The report's dialogs live in the shell window; one hidden in the tray would wait for a click nobody can give.
            Services.GetRequiredService<IShellPresence>().Reveal();
            return Services.GetRequiredService<BugReportFlow>().RunAsync();
        });

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
        ArgumentNullException.ThrowIfNull(e);
        base.OnStartup(e);
        UiPaths.EnsureDirectories();
        ConfigureLogging();
        HookCrashHandlers();
        // 08_UI §Accessibility (P4 PR-8): Esc closes the open ContentDialog or MessageBox, before any window exists.
        DialogKeyboard.Register();
        _run = DiagReport.Requested(e.Args) ? DiagAsync() : RunAsync();
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
            var store = new SqliteSettingsStore(_db);
            var appearance = new AppearanceSettings(store);
            await appearance.LoadAsync().ConfigureAwait(true);
            ApplyCulture(appearance.Language);
            var registered = new RegisteredSettings(store);
            LoggingLevel.SetDebug(await registered.GetBooleanAsync(SettingsRegistry.LogDebug).ConfigureAwait(true));
            var closePolicy = new WindowClosePolicy { MinimizeToTray = await registered.GetBooleanAsync(SettingsRegistry.UiMinimizeToTray).ConfigureAwait(true) };

            _host = BuildHost(_db, appearance, registered, closePolicy);
            await _host.StartAsync().ConfigureAwait(true);
            Log.Information("ui: started ({Version}), ledger {Ledger}", UiIdentity.Version, UiPaths.Database);
            await _host.WaitForShutdownAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Fatal(ex, "ui: the run ended in an exception");
            exitCode = 1;

            // 10_LOGGING §Crash handling: a start that fails is said on screen, not only in the log. A crash the dispatcher
            // already reported has had its dialog.
            if (!_crashes.Crashed)
            {
                StartupFailureDialog.Show(ex, UiPaths.Logs);
            }
        }
        finally
        {
            await TearDownAsync().ConfigureAwait(true);
            // 10_LOGGING §Crash handling: a reported crash still ends in exit code 1, after the normal teardown.
            Shutdown(_crashes.Crashed ? 1 : exitCode);
        }
    }

    /// <summary>
    /// <c>10_LOGGING</c> §Diagnostics extras: <c>--diag</c> writes the environment and capability report under
    /// <c>logs/</c> and to stdout (when a console or a pipe is attached — a WinExe has none of its own), then exits.
    /// No host, no window, no Agent: the report is this install as seen from outside.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture")]
    private async Task DiagAsync()
    {
        int exitCode = 0;
        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            long? schema = null;
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                LedgerDatabase db = await LedgerDatabase.OpenAsync(UiPaths.Database).ConfigureAwait(true);
                await using (db.ConfigureAwait(true))
                {
                    schema = db.SchemaVersion;
                    var settings = new RegisteredSettings(new SqliteSettingsStore(db));
                    foreach (SettingDefinition definition in SettingsRegistry.All)
                    {
                        values[definition.Key] = await settings.GetAsync(definition).ConfigureAwait(true);
                    }
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Warning(ex, "diag: the ledger could not be opened; settings are reported at their defaults");
            }

            string report = DiagReport.Build(UiIdentity.Version, UiPaths.DataDirectory, UiPaths.Logs, schema, values, new AgentLauncher().CanLaunch, now);
            string path = DiagReport.Write(UiPaths.Logs, report, now);
            await Console.Out.WriteLineAsync(string.Format(CultureInfo.CurrentCulture, Strings.Diag_Written_Format, path)).ConfigureAwait(true);
            Log.Information("ui: --diag written to {Path}", path);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Fatal(ex, "ui: --diag failed");
            exitCode = 1;
        }

        Shutdown(exitCode);
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

    private static IHost BuildHost(LedgerDatabase db, AppearanceSettings appearance, RegisteredSettings registered, WindowClosePolicy closePolicy)
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
        builder.Services.AddSingleton(registered);
        builder.Services.AddSingleton(closePolicy);
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
        builder.Services.AddSingleton<IAgentLink>(static sp => sp.GetRequiredService<AgentConnection>());

        // FR-2.1 (P3 PR-4): the consent dialog and the request it ends in; the toggle that opens it is the game page's (PR-5).
        builder.Services.AddSingleton<IConsentPrompt, ConsentPrompt>();
        builder.Services.AddSingleton<HookingConsent>();

        AddLibrary(builder.Services);

        // The shell: the window is TRANSIENT because a language change rebuilds it (09_I18N §Mechanics); the
        // shell host tracks the live one. Pages and their view models are transient (16 §Navigation).
        builder.Services.AddSingleton<ShellHost>();
        builder.Services.AddSingleton<IShellPresence>(static sp => sp.GetRequiredService<ShellHost>());
        builder.Services.AddSingleton<TrayViewModel>();
        builder.Services.AddSingleton<TrayHost>();
        builder.Services.AddTransient<MainWindow>();
        builder.Services.AddSingleton<MainWindowViewModel>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<GamesPage>();
        builder.Services.AddTransient<GamesViewModel>();
        builder.Services.AddTransient<GameDetailPage>();
        builder.Services.AddTransient<GameDetailViewModel>();
        builder.Services.AddTransient<ComparePage>();
        builder.Services.AddTransient<CompareViewModel>();
        builder.Services.AddTransient<LogsPage>();
        builder.Services.AddTransient<LogsViewModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddHostedService<ApplicationHostService>();
        return builder.Build();
    }

    /// <summary>
    /// The library (P3 PR-5): the ports the pages read and write, the selection that travels between pages, and the
    /// pickers, prompts and confirmations behind interfaces so the view models are testable without a window.
    /// </summary>
    private static void AddLibrary(IServiceCollection services)
    {
        services.AddSingleton<IGameRepository, SqliteGameRepository>();
        services.AddSingleton<ISessionRepository, SqliteSessionRepository>();
        services.AddSingleton<ISessionAnnotationRepository, SqliteSessionAnnotationRepository>();
        services.AddSingleton<GameLibrary>();
        services.AddSingleton<GameSelection>();
        services.AddSingleton<IPageNavigator, PageNavigator>();
        services.AddSingleton<IGamePicker, GamePicker>();
        services.AddSingleton<IConfirmations, Confirmations>();
        services.AddSingleton<AddGameFlow>();
        services.AddSingleton<IMessageStrip, SnackbarStrip>();
        services.AddSingleton<IEditGamePrompt, EditGamePrompt>();

        // The session summary (P3 PR-6): the series loader over the ports, the exports' file picker, the window opener.
        services.AddSingleton<IHardwareSnapshotRepository, SqliteHardwareSnapshotRepository>();
        services.AddSingleton<Charts.SessionSeriesLoader>();
        services.AddSingleton<IFileSaver, FileSaver>();
        services.AddSingleton<ISessionSummaryOpener, SessionSummaryOpener>();
        services.AddSingleton<IMixedTierPrompt, MixedTierPrompt>();

        // Settings, the safety notices and the Logs page (P3 PR-8a): the Run entry, the notices over the pipe's
        // events, the tail and the bundle over the logs directory.
        services.AddSingleton<IRunAtLogon, RunAtLogonRegistry>();
        services.AddSingleton<SafetyNotices>();
        services.AddSingleton(static _ => new LogTail(UiPaths.Logs));
        services.AddSingleton(static sp => new BugBundleBuilder(UiPaths.Logs, sp.GetRequiredService<RegisteredSettings>(), crashDumpDirectory: UiPaths.CrashDumps, redactor: LogRedactor.ForCurrentUser()));

        // The bug report's steps 3-4 and the shell's Export (P4 PR-3): the preview dialog, the browser, the clipboard,
        // the flow over them, the session the shell exports, and the CSV/JSON service the summary window's writers serve.
        services.AddSingleton<IUrlOpener, ShellUrlOpener>();
        services.AddSingleton<IClipboard, WpfClipboard>();
        services.AddSingleton<IBugReportPreview, BugReportPreviewPrompt>();
        services.AddSingleton<LastSessionSummary>();
        services.AddSingleton<BugReportFlow>();
        services.AddSingleton<SessionSelection>();
        services.AddSingleton<SessionExportService>();

        AddImport(services);

        // The layer registration and the logon task (P3 PR-8b): read from the machine, written by the Agent's flags.
        services.AddSingleton<IMaintenanceState, MaintenanceState>();
        services.AddSingleton<IAgentTool, AgentTool>();

        AddUpdates(services);

        // Tools ▸ Database maintenance (P4 PR-7): the dialog builds its view model over the ledger each time it opens.
        services.AddSingleton<IDatabaseMaintenancePrompts, DatabaseMaintenancePrompts>();

        // FR-11 (P3 PR-9): the gate over the UI's own table, the documents this build embeds, the window that shows them.
        services.AddSingleton<ILegalAcceptanceStore, SqliteLegalAcceptanceStore>();
        services.AddSingleton(static sp => new LegalGate(sp.GetRequiredService<ILegalAcceptanceStore>(), LegalDocuments.Load()));
        services.AddSingleton<IFirstRunFlow, FirstRunFlow>();
    }

    /// <summary>
    /// The updater (P4 PR-5, <c>11_UPDATER</c>): Velopack behind the <see cref="IUpdateClient"/> port, the dialogs behind
    /// <see cref="IUpdatePrompts"/>, the flow that holds FR-12, and the startup check as a hosted service.
    /// </summary>
    private static void AddUpdates(IServiceCollection services)
    {
        services.AddSingleton<IUpdateClient, VelopackUpdateClient>();
        services.AddSingleton<IUpdatePrompts, UpdatePrompts>();
        services.AddSingleton<UpdateService>();
        services.AddHostedService<UpdateHostedService>();
    }

    /// <summary>FR-1.2's library import (P4 PR-4): the stores, the executable guess, the importer over the games port, the review checklist behind a port.</summary>
    private static void AddImport(IServiceCollection services)
    {
        // FR-1.2's library import (P4 PR-4): the four stores read from their own local files and keys, the executable
        // guess for the two that name none, the importer over the games port, the review checklist behind a port.
        services.AddSingleton<IStoreLibrarySource>(static _ => new SteamLibrarySource());
        services.AddSingleton<IStoreLibrarySource>(static _ => new GogLibrarySource());
        services.AddSingleton<IStoreLibrarySource>(static _ => new EpicLibrarySource());
        services.AddSingleton<IStoreLibrarySource>(static _ => new ItchLibrarySource());
        services.AddSingleton<IExecutableLocator, ExecutableLocator>();
        services.AddSingleton<IExecutableIdentitySource, ExecutableIdentitySource>();
        services.AddSingleton(static sp => new LibraryImporter(
            sp.GetServices<IStoreLibrarySource>(),
            sp.GetRequiredService<IGameRepository>(),
            sp.GetRequiredService<IExecutableIdentitySource>(),
            sp.GetRequiredService<IExecutableLocator>(),
            static line => Serilog.Log.Information("{Line}", line)));
        services.AddSingleton<IImportReview, ImportReviewPrompt>();
        services.AddSingleton<ImportLibraryFlow>();
    }

    /// <summary><c>10_LOGGING</c> §Serilog configuration: <c>logs/ui-.log</c>, daily, 7 kept, 10 MB, the one template; the level follows <c>log.debug</c> at runtime.</summary>
    private static void ConfigureLogging() =>
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(LoggingLevel.Switch)
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

    /// <summary>
    /// <c>10_LOGGING</c> §Crash handling (P4 PR-9). An exception on the dispatcher is reported: Fatal, the minidump, the
    /// crash dialog offering the bug report; the App then closes with exit code 1 through the host's own teardown. One on
    /// any other thread is logged and dumped only, because the runtime is already ending the process and no dialog can be
    /// relied on to appear; the bug report offers that dump the next time it runs. An unobserved task is not fatal and
    /// stays an Error line.
    /// </summary>
    private void HookCrashHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += static (_, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "ui: unhandled exception (AppDomain, terminating {Terminating})", args.IsTerminating);
            string? dump = CrashDumpWriter.TryWrite(UiPaths.CrashDumps, "ui", DateTimeOffset.UtcNow, static line => Log.Warning("{Line}", line));
            Log.Information("ui: crash dump {Dump}", dump ?? "not written");
            Log.CloseAndFlush();
        };
        TaskScheduler.UnobservedTaskException += static (_, args) => Log.Error(args.Exception, "ui: unobserved task exception");
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Handled, so the process lives long enough to ask; the exit code is still 1.
        e.Handled = true;
        _ = ReportCrashAsync(e.Exception);
    }

    /// <summary>The report, then the host's stop (<see cref="RunAsync"/> tears down and exits with 1); with no host, straight to the exit.</summary>
    private async Task ReportCrashAsync(Exception exception)
    {
        try
        {
            if (!await _crashes.HandleAsync(exception, "Dispatcher").ConfigureAwait(true))
            {
                return;
            }

            if (_host is not null)
            {
                _host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
                return;
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            Log.Warning(ex, "ui: the host could not be stopped after the crash report");
        }

        Shutdown(1);
    }
}
