namespace FrameLedger.Application.Import;

/// <summary>
/// One installed title as a store's own records describe it (FR-1.2, P4 PR-4): the platform id in
/// <c>games.platform</c>'s vocabulary, the store's id for it, its display name, where it is installed, the
/// executable when the store names one (GOG and Epic do; Steam and itch do not), and the store's version string.
/// Read from local launcher files and registry keys only — nothing is fetched (CLAUDE.md rule 8).
/// </summary>
public sealed record StoreGame(string Platform, string StoreId, string Name, string InstallDirectory, string? ExePath, string? Version);
