// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Buffers.Binary;
using System.Text;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// What the Steam app's own file on this PC says of controller support for a game (owner decision D163,
/// <c>05_DETECTION</c> §What a store lists): <c>appcache\appinfo.vdf</c>, the copy of the store's application records
/// the Steam client keeps on the disk. <b>A local file: no request is made to any store</b> (CLAUDE.md rule 8).
/// </summary>
/// <remarks>
/// <para>
/// <b>One fact is read.</b> Each record's <c>common</c> section may hold <c>controller_support</c> = <c>full</c> or
/// <c>partial</c>; a record without it lists none. Nothing else of a record is kept, and nothing is written.
/// </para>
/// <para>
/// <b>The format, version 41</b> (magic <c>0x07564429</c>) - the one the Steam client has written since mid-2024, and
/// the only one read; another magic is refused with the reason, and the fact then reads N/A. A header: the magic, a
/// universe, and the offset of a table of strings. Then one record per application until an id of zero: the id, the
/// size of what follows, sixty bytes of state and hashes, and a tree in Valve's binary key-value form whose keys are
/// numbers into that table. The layout was checked on the developer's own Steam folder on 2026-11-14 (3 581 records,
/// every tree read to its last byte); the tests' files are written by hand to the same layout.
/// </para>
/// <para>
/// <b>It trusts no length in the file.</b> A record or a string that claims more than the file holds ends the read
/// with a reason; what was found before it is not returned either - a file that cannot be read whole says nothing.
/// </para>
/// </remarks>
public static class SteamAppInfo
{
    /// <summary><c>games.controller_support_source</c> for a fact read here.</summary>
    public const string Source = "steam-appinfo";

    public const string Full = "full";

    public const string Partial = "partial";

    /// <summary>The store's record is there and lists no controller support.</summary>
    public const string None = "none";

    private const uint _magic41 = 0x07564429;
    private const int _headerBytes = 16;
    private const int _recordStateBytes = 60;
    private const int _maxDepth = 32;

    private const byte _map = 0x00;
    private const byte _string = 0x01;
    private const byte _wideString = 0x05;
    private const byte _end = 0x08;
    private const byte _endAlternate = 0x0B;

    /// <summary>Where the Steam app on this PC keeps the file, or null when Steam is not installed for this user.</summary>
    public static string? InstalledFile() => SteamLibrarySource.InstalledRoot() is { } root ? Path.Combine(root, "appcache", "appinfo.vdf") : null;

