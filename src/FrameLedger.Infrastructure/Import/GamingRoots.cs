// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Buffers.Binary;
using System.Text;

namespace FrameLedger.Infrastructure.Import;

/// <summary>
/// The folders the Xbox app installs games into, as each drive's own <c>.GamingRoot</c> file names them (beta.33,
/// <c>spike-notes</c> §25): the bytes <c>RGBX</c>, a little-endian 32-bit count, then that many NUL-terminated UTF-16
/// folder names relative to the drive — measured <c>XboxGames</c> on C: (28 bytes) and <c>xbox</c> on D: (18 bytes).
/// </summary>
/// <remarks>
/// The file is the Xbox app's, undocumented in the way Steam's <c>libraryfolders.vdf</c> is; it is read defensively: a
/// file that does not start with the magic, claims more names than <see cref="MaxCount"/>, runs past its end, or names
/// anything but a plain relative folder costs that drive (or that name), never the import.
/// </remarks>
public static class GamingRoots
{
    /// <summary>The file at a drive's root.</summary>
    public const string FileName = ".GamingRoot";

    /// <summary>More names than any install would have; a count above it is a file that is not one.</summary>
    public const int MaxCount = 64;

    /// <summary>The largest file read.</summary>
    public const int MaxBytes = 64 * 1024;

    private const int _headerBytes = 8;

    private static ReadOnlySpan<byte> Magic => "RGBX"u8;

    /// <summary>The folders <paramref name="bytes"/> names, each joined to <paramref name="driveRoot"/>; empty for a file that is not a <c>.GamingRoot</c>.</summary>
    public static IReadOnlyList<string> Parse(ReadOnlySpan<byte> bytes, string driveRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driveRoot);
        if (bytes.Length < _headerBytes || !bytes[..4].SequenceEqual(Magic))
        {
            return [];
        }

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..8]);
        if (count == 0 || count > MaxCount)
        {
            return [];
        }

        List<string> roots = [];
        int at = _headerBytes;
        for (uint i = 0; i < count; i++)
        {
            int end = at;
            while (end + 1 < bytes.Length && (bytes[end] != 0 || bytes[end + 1] != 0))
            {
                end += 2;
            }

            if (end + 1 >= bytes.Length)
            {
                break;    // ran past the end: the names read so far stand, the rest is not there
            }

            string name = Encoding.Unicode.GetString(bytes[at..end]);
            if (Resolve(driveRoot, name) is { } root)
            {
                roots.Add(root);
            }

            at = end + 2;
        }

        return roots;
    }

    /// <summary>Every games folder the ready fixed and removable drives' <c>.GamingRoot</c> files name.</summary>
    public static IReadOnlyList<string> OnThisPc()
    {
        List<string> roots = [];
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (IOException)
        {
            return roots;
        }

        foreach (DriveInfo drive in drives)
        {
            try
            {
                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable) || !drive.IsReady)
                {
                    continue;
                }

                string file = Path.Combine(drive.RootDirectory.FullName, FileName);
                var info = new FileInfo(file);
                if (!info.Exists || info.Length > MaxBytes)
                {
                    continue;
                }

                roots.AddRange(Parse(File.ReadAllBytes(file), drive.RootDirectory.FullName));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A drive that will not answer is a drive with no games folder this time.
            }
        }

        return roots;
    }

    /// <summary><paramref name="name"/> joined to the drive, or null for a name that is not a plain relative folder on it.</summary>
    private static string? Resolve(string driveRoot, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name) || name.Contains(':', StringComparison.Ordinal)
            || name.IndexOfAny(Path.GetInvalidPathChars()) >= 0
            || name.Split('\\', '/').Any(static segment => segment is "." or ".."))
        {
            return null;
        }

        string drive = Path.GetFullPath(driveRoot);
        string full = Path.GetFullPath(Path.Combine(drive, name));
        return full.StartsWith(drive, StringComparison.OrdinalIgnoreCase) && full.Length > drive.Length ? full : null;
    }
}
