using System.IO;
using System.IO.Compression;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Settings;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.App.Tests;

/// <summary>08_UI §Logs: the tail of the chosen source through the filters, the status line, and the bug bundle where the user says.</summary>
public sealed class LogsViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-logsvm-" + Guid.NewGuid().ToString("N"));

    public LogsViewModelTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class PathSaver(string? path) : IFileSaver
    {
        public string? Suggested { get; private set; }

        public string? PickSavePath(string filter, string suggestedName)
        {
            Suggested = suggestedName;
            return path;
        }
    }

    private void Write(string name, string text) => File.WriteAllText(Path.Combine(_dir, name), text);

    private static string[] Entries(string zip)
    {
        using ZipArchive archive = ZipFile.OpenRead(zip);
        return [.. archive.Entries.Select(static e => e.FullName)];
    }

    [Fact]
    public async Task TailsTheNewestFileOfTheSourceAndFilters()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        Write("ui-20260914.log", "[12:00:00.000 INF] hello\n[12:00:00.001 WRN] careful\n");
        Write("agent-20260914.log", "[12:00:00.000 INF] agent here\n");
        var strip = new RecordingStrip();
        using var vm = new LogsViewModel(new LogTail(_dir), new BugBundleBuilder(_dir, new RegisteredSettings(new SqliteSettingsStore(s.Db))), new PathSaver(null), new FakeAgentLink(), strip);
        Task pending1 = vm.Pending;
        await pending1;

        vm.TotalCount.Should().Be(2);
        vm.Text.Should().Contain("hello").And.Contain("careful");
        vm.Status.Should().Contain("ui-20260914.log");

        vm.Level = LogLevelFilter.WarningAndAbove;
        vm.ShownCount.Should().Be(1);
        vm.Text.Should().Be("[12:00:00.001 WRN] careful");

        vm.Level = LogLevelFilter.All;
        vm.Search = "AGENT";
        vm.ShownCount.Should().Be(0);

        vm.Source = LogSource.Agent;
        Task pending2 = vm.Pending;
        await pending2;
        vm.ShownCount.Should().Be(1);
        vm.Text.Should().Contain("agent here");
    }

    [Fact]
    public async Task WithoutAFileTheStatusSaysSo()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        using var vm = new LogsViewModel(new LogTail(_dir), new BugBundleBuilder(_dir, new RegisteredSettings(new SqliteSettingsStore(s.Db))), new PathSaver(null), new FakeAgentLink(), new RecordingStrip());
        Task pending = vm.Pending;
        await pending;

        vm.CurrentFile.Should().BeNull();
        vm.Status.Should().Be(Strings.Logs_Empty);
        vm.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task TheBugBundleGoesWhereTheUserSaysAndNowhereElse()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        Write("ui-20260914.log", "[12:00:00.000 INF] hello\n");
        string zip = Path.Combine(_dir, "out", "bundle.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(zip)!);
        var saver = new PathSaver(zip);
        var strip = new RecordingStrip();
        using var vm = new LogsViewModel(new LogTail(_dir), new BugBundleBuilder(_dir, new RegisteredSettings(new SqliteSettingsStore(s.Db))), saver, new FakeAgentLink(), strip);
        Task pending1 = vm.Pending;
        await pending1;

        await vm.ExportBundleCommand.ExecuteAsync(null);

        saver.Suggested.Should().StartWith("FrameLedger-bugreport-").And.EndWith(".zip");
        File.Exists(zip).Should().BeTrue();
        Entries(zip).Should().Contain(["logs/ui-20260914.log", "sysinfo.json", "settings.json"]);

        strip.Shown.Should().ContainSingle().Which.Kind.Should().Be("success");
    }

    [Fact]
    public async Task ACancelledSaveWritesNothing()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var strip = new RecordingStrip();
        using var vm = new LogsViewModel(new LogTail(_dir), new BugBundleBuilder(_dir, new RegisteredSettings(new SqliteSettingsStore(s.Db))), new PathSaver(null), new FakeAgentLink(), strip);

        await vm.ExportBundleCommand.ExecuteAsync(null);

        Directory.EnumerateFiles(_dir, "*.zip").Should().BeEmpty();
        strip.Shown.Should().BeEmpty();
    }
}
