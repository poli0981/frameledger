using FrameLedger.Application.Import;
using Microsoft.Win32;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// Steam's installed library (FR-1.2): <c>steamapps\libraryfolders.vdf</c> names every library folder, and each
/// folder's <c>steamapps\appmanifest_&lt;appid&gt;.acf</c> names one installed title — <c>appid</c>, <c>name</c>,
/// <c>installdir</c> (under <c>steamapps\common</c>), <c>buildid</c>. Steam does not record which executable is the
/// game, so <see cref="StoreGame.ExePath"/> is null and the locator guesses. The Steam root comes from
/// <c>HKCU\Software\Valve\Steam\SteamPath</c> unless a test hands one in.
/// </summary>
public sealed class SteamLibrarySource(string? steamRoot = null) : IStoreLibrarySource
{
    public string Platform => "steam";

    public ValueTask<IReadOnlyList<StoreGame>> ListAsync(CancellationToken ct = default)
    {
        string? root = steamRoot ?? InstalledRoot();
        if (root is null || !Directory.Exists(root))
        {
            return ValueTask.FromResult<IReadOnlyList<StoreGame>>([]);
        }

        List<StoreGame> games = [];
        foreach (string library in Libraries(root))
        {
            ct.ThrowIfCancellationRequested();
            string steamapps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamapps))
            {
                continue;
            }

            foreach (string manifest in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf"))
            {
                if (ReadManifest(manifest, steamapps) is { } game)
                {
                    games.Add(game);
                }
            }
        }

        return ValueTask.FromResult<IReadOnlyList<StoreGame>>(games);
    }

    /// <summary>The root itself plus every <c>path</c> in <c>libraryfolders.vdf</c> (both its old and new shapes name the folder as <c>path</c>).</summary>
    private static HashSet<string> Libraries(string root)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(root) };
        string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (TryRead(vdf) is { } text)
        {
            KeyValuesBlock parsed = ValveKeyValues.Parse(text);
            KeyValuesBlock folders = parsed.Child("libraryfolders") ?? parsed;
            foreach ((_, KeyValuesBlock entry) in folders.Children)
            {
                if (entry.Value("path") is { Length: > 0 } path)
                {
                    seen.Add(Path.GetFullPath(path));
                }
            }
        }

        return seen;
    }

    private static StoreGame? ReadManifest(string manifest, string steamapps)
    {
        if (TryRead(manifest) is not { } text)
        {
            return null;
        }

        KeyValuesBlock state = ValveKeyValues.Parse(text).Child("AppState") ?? ValveKeyValues.Parse(text);
        string? appId = state.Value("appid");
        string? name = state.Value("name");
        string? installDir = state.Value("installdir");
        if (appId is null || name is null || installDir is null)
        {
            return null;
        }

        return new StoreGame("steam", appId, name, Path.Combine(steamapps, "common", installDir), ExePath: null, state.Value("buildid"));
    }

    private static string? InstalledRoot()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam", writable: false);
            return key?.GetValue("SteamPath") as string is { Length: > 0 } path ? Path.GetFullPath(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    private static string? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
