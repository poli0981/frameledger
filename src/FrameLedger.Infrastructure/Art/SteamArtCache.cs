// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Art;
using FrameLedger.Infrastructure.Import;

namespace FrameLedger.Infrastructure.Art;

/// <summary>
/// The artwork the Steam app keeps for each game it shows (beta.23, owner decision D60), under
/// <c>&lt;Steam&gt;\appcache\librarycache\&lt;appid&gt;\</c> — read on the owner's PC on 2026-10-14 (3,082 app folders) in two
/// layouts, sometimes both in one folder: the files directly in the app's folder (<c>library_600x900.jpg</c>,
/// <c>library_hero.jpg</c>, <c>header.jpg</c>), or each in a folder named by a hash (<c>library_capsule.jpg</c>,
/// <c>library_hero.jpg</c>, <c>library_header.jpg</c>). The cover is 300×450, the banner 1920×620, the header 460×215 as Steam
/// keeps them. The folder's own index (<c>assetcache.vdf</c>) is binary and not read: the names say what each file is, and
/// where an asset was replaced its newest copy wins. The Steam root is <c>HKCU\Software\Valve\Steam\SteamPath</c>, as the
/// import reads it, unless a test hands one in.
/// </summary>
public sealed class SteamArtCache(string? steamRoot = null) : IStoreArtCache
{
    private static readonly string[] _covers = ["library_600x900_2x.jpg", "library_600x900.jpg", "library_capsule.jpg"];
    private static readonly string[] _heroes = ["library_hero.jpg"];
    private static readonly string[] _headers = ["header.jpg", "library_header.jpg"];

    public string Platform => "steam";

    public GameArtFiles Find(string storeId)
    {
        if (!IsNumericId(storeId) || (steamRoot ?? SteamLibrarySource.InstalledRoot()) is not { } root)
        {
            return GameArtFiles.None;
        }

        try
        {
            var folder = new DirectoryInfo(Path.Combine(root, "appcache", "librarycache", storeId));
            if (!folder.Exists)
            {
                return GameArtFiles.None;
            }

            List<FileInfo> files = [.. folder.EnumerateFiles()];
            foreach (DirectoryInfo hashed in folder.EnumerateDirectories().Where(static d => !d.Attributes.HasFlag(FileAttributes.ReparsePoint)))
            {
                files.AddRange(hashed.EnumerateFiles());
            }

            return new GameArtFiles(Newest(files, _covers), Newest(files, _heroes), Newest(files, _headers));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return GameArtFiles.None;
        }
    }

    /// <summary>
    /// A store id that is digits only — Steam's app ids and GOG's product ids are — so a row's id can name no other folder
    /// (<c>..\</c>, a rooted path) when it is joined to the store's own.
    /// </summary>
    internal static bool IsNumericId(string? id) => id is { Length: > 0 and <= 12 } && id.All(char.IsAsciiDigit);

    /// <summary>The first of <paramref name="names"/>, in order of preference, that is there and not empty; the newest copy of it.</summary>
    internal static string? Newest(IReadOnlyList<FileInfo> files, IReadOnlyList<string> names)
    {
        foreach (string name in names)
        {
            FileInfo? newest = files
                .Where(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase) && f.Length > 0)
                .OrderByDescending(static f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (newest is not null)
            {
                return newest.FullName;
            }
        }

        return null;
    }
}
