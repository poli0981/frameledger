using System.Globalization;
using System.IO;
using System.Text;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Settings;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>10_LOGGING</c> §Diagnostics extras: <c>FrameLedger.exe --diag</c> prints the environment and capability report —
/// what a support request needs before anything else — to stdout, and writes the same text under <c>logs/</c>
/// (a WinExe has no console of its own, so the file is what a double-click leaves behind). No host is started
/// and no Agent is asked: the report is what this machine and this install look like from the outside.
/// </summary>
public static class DiagReport
{
    public const string Flag = "--diag";

    public static bool Requested(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Contains(Flag, StringComparer.Ordinal);
    }

    /// <summary>The report text. <paramref name="settings"/> is every registry key's effective value; <paramref name="schemaVersion"/> null when the ledger could not be opened.</summary>
    public static string Build(string appVersion, string dataDirectory, string logsDirectory, long? schemaVersion, IReadOnlyDictionary<string, string> settings, bool agentBesideApp, DateTimeOffset now, HardwareSnapshot? hardware = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"FrameLedger --diag  {now.ToString("O", CultureInfo.InvariantCulture)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"app_version: {appVersion}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"os: {Environment.OSVersion.VersionString} ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")}), process {(Environment.Is64BitProcess ? "x64" : "x86")}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"runtime: {Environment.Version}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"locale: {CultureInfo.CurrentUICulture.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"elevated: {Environment.IsPrivilegedProcess}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"data_dir: {dataDirectory}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"logs_dir: {logsDirectory}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"ledger_schema: {(schemaVersion is long v ? v.ToString(CultureInfo.InvariantCulture) : "could not open")}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"agent_beside_app: {agentBesideApp}");

        // The machine (2026-09-21), as a session's hardware snapshot records it. A local file the user reads and chooses to
        // share; the bug bundle's sysinfo.json is deliberately NOT given these (its keys are the issue link's prefill).
        sb.AppendLine(CultureInfo.InvariantCulture, $"cpu: {hardware?.CpuName ?? "n/a"}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"gpu: {hardware?.GpuName ?? "n/a"}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"gpu_driver: {hardware?.GpuDriver ?? "n/a"}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"ram_gb: {(hardware?.RamGb is double gb ? gb.ToString("0.#", CultureInfo.InvariantCulture) : "n/a")}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"display: {hardware?.DisplayRes ?? "n/a"}{(hardware?.DisplayHz is double hz ? " @ " + hz.ToString("0.#", CultureInfo.InvariantCulture) + " Hz" : string.Empty)}");
        sb.AppendLine("settings:");
        foreach (SettingDefinition definition in SettingsRegistry.All)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {definition.Key} = {settings.GetValueOrDefault(definition.Key, definition.Default)}");
        }

        return sb.ToString();
    }

    public static string FileName(DateTimeOffset now) => "diag-" + now.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt";

    /// <summary>Writes the report beside the logs and echoes it to stdout; the path written.</summary>
    public static string Write(string logsDirectory, string report, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);
        ArgumentNullException.ThrowIfNull(report);
        Directory.CreateDirectory(logsDirectory);
        string path = Path.Combine(logsDirectory, FileName(now));
        File.WriteAllText(path, report, new UTF8Encoding(false));
        Console.Out.Write(report);
        Console.Out.Flush();
        return path;
    }
}