    /// <summary>Reads the file at <paramref name="path"/>; a file that is not there, cannot be opened or cannot be read is a result with a <see cref="SteamAppInfoResult.Problem"/>.</summary>
    public static SteamAppInfoResult ReadControllerSupport(string path, IReadOnlyCollection<uint> appIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(appIds);
        byte[] file;
        try
        {
            // Steam rewrites the file while it runs: shared with every other reader and writer, and never locked.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length is 0 or > int.MaxValue)
            {
                return SteamAppInfoResult.Failed("the file is empty or too large");
            }

            file = new byte[stream.Length];
            stream.ReadExactly(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SteamAppInfoResult.Failed(e.GetType().Name);
        }

        return ReadControllerSupport(file, appIds);
    }

    /// <summary>
    /// For each of <paramref name="appIds"/> that has a record in <paramref name="file"/>: <see cref="Full"/>,
    /// <see cref="Partial"/> or <see cref="None"/>. An id with no record is not in the answer: the store's file does
    /// not describe it.
    /// </summary>
    public static SteamAppInfoResult ReadControllerSupport(ReadOnlySpan<byte> file, IReadOnlyCollection<uint> appIds)
    {
        ArgumentNullException.ThrowIfNull(appIds);
        if (file.Length < _headerBytes)
        {
            return SteamAppInfoResult.Failed("shorter than a header");
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(file);
        if (magic != _magic41)
        {
            return SteamAppInfoResult.Failed($"format 0x{magic:X8} is not the one this build reads (0x{_magic41:X8})");
        }

        long tableAt = BinaryPrimitives.ReadInt64LittleEndian(file[8..]);
        if (tableAt < _headerBytes || tableAt > file.Length - 4 || !TryFindKeys(file[(int)tableAt..], out int common, out int support))
        {
            return SteamAppInfoResult.Failed("the table of strings cannot be read");
        }

        var found = new Dictionary<uint, string>();
        return ReadRecords(file[..(int)tableAt], [.. appIds], common, support, found) is { } problem
            ? SteamAppInfoResult.Failed(problem)
            : new SteamAppInfoResult(found, Problem: null);
    }

    /// <summary>Walks the records, which end where the table of strings begins; returns what is wrong with them, or null.</summary>
    private static string? ReadRecords(ReadOnlySpan<byte> records, HashSet<uint> wanted, int common, int support, Dictionary<uint, string> found)
    {
        int tableAt = records.Length;
        int at = _headerBytes;
        while (true)
        {
            if (at > tableAt - 4)
            {
                return "a record runs into the table of strings";
            }

            uint appId = BinaryPrimitives.ReadUInt32LittleEndian(records[at..]);
            if (appId == 0)
            {
                return null;
            }

            if (at > tableAt - 8)
            {
                return "a record has no size";
            }

            uint size = BinaryPrimitives.ReadUInt32LittleEndian(records[(at + 4)..]);
            if (size < _recordStateBytes || size > tableAt - at - 8)
            {
                return "a record claims more than the file holds";
            }

            if (wanted.Contains(appId))
            {
                ReadOnlySpan<byte> tree = records.Slice(at + 8 + _recordStateBytes, (int)size - _recordStateBytes);
                if (!TryReadSupport(tree, common, support, out string? value))
                {
                    return "a record's tree cannot be read";
                }

                found[appId] = string.Equals(value, Full, StringComparison.OrdinalIgnoreCase) ? Full
                    : string.Equals(value, Partial, StringComparison.OrdinalIgnoreCase) ? Partial
                    : None;
            }

            at += 8 + (int)size;
        }
    }

    /// <summary>The numbers of the two keys in the table: <c>u32 count</c>, then that many strings, each ended by a zero.</summary>
    private static bool TryFindKeys(ReadOnlySpan<byte> table, out int common, out int support)
    {
        common = -1;
        support = -1;
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(table);
        ReadOnlySpan<byte> rest = table[4..];
        for (int i = 0; i < count; i++)
        {
            int end = rest.IndexOf((byte)0);
            if (end < 0)
            {
                return false;
            }

            ReadOnlySpan<byte> name = rest[..end];
            if (name.SequenceEqual("common"u8))
            {
                common = i;
            }
            else if (name.SequenceEqual("controller_support"u8))
            {
                support = i;
            }

            rest = rest[(end + 1)..];
        }

        // A table without one of the two is a file in which no record can say anything of controllers: still a file read.
        return true;
    }

    /// <summary>The value of <c>&lt;root&gt;/common/controller_support</c>; false when the tree is not well formed.</summary>
    private static bool TryReadSupport(ReadOnlySpan<byte> tree, int common, int support, out string? value)
    {
        value = null;
        int at = 0;
        return TryWalk(tree, ref at, depth: 0, inCommon: false, common, support, ref value);
    }

    private static bool TryWalk(ReadOnlySpan<byte> tree, ref int at, int depth, bool inCommon, int common, int support, ref string? value)
    {
        if (depth > _maxDepth)
        {
            return false;
        }

        while (true)
        {
            if (at >= tree.Length)
            {
                // The root has no end mark of its own when the record's size ends it; a nested map must have one.
                return depth == 0;
            }

            byte kind = tree[at++];
            if (kind is _end or _endAlternate)
            {
                return true;
            }

            if (tree.Length - at < 4)
            {
                return false;
            }

            int key = BinaryPrimitives.ReadInt32LittleEndian(tree[at..]);
            at += 4;
            if (kind == _map)
            {
                // depth 0 is the root's own map ("appinfo"); its child "common" is at depth 1.
                if (!TryWalk(tree, ref at, depth + 1, inCommon: depth == 1 && key == common, common, support, ref value))
                {
                    return false;
                }

                continue;
            }

            int start = at;
            if (!TrySkipValue(tree, ref at, kind))
            {
                return false;
            }

            if (kind == _string && inCommon && key == support)
            {
                value = Encoding.UTF8.GetString(tree[start..(at - 1)]).Trim();
            }
        }
    }

    private static bool TrySkipValue(ReadOnlySpan<byte> tree, ref int at, byte kind)
    {
        switch (kind)
        {
            case _string:
                int end = tree[at..].IndexOf((byte)0);
                if (end < 0)
                {
                    return false;
                }

                at += end + 1;
                return true;
            case _wideString:
                while (true)
                {
                    if (tree.Length - at < 2)
                    {
                        return false;
                    }

                    bool last = tree[at] == 0 && tree[at + 1] == 0;
                    at += 2;
                    if (last)
                    {
                        return true;
                    }
                }

            case 0x02 or 0x03 or 0x04 or 0x06:
                at += 4;
                return at <= tree.Length;
            case 0x07 or 0x0A:
                at += 8;
                return at <= tree.Length;
            default:
                return false;
        }
    }
}
