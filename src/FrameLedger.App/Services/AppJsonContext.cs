using System.Text.Json.Serialization;

namespace FrameLedger.App.Services;

/// <summary>Source-generated JSON for the two JSON columns the pages READ (<c>field_provenance</c>, <c>capability_flags</c>); the writer is <c>Infrastructure</c>'s.</summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, bool>))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
