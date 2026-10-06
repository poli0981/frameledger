// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.App.Services;

/// <summary>
/// Where the App reads and logs: the Agent's data directory (<c>01_ARCHITECTURE</c> §Data directory —
/// <c>ledger.db</c> beside <c>logs\ui-*.log</c>), or under <c>--data-dir</c> a copy of one (beta.15, D52:
/// <see cref="UseViewerFolder"/>, set once by <c>Program.Main</c> before anything reads a path). The ledger is the Agent's,
/// and <c>06_DATA_MODEL</c> §Writer ownership says which tables the App may write.
/// </summary>
internal static class UiPaths
{
    private static string? _viewerFolder;

    /// <summary><c>%LOCALAPPDATA%\FrameLedger</c>: the folder the profile's Agent records into.</summary>
    public static string ProfileDirectory => LedgerPaths.DefaultDirectory;

    /// <summary>True when this App is a viewer over a copy (<see cref="UiMode"/>).</summary>
    public static bool IsViewer => _viewerFolder is not null;

    public static string DataDirectory => _viewerFolder ?? ProfileDirectory;

    public static string Database => Path.Combine(DataDirectory, LedgerPaths.DatabaseFileName);

    public static string Logs => Path.Combine(DataDirectory, "logs");

    /// <summary><c>10_LOGGING</c> §Crash handling: the minidumps of both processes' crashes (P4 PR-9), the newest five kept.</summary>
    public static string CrashDumps => Path.Combine(DataDirectory, "crashdumps");

    /// <summary>What the composition is told about the folder (<see cref="UiMode"/>).</summary>
    public static UiMode Mode => new(IsViewer, DataDirectory);

    /// <summary>
    /// D52: this App views the copy in <paramref name="folder"/> — once, before the first path is read, and never the
    /// profile's own folder (<see cref="DataFolderArgument"/> refused that already; this is the second lock on that door).
    /// </summary>
    public static void UseViewerFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        if (_viewerFolder is not null)
        {
            throw new InvalidOperationException("the viewer folder is set once");
        }

        if (string.Equals(DataFolderArgument.Normalise(folder), DataFolderArgument.Normalise(ProfileDirectory), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("--data-dir names the profile's own folder; a viewer is for a copy");
        }

        _viewerFolder = folder;
    }

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(Logs);
    }
}
