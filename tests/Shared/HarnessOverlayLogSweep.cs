using System.Diagnostics.CodeAnalysis;
using System.Text;

[assembly: AssemblyFixture(typeof(FrameLedger.Testing.HarnessOverlayLogSweep))]

namespace FrameLedger.Testing;

/// <summary>
/// The hook-harness's overlay logs, removed when this test assembly finishes (<c>17_HOOK_ENGINE</c> §Native logging).
/// </summary>
/// <remarks>
/// The Overlay writes <c>%LOCALAPPDATA%\FrameLedger\logs\overlay-&lt;pid&gt;-&lt;stamp&gt;.log</c>: the real data folder,
/// resolved by the shell and deliberately not by the environment (§S21), and the injection tests use the shipped Overlay,
/// so no test can point it elsewhere. Measured 2026-09-15, that folder held 2506 overlay logs, 2492 of them hook-harness's.
/// <c>FrameLedger.DrainFixtures.targets</c> compiles this file into every test project that stages hook-harness, and at the
/// end of the assembly it removes exactly what the assembly caused: an overlay log created after the assembly started
/// whose first line names a hook-harness image this assembly owns — the copy beside its binary, or a copy under a
/// directory a test registered with <see cref="Own"/>. Test assemblies run in parallel with their own copies, so one
/// never removes a log another is about to read. A game's log names the game; a log from an earlier run predates the
/// start and stays.
/// </remarks>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "xUnit creates the assembly fixture by reflection")]
internal sealed class HarnessOverlayLogSweep : IDisposable
{
    private static readonly Lock _gate = new();
    private static readonly List<string> _owned = [];
    private readonly DateTime _startedUtc = DateTime.UtcNow;

    /// <summary>The Overlay's log directory, resolved the way the Overlay resolves it (the shell's Local AppData).</summary>
    public static string RealLogsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FrameLedger", "logs");

    /// <summary>Registers a directory a test copies hook-harness into, so the copy's logs are this assembly's too.</summary>
    public static void Own(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        lock (_gate)
        {
            _owned.Add(Path.GetFullPath(directory));
        }
    }

    public void Dispose()
    {
        string[] owned;
        lock (_gate)
        {
            owned = [AppContext.BaseDirectory, .. _owned];
        }

        _ = Sweep(RealLogsDirectory, _startedUtc, image => IsHarnessUnder(image, owned));
    }

    /// <summary>
    /// Removes each <c>overlay-*.log</c> directly under <paramref name="logsDirectory"/> created at or after
    /// <paramref name="sinceUtc"/> whose header image <paramref name="isOurs"/> accepts. Everything else stays.
    /// </summary>
    /// <returns>How many were removed, and how many created in the window were left (not ours, or in use).</returns>
    public static (int Removed, int Kept) Sweep(string logsDirectory, DateTime sinceUtc, Func<string, bool> isOurs)
    {
        ArgumentNullException.ThrowIfNull(isOurs);
        if (!Directory.Exists(logsDirectory))
        {
            return (0, 0);
        }

        int removed = 0;
        int kept = 0;
        foreach (FileInfo log in new DirectoryInfo(logsDirectory).EnumerateFiles("overlay-*.log"))
        {
            if (log.CreationTimeUtc < sinceUtc)
            {
                continue;
            }

            string? image = OverlayLogImage(FirstLine(log.FullName));
            if (image is null || !isOurs(image))
            {
                kept++;
                continue;
            }

            try
            {
                log.Delete();
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                kept++;
            }
        }

        return (removed, kept);
    }

    /// <summary>The image path of an Overlay log's first line (<c># FrameLedger.Overlay build … image &lt;path&gt;</c>), or null.</summary>
    public static string? OverlayLogImage(string? firstLine)
    {
        const string prefix = "# FrameLedger.Overlay build ";
        if (firstLine is null || !firstLine.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        int at = firstLine.IndexOf(" image ", StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        string image = firstLine[(at + " image ".Length)..].TrimEnd('\r', '\n', ' ');
        return image.Length == 0 ? null : image;
    }

    /// <summary>True for a <c>hook-harness*.exe</c> directly or deeper under one of <paramref name="directories"/>.</summary>
    public static bool IsHarnessUnder(string image, IEnumerable<string> directories)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(directories);
        string name = Path.GetFileName(image);
        if (!name.StartsWith("hook-harness", StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string full = Normalised(image);
        return directories.Any(directory => full.StartsWith(Normalised(directory) + "\\", StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalised(string path) => path.Replace('/', '\\').TrimEnd('\\');

    // The header's image comes from GetModuleFileNameA (the ANSI code page); Latin-1 reads its bytes without failing.
    private static string? FirstLine(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.Latin1);
            return reader.ReadLine();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
