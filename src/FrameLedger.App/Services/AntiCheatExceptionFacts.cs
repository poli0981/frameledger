namespace FrameLedger.App.Services;

/// <summary>
/// D33: what the exception's disclosure names — the game, the anti-cheat family and its signal. (D38 took the session count
/// out: a grant needs no session, and its first two are a trial the disclosure describes.)
/// </summary>
public sealed record AntiCheatExceptionFacts(string GameName, string Family, string Signal);
