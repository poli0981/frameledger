using System.IO.Compression;
using System.Text.Json;
using Dapper;
using FrameLedger.Application.Import;
using Microsoft.Data.Sqlite;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// itch.io's installed library (FR-1.2), from the two places the itch app records it.
/// </summary>
/// <remarks>
/// <para>
/// <b>butler's database first (2026-09-16).</b> The app keeps its installs in <c>%APPDATA%\itch\db\butler.db</c>:
/// <c>install_locations</c> (id, path) — the folders the user chose, which need not be under <c>%APPDATA%</c> at all —
/// and <c>caves</c>, one per install, with the location id, the folder name, and butler's own <c>verdict</c> JSON naming
/// the executable it decided the game is. Until this date the adapter read only <c>%APPDATA%\itch\apps\*</c>, the
/// default location, and on a machine whose one location was <c>D:\another\it</c> it returned nothing — silently,
/// because it logged nothing. The database is copied to a temporary directory and the copy opened read-only, because
/// the running itch app holds it in WAL mode; the copy is deleted afterwards.
/// </para>
/// <para>
/// <b>Receipts second.</b> Every install still carries <c>.itch\receipt.json.gz</c> — gzipped JSON whose <c>game</c> object
/// names <c>title</c> and <c>id</c> — and <c>apps\</c> under the itch directory is scanned as before, for a machine
/// whose database is missing or unreadable, or an install the database does not list. An install found both ways is
/// one row, the database's. The receipt does not say which executable is the game, so a receipt-only row leaves
/// <see cref="StoreGame.ExePath"/> null for the locator's guess; a database row carries butler's verdict.
/// </para>
/// <para>
/// One line per run goes to <paramref name="log"/> saying what was consulted and what each source yielded, so
/// "0 title(s)" has a reason beside it.
/// </para>
/// </remarks>
public sealed class ItchLibrarySource(string? itchDirectory = null, Action<string>? log = null) : IStoreLibrarySource
{
    private const string _platform = "itch";

