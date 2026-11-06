// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.Application.Settings;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// The Games library (<c>08_UI</c> §Games): every game in the library, searched and sorted in memory; add by pick or drop;
/// open by click. Since beta.11 (owner requests 2026-10-03): shown as a grid of cards that fills the width or as a list
/// (<c>ui.library_view</c>), with the count in the header; <b>Refresh</b> reloads the library, looks in the stores for games
/// installed since and offers only those, and marks entries whose executable is gone ("Not installed"); and the page
/// reloads by itself when a session ends or an import adds games while it is on screen. Since beta.34 (owner request
/// 2026-11-06, "Games stalls 0.5–1.5 s"; <c>spike-notes</c> §27) the page is kept by its window (<see cref="WindowPageCache"/>)
/// and reads the library again each time it is shown; the library is read on the thread pool, the cards appear before
/// the executables are checked, and a read keeps every card that would look the same.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class GamesViewModel : ObservableObject
{
    private const string _listValue = "list";
    private const string _gridValue = "grid";

    /// <summary>The store and engine filters' "everything" (beta.34, D85); no store or engine token is empty.</summary>
    public const string AnyToken = "";

    /// <summary>The engine filter's "not identified".</summary>
    public const string UnknownEngineToken = "?";

    /// <summary>How many removed games one notice names; the rest are counted (<c>Games_Removed_More_Format</c>).</summary>
    private const int _removedNamed = 5;

    private readonly GameLibrary _library;
    private readonly GameSelection _selection;
    private readonly IPageNavigator _navigator;
    private readonly AddGameFlow _addGame;
    private readonly ImportLibraryFlow? _import;
    private readonly RegisteredSettings? _settings;
    private readonly IAgentLink? _agent;
    private readonly LibraryChanges? _changes;
    private readonly IMessageStrip? _strip;
    private readonly GamePlayer? _player;
    private readonly UiMode _mode;
    private readonly TimeProvider _clock;
    private readonly Func<string, bool> _exists;
    private readonly UiThread _ui = new();
    private readonly List<string> _removed = [];
    private List<GameCardViewModel> _all = [];
    private int _reads;
    private bool _viewRead;
    private bool _attached;
    private bool _shownBefore;

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private GamesSort _sort = GamesSort.Name;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _noMatch;

    [ObservableProperty]
    private string _headerText = Strings.Games_Header;

    /// <summary>The list instead of the grid (<c>ui.library_view</c>); remembered when the user changes it.</summary>
    [ObservableProperty]
    private bool _listView;

    /// <summary>beta.34 (D85): the store filter — a store token, or <see cref="AnyToken"/>.</summary>
    [ObservableProperty]
    private string _storeFilter = AnyToken;

    /// <summary>beta.34 (D85): the engine filter — an engine id, <see cref="UnknownEngineToken"/>, or <see cref="AnyToken"/>.</summary>
    [ObservableProperty]
    private string _engineFilter = AnyToken;

    [ObservableProperty]
    private GamesStateFilter _stateFilter;

    [ObservableProperty]
    private YesNoFilter _installedFilter;

    [ObservableProperty]
    private YesNoFilter _playedFilter;

    /// <summary>A filter or the search narrows the list; the header then counts what is shown of all.</summary>
    [ObservableProperty]
    private bool _isFiltered;

    /// <summary>What Play will do, in the mode Settings ▸ Games has now (beta.21, D59).</summary>
    [ObservableProperty]
    private string _playToolTip = Strings.Play_ToolTip_Store;

    public GamesViewModel(GameLibrary library, GameSelection selection, IPageNavigator navigator, AddGameFlow addGame, ImportLibraryFlow? import = null,
        RegisteredSettings? settings = null, IAgentLink? agent = null, LibraryChanges? changes = null, IMessageStrip? strip = null, TimeProvider? clock = null,
        Func<string, bool>? executableExists = null, GamePlayer? player = null, UiMode? mode = null)
    {
        _strip = strip;
        _player = player;
        _mode = mode ?? UiMode.Profile;
        _clock = clock ?? TimeProvider.System;
        _exists = executableExists ?? GameLibrary.ExecutableExists;
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _addGame = addGame ?? throw new ArgumentNullException(nameof(addGame));
        _import = import;
        _settings = settings;
        _agent = agent;
        _changes = changes;
        Pending = LoadAsync();
    }

    public static string EmptyText => Strings.Games_Empty;

    /// <summary>beta.21 (D52's rule for Play): a viewer over a copy starts no game.</summary>
    public bool ActsOnThisPc => _mode.ActsOnThisPc;

    public static string NoMatchText => Strings.Games_NoMatch;

    public static string AddText => Strings.Games_Add;

    public static string SearchPlaceholder => Strings.Games_Search_Placeholder;

    public static string SortLabel => Strings.Games_Sort_Label;

    public static string RefreshText => Strings.Games_Refresh;

    public static string RefreshToolTip => Strings.Games_Refresh_ToolTip;

    public static string GridText => Strings.Games_View_Grid;

    public static string ListText => Strings.Games_View_List;

    /// <summary>
    /// How long the page waits after the Agent says it removed a game (D55) for the rest of the same check before it reloads
    /// and says what went (beta.19). One check removes its games a few milliseconds apart, each in its own event.
    /// </summary>
    public static TimeSpan RemovalSettle { get; } = TimeSpan.FromSeconds(1);

    /// <summary>The sort orders, labelled in the language this page opened in (beta.15: a static list kept the first language).</summary>
    public IReadOnlyList<Choice<GamesSort>> Sorts { get; } =
    [
        new(GamesSort.Name, Strings.Games_Sort_Name),
        new(GamesSort.LastPlayed, Strings.Games_Sort_LastPlayed),
        new(GamesSort.Playtime, Strings.Games_Sort_Playtime),
    ];

    public ObservableCollection<GameCardViewModel> Games { get; } = [];

    /// <summary>beta.34 (D85): the stores the library holds, "All stores" first — rebuilt when the library changes them.</summary>
    public ObservableCollection<Choice<string>> Stores { get; } = [];

    /// <summary>beta.34 (D85): the engines the library holds, "All engines" first, "Engine not identified" last when any is.</summary>
    public ObservableCollection<Choice<string>> Engines { get; } = [];

    /// <summary>The states, labelled in the language the page opened in, as the pills say them.</summary>
    public IReadOnlyList<Choice<GamesStateFilter>> States { get; } =
    [
        new(GamesStateFilter.All, Strings.Games_Filter_State_All),
        new(GamesStateFilter.HookingOn, Strings.Games_Card_HookOn),
        new(GamesStateFilter.HookingOff, Strings.Games_Card_HookOff),
        new(GamesStateFilter.AntiCheat, Strings.Games_Card_AntiCheat),
        new(GamesStateFilter.Exception, Strings.Games_Card_Exception),
        new(GamesStateFilter.NotRecorded, Strings.Games_Card_NotRecorded),
    ];

    public IReadOnlyList<Choice<YesNoFilter>> InstalledChoices { get; } =
    [
        new(YesNoFilter.All, Strings.Games_Filter_Installed_All),
        new(YesNoFilter.Yes, Strings.Games_Filter_Installed_Yes),
        new(YesNoFilter.No, Strings.Games_Card_NotInstalled),
    ];

    public IReadOnlyList<Choice<YesNoFilter>> PlayedChoices { get; } =
    [
        new(YesNoFilter.All, Strings.Games_Filter_Played_All),
        new(YesNoFilter.Yes, Strings.Games_Filter_Played_Yes),
        new(YesNoFilter.No, Strings.Games_Card_NeverPlayed),
    ];

    public static string ClearFiltersText => Strings.Filter_Clear;

    /// <summary>The load in flight, for a test to await.</summary>
    public Task Pending { get; private set; }

    /// <summary>
    /// The library read and listed. Reads overlap — a session that ends, an import, a removal — and the one that started first
    /// can finish last, its checks waiting on a drive that is slow to answer: only the newest read is shown (beta.19 — an
    /// older one put back the card of a game the Agent had just removed, until the next read).
    /// </summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        int read = Interlocked.Increment(ref _reads);
        await ReadViewAsync(ct).ConfigureAwait(true);
        if (_player is not null)
        {
            PlayToolTip = await _player.IsDirectAsync(ct).ConfigureAwait(true) ? Strings.Play_ToolTip_Direct : Strings.Play_ToolTip_Store;
        }

        // beta.34: the library on the thread pool — Microsoft.Data.Sqlite completes its asynchronous calls synchronously,
        // so on the UI thread the read was the page's to wait for.
        GameLibrary library = _library;
        IReadOnlyList<GameCard> cards = await Task.Run(() => library.ListCardsAsync(ct), ct).ConfigureAwait(true);
        if (read != Volatile.Read(ref _reads))
        {
            return;
        }

        // The cards first — each as the card it was, where it would look the same — and only then whether each executable
        // is on disk: a file-system question per entry that a sleeping drive answers in a second or more (39 of the owner's
        // 41 Steam games are on a USB drive), and no card was shown until every one had answered (spike-notes §27).
        _all = Merge(_all, cards);
        RefreshChoices();
        Apply();

        Func<string, bool> exists = _exists;
        (bool Installed, string? WhyNot)[] checks = await Task.Run(() => cards.Select(c =>
        {
            string path = c.Row.Fingerprint.ExePath;
            bool installed = exists(path);
            return (installed, installed ? null : Formats.ExecutableMissingText(path));
        }).ToArray(), ct).ConfigureAwait(true);
        if (read != Volatile.Read(ref _reads))
        {
            return;
        }

        var byId = _all.ToDictionary(static c => c.Id);
        for (int i = 0; i < cards.Count; i++)
        {
            if (byId.TryGetValue(cards[i].Row.Id, out GameCardViewModel? card))
            {
                card.SetInstalled(checks[i].Installed, checks[i].WhyNot);
            }
        }

        Apply();
        PlayCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// The library read again, as cards: an entry that would look the same keeps the card it has — its place on screen and
    /// its installed state until the check answers again — and only an entry that changed, or is new, gets a new one.
    /// </summary>
    private static List<GameCardViewModel> Merge(IReadOnlyList<GameCardViewModel> shown, IReadOnlyList<GameCard> cards)
    {
        var old = shown.ToDictionary(static c => c.Id);
        var merged = new List<GameCardViewModel>(cards.Count);
        foreach (GameCard card in cards)
        {
            var fresh = new GameCardViewModel(card);
            if (old.TryGetValue(card.Row.Id, out GameCardViewModel? kept))
            {
                if (kept.LooksLike(fresh))
                {
                    merged.Add(kept);
                    continue;
                }

                fresh.SetInstalled(!kept.NotInstalled, kept.NotInstalledToolTip);
            }

            merged.Add(fresh);
        }

        return merged;
    }

    /// <summary>
    /// Listens while the page is on screen: a session that ends changes a card's count, playtime and last played, and an
    /// import made from the menu adds cards. The page calls it on Loaded and <see cref="Detach"/> on Unloaded; the link and
    /// the notifier are singletons and must not hold the page.
    /// </summary>
    public void Attach()
    {
        if (_attached)
        {
            return;
        }

        if (_agent is not null)
        {
            _agent.EventReceived += OnAgentEvent;
        }

        if (_changes is not null)
        {
            _changes.Changed += OnLibraryChanged;
        }

        _attached = true;

        // beta.34: the page is kept by its window and shown again on a later visit (WindowPageCache), so what happened while
        // it was away — a session that ended, a game removed, renamed or added on another page — is read now, behind the
        // cards already on screen. Its first Loaded follows the constructor's own read.
        if (_shownBefore)
        {
            Pending = LoadAsync();
        }

        _shownBefore = true;
    }

    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        if (_agent is not null)
        {
            _agent.EventReceived -= OnAgentEvent;
        }

        if (_changes is not null)
        {
            _changes.Changed -= OnLibraryChanged;
        }

        _attached = false;
    }

    /// <summary>
    /// beta.11: the library read again, then the stores asked for games installed since — offered in the import checklist,
    /// the games already in the library hidden — and read again when any was added. A game whose executable is gone is
    /// marked on its card by the read; nothing is removed.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        Pending = LoadAsync();
        await Pending.ConfigureAwait(true);
        if (_import is not null && await _import.RunAsync(onlyNew: true).ConfigureAwait(true) is { Added: > 0 })
        {
            Pending = LoadAsync();
            await Pending.ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void ShowGrid() => ListView = false;

    [RelayCommand]
    private void ShowList() => ListView = true;

    [RelayCommand]
    private async Task AddGameAsync() => _ = await _addGame.RunAsync().ConfigureAwait(true);

    /// <summary>beta.21 (D59): a card's or a row's Play — as the game's page plays it (<see cref="GamePlayer"/>).</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task PlayAsync(GameCardViewModel? card)
    {
        if (_player is null || card is null)
        {
            return;
        }

        PlayResult result = await _player.PlayAsync(card.Card.Row).ConfigureAwait(true);
        (string message, bool warning) = GamePlayer.Describe(result, card.Name);
        if (warning)
        {
            _strip?.Warn(Strings.Play_Title, message);
        }
        else
        {
            _strip?.Info(Strings.Play_Title, message);
        }
    }

    private bool CanPlay(GameCardViewModel? card) => _player is not null && card is { NotInstalled: false };

    [RelayCommand]
    private void Open(GameCardViewModel? card)
    {
        if (card is null)
        {
            return;
        }

        _selection.GameId = card.Id;
        _navigator.Navigate<GameDetailPage>();
    }

    /// <summary>FR-1.1's drop: every <c>.exe</c> among the paths is added; the last one added is opened.</summary>
    public async Task DropAsync(IEnumerable<string> paths, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        foreach (string path in paths.Where(static p => p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
        {
            await _addGame.AddPathAsync(path, ct).ConfigureAwait(true);
        }
    }

    partial void OnSearchChanged(string value) => Apply();

    partial void OnSortChanged(GamesSort value) => Apply();

    partial void OnStoreFilterChanged(string value) => Apply();

    partial void OnEngineFilterChanged(string value) => Apply();

    partial void OnStateFilterChanged(GamesStateFilter value) => Apply();

    partial void OnInstalledFilterChanged(YesNoFilter value) => Apply();

    partial void OnPlayedFilterChanged(YesNoFilter value) => Apply();

    /// <summary>beta.34 (D85): every filter back to everything; the search stays (it has its own box).</summary>
    [RelayCommand]
    private void ClearFilters()
    {
        StoreFilter = AnyToken;
        EngineFilter = AnyToken;
        StateFilter = GamesStateFilter.All;
        InstalledFilter = YesNoFilter.All;
        PlayedFilter = YesNoFilter.All;
    }

    /// <summary>
    /// The store and engine choices from what the library holds — rebuilt only when that set changes, so an open list does not
    /// lose its selection on every read. A filter whose store or engine left the library goes back to everything.
    /// </summary>
    private void RefreshChoices()
    {
        Choice<string>[] stores =
        [
            new(AnyToken, Strings.Games_Filter_Store_All),
            .. _all.Select(static c => c.PlatformToken).Distinct(StringComparer.Ordinal)
                .Select(static t => new Choice<string>(t, Formats.Platform(t) is { Length: > 0 } name ? name : Strings.Games_Filter_Store_Manual))
                .OrderBy(static c => c.Label, StringComparer.CurrentCultureIgnoreCase),
        ];
        Choice<string>[] engines =
        [
            new(AnyToken, Strings.Games_Filter_Engine_All),
            .. _all.Select(static c => c.EngineToken).OfType<string>().Distinct(StringComparer.Ordinal)
                .Select(static t => new Choice<string>(t, Formats.Engine(t) is { Length: > 0 } name ? name : t))
                .OrderBy(static c => c.Label, StringComparer.CurrentCultureIgnoreCase),
            .. _all.Any(static c => c.EngineToken is null) ? new[] { new Choice<string>(UnknownEngineToken, Strings.Games_Filter_Engine_Unknown) } : Array.Empty<Choice<string>>(),
        ];
        Replace(Stores, stores);
        Replace(Engines, engines);
        if (!Stores.Any(c => string.Equals(c.Value, StoreFilter, StringComparison.Ordinal)))
        {
            StoreFilter = AnyToken;
        }

        if (!Engines.Any(c => string.Equals(c.Value, EngineFilter, StringComparison.Ordinal)))
        {
            EngineFilter = AnyToken;
        }
    }

    private static void Replace(ObservableCollection<Choice<string>> target, Choice<string>[] wanted)
    {
        if (target.SequenceEqual(wanted))
        {
            return;
        }

        target.Clear();
        foreach (Choice<string> choice in wanted)
        {
            target.Add(choice);
        }
    }

    private bool Passes(GameCardViewModel c) =>
        (StoreFilter.Length == 0 || string.Equals(c.PlatformToken, StoreFilter, StringComparison.Ordinal))
        && (EngineFilter.Length == 0 || string.Equals(c.EngineToken ?? UnknownEngineToken, EngineFilter, StringComparison.Ordinal))
        && (StateFilter == GamesStateFilter.All || c.State == StateFilter)
        && InstalledFilter switch { YesNoFilter.Yes => !c.NotInstalled, YesNoFilter.No => c.NotInstalled, _ => true }
        && PlayedFilter switch { YesNoFilter.Yes => c.SessionCount > 0, YesNoFilter.No => c.SessionCount == 0, _ => true };

    partial void OnListViewChanged(bool value)
    {
        if (_viewRead && _settings is not null)
        {
            _ = SaveViewAsync(value);
        }
    }

    private async Task SaveViewAsync(bool list) =>
        await _settings!.SetAsync(SettingsRegistry.UiLibraryView, list ? _listValue : _gridValue).ConfigureAwait(true);

    /// <summary>The remembered view, read once per page before the first list — so reading it is not taken for a change.</summary>
    private async Task ReadViewAsync(CancellationToken ct)
    {
        if (_viewRead)
        {
            return;
        }

        if (_settings is not null)
        {
            ListView = string.Equals(await _settings.GetAsync(SettingsRegistry.UiLibraryView, ct).ConfigureAwait(true), _listValue, StringComparison.Ordinal);
        }

        _viewRead = true;
    }

    private void OnAgentEvent(object? sender, AgentEventArgs e) => _ui.Post(() =>
    {
        if (string.Equals(e.Envelope.Type, IpcMessageType.SessionCompleted, StringComparison.Ordinal))
        {
            Pending = LoadAsync();
        }
        else if (string.Equals(e.Envelope.Type, IpcMessageType.GameRemoved, StringComparison.Ordinal)
                 && IpcCodec.Payload<GameRemovedEvent>(e.Envelope) is { } removed)
        {
            // D55 (beta.19): the Agent removed an entry whose game is no longer installed; the card goes, and the page says why.
            // A check that removes five says it in five events: the first waits for the rest, then one reload and one notice.
            _removed.Add(removed.GameName ?? removed.ExePath);
            if (_removed.Count == 1)
            {
                Pending = SayRemovedAsync();
            }
        }
    });

    /// <summary>
    /// The games the Agent removed since the first of them, after <see cref="RemovalSettle"/>: the library read once, then one
    /// notice — the game's name, or how many went and which. A removal heard during the read starts the next notice.
    /// </summary>
    private async Task SayRemovedAsync()
    {
        await Task.Delay(RemovalSettle, _clock).ConfigureAwait(true);
        string[] names = [.. _removed];
        _removed.Clear();
        await LoadAsync().ConfigureAwait(true);
        if (_strip is null)
        {
            return;
        }

        string listed = names.Length <= _removedNamed
            ? string.Join(", ", names)
            : string.Format(CultureInfo.CurrentCulture, Strings.Games_Removed_More_Format, string.Join(", ", names.Take(_removedNamed)), names.Length - _removedNamed);
        _strip.Info(Strings.Games_Removed_Title, names.Length == 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.Games_Removed_Format, names[0])
            : string.Format(CultureInfo.CurrentCulture, Strings.Games_Removed_Many_Format, names.Length, listed));
    }

    private void OnLibraryChanged(object? sender, EventArgs e) => _ui.Post(() => Pending = LoadAsync());

    private void Apply()
    {
        IEnumerable<GameCardViewModel> shown = _all.Where(Passes);
        string q = Search.Trim();
        if (q.Length > 0)
        {
            shown = shown.Where(c => c.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase));
        }

        shown = Sort switch
        {
            GamesSort.LastPlayed => shown.OrderByDescending(static c => c.LastPlayedAt ?? DateTimeOffset.MinValue).ThenBy(static c => c.Name, StringComparer.CurrentCultureIgnoreCase),
            GamesSort.Playtime => shown.OrderByDescending(static c => c.TotalSeconds).ThenBy(static c => c.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => shown.OrderBy(static c => c.Name, StringComparer.CurrentCultureIgnoreCase),
        };

        Sync(Games, [.. shown]);
        IsEmpty = _all.Count == 0;
        NoMatch = _all.Count > 0 && Games.Count == 0;
        IsFiltered = q.Length > 0 || StoreFilter.Length > 0 || EngineFilter.Length > 0 || StateFilter != GamesStateFilter.All
            || InstalledFilter != YesNoFilter.All || PlayedFilter != YesNoFilter.All;
        HeaderText = IsFiltered
            ? string.Format(CultureInfo.CurrentCulture, Strings.Games_Header_Filtered_Format, Games.Count, _all.Count)
            : string.Format(CultureInfo.CurrentCulture, Strings.Games_Header_Count_Format, _all.Count);
    }

    /// <summary>
    /// Makes <paramref name="target"/> list <paramref name="wanted"/>, in its order, by removing, moving and inserting —
    /// never clearing (beta.34): a card that stays is the element already on screen, and the panel builds only the new.
    /// </summary>
    internal static void Sync(ObservableCollection<GameCardViewModel> target, IReadOnlyList<GameCardViewModel> wanted)
    {
        var keep = new HashSet<GameCardViewModel>(wanted);
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(target[i]))
            {
                target.RemoveAt(i);
            }
        }

        for (int i = 0; i < wanted.Count; i++)
        {
            if (i < target.Count && ReferenceEquals(target[i], wanted[i]))
            {
                continue;
            }

            int at = target.IndexOf(wanted[i]);
            if (at >= 0)
            {
                target.Move(at, i);
            }
            else
            {
                target.Insert(i, wanted[i]);
            }
        }
    }
}
