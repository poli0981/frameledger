using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using FrameLedger.App.Pages;
using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>
/// FR-1.1 as one flow shared by File ▸ Add game…, the Games page's button and a dropped executable: pick (or take
/// the path), add the row hooking-off, say so in the strip, and open the game. A file that cannot be read adds
/// nothing and says that instead.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed class AddGameFlow
{
    private readonly GameLibrary _library;
    private readonly IGamePicker _picker;
    private readonly GameSelection _selection;
    private readonly IPageNavigator _navigator;
    private readonly IMessageStrip _strip;

    public AddGameFlow(GameLibrary library, IGamePicker picker, GameSelection selection, IPageNavigator navigator, IMessageStrip strip)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
    }

    /// <summary>Pick, then add. Null when the user cancelled or the file could not be read.</summary>
    public Task<GameRow?> RunAsync(CancellationToken ct = default)
    {
        string? path = _picker.PickExecutable();
        return path is null ? Task.FromResult<GameRow?>(null) : AddPathAsync(path, ct);
    }

    /// <summary>Add one path (a drop, or the pick), open the game on success.</summary>
    public async Task<GameRow?> AddPathAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        GameRow? row = await _library.AddAsync(path, ct).ConfigureAwait(true);
        if (row is null)
        {
            _strip.Warn(Strings.Games_Header, string.Format(CultureInfo.CurrentCulture, Strings.AddGame_Failed_Format, Path.GetFileName(path)));
            return null;
        }

        _strip.Success(Strings.Games_Header, string.Format(CultureInfo.CurrentCulture, Strings.AddGame_Added_Format, row.Name));
        _selection.GameId = row.Id;
        _navigator.Navigate<GameDetailPage>();
        return row;
    }
}
