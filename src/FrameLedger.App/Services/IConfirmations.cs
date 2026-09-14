namespace FrameLedger.App.Services;

/// <summary>The confirmations a page asks (<c>08_UI</c> §UX rules: every destructive action confirms), behind an interface for the tests.</summary>
public interface IConfirmations
{
    Task<RemoveGameChoice> RemoveGameAsync(string gameName, CancellationToken ct = default);
}
