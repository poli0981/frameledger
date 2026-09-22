namespace FrameLedger.Application.Import;

/// <summary>
/// One row of the review checklist (FR-1.2): the store's record, the executable the import would add — the store's
/// own when it names one, else the locator's guess — whether the ledger already has it, and whether the file is
/// there to be read at all. A candidate with no executable cannot be imported and the list says so.
/// </summary>
/// <remarks>
/// <see cref="MovedFrom"/> (2026-09-23) is the path of the library entry this executable already is, on a drive whose
/// letter changed: that entry follows the file (the Agent's relocator), so importing it again would make the second,
/// side-by-side entry the owner's library showed for every game on the re-lettered drive.
/// </remarks>
public sealed record ImportCandidate(StoreGame Game, string? ExePath, bool AlreadyInLibrary, bool ExecutableGuessed, bool ExecutableMissing = false,
    string? MovedFrom = null)
{
    /// <summary>An executable is known, it is on disk, and the ledger does not have it yet — under this letter or another.</summary>
    public bool CanImport => ExePath is not null && !ExecutableMissing && !AlreadyInLibrary && MovedFrom is null;
}
