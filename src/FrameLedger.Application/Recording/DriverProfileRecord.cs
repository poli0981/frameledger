using System.Globalization;
using System.Text.Json;
using FrameLedger.Application.Capture;

namespace FrameLedger.Application.Recording;

/// <summary>
/// What <c>sessions.driver_profile</c> stores (schema 0012, beta.8): the NVIDIA driver profile the session's executable ran
/// under, as <see cref="IDriverProfileSource"/> read it at the session's start — the outcome, the profile's name, and each
/// setting asked with its value (null = set in no profile) and where the value comes from. Ids are stored as hex text so the
/// column reads as the driver's own names do.
/// </summary>
public sealed record DriverProfileRecord
{
    public required string Outcome { get; init; }

    public string? Profile { get; init; }

    public int? NvapiStatus { get; init; }

    public IReadOnlyList<DriverSettingRecord> Settings { get; init; } = [];

    /// <summary>The column for a reading; null for one that was never asked.</summary>
    public static string? Serialize(DriverProfileReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        if (reading.Outcome == DriverProfileOutcome.NotRead)
        {
            return null;
        }

        var record = new DriverProfileRecord
        {
            Outcome = reading.Outcome.ToString(),
            Profile = reading.ProfileName,
            NvapiStatus = reading.NvapiStatus,
            Settings = [.. reading.Settings.Select(static s => new DriverSettingRecord
            {
                Id = Hex(s.Id),
                Value = s.Value,
                Location = s.Location.ToString(),
                Predefined = s.Predefined,
            })],
        };
        return JsonSerializer.Serialize(record, RecordingJsonContext.Default.DriverProfileRecord);
    }

    /// <summary>The column read back; null for NULL and for a value this build cannot read.</summary>
    public static DriverProfileRecord? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, RecordingJsonContext.Default.DriverProfileRecord);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The value the profile gives <paramref name="id"/>, or null when it is set in no profile or was not read.</summary>
    public uint? ValueOf(uint id) =>
        Settings.FirstOrDefault(s => string.Equals(s.Id, Hex(id), StringComparison.OrdinalIgnoreCase))?.Value;

    private static string Hex(uint id) => "0x" + id.ToString("X8", CultureInfo.InvariantCulture);
}
