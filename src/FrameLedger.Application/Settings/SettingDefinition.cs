using System.Globalization;

namespace FrameLedger.Application.Settings;

/// <summary>
/// One row of the settings registry (HANDOFF §P3 decision D16, <c>06_DATA_MODEL</c> §settings): the key, its
/// kind, its default, its range or choices, and whether the Agent reads it. A definition VALIDATES writes and
/// TOLERATES reads — a hand-edited row that fails validation reads as the default, never as an exception.
/// </summary>
public sealed record SettingDefinition
{
    public required string Key { get; init; }

    public required SettingKind Kind { get; init; }

    /// <summary>The stored text of the default, exactly as <see cref="Normalise"/> would write it.</summary>
    public required string Default { get; init; }

    /// <summary>Inclusive bounds for <see cref="SettingKind.WholeNumber"/>.</summary>
    public int Minimum { get; init; }

    public int Maximum { get; init; } = int.MaxValue;

    /// <summary>The allowed values for <see cref="SettingKind.Choice"/>.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>
    /// True when the Agent reads the key at each session start (D16: <c>hooking.*</c>, <c>capture.*</c>,
    /// <c>telemetry.*</c>, <c>retention.*</c>); no "setting changed" message exists or is needed.
    /// </summary>
    public bool AgentReads { get; init; }

    /// <summary>Why a value is refused, or null when it is valid.</summary>
    public string? Validate(string? value)
    {
        if (value is null)
        {
            return "no value";
        }

        return Kind switch
        {
            SettingKind.Boolean => value is "0" or "1" ? null : $"'{value}' is not 0 or 1",
            SettingKind.WholeNumber => ValidateInteger(value),
            SettingKind.Choice => Choices.Contains(value, StringComparer.Ordinal) ? null : $"'{value}' is not one of {string.Join('|', Choices)}",
            _ => throw new InvalidOperationException($"{Key}: unknown kind {Kind}"),
        };
    }

    /// <summary>The stored text for a value this definition accepts; throws otherwise.</summary>
    public string Normalise(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string trimmed = value.Trim();
        if (Validate(trimmed) is string why)
        {
            throw new ArgumentException($"{Key}: {why}", nameof(value));
        }

        return Kind == SettingKind.WholeNumber
            ? int.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
            : trimmed;
    }

    /// <summary>A valid stored value, or the default when the row is absent or fails validation (tolerant read).</summary>
    public string Effective(string? stored) => stored is not null && Validate(stored) is null ? stored : Default;

    public bool AsBoolean(string? stored) => string.Equals(Effective(stored), "1", StringComparison.Ordinal);

    public int AsInteger(string? stored) => int.Parse(Effective(stored), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private string? ValidateInteger(string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return $"'{value}' is not an integer";
        }

        return parsed < Minimum || parsed > Maximum ? $"{parsed} is outside {Minimum}..{Maximum}" : null;
    }
}
