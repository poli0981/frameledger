using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Infrastructure.Detection;

/// <summary>
/// The module names an executable imports — its import directory and its delay-load directory — read from the
/// file with bounded, read-only I/O (P4 PR-2). Null when the file is not a PE this reader understands or could not
/// be read; an empty set when it is a PE that imports nothing. Names are returned as the file spells them, in a
/// case-insensitive set. Since beta.8 (2026-09-25) the same headers also say what the file runs as
/// (<see cref="ReadArchitecture(string)"/>).
/// </summary>
/// <remarks>
/// <para>
/// This is the static half of the "is Vulkan" fact (<c>17_HOOK_ENGINE</c> §Vulkan): a title that links
/// <c>vulkan-1.dll</c> at build time names it here. A title that reaches the loader through <c>LoadLibrary</c> does
/// not, which is why <c>GameFileProbe</c> also looks for the name in the bounded strings scan.
/// </para>
/// <para>
/// Every offset is checked against the file length and every walk is capped, because a game's executable is
/// untrusted input to this process (CLAUDE.md rule 4 is about game <em>memory</em>; a malformed file must
/// likewise cost an answer, never the Agent). No exception leaves this class for a malformed file.
/// </para>
/// </remarks>
public static class PeImports
{
    /// <summary>Descriptors walked per directory before the reader gives up: no real executable has this many.</summary>
    public const int MaxDescriptors = 4096;

    private const int _maxNameBytes = 260;

    public static IReadOnlySet<string>? Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using FileStream fs = File.OpenRead(path);
            return Read(fs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>The same over an open stream (tests hand in a synthetic PE).</summary>
    public static IReadOnlySet<string>? Read(Stream file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!file.CanSeek || !file.CanRead)
        {
            return null;
        }

        byte[] directories = new byte[16 * 8];
        List<Section>? sections = ReadHeaders(file, directories, out _);
        if (sections is null)
        {
            return null;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Directory 1: imports (IMAGE_IMPORT_DESCRIPTOR, 20 bytes, name at +12); directory 13: delay-load
        // (IMAGE_DELAYLOAD_DESCRIPTOR, 32 bytes, name at +4). Both end at an all-zero descriptor.
        if (!WalkNames(file, sections, directories, 1, descriptorSize: 20, nameAt: 12, names)
            || !WalkNames(file, sections, directories, 13, descriptorSize: 32, nameAt: 4, names))
        {
            return null;
        }

        return names;
    }

    /// <summary>
    /// What the executable runs as (beta.8): the COFF machine and, for a .NET executable, its CLR header's flags, as an
    /// <see cref="ExecutableArchitecture"/> id — <see cref="ExecutableArchitecture.Unknown"/> when the file is not a PE
    /// this reader understands or could not be read. Never throws for a malformed or missing file.
    /// </summary>
    public static string ReadArchitecture(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using FileStream fs = File.OpenRead(path);
            return ReadArchitecture(fs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return ExecutableArchitecture.Unknown;
        }
    }

    /// <summary>The same over an open stream (tests hand in a synthetic PE).</summary>
    public static string ReadArchitecture(Stream file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!file.CanSeek || !file.CanRead)
        {
            return ExecutableArchitecture.Unknown;
        }

        byte[] directories = new byte[16 * 8];
        if (ReadHeaders(file, directories, out ushort machine) is not { } sections)
        {
            return ExecutableArchitecture.Unknown;
        }

        // Directory 14: the CLR runtime header (IMAGE_COR20_HEADER, Flags at +16). Present = a .NET executable, whose
        // flags decide between AnyCPU and x86 on an I386 machine; one we cannot read is unknown, never "native".
        uint rva = BinaryPrimitives.ReadUInt32LittleEndian(directories.AsSpan(14 * 8));
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(directories.AsSpan((14 * 8) + 4));
        uint? clrFlags = null;
        if (rva != 0 && size != 0)
        {
            Span<byte> cor = stackalloc byte[20];
            long at = ToOffset(sections, rva);
            if (size < cor.Length || at < 0 || !ReadAt(file, at, cor))
            {
                return ExecutableArchitecture.Unknown;
            }

            clrFlags = BinaryPrimitives.ReadUInt32LittleEndian(cor[16..]);
        }

        return ExecutableArchitecture.Of(machine, clrFlags);
    }

