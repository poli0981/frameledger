using System.Text.Json;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// An RPG Maker MV/MZ game's own title (2026-09-23): <c>gameTitle</c> in <c>data\System.json</c> — under <c>www\</c> for
/// MV, at the root for MZ — beside the executable or up to two folders above it (a launcher-fronted game keeps its
/// runtime in a subfolder: <i>HELLO, HELLO WORLD!</i>'s is <c>swiftshader\</c>). Read only when the executable's own name
/// says nothing (<c>Game.exe</c>), and bounded: a file over 1 MiB, an unreadable one or one without the field is null.
/// </summary>
public static class RpgMakerTitle
{
    private const long _maxBytes = 1024 * 1024;

    /// <summary>The title, or null when there is none to be read.</summary>
    public static string? TryRead(string exePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        string? dir = Path.GetDirectoryName(exePath);
        for (int up = 0; up <= 2 && !string.IsNullOrEmpty(dir); up++, dir = Path.GetDirectoryName(dir))
        {
            foreach (string relative in (string[])[Path.Combine("www", "data", "System.json"), Path.Combine("data", "System.json")])
            {
                if (TryTitle(Path.Combine(dir, relative)) is { } title)
                {
                    return title;
                }
            }
        }

        return null;
    }

    private static string? TryTitle(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > _maxBytes)
            {
                return null;
            }

            using FileStream stream = file.OpenRead();
            using JsonDocument doc = JsonDocument.Parse(stream);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("gameTitle", out JsonElement title)
                   && title.ValueKind == JsonValueKind.String
                   && title.GetString() is { } text
                   && !string.IsNullOrWhiteSpace(text)
                ? text.Trim()
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
