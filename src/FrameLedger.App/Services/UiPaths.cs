using System.IO;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.App.Services;

/// <summary>
/// Where the App reads and logs: the Agent's data directory (<c>01_ARCHITECTURE</c> §Data directory —
/// <c>ledger.db</c> beside <c>logs\ui-*.log</c>). The App never chooses another location; the ledger is the
/// Agent's, and <c>06_DATA_MODEL</c> §Writer ownership says which tables the App may write.
/// </summary>
internal static class UiPaths
{
    public static string DataDirectory => LedgerPaths.DefaultDirectory;

    public static string Database => LedgerPaths.DefaultDatabase;

    public static string Logs => Path.Combine(DataDirectory, "logs");

    /// <summary><c>10_LOGGING</c> §Crash handling: the minidumps of both processes' crashes (P4 PR-9), the newest five kept.</summary>
    public static string CrashDumps => Path.Combine(DataDirectory, "crashdumps");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(Logs);
    }
}
