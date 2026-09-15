namespace FrameLedger.App.Services;

/// <summary>The newest session in the ledger, as the bug report offers it: which one, of what, and when.</summary>
public sealed record LastSessionInfo(long SessionId, string Game, DateTimeOffset StartedAt);
