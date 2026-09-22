namespace FrameLedger.Application.Import;

/// <summary>
/// The name an entry gets when the user adds an executable by hand (2026-09-23). The file name is the right answer for
/// <c>DarkSoulsIII.exe</c> and the wrong one for <c>Game.exe</c>: every RPG Maker game, and a good share of engine
/// runtimes, ship an executable whose name says nothing about the game. For those the engine's own title wins when there
/// is one (RPG Maker's <c>System.json</c> <c>gameTitle</c>), then the nearest folder that is not a runtime folder — so
/// <i>HELLO, HELLO WORLD!</i>'s <c>swiftshader\Game.exe</c> is "HELLO, HELLO WORLD!", not "Game" or "swiftshader".
/// </summary>
public static class GameTitleGuess
{
    // Executable stems that name a runtime or a role, never a game.
    private static readonly HashSet<string> _generic = new(StringComparer.OrdinalIgnoreCase)
    {
        "game", "nw", "launcher", "launch", "start", "play", "main", "app", "client", "run", "runtime", "engine",
        "player", "bootstrap", "bootstrapper", "win64", "shipping",
    };

    // Folders an engine puts its executable in; the game's name is above them.
    private static readonly HashSet<string> _runtimeFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "swiftshader", "bin", "binaries", "win64", "win32", "x64", "x86", "www", "nw", "game", "exe", "app", "runtime",
    };

    /// <summary>
    /// The name for <paramref name="exePath"/>: its file name when that names something, else <paramref name="engineTitle"/>
    /// (asked only then — it reads a file), else the nearest folder that is not a runtime folder, else the file name.
    /// </summary>
    public static string Guess(string exePath, Func<string?>? engineTitle = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        string stem = Path.GetFileNameWithoutExtension(exePath);
        if (!_generic.Contains(stem))
        {
            return stem;
        }

        if (engineTitle?.Invoke() is { } title && !string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        for (string? dir = Path.GetDirectoryName(exePath); !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
        {
            string name = Path.GetFileName(dir);
            if (name.Length > 0 && !_runtimeFolders.Contains(name))
            {
                return name;
            }
        }

        return stem;
    }
}
