using System.IO;
using System.Windows;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.Controls;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Settings;
using FrameLedger.Domain.Metrics;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Shared.Ipc;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Markup;

namespace FrameLedger.App.Tests;

/// <summary>
/// Every page's XAML and the two control templates load under the real theme dictionaries — the failure a
/// binding typo or a missing resource key produces is a XamlParseException at first navigation, which no
/// view-model test sees. Runs on an STA thread with an <c>Application</c> of its own.
/// </summary>
public sealed class PagesLoadTests
{
    private sealed class NoAgent : IAgentLink
    {
        public AgentConnectionState State => AgentConnectionState.Missing;

        public StatusAck? Status => null;

        public HelloAck? Hello => null;

        public bool IsConnected => false;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public event EventHandler<AgentEventArgs>? EventReceived
        {
            add { }
            remove { }
        }

        public Task<IpcEnvelope> RequestAsync<TRequest>(string type, TRequest payload, CancellationToken ct = default)
            where TRequest : class => throw new NotSupportedException();
    }

    private sealed class NoNavigation : IPageNavigator
    {
        public void Navigate<TPage>()
            where TPage : class
        {
        }

        public void GoBack()
        {
        }
    }

    private sealed class NoPrompt : IConsentPrompt
    {
        public Task<bool> ShowAsync(string gameName, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class NoConfirm : IConfirmations
    {
        public Task<RemoveGameChoice> RemoveGameAsync(string gameName, CancellationToken ct = default) => Task.FromResult(RemoveGameChoice.Cancel);
    }

    private sealed class NoEdit : IEditGamePrompt
    {
        public Task<GameMetadata?> EditAsync(GameMetadata current, string? provenanceJson, CancellationToken ct = default) => Task.FromResult<GameMetadata?>(null);
    }

    private sealed class NoStrip : IMessageStrip
    {
        public void Info(string title, string body)
        {
        }

        public void Success(string title, string body)
        {
        }

        public void Warn(string title, string body)
        {
        }
    }

    private sealed class NoSummaries : ISessionSummaryOpener
    {
        public void Open(long sessionId)
        {
        }
    }

    private sealed class NoMixed : IMixedTierPrompt
    {
        public Task<bool> AcknowledgeAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class NoSaver : IFileSaver
    {
        public string? PickSavePath(string filter, string suggestedName) => null;
    }

    private sealed class NoPicker : IGamePicker
    {
        public string? PickExecutable() => null;
    }

    private sealed class NoTheme : IThemeApplier
    {
        public void Apply(AppTheme theme, Window? window)
        {
        }
    }

    private sealed class NoRun : IRunAtLogon
    {
        public bool IsSet => false;

        public void Apply(bool enabled)
        {
        }
    }

    private sealed class NoMaintenance : IMaintenanceState
    {
        public Task<MaintenanceSnapshot> ReadAsync(CancellationToken ct = default) => Task.FromResult(new MaintenanceSnapshot(false, false, Infrastructure.Startup.LogonTaskState.NotInstalled));
    }

    private sealed class NoTool : IAgentTool
    {
        public Task<AgentToolResult> RunAsync(string flag, CancellationToken ct = default) => Task.FromResult(new AgentToolResult(-1, "no agent in this test"));
    }

    private sealed class NoFirstRun : IFirstRunFlow
    {
        public Task<bool> IsRequiredAsync(CancellationToken ct = default) => Task.FromResult(false);

        public Task<bool> RunAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task ShowDocumentsAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>
    /// ONE STA thread with a running dispatcher for the whole class. The <c>Application</c> and its merged
    /// dictionaries are created on it once; every render is queued to it. A thread per test used to work only
    /// while a single test touched a given style: a Setter's <c>SolidColorBrush</c> with a <c>DynamicResource</c>
    /// colour cannot be frozen, so the first thread to instantiate it owns it and the second thread's
    /// <c>Measure</c> throws "Cannot access Freezable across threads" (measured 2026-09-14 when a second test
    /// rendered a <c>ui:Button</c>).
    /// </summary>
    private static readonly System.Collections.Concurrent.BlockingCollection<Action> _staQueue = StartSta();

    private static System.Collections.Concurrent.BlockingCollection<Action> StartSta()
    {
        var queue = new System.Collections.Concurrent.BlockingCollection<Action>();
        var thread = new Thread(() =>
        {
            foreach (Action job in queue.GetConsumingEnumerable())
            {
                job();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return queue;
    }

    private static Task<T> OnStaAsync<T>(Func<T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _staQueue.Add(() =>
        {
            try
            {
                if (System.Windows.Application.Current is null)
                {
                    var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    app.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark });
                    app.Resources.MergedDictionaries.Add(new ControlsDictionary());
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/FrameLedger;component/Styles/FrameLedger.xaml") });
                }

                tcs.SetResult(work());
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    /// <summary>The two PR-8a pages' view models, loaded (the Logs tail over a directory with no file).</summary>
    private static async Task<(SettingsViewModel Settings, LogsViewModel Logs)> SettingsAndLogsAsync(ScratchLedger s, HookingConsent consent, IMessageStrip strip)
    {
        var store = new SqliteSettingsStore(s.Db);
        var appearance = new AppearanceSettings(store);
        await appearance.LoadAsync().ConfigureAwait(false);
        var settings = new SettingsViewModel(appearance, new NoTheme(), new ShellHost(new ServiceCollection().BuildServiceProvider(), null!, null!, new WindowClosePolicy()), new RegisteredSettings(store), s.Library, consent, new NoAgent(), new NoRun(), strip, new NoMaintenance(), new NoTool(), new WindowClosePolicy(), new NoFirstRun());
        Task pending1 = settings.Pending;
        await pending1.ConfigureAwait(false);
        var logs = new LogsViewModel(new LogTail(Path.Combine(Path.GetTempPath(), "fl-nologs-" + Guid.NewGuid().ToString("N"))), new BugBundleBuilder(Path.GetTempPath(), new RegisteredSettings(store)), new NoSaver(), new NoAgent(), strip);
        Task pending2 = logs.Pending;
        await pending2.ConfigureAwait(false);
        return (settings, logs);
    }

    /// <summary>The first-run steps (PR-9): the gate, then the Agent step, under the same dictionaries.</summary>
    private static void RenderFirstRun(ScratchLedger s)
    {
        using var firstRun = new FirstRunViewModel(new LegalGate(new SqliteLegalAcceptanceStore(s.Db), LegalDocuments.Load()), new NoAgent(), readOnly: false);
        Render(new FirstRunContent(firstRun));
        firstRun.NextCommand.Execute(null);
        Render(new FirstRunContent(firstRun));
    }

    /// <summary>The two control templates (PR-5), rendered on the STA thread.</summary>
    private static void RenderControls(GameDetailViewModel detail)
    {
        var readout = new FpsReadout { Model = FpsPresentation.FromRow(detail.Sessions[0].Row) };
        Render(readout);
        var chip = new TriStateChip { Model = new TriStateChipModel("RT", Tri.Yes, Application.TriState.TriStateSource.Measured) };
        Render(chip);
    }

    private static void Render(FrameworkElement element)
    {
        element.Measure(new Size(1200, 900));
        element.Arrange(new Rect(0, 0, 1200, 900));
        element.UpdateLayout();
    }

    /// <summary>
    /// WPF UI 4.3.0's <c>InfoBar</c> template has no <c>ContentPresenter</c>, so the action button 08_UI
    /// §Notifications policy puts on every persistent banner — the one dismissal of a safety notice, since the X is
    /// off by design — was never rendered: a refusal was a red bar nobody could close. <c>Styles/FrameLedger.xaml</c>
    /// re-templates the control with the presenter. Both halves are asserted: the app's template renders the
    /// Content, the library's does not — so this turns red the day upstream renders it, which is the day the
    /// override comes out rather than staying by habit.
    /// </summary>
    [Fact]
    public async Task TheInfoBarTemplateRendersItsContent()
    {
        (bool ours, bool library) = await OnStaAsync(() =>
        {
            Wpf.Ui.Controls.InfoBar ours = NoticeWithAction();
            Render(ours);

            Wpf.Ui.Controls.InfoBar library = NoticeWithAction();
            library.Style = (Style)new ControlsDictionary()[typeof(Wpf.Ui.Controls.InfoBar)];
            Render(library);

            return (HasRenderedActionButton(ours), HasRenderedActionButton(library));
        });

        ours.Should().BeTrue("the app's InfoBar template must render the action button, or a safety notice cannot be dismissed");
        library.Should().BeFalse("WPF UI 4.3.0 drops InfoBar.Content; when this turns true the override in Styles/FrameLedger.xaml is no longer needed");
    }

    private static Wpf.Ui.Controls.InfoBar NoticeWithAction() => new()
    {
        IsOpen = true,
        IsClosable = false,
        Severity = Wpf.Ui.Controls.InfoBarSeverity.Error,
        Title = "Game: hooking refused",
        Message = "Easy Anti-Cheat was detected in this game.",
        Content = new Wpf.Ui.Controls.Button { Content = "Record this session without measuring it" },
    };

    private static bool HasRenderedActionButton(DependencyObject root)
    {
        if (root is Wpf.Ui.Controls.Button { Content: string, Visibility: Visibility.Visible, ActualWidth: > 0 })
        {
            return true;
        }

        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            if (HasRenderedActionButton(System.Windows.Media.VisualTreeHelper.GetChild(root, i)))
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public async Task EveryPageLoadsUnderTheThemeDictionaries()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-1), fgMode: "dlssg", native: 62, displayed: 118, factor: 1.9);
        await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-3), hooked: false);
        var selection = new GameSelection { GameId = game.Id };
        var nav = new NoNavigation();
        var strip = new NoStrip();
        var addGame = new AddGameFlow(s.Library, new NoPicker(), selection, nav, strip);
        var consent = new HookingConsent(new NoAgent(), new NoPrompt());

        // The view models load off the STA thread; the pages bind to them on it.
        using var dashboard = new DashboardViewModel(new NoAgent(), s.Library, selection, nav, new NoSummaries(), strip);
        Task pending1 = dashboard.Pending;
        await pending1;
        var games = new GamesViewModel(s.Library, selection, nav, addGame);
        Task pending2 = games.Pending;
        await pending2;
        var detail = new GameDetailViewModel(s.Library, selection, consent, nav, new NoConfirm(), new NoEdit(), strip, new NoSummaries(),
            new SessionSeriesLoader(s.Sessions), new Infrastructure.Persistence.SqliteHardwareSnapshotRepository(s.Db));
        var compare = new CompareViewModel(s.Library, new SessionSeriesLoader(s.Sessions), new NoMixed(), new NoSaver(), strip);
        Task pending3 = detail.Pending;
        await pending3;
        Task pending4 = compare.Pending;
        await pending4;
        (SettingsViewModel settings, LogsViewModel logs) = await SettingsAndLogsAsync(s, consent, strip);
        using LogsViewModel _ = logs;

        string loaded = await OnStaAsync(() =>
        {
            Render(new DashboardPage(dashboard));
            Render(new GamesPage(games));
            Render(new GameDetailPage(detail));
            Render(new ComparePage(compare));
            Render(new SettingsPage(settings));
            Render(new LogsPage(logs));
            RenderFirstRun(s);
            RenderControls(detail);
            return "dashboard,games,detail,settings,logs,firstrun,readout,chip";
        });

        loaded.Should().Be("dashboard,games,detail,settings,logs,firstrun,readout,chip");
        settings.MinSessionSeconds.Should().Be(30);
        games.Games.Should().ContainSingle();
        detail.Sessions.Should().HaveCount(2);
        detail.Sessions.Should().Contain(static r => r.TierText == Strings.Tier_NotHooked);
    }
    [Fact]
    public async Task TheChartsDrawThroughSkiaUnderTheThemeDictionaries()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        long withFrames = await s.SessionWithFramesAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-2), frames: 3000, spikeEvery: 400);
        SessionSeries series = (await new SessionSeriesLoader(s.Sessions).LoadAsync(withFrames, TestContext.Current.CancellationToken))!;

        // A missing native library or a wrong ScottPlot member is red here, not at the first summary window.
        int drawn = await OnStaAsync(() =>
        {
            var frametime = new FrametimeChart();
            frametime.Show(series, displayed: true, sensors: true);
            Render(frametime);
            using ScottPlot.Image image = frametime.ScottPlot.GetImage(640, 320);
            var distribution = new DistributionChart();
            distribution.Show(series);
            Render(distribution);
            using ScottPlot.Image histogram = distribution.HistogramPlot.GetImage(320, 240);
            var trend = new TrendChart();
            trend.Show([new TrendPoint(DateTimeOffset.UtcNow.AddDays(-1), 60, 1, false), new TrendPoint(DateTimeOffset.UtcNow, 65, 2, false)], [new HardwareChange(DateTimeOffset.UtcNow, "GPU driver: a → b")], "avg");
            Render(trend);
            using ScottPlot.Image trendImage = trend.ScottPlot.GetImage(640, 320);
            var sensors = new SensorsChart();
            sensors.Show(series);
            Render(sensors);
            var latency = new LatencyChart();
            latency.Show(series, 12_000, 18_000);
            Render(latency);
            var compareChart = new CompareChart();
            compareChart.Show([new CompareCurve("a", [0, 50, 100], [30, 60, 90])], Strings.Compare_Mixed_Legend);
            Render(compareChart);
            using ScottPlot.Image compareImage = compareChart.ScottPlot.GetImage(640, 320);
            return frametime.DrawnPoints + (sensors.DrawnSeries > 0 ? 0 : 1_000_000) + (trend.DrawnPoints == 2 ? 0 : 1_000_000) + (compareChart.DrawnCurves == 1 ? 0 : 1_000_000);
        });

        drawn.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(Decimator.DefaultBuckets * 2 + 2100, "FR-5.3: decimated per draw (the series, the markers, the sensors)");
        series.Stutter!.Count(static f => f).Should().Be(7);
    }
}
