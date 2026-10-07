// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using FrameLedger.App.Charts;
using FrameLedger.App.Controls;
using FrameLedger.App.Dialogs;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.App.Windows;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Settings;
using FrameLedger.Application.TriState;
using FrameLedger.Domain.Metrics;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.App.Tests;

public sealed partial class PagesLoadTests
{
    private sealed class NoOverride : ITriStateOverridePrompt
    {
        public Task<TriStateOverrideChoice?> AskAsync(TriStateChipModel chip, CancellationToken ct = default) => Task.FromResult<TriStateOverrideChoice?>(null);
    }

    /// <summary>
    /// Every surface the App draws, built the way the App builds it (beta.17): the six pages over loaded view models, the
    /// first run, the three secondary windows, every dialog's content, the safety InfoBar and the two custom controls. The
    /// view models load off the STA thread (<see cref="LoadAsync"/>); <see cref="All"/> hands out one builder per surface,
    /// called on the STA thread — twice when a check compares a switched instance with a fresh one.
    /// </summary>
    internal sealed class Surfaces : IDisposable
    {
        private readonly List<IDisposable> _owned = [];

        private Surfaces(ScratchLedger ledger) => Ledger = ledger;

        public ScratchLedger Ledger { get; }

        public DashboardViewModel Dashboard { get; private set; } = null!;

        public GamesViewModel Games { get; private set; } = null!;

        public GameDetailViewModel Detail { get; private set; } = null!;

        public CompareViewModel Compare { get; private set; } = null!;

        public SettingsViewModel Settings { get; private set; } = null!;

        public LogsViewModel Logs { get; private set; } = null!;

        public SessionSummaryViewModel Summary { get; private set; } = null!;

        public FirstRunViewModel FirstRun { get; private set; } = null!;

