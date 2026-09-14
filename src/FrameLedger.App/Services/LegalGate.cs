using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>
/// FR-11 over <c>ILegalAcceptanceStore</c>: the gate is required while any of the four documents has no
/// acceptance row at this build's version — a first run, or a document whose version moved. Accept writes one
/// row per document with the document's own version (<c>06_DATA_MODEL</c> §Writer ownership: the UI's table).
/// D8 stands: accepting here is not a precondition of per-game consent, which has its own dialog.
/// </summary>
public sealed class LegalGate
{
    private readonly ILegalAcceptanceStore _store;
    private readonly TimeProvider _clock;

    public LegalGate(ILegalAcceptanceStore store, IReadOnlyList<LegalDocument> documents, TimeProvider? clock = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        Documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _clock = clock ?? TimeProvider.System;
    }

    public IReadOnlyList<LegalDocument> Documents { get; }

    /// <summary>The documents with no acceptance at their current version; empty when the gate is satisfied.</summary>
    public async Task<IReadOnlyList<LegalDocument>> OutstandingAsync(CancellationToken ct = default)
    {
        var outstanding = new List<LegalDocument>();
        foreach (LegalDocument document in Documents)
        {
            LegalAcceptance? accepted = await _store.FindAsync(document.Key, ct).ConfigureAwait(false);
            if (accepted is null || !string.Equals(accepted.Version, document.Version, StringComparison.Ordinal))
            {
                outstanding.Add(document);
            }
        }

        return outstanding;
    }

    public async Task<bool> IsRequiredAsync(CancellationToken ct = default) => (await OutstandingAsync(ct).ConfigureAwait(false)).Count > 0;

    /// <summary>One Accept, four rows — the single button FR-11 specifies.</summary>
    public async Task AcceptAllAsync(CancellationToken ct = default)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        foreach (LegalDocument document in Documents)
        {
            await _store.RecordAsync(new LegalAcceptance(document.Key, document.Version, now), ct).ConfigureAwait(false);
        }
    }
}
