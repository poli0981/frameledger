namespace FrameLedger.App.Services;

/// <summary>The save-file question (FR-9), behind an interface so the export path is testable without a dialog.</summary>
public interface IFileSaver
{
    /// <summary>The chosen path, or null when cancelled. <paramref name="filter"/> is the dialog filter text; <paramref name="suggestedName"/> the default file name.</summary>
    string? PickSavePath(string filter, string suggestedName);
}
