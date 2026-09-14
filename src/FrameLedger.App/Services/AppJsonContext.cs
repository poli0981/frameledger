using System.Text.Json.Serialization;

namespace FrameLedger.App.Services;

/// <summary>Source-generated JSON for what the pages READ (<c>field_provenance</c>, <c>capability_flags</c>) and the one document they WRITE (FR-9.2).</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, bool>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(SessionExportDocument))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
