using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Tests;

/// <summary>An <see cref="ISettingsStore"/> in a dictionary, for a flow that reads a setting or two and has no ledger of its own.</summary>
internal sealed class MemorySettings : ISettingsStore
{
    public Dictionary<string, string> Rows { get; } = new(StringComparer.Ordinal);

    public ValueTask<string?> GetAsync(string key, CancellationToken ct = default) => ValueTask.FromResult(Rows.GetValueOrDefault(key));

    public ValueTask SetAsync(string key, string value, CancellationToken ct = default)
    {
        Rows[key] = value;
        return ValueTask.CompletedTask;
    }
}
