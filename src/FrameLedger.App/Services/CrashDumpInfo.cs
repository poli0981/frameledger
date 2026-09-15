namespace FrameLedger.App.Services;

/// <summary>The newest minidump the bug bundle could carry (P4 PR-9): where it is, when it was written, how big it is.</summary>
public sealed record CrashDumpInfo(string Path, DateTimeOffset WrittenAt, long Bytes);
