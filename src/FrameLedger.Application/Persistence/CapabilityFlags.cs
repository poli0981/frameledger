using System.Text.Json;

namespace FrameLedger.Application.Persistence;

/// <summary>
/// Reads <c>games.capability_flags</c> (P4 PR-1: a JSON array of ids; older rows: an object of booleans) without a
/// serializer context, for the one question the Agent asks of it: does the row carry a given id?
/// </summary>
public static class CapabilityFlags
{
    /// <summary>True when <paramref name="json"/> names <paramref name="id"/> (an array member, or an object key set to true). Unreadable JSON is no.</summary>
    public static bool Contains(string? json, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement e in root.EnumerateArray())
                {
                    if (e.ValueKind == JsonValueKind.String && string.Equals(e.GetString(), id, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }

            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(id, out JsonElement flag)
                && flag.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
