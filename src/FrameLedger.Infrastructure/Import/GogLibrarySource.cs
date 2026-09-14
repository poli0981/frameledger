using FrameLedger.Application.Import;
using Microsoft.Win32;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// GOG Galaxy's installed library (FR-1.2): one subkey per title under
/// <c>HKLM\SOFTWARE\WOW6432Node\GOG.com\Games</c> — the subkey name is the store id; <c>gameName</c>, <c>path</c>,
/// <c>exe</c> (the launch executable's full path) and <c>ver</c> are its values. Read-only, whichever hive and key
/// the caller names (a test uses its own HKCU key); a key that is not there is an empty library.
/// </summary>
public sealed class GogLibrarySource(RegistryHive hive = RegistryHive.LocalMachine, string keyPath = GogLibrarySource.DefaultKeyPath) : IStoreLibrarySource
{
    public const string DefaultKeyPath = @"SOFTWARE\WOW6432Node\GOG.com\Games";

    public string Platform => "gog";

    public ValueTask<IReadOnlyList<StoreGame>> ListAsync(CancellationToken ct = default)
    {
        List<StoreGame> games = [];
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using RegistryKey? root = baseKey.OpenSubKey(keyPath, writable: false);
            if (root is null)
            {
                return ValueTask.FromResult<IReadOnlyList<StoreGame>>([]);
            }

            foreach (string id in root.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();
                using RegistryKey? game = root.OpenSubKey(id, writable: false);
                if (game is null)
                {
                    continue;
                }

                string? name = game.GetValue("gameName") as string;
                string? path = game.GetValue("path") as string;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                string? exe = game.GetValue("exe") as string;
                games.Add(new StoreGame("gog", id, name, path, string.IsNullOrWhiteSpace(exe) ? null : exe, game.GetValue("ver") as string));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // A hive we may not read is an empty library, not a failed import.
            return ValueTask.FromResult<IReadOnlyList<StoreGame>>([]);
        }

        return ValueTask.FromResult<IReadOnlyList<StoreGame>>(games);
    }
}
