using System.Buffers;
using System.Diagnostics;
using System.Text;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Infrastructure.Detection;

/// <summary>
/// Unreal Engine's build facts from a shipping executable on disk (beta.10, <c>05_DETECTION</c> §Engine version): its
/// version resource and the engine's branch name compiled into it (<c>++UE5+Release-5.1</c>, a UTF-16 string). The file is
/// read; the game's process is never opened (CLAUDE.md rule 4 is about game memory, and this is the file the user added).
/// </summary>
/// <remarks>
/// <para>
/// <b>Where the branch name is looked for, measured 2026-09-27 on ten titles.</b> In the <c>.rdata</c> section — 15 to 39 MB
/// of executables of 88 to 192 MB — in every title that carries one; Black Myth: Wukong's protected executable has an 8 KB
/// <c>.rdata</c> and the name 290 MB into another section, so a PE whose <c>.rdata</c> is under <see cref="MinRdataBytes"/>
/// on disk (or absent) is scanned whole, up to <see cref="MaxScanBytes"/>. A title whose version strings already name the
/// branch (UMIGARI: <c>++UE5+Release-5.5-CL-40574608</c>) is not scanned at all.
/// </para>
/// <para>
/// <b>Bounded and interruptible.</b> The scan reads <see cref="ChunkBytes"/> at a time with an overlap, stops at the first
/// chunk that names a release line, and observes cancellation between chunks — the Agent's host allows 15 s to stop and the
/// updater 10 s, and a gigabyte on a USB drive takes longer than either. A file that cannot be opened is no facts, never a
/// guess, and nothing here throws for a malformed file.
/// </para>
/// </remarks>
public static class UnrealBuildReader
{
    /// <summary>The most a scan reads: one gigabyte, beyond every shipping executable measured.</summary>
    public const long MaxScanBytes = 1L << 30;

    /// <summary>One read's size.</summary>
    public const int ChunkBytes = 4 * 1024 * 1024;

    /// <summary>
    /// An <c>.rdata</c> smaller than this does not hold an engine's constants — a protector merged them elsewhere — and the
    /// file is scanned whole instead. Measured 2026-09-27: 15 to 39 MB in nine shipping executables, 8 KB in Black Myth:
    /// Wukong's protected one, whose branch name sits 290 MB into its <c>.code</c> section.
    /// </summary>
    public const long MinRdataBytes = 1024 * 1024;

    /// <summary>The branch names the engine compiles in, as the UTF-16 bytes the executable holds them in.</summary>
    private static readonly byte[][] _needles = [Encoding.Unicode.GetBytes("++UE4+Release-"), Encoding.Unicode.GetBytes("++UE5+Release-")];

    /// <summary>What follows a needle and is parsed: up to six characters (<c>99.999</c>), two bytes each.</summary>
    private const int _tailBytes = 12;

    private static readonly int _overlap = _needles.Max(static n => n.Length) + _tailBytes;

    /// <summary>The facts in <paramref name="exePath"/>, or null when the file could not be opened at all.</summary>
    /// <param name="exePath">The shipping executable.</param>
    /// <param name="relativePath">The same file under the install root, for the log; not used to decide anything.</param>
    /// <param name="ct">Observed between chunks.</param>
    public static UnrealBuildFacts? Read(string exePath, string? relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        FileVersionInfo info;
        try
        {
            info = FileVersionInfo.GetVersionInfo(exePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }

        IReadOnlyList<int>? fixedVersion = info.FileMajorPart == 0 && info.FileMinorPart == 0 && info.FileBuildPart == 0 && info.FilePrivatePart == 0
            ? null
            : [info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart];
        var facts = new UnrealBuildFacts
        {
            ExecutableRelativePath = relativePath,
            FixedFileVersion = fixedVersion,
            FileVersionText = Blank(info.FileVersion),
            ProductVersionText = Blank(info.ProductVersion),
            CompanyName = Blank(info.CompanyName),
        };

        // The version strings already carry the branch name: the same witness, and no reason to read the file for it.
        if (NamesABranch(facts.FileVersionText) || NamesABranch(facts.ProductVersionText))
        {
            return facts;
        }

        try
        {
            using var file = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 1, FileOptions.SequentialScan);
            return facts with { BranchVersions = ScanBranches(file, ct) };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return facts;    // the version resource was read; the bytes were not — the ladder decides with what there is
        }
    }

    /// <summary>
    /// The release lines (<c>5.1</c>) the stream names after <c>++UE4+Release-</c> / <c>++UE5+Release-</c>: in its
    /// <c>.rdata</c> section when the PE has one on disk, else in the whole stream up to <see cref="MaxScanBytes"/>. Public
    /// because the tests hand in synthetic executables.
    /// </summary>
    public static IReadOnlyList<string> ScanBranches(Stream file, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!file.CanRead || !file.CanSeek)
        {
            return [];
        }

        (long start, long length) = PeImports.SectionRange(file, ".rdata") is { Length: >= MinRdataBytes } rdata ? rdata : (0, file.Length);
        long end = start + Math.Min(length, MaxScanBytes);
        var found = new SortedSet<string>(StringComparer.Ordinal);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkBytes + _overlap);
        try
        {
            long position = start;
            int carried = 0;
            while (position < end && found.Count == 0)
            {
                ct.ThrowIfCancellationRequested();
                int want = (int)Math.Min(ChunkBytes, end - position);
                file.Position = position;
                int read = file.ReadAtLeast(buffer.AsSpan(carried, want), want, throwOnEndOfStream: false);
                if (read == 0)
                {
                    break;
                }

                position += read;
                int filled = carried + read;
                Collect(buffer.AsSpan(0, filled), atEnd: position >= end, found);

                // The last bytes go to the front, so a name cut by the chunk's edge is whole in the next window.
                carried = Math.Min(_overlap, filled);
                buffer.AsSpan(filled - carried, carried).CopyTo(buffer);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return [.. found];
    }

    /// <summary>Every whole name in <paramref name="window"/>; a name whose digits run past it is left for the next window unless this is the last.</summary>
    private static void Collect(ReadOnlySpan<byte> window, bool atEnd, SortedSet<string> found)
    {
        foreach (byte[] needle in _needles)
        {
            int from = 0;
            while (from < window.Length)
            {
                int hit = window[from..].IndexOf(needle);
                if (hit < 0)
                {
                    break;
                }

                int tail = from + hit + needle.Length;
                if (tail + _tailBytes <= window.Length || atEnd)
                {
                    if (UnrealVersion.ReleaseLine(Digits(window[tail..Math.Min(window.Length, tail + _tailBytes)])) is { } line)
                    {
                        found.Add(line);
                    }
                }

                from += hit + 2;
            }
        }
    }

    /// <summary>The digits and dots a UTF-16LE tail starts with (<c>"5.1"</c> of <c>"5.1-CL-0"</c>).</summary>
    private static string Digits(ReadOnlySpan<byte> tail)
    {
        var text = new StringBuilder(_tailBytes / 2);
        for (int i = 0; i + 1 < tail.Length; i += 2)
        {
            char c = (char)(tail[i] | (tail[i + 1] << 8));
            if (c is not ('.' or (>= '0' and <= '9')))
            {
                break;
            }

            text.Append(c);
        }

        return text.ToString();
    }

    private static bool NamesABranch(string? text) =>
        text is not null && (text.Contains("++UE4+Release-", StringComparison.Ordinal) || text.Contains("++UE5+Release-", StringComparison.Ordinal));

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
