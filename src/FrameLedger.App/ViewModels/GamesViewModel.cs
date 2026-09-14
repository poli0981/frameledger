using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;

namespace FrameLedger.App.ViewModels;

/// <summary>The Games grid (<c>08_UI</c> §Games): every game in the library as a card, searched and sorted in memory; add by pick or drop; open by click.</summary>
public sealed partial class GamesViewModel : ObservableObject
{
    private readonly GameLibrary _library;
    private readonly GameSelection _selection;
    private readonly IPageNavigator _navigator;
    private readonly AddGameFlow _addGame;
    private IReadOnlyList<GameCardViewModel> _all = [];

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private GamesSort _sort = GamesSort.Name;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _noMatch;

    public GamesViewModel(GameLibrary library, GameSelection selection, IPageNavigator navigator, AddGameFlow addGame)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _addGame = addGame ?? throw new ArgumentNullException(nameof(addGame));
        Pending = LoadAsync();
    }

    public static string Header => Strings.Games_Header;

    public static string EmptyText => Strings.Games_Empty;

    public static string NoMatchText => Strings.Games_NoMatch;

    public static string AddText => Strings.Games_Add;

    public static string SearchPlaceholder => Strings.Games_Search_Placeholder;

    public static string SortLabel => Strings.Games_Sort_Label;

    public static IReadOnlyList<Choice<GamesSort>> Sorts { get; } =
    [
        new(GamesSort.Name, Strings.Games_Sort_Name),
        new(GamesSort.LastPlayed, Strings.Games_Sort_LastPlayed),
        new(GamesSort.Playtime, Strings.Games_Sort_Playtime),
    ];

    public ObservableCollection<GameCardViewModel> Games { get; } = [];

    /// <summary>The load in flight, for a test to await.</summary>
    public Task Pending { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IReadOnlyList<GameCard> cards = await _library.ListCardsAsync(ct).ConfigureAwait(true);
        _all = [.. cards.Select(static c => new GameCardViewModel(c))];
        Apply();
    }

    [RelayCommand]
    private async Task AddGameAsync() => _ = await _addGame.RunAsync().ConfigureAwait(true);

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