        /// <summary>A game with a frame-generation session and a recorded-only one, every view model loaded over it.</summary>
        public static async Task<Surfaces> LoadAsync(ScratchLedger s)
        {
            ArgumentNullException.ThrowIfNull(s);
            GameRow game = await s.GameAsync("Alpha").ConfigureAwait(false);
            await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-1), fgMode: "dlssg", native: 62, displayed: 118, factor: 1.9).ConfigureAwait(false);
            await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-3), hooked: false).ConfigureAwait(false);
            long withFrames = await s.SessionWithFramesAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-5), frames: 600, spikeEvery: 200).ConfigureAwait(false);
            var surfaces = new Surfaces(s);
            await surfaces.LoadPagesAsync(game.Id).ConfigureAwait(false);
            surfaces.Summary = new SessionSummaryViewModel(s.Sessions, s.Games, s.Annotations, new SqliteHardwareSnapshotRepository(s.Db),
                new SessionSeriesLoader(s.Sessions), new NoOverride(), new NoSaver(), new NoStrip());
            await surfaces.Summary.LoadAsync(withFrames, CancellationToken.None).ConfigureAwait(false);
            surfaces.FirstRun = surfaces.Own(new FirstRunViewModel(new LegalGate(new SqliteLegalAcceptanceStore(s.Db), LegalDocuments.Load()), new NoAgent(), readOnly: false));
            return surfaces;
        }

        private async Task LoadPagesAsync(long gameId)
        {
            ScratchLedger s = Ledger;
            var selection = new GameSelection { GameId = gameId };
            var nav = new NoNavigation();
            var strip = new NoStrip();
            var consent = new HookingConsent(new NoAgent(), new NoPrompt());

            // The view models load off the STA thread; the pages bind to them on it.
            Dashboard = Own(new DashboardViewModel(new NoAgent(), s.Library, selection, nav, new NoSummaries(), strip));
            Task pending1 = Dashboard.Pending;
            await pending1.ConfigureAwait(false);
            Games = new GamesViewModel(s.Library, selection, nav, new AddGameFlow(s.Library, new NoPicker(), selection, nav, strip));
            Task pending2 = Games.Pending;
            await pending2.ConfigureAwait(false);
            Detail = new GameDetailViewModel(s.Library, selection, consent, nav, new NoConfirm(), new NoEdit(), strip, new NoSummaries(),
                new SessionSeriesLoader(s.Sessions), new SqliteHardwareSnapshotRepository(s.Db), new SessionSelection(), new NoPicker());
            Task pending3 = Detail.Pending;
            await pending3.ConfigureAwait(false);
            Compare = new CompareViewModel(s.Library, new SessionSeriesLoader(s.Sessions), new NoMixed(), new NoSaver(), strip);
            Task pending4 = Compare.Pending;
            await pending4.ConfigureAwait(false);
            (SettingsViewModel settings, LogsViewModel logs) = await SettingsAndLogsAsync(s, consent, strip).ConfigureAwait(false);
            Settings = settings;
            Logs = Own(logs);
        }

        /// <summary>Every surface by name, each with a builder to call on the STA thread.</summary>
        public IEnumerable<(string Name, Func<FrameworkElement> Build)> All() => [.. Pages(), .. Windows(), .. Dialogs(), .. Controls()];

        /// <summary>The six pages the navigation opens.</summary>
        public IEnumerable<(string Name, Func<FrameworkElement> Build)> Pages() =>
        [
            ("Dashboard", () => new DashboardPage(Dashboard)),
            ("Games", () => new GamesPage(Games)),
            ("Game detail", () => new GameDetailPage(Detail)),
            ("Compare", () => new ComparePage(Compare)),
            ("Settings", () => new SettingsPage(Settings, new SystemInfoViewModel(new FixedHardware(), new NoClipboard(), new NoStrip()))),
            ("Logs", () => new LogsPage(Logs)),
        ];

        /// <summary>The first run's gate and the secondary windows: About, the user guide, a session's summary.</summary>
        public IEnumerable<(string Name, Func<FrameworkElement> Build)> Windows() =>
        [
            ("First run", () => new FirstRunContent(FirstRun)),
            ("About", () => new AboutWindow(new NoUrlOpener(), new NoTheme(), new AppearanceSettings(new MemorySettings()))),
            ("User guide", () => new DocumentWindow(Strings.Guide_Title, EmbeddedDocuments.Guide(), new NoUrlOpener(), new NoTheme(), new AppearanceSettings(new MemorySettings()))),
            ("Session summary", () => new SessionSummaryWindow(Summary, new NoTheme(), new AppearanceSettings(new MemorySettings()))),
        ];

        /// <summary>Every dialog's content, over a view model of its own.</summary>
        public IEnumerable<(string Name, Func<FrameworkElement> Build)> Dialogs() =>
        [
            ("Consent", () => new ConsentDialogContent(new ConsentDialogViewModel("Alpha"))),
            ("Admin mode", () => new AgentAdminDialogContent(new AgentAdminDialogViewModel())),
            ("Anti-cheat exception", () => new AntiCheatExceptionDialogContent(new AntiCheatExceptionDialogViewModel(new AntiCheatExceptionFacts("Alpha", "NetEase Yidun", "NEP2.dll")))),
            ("Bug report preview", () => new BugReportPreviewContent(new BugReportPreviewViewModel(new BugReportPreviewModel(@"C:\data\bug-report.zip",
                ["logs/ui-20261007.log", "system.txt"], new Uri("https://github.com/poli0981/frameledger/issues/new"), "## What happened")))),
            ("Edit game", () => new EditGameContent(new EditGameViewModel(new GameMetadata { Name = "Alpha" }, provenanceJson: null))),
            ("Override", () => new TriStateOverrideContent(new TriStateOverrideViewModel(new TriStateChipModel("RT", Tri.Yes, TriStateSource.Measured)))),
            ("Bug bundle options", () => new BugBundleOptionsContent(new BugBundleOptionsViewModel(new BugBundleOffer(
                new CrashDumpInfo(@"C:\data\crashdumps\ui-20261007-010203-42.dmp", DateTimeOffset.UtcNow, 2 * 1024 * 1024), new LastSessionInfo(1, "Alpha", DateTimeOffset.UtcNow))))),
            ("Database maintenance", () => new DatabaseMaintenanceContent(new DatabaseMaintenanceViewModel(new LedgerMaintenance(Ledger.Db), Ledger.Db.Path, new NoAgent(),
                new NoSaver(), new NoMaintenancePrompts(), new RegisteredSettings(new SqliteSettingsStore(Ledger.Db))))),
            ("Import review", () => new ImportReviewContent(new ImportReviewViewModel(
            [
                new Application.Import.ImportCandidate(new Application.Import.StoreGame("steam", "1", "A", @"C:\a", @"C:\a\a.exe", null), @"C:\a\a.exe",
                    AlreadyInLibrary: false, ExecutableGuessed: false),
                new Application.Import.ImportCandidate(new Application.Import.StoreGame("steam", "2", "B", @"C:\b", @"C:\b\b.exe", null), @"C:\b\b.exe",
                    AlreadyInLibrary: true, ExecutableGuessed: false),
            ])
            { ShowExisting = true })),
        ];

        /// <summary>The safety notice's InfoBar (its own template, Styles/FrameLedger.xaml) and the two custom controls.</summary>
        public IEnumerable<(string Name, Func<FrameworkElement> Build)> Controls() =>
        [
            ("Safety InfoBar", NoticeWithAction),
            ("FPS readout", () => new FpsReadout { Model = FpsPresentation.FromRow(Detail.Sessions[0].Row) }),
            ("Tri-state chip", () => new TriStateChip { Model = new TriStateChipModel("RT", Tri.NotApplicable, TriStateSource.Measured) }),
        ];

        public void Dispose()
        {
            foreach (IDisposable owned in _owned)
            {
                owned.Dispose();
            }
        }

        private T Own<T>(T disposable)
            where T : IDisposable
        {
            _owned.Add(disposable);
            return disposable;
        }
    }
}
