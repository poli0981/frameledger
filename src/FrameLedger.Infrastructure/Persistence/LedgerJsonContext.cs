using System.Text.Json.Serialization;

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>Source-generated JSON for the two JSON columns the UI writes: <c>session_annotations.tags</c> (a string array) and <c>games.field_provenance</c> (field → provenance) — and, since schema 0011, the Agent's <c>games.library_versions</c>.</summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(LibraryVersionsJson.Dto[]), TypeInfoPropertyName = "LibraryFileArray")]
internal sealed partial class LedgerJsonContext : JsonSerializerContext
{
}
