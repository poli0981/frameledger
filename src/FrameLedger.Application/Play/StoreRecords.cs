// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Import;

namespace FrameLedger.Application.Play;

/// <summary>Which store installed a library entry's game (beta.21, Play): asked of the stores' own records, as the import reads them.</summary>
public static class StoreRecords
{
    /// <summary>
    /// The record whose install folder holds <paramref name="exePath"/> — the deepest such folder, and among equals the one
    /// whose id is the entry's <paramref name="storeId"/> — or null when no store installed it. The entry's own platform is
    /// not asked: detection may have rewritten it (an itch or GOG build that ships Steam's API reads as Steam) while its store
    /// id stayed the importer's, and a Steam link built from an itch id would start some other game.
    /// </summary>
    public static StoreGame? Owning(string exePath, string? storeId, IEnumerable<StoreGame> records)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        ArgumentNullException.ThrowIfNull(records);
        string exe = Full(exePath);
        StoreGame? best = null;
        int bestLength = -1;
        foreach (StoreGame record in records)
        {
            if (string.IsNullOrWhiteSpace(record.InstallDirectory))
            {
                continue;
            }

            string folder = Full(record.InstallDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!exe.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool deeper = folder.Length > bestLength;
            bool sameButNamed = folder.Length == bestLength && storeId is not null
                && string.Equals(record.StoreId, storeId, StringComparison.Ordinal)
                && !string.Equals(best?.StoreId, storeId, StringComparison.Ordinal);
            if (deeper || sameButNamed)
            {
                best = record;
                bestLength = folder.Length;
            }
        }

        return best;
    }

    private static string Full(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }
}