    /// <summary>DOS header → PE signature → optional header (PE32 or PE32+) → the 16 data directories and the section table. Null when any step does not read as a PE.</summary>
    private static List<Section>? ReadHeaders(Stream file, Span<byte> directories, out ushort machine)
    {
        machine = 0;
        Span<byte> dos = stackalloc byte[64];
        if (!ReadAt(file, 0, dos) || dos[0] != (byte)'M' || dos[1] != (byte)'Z')
        {
            return null;
        }

        long peOffset = BinaryPrimitives.ReadUInt32LittleEndian(dos[60..]);
        Span<byte> sig = stackalloc byte[4 + 20];    // "PE\0\0" + IMAGE_FILE_HEADER
        if (!ReadAt(file, peOffset, sig) || sig[0] != (byte)'P' || sig[1] != (byte)'E' || sig[2] != 0 || sig[3] != 0)
        {
            return null;
        }

        machine = BinaryPrimitives.ReadUInt16LittleEndian(sig[4..]);
        int sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(sig[6..]);
        int optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(sig[20..]);
        long optionalOffset = peOffset + 24;
        Span<byte> optional = stackalloc byte[2];
        if (optionalSize < 2 || !ReadAt(file, optionalOffset, optional))
        {
            return null;
        }

        int directoriesAt = BinaryPrimitives.ReadUInt16LittleEndian(optional) switch
        {
            0x10B => 96,     // PE32
            0x20B => 112,    // PE32+
            _ => -1,
        };
        if (directoriesAt < 0 || optionalSize < directoriesAt + directories.Length || !ReadAt(file, optionalOffset + directoriesAt, directories))
        {
            return null;
        }

        return ReadSections(file, optionalOffset + optionalSize, sectionCount);
    }

    private static bool WalkNames(Stream file, List<Section> sections, ReadOnlySpan<byte> directories, int index, int descriptorSize, int nameAt, HashSet<string> names)
    {
        uint rva = BinaryPrimitives.ReadUInt32LittleEndian(directories[(index * 8)..]);
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(directories[(index * 8 + 4)..]);
        if (rva == 0 || size == 0)
        {
            return true;    // no such directory: a PE that imports nothing that way
        }

        Span<byte> descriptor = stackalloc byte[32];
        Span<byte> nameBuffer = stackalloc byte[_maxNameBytes];
        for (int i = 0; i < MaxDescriptors; i++)
        {
            long at = ToOffset(sections, rva + (uint)(i * descriptorSize));
            if (at < 0 || !ReadAt(file, at, descriptor[..descriptorSize]))
            {
                return false;
            }

            if (IsZero(descriptor[..descriptorSize]))
            {
                return true;
            }

            uint nameRva = BinaryPrimitives.ReadUInt32LittleEndian(descriptor[nameAt..]);
            long nameOffset = ToOffset(sections, nameRva);
            if (nameOffset < 0)
            {
                return false;
            }

            int n = ReadUpTo(file, nameOffset, nameBuffer);
            int end = nameBuffer[..n].IndexOf((byte)0);
            if (end <= 0)
            {
                return false;
            }

            names.Add(Encoding.ASCII.GetString(nameBuffer[..end]));
        }

        return false;    // more descriptors than any real binary carries: malformed
    }

    private static List<Section>? ReadSections(Stream file, long tableOffset, int count)
    {
        if (count is <= 0 or > 96)
        {
            return null;
        }

        var sections = new List<Section>(count);
        Span<byte> header = stackalloc byte[40];
        for (int i = 0; i < count; i++)
        {
            if (!ReadAt(file, tableOffset + i * 40L, header))
            {
                return null;
            }

            sections.Add(new Section(
                VirtualAddress: BinaryPrimitives.ReadUInt32LittleEndian(header[12..]),
                VirtualSize: BinaryPrimitives.ReadUInt32LittleEndian(header[8..]),
                RawPointer: BinaryPrimitives.ReadUInt32LittleEndian(header[20..]),
                RawSize: BinaryPrimitives.ReadUInt32LittleEndian(header[16..])));
        }

        return sections;
    }

    /// <summary>RVA → file offset through the section that contains it; -1 when none does.</summary>
    private static long ToOffset(List<Section> sections, uint rva)
    {
        foreach (Section s in sections)
        {
            uint span = Math.Max(s.VirtualSize, s.RawSize);
            if (rva >= s.VirtualAddress && rva < s.VirtualAddress + span)
            {
                return s.RawPointer + (rva - s.VirtualAddress);
            }
        }

        return -1;
    }

    private static bool ReadAt(Stream file, long offset, Span<byte> into)
    {
        if (offset < 0 || offset + into.Length > file.Length)
        {
            return false;
        }

        file.Position = offset;
        return file.ReadAtLeast(into, into.Length, throwOnEndOfStream: false) == into.Length;
    }

    private static int ReadUpTo(Stream file, long offset, Span<byte> into)
    {
        if (offset < 0 || offset >= file.Length)
        {
            return 0;
        }

        file.Position = offset;
        int take = (int)Math.Min(into.Length, file.Length - offset);
        return file.ReadAtLeast(into[..take], take, throwOnEndOfStream: false);
    }

    private static bool IsZero(ReadOnlySpan<byte> bytes)
    {
        foreach (byte b in bytes)
        {
            if (b != 0)
            {
                return false;
            }
        }

        return true;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Section(uint VirtualAddress, uint VirtualSize, uint RawPointer, uint RawSize);
}
