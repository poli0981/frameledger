namespace FrameLedger.Application.Import;

/// <summary>What one import did: rows added (hooking off, every one), and candidates left alone (already there, or no executable).</summary>
public sealed record ImportReport(int Added, int Skipped);
