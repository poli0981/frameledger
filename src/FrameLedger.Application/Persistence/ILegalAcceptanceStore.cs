namespace FrameLedger.Application.Persistence;

/// <summary>
/// <c>legal_acceptance</c>: one row per document (FR-11). Read by both processes; written by the UI's Legal
/// Gate only (<c>06_DATA_MODEL</c> §Writer ownership) — the writer exists since P3 PR-3, the gate that calls it
/// is PR-9's.
/// </summary>
public interface ILegalAcceptanceStore
{
    ValueTask<LegalAcceptance?> FindAsync(string document, CancellationToken ct = default);

    ValueTask<IReadOnlyList<LegalAcceptance>> ListAsync(CancellationToken ct = default);

    /// <summary>Records (or re-records, on a version increment) the acceptance of one document.</summary>
    ValueTask RecordAsync(LegalAcceptance acceptance, CancellationToken ct = default);
}
