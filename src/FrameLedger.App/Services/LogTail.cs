using System.IO;
using System.Text;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>10_LOGGING</c> §In-app log viewer: the newest <c>ui-*.log</c> / <c>agent-*.log</c> read with shared access
/// (Serilog keeps it open), at most the last 2 MB, split into lines; the level and text filters are pure functions
/// over those lines so a test can hand in text.
/// </summary>
public sealed class LogTail
{
    public const int MaxBytes = 2 * 1024 * 1024;

    private readonly string _directory;

    public LogTail(string directory) => _directory = directory ?? throw new ArgumentNullException(nameof(directory));

    /// <summary>The newest file of the source, or null when none exists.</summary>
    public string? NewestFile(LogSource source)
    {
        if (!Directory.Exists(_directory))
        {
            return null;
        }

        string pattern = source == LogSource.Agent ? "agent-*.log" : "ui-*.log";
        return Directory.EnumerateFiles(_directory, pattern).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    /// <summary>The last ≤ 2 MB of the file as lines (a partial first line is dropped); empty when the file is missing.</summary>
    public static IReadOnlyList<string> ReadLines(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return [];
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long start = Math.Max(0, stream.Length - MaxBytes);
        stream.Position = start;
        var buffer = new byte[stream.Length - start];
        stream.ReadExactly(buffer);
        string text = Encoding.UTF8.GetString(buffer);
        string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<string> kept = start > 0 && lines.Length > 0 ? lines.Skip(1) : lines;
        return [.. kept.Select(static l => l.TrimEnd('\r'))];
    }

    /// <summary>The level and text filters, in that order.</summary>
    public static IReadOnlyList<string> Filter(IReadOnlyList<string> lines, LogLevelFilter level, string? search)
    {
        ArgumentNullException.ThrowIfNull(lines);
        IEnumerable<string> result = lines;
        if (level != LogLevelFilter.All)
        {
            result = result.Where(l => LevelOf(l) is string lvl && (level == LogLevelFilter.WarningAndAbove ? lvl is "WRN" or "ERR" or "FTL" : lvl is "ERR" or "FTL"));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            result = result.Where(l => l.Contains(search, StringComparison.CurrentCultureIgnoreCase));
        }

        return [.. result];
    }

    /// <summary>The three-letter level of a line under the template <c>[HH:mm:ss.fff LVL]</c>, or null for a continuation line.</summary>
    public static string? LevelOf(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Length < 18 || line[0] != '[')
        {
            return null;
        }

        int close = line.IndexOf(']', StringComparison.Ordinal);
        if (close < 5)
        {
            return null;
        }

        string level = line[(close - 3)..close];
        return level is "VRB" or "DBG" or "INF" or "WRN" or "ERR" or "FTL" ? level : null;
    }
}
