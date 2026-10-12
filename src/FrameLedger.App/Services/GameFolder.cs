// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using FrameLedger.Infrastructure.Detection;

namespace FrameLedger.App.Services;

/// <summary>
/// The folder Change executable… opens in (beta.19, owner request 2026-10-12): the game's own root folder rather than
/// wherever Windows' dialog was last — the pick is almost always another file of the same install.
/// </summary>
/// <remarks>
/// <para>
/// The root is <see cref="InstallRoot.Resolve"/>'s — the folder a store boundary names (<c>steamapps\common\X</c>,
/// <c>GOG Galaxy\Games\X</c>, <c>Epic Games\X</c>, <c>GamesInstalled\X</c>) — and, where no boundary is recognised, out of
/// an Unreal package's <c>&lt;root&gt;\&lt;Project&gt;\Binaries\Win64</c> to <c>&lt;root&gt;</c>, else the executable's own
/// folder. Only the row's path is stored (no install folder), so this is derived, never remembered.
/// </para>
/// <para>
/// An entry whose folder is gone (*Not installed*) opens at the nearest folder above it that is still there; an entry on a
/// drive that is not connected gets null, and the dialog opens where Windows last was. It asks the file system, so its
/// caller runs it off the UI thread: a share that went away can take seconds to answer.
/// </para>
/// </remarks>
public static class GameFolder
{
    /// <summary>The folder to open the pick in for an entry at <paramref name="exePath"/>, or null when there is none.</summary>
    public static string? StartFolder(string exePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        string full;
        try
        {
            full = Path.GetFullPath(exePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        string? drive = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(drive) || !Directory.Exists(drive))
        {
            return null;
        }

        string exeDirectory = Path.GetDirectoryName(full) ?? drive;
        string root = InstallRoot.Resolve(full);
        if (string.Equals(root, exeDirectory, StringComparison.OrdinalIgnoreCase) && UnrealRoot(exeDirectory) is { } unreal)
        {
            root = unreal;
        }

        for (string? folder = root; !string.IsNullOrEmpty(folder); folder = Path.GetDirectoryName(folder))
        {
            if (Directory.Exists(folder))
            {
                return folder;
            }
        }

        return drive;
    }

    /// <summary><c>&lt;root&gt;\&lt;Project&gt;\Binaries\Win64</c> (or <c>WinGDK</c>) → <c>&lt;root&gt;</c>; null for any other folder.</summary>
    private static string? UnrealRoot(string exeDirectory)
    {
        string platform = Path.GetFileName(exeDirectory);
        string? binaries = Path.GetDirectoryName(exeDirectory);
        bool unreal = (string.Equals(platform, "Win64", StringComparison.OrdinalIgnoreCase) || string.Equals(platform, "WinGDK", StringComparison.OrdinalIgnoreCase))
            && binaries is not null && string.Equals(Path.GetFileName(binaries), "Binaries", StringComparison.OrdinalIgnoreCase);
        string? project = unreal ? Path.GetDirectoryName(binaries) : null;
        return project is null ? null : Path.GetDirectoryName(project);
    }
}
