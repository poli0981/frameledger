namespace FrameLedger.App.Services;

/// <summary>D33: what the exception's disclosure names — the game, the anti-cheat family and its signal, and the evidence.</summary>
public sealed record AntiCheatExceptionFacts(string GameName, string Family, string Signal, int Sessions);
