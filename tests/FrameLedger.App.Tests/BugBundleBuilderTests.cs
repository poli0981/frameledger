using System.IO;
using System.IO.Compression;
using System.Text.Json;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Settings;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// <c>10_LOGGING</c> §Bug report flow step 2: our logs of the last seven days, the last overlay logs, sysinfo and the
/// sanitized settings — and nothing that is not ours (no game's file, however it is named).
/// </summary>
public sealed class BugBundleBuilderTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-bundle-" + Guid.NewGuid().ToString("N"));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    public BugBundleBuilderTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task ShipsOurLogsOfSevenDaysTheLastOverlaysAndTheTwoJsonFiles()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero));
        DateTime now = clock.GetUtcNow().UtcDateTime;
        Write("ui-20260914.log", now);
        Write("agent-20260914.log", now.AddHours(-1));
        Write("ui-20260901.log", now.AddDays(-8));
        Write("game-crash.log", now);
        for (int i = 0; i < BugBundleBuilder.OverlayLogsKept + 2; i++)
        {
            Write($"overlay-{1000 + i}-20260914-12000{i}.log", now.AddMinutes(-i));
        }

        var settings = new RegisteredSettings(new SqliteSettingsStore(s.Db));
        await settings.SetAsync(SettingsRegistry.HookingKillSwitch, true, Ct);
        string zip = Path.Combine(_dir, "bundle.zip");

        IReadOnlyList<string> written = await new BugBundleBuilder(_dir, settings, clock).WriteAsync(zip, agent: null, Ct);

        written.Should().Contain(["logs/ui-20260914.log", "logs/agent-20260914.log", "sysinfo.json", "settings.json"]);
        written.Should().NotContain("logs/ui-20260901.log", "older than seven days");
        written.Should().NotContain(static e => e.Contains("game-crash", StringComparison.Ordinal), "we ship our logs only");
        written.Count(static e => e.StartsWith("overlay/", StringComparison.Ordinal)).Should().Be(BugBundleBuilder.OverlayLogsKept);
        written.Should().Contain("overlay/overlay-1000-20260914-120000.log", "the newest overlay log");
        written.Should().NotContain("overlay/overlay-1006-20260914-120006.log", "the oldest is past the cap");

        Dictionary<string, string> values = ReadJson(zip, "settings.json");
        values.Should().ContainKey("hooking.kill_switch").WhoseValue.Should().Be("1");
        values.Keys.Should().BeEquivalentTo(SettingsRegistry.All.Select(static d => d.Key), "every registry key, and no path");
        Dictionary<string, string> sysinfo = ReadJson(zip, "sysinfo.json");
        sysinfo["agent_version"].Should().Be("not connected");
        sysinfo["app_version"].Should().Be(UiIdentity.Version);
    }

    [Fact]
    public async Task TheAgentsHelloFillsSysinfoWhenConnected()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        string zip = Path.Combine(_dir, "bundle.zip");
        var hello = new HelloAck("1.2.3", IpcProtocol.Version, 4, true, "build-x", true, "l3", true, "consent-dialog/1");

        await new BugBundleBuilder(_dir, new RegisteredSettings(new SqliteSettingsStore(s.Db))).WriteAsync(zip, hello, Ct);

        Dictionary<string, string> sysinfo = ReadJson(zip, "sysinfo.json");
        sysinfo["agent_version"].Should().Be("1.2.3");
        sysinfo["overlay_build_id"].Should().Be("build-x");
        sysinfo["vulkan_layer_registered"].Should().Be("True");
        sysinfo["telemetry_source"].Should().Be("l3");
    }

    [Fact]
    public void TheSuggestedNameIsTheDocsFormat()
    {
        BugBundleBuilder.SuggestedName(new DateTimeOffset(2026, 9, 14, 12, 34, 0, TimeSpan.Zero).ToLocalTime()).Should().MatchRegex(@"^FrameLedger-bugreport-\d{8}-\d{4}\.zip$");
    }

    private static Dictionary<string, string> ReadJson(string zip, string entry)
    {
        using ZipArchive archive = ZipFile.OpenRead(zip);
        using Stream stream = archive.GetEntry(entry)!.Open();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }

    private void Write(string name, DateTime mtimeUtc)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, "[12:00:00.000 INF] " + name + "\n");
        File.SetLastWriteTimeUtc(path, mtimeUtc);
    }
}
