namespace FrameLedger.App.Charts;

/// <summary>One session on the trend line: when, the metric's value, which session, and whether FR-6.4 excludes it by default.</summary>
public sealed record TrendPoint(DateTimeOffset At, double Value, long SessionId, bool SettingsChangedMidSession);
