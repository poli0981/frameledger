using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>A session with its game's name, for the Dashboard's recent list (rows carry only a game id).</summary>
public sealed record RecentSession(SessionRow Row, string GameName);
