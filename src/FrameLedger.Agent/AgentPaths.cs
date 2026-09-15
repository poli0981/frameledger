using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.Agent;

/// <summary>
/// Where the Agent keeps its state: <c>%LOCALAPPDATA%\FrameLedger</c> (<c>01_ARCHITECTURE</c> §Data directory),
/// of which the Agent is the sole owner (§S18 blocker 3) — or, under <c>--console --data-dir</c> only, a directory
/// an operator or an integration test names (HANDOFF §P2 decision D6). <c>--serve</c> never takes one: the
/// product's directory is not selectable, and a test must not be able to point the product at a profile.
/// </summary>
/// <param name="DataDirectory">The root.</param>
internal sealed record AgentPaths(string DataDirectory)
{
    public static AgentPaths Default => new(LedgerPaths.DefaultDirectory);

    /// <summary>
    /// The profile ledger (<c>%LOCALAPPDATA%\FrameLedger</c>) rather than a <c>--data-dir</c>: machine-wide side effects —
    /// the Vulkan layer's HKCU registration (P4 PR-2) — are taken only here, so a test or developer ledger never
    /// reaches the user's registry (HANDOFF D6/D17's shape).
    /// </summary>
    public bool IsProfile => string.Equals(Path.GetFullPath(DataDirectory), Path.GetFullPath(LedgerPaths.DefaultDirectory), StringComparison.OrdinalIgnoreCase);

    public string Database => Path.Combine(DataDirectory, LedgerPaths.DatabaseFileName);

    public string Tmp => Path.Combine(DataDirectory, "tmp");

    public string Logs => Path.Combine(DataDirectory, "logs");

    /// <summary><c>10_LOGGING</c> §Crash handling: the minidumps of both processes' crashes (P4 PR-9), the newest five kept.</summary>
    public string CrashDumps => Path.Combine(DataDirectory, "crashdumps");

    public string VkLayerDirectory => Path.Combine(DataDirectory, "vklayer");

    /// <summary>§S22: the payload must resolve into the guard's own directory, so it is beside this binary and nowhere else.</summary>
    public static string Payload => Path.Combine(AppContext.BaseDirectory, "FrameLedger.Overlay.dll");

    public static string VkLayerDll => Path.Combine(AppContext.BaseDirectory, "FrameLedger.VkLayer.dll");
}
