using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FrameLedger.Application.Settings;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>10_LOGGING</c> §Bug report flow, step 2 — the bundle: <c>logs/</c> (this app's and the Agent's files of the
/// last seven days), the last <c>overlay-*.log</c> files, <c>sysinfo.json</c> (app, agent, overlay build id, OS,
/// locale, telemetry source, Vulkan layer state, elevation), and <c>settings.json</c> (the registry's keys and values —
/// no paths). We ship our logs only, never a game's; nothing is sent anywhere (step 3's preview dialog and step
/// 4's GitHub link arrive with P4's bug-report flow).
/// </summary>
public sealed class BugBundleBuilder
{
    public const int OverlayLogsKept = 5;

    private readonly string _logs;
    private readonly RegisteredSettings _settings;
    private readonly TimeProvider _clock;

    public BugBundleBuilder(string logsDirectory, RegisteredSettings settings, TimeProvider? clock = null)
    {
        _logs = logsDirectory ?? throw new ArgumentNullException(nameof(logsDirectory));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Writes the zip at <paramref name="path"/>; returns the entry names written.</summary>
    public async Task<IReadOnlyList<string>> WriteAsync(string path, HelloAck? agent, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var written = new List<string>();
        DateTime cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-7);
        using FileStream file = File.Create(path);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false);
        if (Directory.Exists(_logs))
        {
            foreach (string log in Directory.EnumerateFiles(_logs, "*.log").Where(f => IsOurs(f) && File.GetLastWriteTimeUtc(f) >= cutoff))
            {
                AddFile(zip, log, "logs/" + Path.GetFileName(log), written);
            }

            foreach (string overlay in Directory.EnumerateFiles(_logs, "overlay-*.log").OrderByDescending(File.GetLastWriteTimeUtc).Take(OverlayLogsKept))
            {
                AddFile(zip, overlay, "overlay/" + Path.GetFileName(overlay), written);
            }
        }

        AddText(zip, "sysinfo.json", SysInfo(agent), written);
        AddText(zip, "settings.json", await SettingsJsonAsync(ct).ConfigureAwait(false), written);
        return written;
    }

    public static string SuggestedName(DateTimeOffset now) => "FrameLedger-bugreport-" + now.ToLocalTime().ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".zip";

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

    private static void AddText(ZipArchive zip, string entryName, string text, List<string> written)
    {
        ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using Stream target = entry.Open();
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        target.Write(bytes, 0, bytes.Length);
        written.Add(entryName);
    }

    private string SysInfo(HelloAck? agent)
    {
        var info = new Dictionary<string, string>(StringComparer.Ordinal)
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
            ["written_at"] = _clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
        };
        return JsonSerializer.Serialize(info, AppJsonContext.Default.DictionaryStringString);
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
