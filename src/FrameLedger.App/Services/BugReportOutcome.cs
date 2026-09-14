namespace FrameLedger.App.Services;

/// <summary>Where one run of the flow ended.</summary>
public sealed record BugReportOutcome(string? ZipPath, BugReportChoice Choice, bool Written);
