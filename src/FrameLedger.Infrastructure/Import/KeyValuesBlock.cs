namespace FrameLedger.Infrastructure.Import;

/// <summary>A block: string values or nested blocks under case-insensitive keys.</summary>
public sealed class KeyValuesBlock
{
    private readonly Dictionary<string, object> _entries = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, object> Entries => _entries;

    public string? Value(string key) => _entries.TryGetValue(key, out object? v) && v is string s ? s : null;

    public KeyValuesBlock? Child(string key) => _entries.TryGetValue(key, out object? v) && v is KeyValuesBlock b ? b : null;

    /// <summary>Every nested block, in file order.</summary>
    public IEnumerable<KeyValuePair<string, KeyValuesBlock>> Children => _entries.Where(static e => e.Value is KeyValuesBlock).Select(static e => new KeyValuePair<string, KeyValuesBlock>(e.Key, (KeyValuesBlock)e.Value));

    internal void Set(string key, object value) => _entries[key] = value;
}
