using System.Text.Json.Serialization;

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>Source-generated JSON for the two JSON columns the UI writes: <c>session_annotations.tags</c> (a string array) and <c>games.field_provenance</c> (field → provenance).</summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class LedgerJsonContext : JsonSerializerContext
{
}
