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

        IReadOnlyList<string> written = await new BugBundleBuilder(_dir, settings, clock).WriteAsync(zip, agent: null, ct: Ct);

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

        await new BugBundleBuilder(_dir, new RegisteredSettings(new SqliteSettingsStore(s.Db))).WriteAsync(zip, hello, ct: Ct);

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

    [Fact]
    public async Task TheCrashDumpGoesInOnlyWhenPassedAndOnlyFromTheDumpDirectory()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
        string dumps = Path.Combine(_dir, "crashdumps");
        string dump = WriteDump(dumps, "ui-20260915-100000-42.dmp", clock.GetUtcNow().UtcDateTime.AddHours(-2), bytes: 3000);
        var builder = new BugBundleBuilder(_dir, new RegisteredSettings(new SqliteSettingsStore(s.Db)), clock, dumps);
        CrashDumpInfo latest = builder.LatestCrashDump()!;
        latest.Path.Should().Be(dump);
        latest.Bytes.Should().Be(3000);

        IReadOnlyList<string> without = await builder.WriteAsync(Path.Combine(_dir, "without.zip"), agent: null, ct: Ct);
        IReadOnlyList<string> with = await builder.WriteAsync(Path.Combine(_dir, "with.zip"), agent: null, latest, Ct);

        without.Should().NotContain(static e => e.StartsWith("crashdumps/", StringComparison.Ordinal), "a dump goes in only when the user ticked it");
        with.Should().Contain("crashdumps/ui-20260915-100000-42.dmp");
        string foreign = Path.Combine(_dir, "game.dmp");
        await File.WriteAllTextAsync(foreign, "MDMP", Ct);
        Func<Task> outside = () => builder.WriteAsync(Path.Combine(_dir, "foreign.zip"), agent: null, latest with { Path = foreign }, Ct);
        await outside.Should().ThrowAsync<ArgumentException>("the bundle never carries a file from outside the dump directory");
        File.Exists(Path.Combine(_dir, "foreign.zip")).Should().BeFalse("refused before the zip is created");
    }

    [Fact]
    public async Task TheOfferedDumpIsTheNewestOfTheLastSevenDays()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var settings = new RegisteredSettings(new SqliteSettingsStore(s.Db));
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
        DateTime now = clock.GetUtcNow().UtcDateTime;
        string dumps = Path.Combine(_dir, "crashdumps");
        new BugBundleBuilder(_dir, settings, clock).LatestCrashDump().Should().BeNull("no dump directory was given");
        new BugBundleBuilder(_dir, settings, clock, dumps).LatestCrashDump().Should().BeNull("the directory does not exist yet");
        WriteDump(dumps, "ui-old.dmp", now.AddDays(-8), bytes: 10);
        new BugBundleBuilder(_dir, settings, clock, dumps).LatestCrashDump().Should().BeNull("older than seven days");

        WriteDump(dumps, "agent-older.dmp", now.AddDays(-2), bytes: 20);
        WriteDump(dumps, "ui-newest.dmp", now.AddHours(-1), bytes: 30);
        WriteDump(dumps, "ui-notes.txt", now, bytes: 40);
        CrashDumpInfo? latest = new BugBundleBuilder(_dir, settings, clock, dumps).LatestCrashDump();

        latest.Should().NotBeNull();
        Path.GetFileName(latest!.Path).Should().Be("ui-newest.dmp", "the newest dump of either process; a .txt is not a dump");
        latest.WrittenAt.Should().Be(new DateTimeOffset(now.AddHours(-1), TimeSpan.Zero));
        latest.Bytes.Should().Be(30);
    }

    private static string WriteDump(string directory, string name, DateTime mtimeUtc, int bytes)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path, new byte[bytes]);
        File.SetLastWriteTimeUtc(path, mtimeUtc);
        return path;
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
