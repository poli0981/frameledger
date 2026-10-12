// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Security;
using FrameLedger.Application.Watch;

namespace FrameLedger.Infrastructure.Watch;

/// <summary>
/// <see cref="IExecutablePresence"/> over the file system (beta.19, D55): tells a game that is no longer installed from a
/// drive that is not connected, a different disk under the same letter, and a folder Windows will not list — the cases
/// <c>ExecutableIdentity.Read</c>'s null runs together.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked of listings, not of <c>Exists</c>.</b> <c>File.Exists</c> and <c>Directory.Exists</c> answer false for a path
/// they may not look at as for one that is not there, so the file is looked for in its folder's listing, and a folder that
/// reads as missing is looked for in its parent's: listed but unreadable is <see cref="ExecutablePresence.Unknown"/>.
/// </para>
/// <para>
/// <b>The drive and the path above the game.</b> A drive that <see cref="VolumeRoots"/> does not list as ready (fixed or
/// removable — never a share) is <see cref="ExecutablePresence.DriveAbsent"/>. When nothing of the path is left but the
/// drive's root, the letter may well be another disk's now (the owner's USB drive has changed letters, 2026-09-22):
/// <see cref="ExecutablePresence.OnlyTheDriveRemains"/>, never a removal. An uninstall leaves the folder the game was
/// installed in — <c>steamapps\common</c>, the itch location, <c>D:\Games</c>.
/// </para>
/// </remarks>
public sealed class ExecutablePresenceProbe : IExecutablePresence
{
    private readonly Func<string, bool> _driveReady;

    /// <summary>
    /// The probe over <paramref name="driveReady"/>: whether a root is a ready fixed or removable drive — by default
    /// <see cref="VolumeRoots.IsReady"/>, which asks that one drive rather than listing them all for every entry.
    /// </summary>
    public ExecutablePresenceProbe(Func<string, bool>? driveReady = null) => _driveReady = driveReady ?? VolumeRoots.IsReady;

    /// <inheritdoc />
    public ExecutablePresence Probe(string exePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        try
        {
            string full = Path.GetFullPath(exePath);
            string? root = Path.GetPathRoot(full);
            string? folder = Path.GetDirectoryName(full);
            if (string.IsNullOrEmpty(root) || folder is null || full.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return ExecutablePresence.Unknown;
            }

            if (!_driveReady(root))
            {
                return ExecutablePresence.DriveAbsent;
            }

            if (Directory.Exists(folder))
            {
                return Directory.EnumerateFiles(folder, Path.GetFileName(full)).Any() ? ExecutablePresence.Present : Gone(folder, root);
            }

            // The folder reads as missing: up to the nearest folder that is there, which must list the one below it as absent.
            string missing = folder;
            string? parent = Path.GetDirectoryName(missing);
            while (parent is not null && !Directory.Exists(parent))
            {
                missing = parent;
                parent = Path.GetDirectoryName(parent);
            }

            return parent is null || Directory.EnumerateDirectories(parent, Path.GetFileName(missing)).Any()
                ? ExecutablePresence.Unknown
                : Gone(parent, root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException)
        {
            return ExecutablePresence.Unknown;
        }
    }

    /// <summary>Gone from <paramref name="nearest"/>, the nearest folder still there — unless that is the drive's root.</summary>
    private static ExecutablePresence Gone(string nearest, string root) =>
        string.Equals(Path.TrimEndingDirectorySeparator(nearest), Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase)
            ? ExecutablePresence.OnlyTheDriveRemains
            : ExecutablePresence.FileGone;
}
