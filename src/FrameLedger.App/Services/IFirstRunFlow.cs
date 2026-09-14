namespace FrameLedger.App.Services;

/// <summary>The first-run window, behind a seam so the Settings page's "reopen the legal documents" is testable.</summary>
public interface IFirstRunFlow
{
    /// <summary>Whether FR-11's gate must be shown before the shell.</summary>
    Task<bool> IsRequiredAsync(CancellationToken ct = default);

    /// <summary>Show the flow and wait for it: true when accepted and finished, false when declined or closed.</summary>
    Task<bool> RunAsync(CancellationToken ct = default);

    /// <summary>Show the documents only (Settings ▸ Reopen); nothing is written.</summary>
    Task ShowDocumentsAsync(CancellationToken ct = default);
}
