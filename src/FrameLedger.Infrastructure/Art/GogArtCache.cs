// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Art;

namespace FrameLedger.Infrastructure.Art;

/// <summary>
/// The artwork GOG GALAXY keeps for each game in its library (beta.23, owner decision D60), under
/// <c>%ProgramData%\GOG.com\Galaxy\webcache\&lt;user&gt;\gog\&lt;productId&gt;\</c> — read on the owner's PC on 2026-10-14 (80
/// products): <c>&lt;sha256&gt;_glx_vertical_cover.webp</c> (the cover), <c>&lt;sha256&gt;_glx_bg_top_padding_7.webp</c> (the
/// banner) and a square icon, WebP that Windows' own WebP codec decodes (an optional Store component: without it the
/// game simply has no image). Every Galaxy user's folder is looked in, the newest copy wins. GOG keeps no header.
/// </summary>
public sealed class GogArtCache(string? webcacheRoot = null) : IStoreArtCache
{
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GOG.com", "Galaxy", "webcache");

    public string Platform => "gog";

    public GameArtFiles Find(string storeId)
    {
        if (!SteamArtCache.IsNumericId(storeId))
        {
            return GameArtFiles.None;
        }

        try
        {
            var root = new DirectoryInfo(webcacheRoot ?? DefaultRoot);
            if (!root.Exists)
            {
                return GameArtFiles.None;
            }

            List<FileInfo> files = [];
            foreach (DirectoryInfo user in root.EnumerateDirectories().Where(static d => !d.Attributes.HasFlag(FileAttributes.ReparsePoint)))
            {
                var product = new DirectoryInfo(Path.Combine(user.FullName, "gog", storeId));
                if (product.Exists && !product.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    files.AddRange(product.EnumerateFiles());
                }
            }

            return new GameArtFiles(Newest(files, "_glx_vertical_cover."), Newest(files, "_glx_bg_top_padding_") ?? Newest(files, "_glx_background"), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return GameArtFiles.None;
        }
    }

    private static string? Newest(IEnumerable<FileInfo> files, string marker) => files
        .Where(f => f.Name.Contains(marker, StringComparison.OrdinalIgnoreCase) && f.Length > 0)
        .OrderByDescending(static f => f.LastWriteTimeUtc)
        .FirstOrDefault()?.FullName;
}
