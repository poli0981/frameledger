namespace FrameLedger.Application.Persistence;

/// <summary>One on-demand retention sweep across every game: the games that have sessions, and the sessions whose raw series went.</summary>
public sealed record RetentionSweepResult(int Games, int Sessions);
