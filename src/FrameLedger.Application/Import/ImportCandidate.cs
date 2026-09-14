namespace FrameLedger.Application.Import;

/// <summary>
/// One row of the review checklist (FR-1.2): the store's record, the executable the import would add — the store's
/// own when it names one, else the locator's guess — whether the ledger already has it, and whether the file is
/// there to be read at all. A candidate with no executable cannot be imported and the list says so.
/// </summary>
public sealed record ImportCandidate(StoreGame Game, string? ExePath, bool AlreadyInLibrary, bool ExecutableGuessed, bool ExecutableMissing = false)
{
    /// <summary>An executable is known, it is on disk, and the ledger does not have it yet.</summary>
    public bool CanImport => ExePath is not null && !ExecutableMissing && !AlreadyInLibrary;
}
