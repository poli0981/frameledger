using System.Text.Json;
using FrameLedger.Application.Import;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// The Epic Games Launcher's installed library (FR-1.2): one <c>*.item</c> JSON per title under
/// <c>%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests</c> — <c>DisplayName</c>, <c>InstallLocation</c>,
/// <c>LaunchExecutable</c> (relative to the install), <c>AppName</c> (the store id) and <c>AppVersionString</c>.
/// Read with <c>JsonDocument</c> so an unexpected shape costs that one file, never the import.
/// </summary>
public sealed class EpicLibrarySource(string? manifestsDirectory = null) : IStoreLibrarySource
{
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");

    public string Platform => "epic";

    public ValueTask<IReadOnlyList<StoreGame>> ListAsync(CancellationToken ct = default)
    {
        string dir = manifestsDirectory ?? DefaultDirectory;
        if (!Directory.Exists(dir))
        {
            return ValueTask.FromResult<IReadOnlyList<StoreGame>>([]);
        }

        List<StoreGame> games = [];
        foreach (string item in Directory.EnumerateFiles(dir, "*.item"))
        {
            ct.ThrowIfCancellationRequested();
            if (Read(item) is { } game)
            {
                games.Add(game);
            }
        }

        return ValueTask.FromResult<IReadOnlyList<StoreGame>>(games);
    }

    private static StoreGame? Read(string item)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(item));
            JsonElement root = doc.RootElement;
            string? name = Text(root, "DisplayName");
            string? location = Text(root, "InstallLocation");
            string? id = Text(root, "AppName");
            if (name is null || location is null || id is null)
            {
                return null;
            }

            string? launch = Text(root, "LaunchExecutable");
            string? exe = string.IsNullOrWhiteSpace(launch) ? null : Path.Combine(location, launch);
            return new StoreGame("epic", id, name, location, exe, Text(root, "AppVersionString"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string property) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(property, out JsonElement e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
}
