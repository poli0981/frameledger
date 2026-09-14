using FrameLedger.Application.Persistence;

namespace FrameLedger.Application.Settings;

/// <summary>
/// The registry over the store: reads are tolerant (an absent or invalid row is the definition's default),
/// writes are exact (a value the definition refuses throws before anything is stored). Every read goes to the
/// table — nothing is cached, which is what lets the Agent see the UI's write at the next session start (D16).
/// </summary>
public sealed class RegisteredSettings
{
    private readonly ISettingsStore _store;

    public RegisteredSettings(ISettingsStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>The effective stored text: valid row, or the default.</summary>
    public async ValueTask<string> GetAsync(SettingDefinition definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Effective(await _store.GetAsync(definition.Key, ct).ConfigureAwait(false));
    }

    public async ValueTask<bool> GetBooleanAsync(SettingDefinition definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.AsBoolean(await _store.GetAsync(definition.Key, ct).ConfigureAwait(false));
    }

    public async ValueTask<int> GetIntegerAsync(SettingDefinition definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.AsInteger(await _store.GetAsync(definition.Key, ct).ConfigureAwait(false));
    }

    /// <summary>Stores <paramref name="value"/> under the definition's key, normalised; refuses a value the definition does not accept.</summary>
    public ValueTask SetAsync(SettingDefinition definition, string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return _store.SetAsync(definition.Key, definition.Normalise(value), ct);
    }

    public ValueTask SetAsync(SettingDefinition definition, bool value, CancellationToken ct = default) =>
        SetAsync(definition, value ? "1" : "0", ct);

    public ValueTask SetAsync(SettingDefinition definition, int value, CancellationToken ct = default) =>
        SetAsync(definition, value.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
}
