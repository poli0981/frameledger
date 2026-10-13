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
/// reloads by itself when a session ends or an import adds games while it is on screen.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class GamesViewModel : ObservableObject
{
    private const string _listValue = "list";
    private const string _gridValue = "grid";

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
    private IReadOnlyList<GameCardViewModel> _all = [];
    private int _reads;
    private bool _viewRead;
    private bool _attached;

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

        IReadOnlyList<GameCard> cards = await _library.ListCardsAsync(ct).ConfigureAwait(true);
        // Whether each executable is on disk is a file-system question per entry — a drive that is gone can take a moment
        // to say so — so it is asked off the UI thread, the game page's own way (GameLibrary.ExecutableExists).
        Func<string, bool> exists = _exists;
        bool[] installed = await Task.Run(() => cards.Select(c => exists(c.Row.Fingerprint.ExePath)).ToArray(), ct)
            .ConfigureAwait(true);
        if (read != Volatile.Read(ref _reads))
        {
            return;
        }

        _all = [.. cards.Select((c, i) => new GameCardViewModel(c, installed[i]))];
        HeaderText = string.Format(CultureInfo.CurrentCulture, Strings.Games_Header_Count_Format, _all.Count);
        Apply();
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
        IEnumerable<GameCardViewModel> shown = _all;
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

        Games.Clear();
        foreach (GameCardViewModel card in shown)
        {
            Games.Add(card);
        }

        IsEmpty = _all.Count == 0;
        NoMatch = _all.Count > 0 && Games.Count == 0;
    }
}
