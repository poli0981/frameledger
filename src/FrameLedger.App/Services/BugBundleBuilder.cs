using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FrameLedger.Application.Settings;
using FrameLedger.Infrastructure.Diagnostics;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>10_LOGGING</c> §Bug report flow, step 2 — the bundle: <c>logs/</c> (this app's and the Agent's files of the
/// last seven days) and the last <c>overlay-*.log</c> files, both as copies with every user name in a path replaced
/// (<see cref="LogRedactor"/>), <c>sysinfo.json</c> (app, agent, overlay build id, OS,
/// locale, telemetry source, Vulkan layer state, elevation), <c>settings.json</c> (the registry's keys and values —
/// no paths), and <c>crashdumps/</c> with one minidump only when the user ticked it (P4 PR-9). We ship our files only,
/// never a game's; nothing is sent anywhere (<see cref="BugReportFlow"/> is steps 3 and 4).
/// </summary>
public sealed class BugBundleBuilder
{
    public const int OverlayLogsKept = 5;

    private readonly string _logs;
    private readonly RegisteredSettings _settings;
    private readonly TimeProvider _clock;
    private readonly string? _crashDumps;
    private readonly LogRedactor _redactor;

    /// <summary>A builder over the logs directory, the settings registry and, when given, the crash dump directory.</summary>
    /// <param name="logsDirectory">The logs directory: ours and the Overlay's.</param>
    /// <param name="settings">The registry the bundle's <c>settings.json</c> reads.</param>
    /// <param name="clock">The seven-day cut's clock.</param>
    /// <param name="crashDumpDirectory">The minidumps' directory (P4 PR-9); null offers none.</param>
    /// <param name="redactor">What the log copies pass through; the current user's by default.</param>
    public BugBundleBuilder(string logsDirectory, RegisteredSettings settings, TimeProvider? clock = null, string? crashDumpDirectory = null, LogRedactor? redactor = null)
    {
        _logs = logsDirectory ?? throw new ArgumentNullException(nameof(logsDirectory));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clock = clock ?? TimeProvider.System;
        _crashDumps = crashDumpDirectory;
        _redactor = redactor ?? LogRedactor.ForCurrentUser();
    }

    /// <summary>
    /// The newest minidump written in the last seven days, or null: what the bug report offers to include (P4 PR-9). Either
    /// process's dump qualifies; both write to the same directory.
    /// </summary>
    public CrashDumpInfo? LatestCrashDump()
    {
        if (_crashDumps is null || !Directory.Exists(_crashDumps))
        {
            return null;
        }

        DateTime cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-7);
        FileInfo? newest = new DirectoryInfo(_crashDumps).EnumerateFiles(CrashDumpWriter.Pattern)
            .Where(f => f.LastWriteTimeUtc >= cutoff)
            .OrderByDescending(static f => f.LastWriteTimeUtc)
            .FirstOrDefault();
        return newest is null ? null : new CrashDumpInfo(newest.FullName, new DateTimeOffset(newest.LastWriteTimeUtc, TimeSpan.Zero), newest.Length);
    }

    /// <summary>
    /// Writes the zip at <paramref name="path"/>; returns the entry names written. <paramref name="crashDump"/> goes in only
    /// when the caller passes one, which is the user's tick (P4 PR-9), and only from the crash dump directory;
    /// <paramref name="lastSessionJson"/> likewise, as <c>session.json</c> with the user names in its paths replaced.
    /// </summary>
    public async Task<IReadOnlyList<string>> WriteAsync(string path, HelloAck? agent, CrashDumpInfo? crashDump = null, byte[]? lastSessionJson = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (crashDump is not null && !IsOurDump(crashDump.Path))
        {
            throw new ArgumentException("a crash dump is taken from the crash dump directory only", nameof(crashDump));
        }

        var written = new List<string>();
        DateTime cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-7);
        using FileStream file = File.Create(path);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false);
        if (Directory.Exists(_logs))
        {
            foreach (string log in Directory.EnumerateFiles(_logs, "*.log").Where(f => IsOurs(f) && File.GetLastWriteTimeUtc(f) >= cutoff))
            {
                AddRedactedFile(zip, log, "logs/" + Path.GetFileName(log), written);
            }

            foreach (string overlay in Directory.EnumerateFiles(_logs, "overlay-*.log").OrderByDescending(File.GetLastWriteTimeUtc).Take(OverlayLogsKept))
            {
                AddRedactedFile(zip, overlay, "overlay/" + Path.GetFileName(overlay), written);
            }
        }

        // Step 2's optional item: the one dump the user ticked, nothing else from that directory.
        if (crashDump is not null)
        {
            AddFile(zip, crashDump.Path, "crashdumps/" + Path.GetFileName(crashDump.Path), written);
        }

        // The other optional item: the last session's summary the user ticked, redacted like a log (it names the executable).
        if (lastSessionJson is not null)
        {
            AddBytes(zip, "session.json", _redactor.Redact(lastSessionJson), written);
        }

        AddText(zip, "sysinfo.json", SysInfo(agent), written);
        AddText(zip, "settings.json", await SettingsJsonAsync(ct).ConfigureAwait(false), written);
        return written;
    }

    public static string SuggestedName(DateTimeOffset now) => "FrameLedger-bugreport-" + now.ToLocalTime().ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".zip";

    /// <summary>A <c>.dmp</c> directly under the crash dump directory: no other file reaches the zip under that entry.</summary>
    private bool IsOurDump(string dumpPath) =>
        _crashDumps is not null
        && string.Equals(Path.GetExtension(dumpPath), ".dmp", StringComparison.OrdinalIgnoreCase)
        && string.Equals(Path.GetDirectoryName(Path.GetFullPath(dumpPath)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(_crashDumps)), StringComparison.OrdinalIgnoreCase);

    private static bool IsOurs(string path)
    {
        string name = Path.GetFileName(path);
        return name.StartsWith("ui-", StringComparison.OrdinalIgnoreCase) || name.StartsWith("agent-", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddFile(ZipArchive zip, string source, string entryName, List<string> written)
    {
        ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using Stream target = entry.Open();
        // Shared read: Serilog and the Overlay keep their files open.
        using var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        stream.CopyTo(target);
        written.Add(entryName);
    }

    // legal/PRIVACY_POLICY.md §3: a log leaves this machine only as a copy with the user names in its paths replaced. A log is
    // at most 10 MB (Serilog's roll size), so it is read whole; a crash dump is memory and goes in as it is, behind its box.
    private void AddRedactedFile(ZipArchive zip, string source, string entryName, List<string> written)
    {
        byte[] bytes;
        using (var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }

        AddBytes(zip, entryName, _redactor.Redact(bytes), written);
    }

    private static void AddBytes(ZipArchive zip, string entryName, byte[] bytes, List<string> written)
    {
        ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using Stream target = entry.Open();
        target.Write(bytes, 0, bytes.Length);
        written.Add(entryName);
    }

    private static void AddText(ZipArchive zip, string entryName, string text, List<string> written)
    {
        ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using Stream target = entry.Open();
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        target.Write(bytes, 0, bytes.Length);
        written.Add(entryName);
    }

    private string SysInfo(HelloAck? agent) => JsonSerializer.Serialize(new Dictionary<string, string>(SysInfoOf(agent, _clock.GetUtcNow()), StringComparer.Ordinal), AppJsonContext.Default.DictionaryStringString);

    /// <summary>The twelve keys of <c>sysinfo.json</c>, also the issue link's prefill and the clipboard Markdown (P4 PR-3).</summary>
    public static IReadOnlyDictionary<string, string> SysInfoOf(HelloAck? agent, DateTimeOffset now)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["app_version"] = UiIdentity.Version,
            ["agent_version"] = agent?.AgentVersion ?? "not connected",
            ["overlay_build_id"] = agent?.OverlayBuildId ?? "n/a",
            ["telemetry_source"] = agent?.TelemetrySource ?? "n/a",
            ["vulkan_layer_registered"] = agent?.VulkanLayerRegistered.ToString() ?? "n/a",
            ["agent_elevated"] = agent?.Elevated.ToString() ?? "n/a",
            ["cpu_temp_available"] = agent?.CpuTempAvailable.ToString() ?? "n/a",
            ["disclosure_version"] = agent?.DisclosureVersion ?? "n/a",
            ["os"] = Environment.OSVersion.VersionString,
            ["os_64bit"] = Environment.Is64BitOperatingSystem.ToString(),
            ["locale"] = CultureInfo.CurrentUICulture.Name,
            ["written_at"] = now.ToString("O", CultureInfo.InvariantCulture),
        };
    }

    private async Task<string> SettingsJsonAsync(CancellationToken ct)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (SettingDefinition definition in SettingsRegistry.All)
        {
            values[definition.Key] = await _settings.GetAsync(definition, ct).ConfigureAwait(false);
        }

        return JsonSerializer.Serialize(values, AppJsonContext.Default.DictionaryStringString);
    }
}
