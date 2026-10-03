namespace FrameLedger.App.Services;

/// <summary>
/// The session summary windows that are open (beta.11, owner decision D40). After Delete all sessions a window left open
/// would show a session that is gone, and a note written from it would go to whichever session reuses its id (the table has
/// no AUTOINCREMENT).
/// </summary>
public interface ISessionWindows
{
    /// <summary>Closes every open summary window whose session is no longer in the ledger.</summary>
    Task CloseDeletedAsync(CancellationToken ct = default);
}
