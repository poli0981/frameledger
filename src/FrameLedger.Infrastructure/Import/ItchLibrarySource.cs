using System.IO.Compression;
using System.Text.Json;
using FrameLedger.Application.Import;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// itch.io's installed library (FR-1.2): every install under <c>%APPDATA%\itch\apps</c> carries
/// <c>.itch\receipt.json.gz</c> — gzipped JSON whose <c>game</c> object names <c>title</c> and <c>id</c>. The receipt
/// does not say which executable is the game, so the locator guesses. A receipt that will not gunzip or parse
/// costs that one install.
/// </summary>
public sealed class ItchLibrarySource(string? appsDirectory = null) : IStoreLibrarySource
{
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "itch", "apps");

    public string Platform => "itch";

    public ValueTask<IReadOnlyList<StoreGame>> ListAsync(CancellationToken ct = default)
    {
        string dir = appsDirectory ?? DefaultDirectory;
        if (!Directory.Exists(dir))
        {
            return ValueTask.FromResult<IReadOnlyList<StoreGame>>([]);
        }

        List<StoreGame> games = [];
        foreach (string install in Directory.EnumerateDirectories(dir))
        {
            ct.ThrowIfCancellationRequested();
            string receipt = Path.Combine(install, ".itch", "receipt.json.gz");
            if (File.Exists(receipt) && Read(receipt, install) is { } game)
            {
                games.Add(game);
            }
        }

        return ValueTask.FromResult<IReadOnlyList<StoreGame>>(games);
    }

    private static StoreGame? Read(string receipt, string install)
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
            return title is null || id is null ? null : new StoreGame("itch", id, title, install, ExePath: null, Version: null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return null;
        }
    }
}
