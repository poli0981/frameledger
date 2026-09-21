using FrameLedger.Application.Import;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// <see cref="IExecutableLocator"/> for the stores that do not name the game's executable (Steam, itch): walk the
/// install directory four levels deep, drop the helpers a game ships beside itself (crash handlers, installers,
/// redistributables, anti-cheat services, embedded browsers, launchers' own updaters) and everything under an engine's
/// plugin folder, then RANK what is left: an executable the engine's own layout names as the game beats any other, and
/// size decides only between equals. A guess the review list shows; the user unticks what is wrong, and the game's edit
/// dialog can point the row at another executable afterwards. Never launched, never read past its size.
/// </summary>
/// <remarks>
/// <para>
/// <b>It was "the largest non-helper wins" until 2026-09-21, and that was measured wrong on a real title.</b> GIRLS'
/// FRONTLINE 2: EXILIUM (Steam 3347400, Unity) ships <c>GF2_Exilium.exe</c> at 675,392 bytes — a Unity player stub is
/// small, the game is <c>UnityPlayer.dll</c> and <c>GameAssembly.dll</c> — and an embedded-browser helper,
/// <c>GF2_Exilium_Data\Plugins\ZFGameBrowser.exe</c>, at 1,082,944. The helper won, the row was created for it, and
/// because the watcher, consent and the gate are all keyed on the executable path, enabling hooking on that row could
/// never match the game. The same ledger held Rune Factory: Guardians of Azuma twice, as the root <c>Game.exe</c>
/// bootstrapper and as <c>Game-Win64-Shipping.exe</c>.
/// </para>
/// <para>
/// The two layout rules are the engines' own conventions, not guesses about names: a Unity player is <c>X.exe</c>
/// beside a directory <c>X_Data</c>, and an Unreal packaged build's game process is <c>*-Win64-Shipping.exe</c> (the
/// root executable of the same name is a bootstrapper that starts it and exits).
/// </para>
/// </remarks>
public sealed class ExecutableLocator : IExecutableLocator
{
    /// <summary>
    /// Four since 2026-09-21: Dying Light: The Beast keeps its game at <c>ph_ft/work/bin/x64</c>, one level past the three
    /// this walked, and the import offered no executable for it at all.
    /// </summary>
    public const int MaxDepth = 4;

    private const int _rankEngineLayout = 2;
    private const int _rankNamedAfterTheInstall = 1;
    private const int _minimumNameLength = 4;

    /// <summary>Name fragments of the helpers a game ships beside itself; a file whose name carries one is never the game.</summary>
    public static readonly IReadOnlyList<string> HelperFragments =
    [
        "crash", "handler", "unins", "setup", "install", "redist", "vc_redist", "dxsetup", "dotnet", "cef", "report", "updater",
        "launcher_helper", "easyanticheat", "beservice", "battleye", "steamerrorreporter", "ue4prereq", "uecc", "vcredist", "python", "node",
        "browser", "webview", "subprocess", "analyzer", "vconsole",
    ];

    /// <summary>
    /// Folder-name fragments of what a store install carries beside the game: a folder whose name holds one is not walked.
    /// Red Dead Redemption 2's <c>Redistributables\Rockstar-Games-Launcher.exe</c> is 140 MB to <c>RDR2.exe</c>'s 90.
    /// </summary>
    public static readonly IReadOnlyList<string> SkippedFolderFragments = ["redist", "prereq", "directx", "installer"];

    public static bool IsSkippedFolder(string folderName)
    {
        ArgumentNullException.ThrowIfNull(folderName);
        foreach (string fragment in SkippedFolderFragments)
        {
            if (folderName.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsHelper(string fileNameWithoutExtension)
    {
        ArgumentNullException.ThrowIfNull(fileNameWithoutExtension);
        foreach (string fragment in HelperFragments)
        {
            if (fileNameWithoutExtension.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True for a path inside an engine's plugin folder — Unity's <c>X_Data\Plugins</c>, where native helpers such as an
    /// embedded browser live. Nothing there is the game, whatever its size.
    /// </summary>
    public static bool IsUnderPluginFolder(string installDirectory, string executable)
    {
        ArgumentNullException.ThrowIfNull(installDirectory);
        ArgumentNullException.ThrowIfNull(executable);
        string relative = Path.GetRelativePath(installDirectory, executable);
        string[] parts = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        // The last part is the file; a "<X>_Data", "Plugins" pair anywhere before it is the folder.
        for (int i = 0; i + 2 < parts.Length; i++)
        {
            if (parts[i].EndsWith("_Data", StringComparison.OrdinalIgnoreCase) && string.Equals(parts[i + 1], "Plugins", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public string? PickPrimary(string installDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        if (!Directory.Exists(installDirectory))
        {
            return null;
        }

        string installName = Squash(Path.GetFileName(Path.TrimEndingDirectorySeparator(installDirectory)));
        string? best = null;
        int bestRank = -1;
        long bestSize = -1;
        foreach (string exe in Walk(installDirectory, 0))
        {
            string name = Path.GetFileNameWithoutExtension(exe);
            if (IsHelper(name) || IsUnderPluginFolder(installDirectory, exe))
            {
                continue;
            }

            long size;
            try
            {
                size = new FileInfo(exe).Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            int rank = Rank(exe, name, installName);
            if (rank > bestRank || (rank == bestRank && size > bestSize))
            {
                best = exe;
                bestRank = rank;
                bestSize = size;
            }
        }

        return best;
    }

    private static int Rank(string exe, string name, string installName)
    {
        string? dir = Path.GetDirectoryName(exe);
        if (dir is not null && Directory.Exists(Path.Combine(dir, name + "_Data")))
        {
            return _rankEngineLayout;    // Unity: X.exe beside X_Data
        }

        if (name.EndsWith("-Win64-Shipping", StringComparison.OrdinalIgnoreCase) || name.EndsWith("-WinGDK-Shipping", StringComparison.OrdinalIgnoreCase))
        {
            return _rankEngineLayout;    // Unreal: the packaged game process, not the root bootstrapper
        }

        string squashed = Squash(name);
        bool named = squashed.Length >= _minimumNameLength && installName.Length >= _minimumNameLength
            && (installName.Contains(squashed, StringComparison.Ordinal) || squashed.Contains(installName, StringComparison.Ordinal));
        return named ? _rankNamedAfterTheInstall : 0;
    }

    /// <summary>Letters and digits, lower-cased: "Cyberpunk 2077" and "Cyberpunk2077" are the same name.</summary>
    private static string Squash(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant));

    private static IEnumerable<string> Walk(string dir, int depth)
    {
        IEnumerable<string> files;
        IEnumerable<string> dirs;
        try
        {
            files = Directory.EnumerateFiles(dir, "*.exe");
            dirs = depth < MaxDepth ? Directory.EnumerateDirectories(dir) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (string f in files)
        {
            yield return f;
        }

        foreach (string d in dirs)
        {
            if (IsSkippedFolder(Path.GetFileName(d)))
            {
                continue;
            }

            foreach (string f in Walk(d, depth + 1))
            {
                yield return f;
            }
        }
    }
}