    /// <summary><c>%APPDATA%\itch</c> — the app's own directory; <c>db\butler.db</c> and <c>apps\</c> are under it.</summary>
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "itch");

    public string Platform => _platform;

    public async ValueTask<IReadOnlyList<StoreGame>> ListAsync(CancellationToken ct = default)
    {
        string root = itchDirectory ?? DefaultDirectory;
        if (!Directory.Exists(root))
        {
            log?.Invoke($"import: itch — not installed (no {root})");
            return [];
        }

        // Keyed by install directory, so an install the database lists and a receipt also names is one row.
        var byInstall = new Dictionary<string, StoreGame>(StringComparer.OrdinalIgnoreCase);
        string database = Path.Combine(root, "db", "butler.db");
        string databaseNote = await ReadDatabaseAsync(database, byInstall, ct).ConfigureAwait(false);
        int fromDatabase = byInstall.Count;
        int receipts = ScanReceipts(Path.Combine(root, "apps"), byInstall, ct);
        log?.Invoke($"import: itch — {databaseNote}; apps\\ {receipts} receipt(s), {byInstall.Count - fromDatabase} new");
        return [.. byInstall.Values];
    }

    /// <summary>Reads a copy of butler's database; returns the log fragment describing what happened.</summary>
    private static async Task<string> ReadDatabaseAsync(string database, Dictionary<string, StoreGame> byInstall, CancellationToken ct)
    {
        if (!File.Exists(database))
        {
            return $"no {database}";
        }

        string temp = Path.Combine(Path.GetTempPath(), "fl-itch-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            string copy = Path.Combine(temp, "butler.db");
            File.Copy(database, copy);
            if (File.Exists(database + "-wal"))
            {
                File.Copy(database + "-wal", copy + "-wal");    // the frames since the app's last checkpoint; no -shm, so SQLite rebuilds the index
            }

            int locations;
            List<CaveRow> caves;
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = copy, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            await using (connection.ConfigureAwait(false))
            {
                await connection.OpenAsync(ct).ConfigureAwait(false);
                locations = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM install_locations", cancellationToken: ct)).ConfigureAwait(false);
                // Tuples rather than a row type: Dapper maps a value tuple by position, and a private row class would be
                // "never instantiated" to the analyzer (CA1812) though Dapper instantiates it.
                caves = [.. (await connection.QueryAsync<(long GameId, string? LocationPath, string? FolderName, string? CustomFolder, string? Verdict, string? Title)>(new CommandDefinition(
                    """
                    SELECT c.game_id, l.path, c.install_folder_name, c.custom_install_folder, c.verdict, g.title
                    FROM caves c
                    LEFT JOIN install_locations l ON l.id = c.install_location_id
                    LEFT JOIN games g ON g.id = c.game_id
                    """, cancellationToken: ct)).ConfigureAwait(false)).Select(static r => new CaveRow(r.GameId, r.LocationPath, r.FolderName, r.CustomFolder, r.Verdict, r.Title))];
            }

            int added = 0;
            foreach (CaveRow cave in caves)
            {
                if (ToGame(cave) is { } game && byInstall.TryAdd(game.InstallDirectory, game))
                {
                    added++;
                }
            }

            return $"butler.db read: {locations} location(s), {caves.Count} cave(s), {added} title(s)";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or InvalidOperationException)
        {
            return $"butler.db unreadable ({ex.GetType().Name}: {ex.Message})";
        }
        finally
        {
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static StoreGame? ToGame(CaveRow cave)
    {
        string? install = !string.IsNullOrWhiteSpace(cave.CustomFolder) ? cave.CustomFolder
            : cave.LocationPath is { Length: > 0 } && cave.FolderName is { Length: > 0 } ? Path.Combine(cave.LocationPath, cave.FolderName)
            : null;
        if (install is null)
        {
            return null;
        }

        install = Path.GetFullPath(install);
        string name = cave.Title is { Length: > 0 } ? cave.Title : ReadReceipt(Path.Combine(install, ".itch", "receipt.json.gz"))?.Title ?? Path.GetFileName(install);
        return new StoreGame(_platform, cave.GameId.ToString(System.Globalization.CultureInfo.InvariantCulture), name, install, VerdictExecutable(cave.Verdict, install), Version: null);
    }

    /// <summary>
    /// butler's verdict: <c>{"basePath":…,"candidates":[{"path":"Sub/Game.exe","flavor":"windows",…}]}</c>. The first
    /// Windows candidate is the executable butler itself launches; anything else (an HTML5 or Love2D game, a
    /// verdict that will not parse) leaves the guess to the locator.
    /// </summary>
    private static string? VerdictExecutable(string? verdict, string install)
    {
        if (string.IsNullOrWhiteSpace(verdict))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(verdict);
            if (!doc.RootElement.TryGetProperty("candidates", out JsonElement candidates) || candidates.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (JsonElement candidate in candidates.EnumerateArray())
            {
                bool windows = candidate.TryGetProperty("flavor", out JsonElement flavor) && flavor.ValueKind == JsonValueKind.String
                    && string.Equals(flavor.GetString(), "windows", StringComparison.Ordinal);
                if (windows && candidate.TryGetProperty("path", out JsonElement path) && path.ValueKind == JsonValueKind.String && path.GetString() is { Length: > 0 } relative)
                {
                    return Path.GetFullPath(Path.Combine(install, relative.Replace('/', Path.DirectorySeparatorChar)));
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int ScanReceipts(string apps, Dictionary<string, StoreGame> byInstall, CancellationToken ct)
    {
        if (!Directory.Exists(apps))
        {
            return 0;
        }

        int receipts = 0;
        foreach (string install in Directory.EnumerateDirectories(apps))
        {
            ct.ThrowIfCancellationRequested();
            string receipt = Path.Combine(install, ".itch", "receipt.json.gz");
            if (File.Exists(receipt) && ReadReceipt(receipt) is { } r)
            {
                receipts++;
                string full = Path.GetFullPath(install);
                byInstall.TryAdd(full, new StoreGame(_platform, r.Id, r.Title, full, ExePath: null, Version: null));
            }
        }

        return receipts;
    }

    private static (string Id, string Title)? ReadReceipt(string receipt)
    {
        try
        {
            using FileStream file = File.OpenRead(receipt);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using JsonDocument doc = JsonDocument.Parse(gzip);
            if (!doc.RootElement.TryGetProperty("game", out JsonElement game) || game.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? title = game.TryGetProperty("title", out JsonElement t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            string? id = game.TryGetProperty("id", out JsonElement i) ? i.ValueKind switch
            {
                JsonValueKind.Number => i.GetRawText(),
                JsonValueKind.String => i.GetString(),
                _ => null,
            } : null;
            return title is null || id is null ? null : (id, title);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return null;
        }
    }

    private sealed record CaveRow(long GameId, string? LocationPath, string? FolderName, string? CustomFolder, string? Verdict, string? Title);
}
