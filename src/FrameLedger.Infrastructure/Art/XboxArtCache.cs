// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Art;
using FrameLedger.Application.Import;
using FrameLedger.Infrastructure.Import;

namespace FrameLedger.Infrastructure.Art;

/// <summary>
/// The pictures an Xbox game ships in its own package folder (beta.33, D60 "a game's artwork is what is already on the
/// PC"): <c>MicrosoftGame.config</c>'s <c>ShellVisuals</c> names them — <c>Square480x480Logo</c> (480×480 on the owner's 11
/// games, else <c>Square150x150Logo</c>) as a square cover, <c>SplashScreenImage</c> (1920×1080) as the banner and the
/// landscape header (<c>spike-notes</c> §25). Read from the record's install folder, which is the package root.
/// </summary>
/// <remarks>
/// A name that points outside the package root is ignored, whatever the file says. Where the plain file is absent, the
/// MSIX resource variants beside it (<c>Logo.scale-200.png</c>) are tried, the largest first; the 11 measured all ship the
/// plain names.
/// </remarks>
public sealed class XboxArtCache : IStoreArtCache
{
    public string Platform => "xbox";

    public GameArtFiles Find(StoreGame record)
    {
        ArgumentNullException.ThrowIfNull(record);
        try
        {
            if (GdkPackageFiles.Read(record.InstallDirectory) is not { } package)
            {
                return GameArtFiles.None;
            }

            string? square = Image(record.InstallDirectory, package.Square480Logo) ?? Image(record.InstallDirectory, package.Square150Logo);
            string? splash = Image(record.InstallDirectory, package.SplashScreen);
            return new GameArtFiles(Cover: null, Hero: splash, Header: splash, Square: square);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return GameArtFiles.None;
        }
    }

    /// <summary>The image <paramref name="relative"/> names inside <paramref name="root"/>, or null when there is none there.</summary>
    public static string? Image(string root, string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        string rootFull;
        string full;
        try
        {
            rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            full = Path.GetFullPath(Path.Combine(rootFull, relative));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        if (!full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return null;    // a name that escapes the package is not the package's picture
        }

        var plain = new FileInfo(full);
        if (plain.Exists && plain.Length > 0)
        {
            return plain.FullName;
        }

        return plain.Directory is { Exists: true } folder
            ? folder.EnumerateFiles(Path.GetFileNameWithoutExtension(full) + ".scale-*" + Path.GetExtension(full))
                .Where(static f => f.Length > 0)
                .OrderByDescending(static f => f.Length)
                .FirstOrDefault()?.FullName
            : null;
    }
}
