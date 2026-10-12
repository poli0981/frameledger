// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

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
/// The layout rules are the engines' own conventions, not guesses about names: a Unity player is <c>X.exe</c> beside a
/// directory <c>X_Data</c>, an Unreal packaged build's game process is <c>*-Win64-Shipping.exe</c> (the root executable
/// of the same name is a bootstrapper that starts it and exits), and — since beta.19 (owner decision D56) — an NW.js
/// runtime is the executable beside its <c>nw.dll</c> and <c>package.json</c>, which is how an RPG Maker MV/MZ game
/// ships. The last one ranks below the first two and above a name: an NW.js folder can be a tool shipped beside a Unity
/// or Unreal game, and a launcher named after the install is exactly what fronts the runtime on A Doll's Locket - Remake
/// (itch), whose 213 MB <c>DollsLocket.exe</c> starts <c>css\Game.exe</c> — the process that draws, the one D28 pointed
/// HELLO, HELLO WORLD!'s entry at by hand.
/// </para>
/// </remarks>
public sealed class ExecutableLocator : IExecutableLocator
{
    /// <summary>
    /// Four since 2026-09-21: Dying Light: The Beast keeps its game at <c>ph_ft/work/bin/x64</c>, one level past the three
    /// this walked, and the import offered no executable for it at all.
    /// </summary>
    public const int MaxDepth = 4;

    private const int _rankEngineLayout = 3;
    private const int _rankNwjsRuntime = 2;
    private const int _rankNamedAfterTheInstall = 1;
    private const int _minimumNameLength = 4;

    /// <summary>Name fragments of the helpers a game ships beside itself; a file whose name carries one is never the game.</summary>
    public static readonly IReadOnlyList<string> HelperFragments =
    [
        "crash", "handler", "unins", "setup", "install", "redist", "vc_redist", "dxsetup", "dotnet", "cef", "report", "updater",
        "launcher_helper", "easyanticheat", "beservice", "battleye", "steamerrorreporter", "ue4prereq", "uecc", "vcredist", "python", "node",
        "browser", "webview", "subprocess", "analyzer", "vconsole",
        // beta.14: BugSplat's sender (BsSndRpt64.exe, Kingdom Come: Deliverance II) matches none of the above — its name says
        // "report" without the letters; KCD2 picked its game by rank alone.
        "bssndrpt", "bugsplat",
        // beta.19: what an NW.js runtime keeps beside itself — Chromium's toast helper (NW.js 0.48.4 on A Doll's Locket - Remake,
        // 0.82.0 on Flower in Us), and the SDK flavour's compiler and driver, which the NW.js rank would otherwise weigh as the game.
        "notification_helper", "nwjc", "chromedriver",
    ];

    /// <summary>
    /// Whole names (no extension) of helpers a runtime ships beside a game, which no store's choice makes the game (beta.19).
    /// itch's butler keeps the shallowest candidates and drops those that ask for elevation, so its verdict for A Doll's
    /// Locket - Remake named <c>notification_helper.exe</c> and nothing else. Whole names rather than
    /// <see cref="HelperFragments"/>: a store names its own pick, and a fragment such as <c>node</c> is inside a real game's
    /// name (<c>Nodebuster</c>) — the locator's guess can afford that, overruling a store's verdict cannot.
    /// </summary>
    public static readonly IReadOnlySet<string> RuntimeHelperNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "notification_helper", "crashpad_handler", "UnityCrashHandler64", "UnityCrashHandler32", "CrashReportClient", "nwjc", "chromedriver",
    };

    /// <summary>True for a name in <see cref="RuntimeHelperNames"/>.</summary>
    public static bool IsRuntimeHelper(string fileNameWithoutExtension)
    {
        ArgumentNullException.ThrowIfNull(fileNameWithoutExtension);
        return RuntimeHelperNames.Contains(fileNameWithoutExtension);
    }

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

        if (dir is not null && File.Exists(Path.Combine(dir, "nw.dll")) && File.Exists(Path.Combine(dir, "package.json")))
        {
            return _rankNwjsRuntime;    // NW.js (RPG Maker MV/MZ): the runtime, not the launcher in front of it (D56)
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
