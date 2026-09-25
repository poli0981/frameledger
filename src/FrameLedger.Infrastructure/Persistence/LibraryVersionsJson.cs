using System.Text.Json;
using System.Text.Json.Serialization;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>
/// <c>games.library_versions</c> (schema 0011): the capability files a game ships, as a JSON array of
/// <c>{"capability","path","fileVersion","productVersion"}</c>. The names are the column's, not the Domain record's, so
/// renaming a property cannot silently change what is stored.
/// </summary>
public static class LibraryVersionsJson
{
    public static string Serialize(IReadOnlyList<LibraryFile> libraries)
    {
        ArgumentNullException.ThrowIfNull(libraries);
        Dto[] dtos = [.. libraries.Select(static l => new Dto(l.CapabilityId, l.RelativePath, l.FileVersion, l.ProductVersion))];
        return JsonSerializer.Serialize(dtos, LedgerJsonContext.Default.LibraryFileArray);
    }

    /// <summary>What the column holds; empty for NULL and for a value this build cannot read (the next scan rewrites it).</summary>
    public static IReadOnlyList<LibraryFile> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            Dto[]? dtos = JsonSerializer.Deserialize(json, LedgerJsonContext.Default.LibraryFileArray);
            return dtos is null
                ? []
                : [.. dtos.Where(static d => !string.IsNullOrWhiteSpace(d.Capability) && !string.IsNullOrWhiteSpace(d.Path))
                    .Select(static d => new LibraryFile(d.Capability!, d.Path!, d.FileVersion, d.ProductVersion))];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>One element of the column, as stored.</summary>
    internal sealed record Dto(
        [property: JsonPropertyName("capability")] string? Capability,
        [property: JsonPropertyName("path")] string? Path,
        [property: JsonPropertyName("fileVersion")] string? FileVersion,
        [property: JsonPropertyName("productVersion")] string? ProductVersion);
}
