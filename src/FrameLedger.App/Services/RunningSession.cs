using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>One session the Agent is running, as the App knows it: its entry, its tier and — for Tier 2 — why nothing is measured.</summary>
public sealed record RunningSession(Guid SessionGuid, long GameId, string? GameName, int Tier, DateTimeOffset StartedAt, SessionHold? Hold);
