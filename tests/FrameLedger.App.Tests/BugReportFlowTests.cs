using System.IO;
using System.IO.Compression;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Settings;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// <c>10_LOGGING</c> §Bug report flow steps 2–4 (P4 PR-3): the zip goes where the user says and the preview lists what
/// it holds; "Open GitHub issue" opens the form with the two short fields prefilled by their real ids; the fallback
/// puts the environment summary on the clipboard as Markdown; a cancelled save does nothing; nothing is ever sent.
/// </summary>
public sealed class BugReportFlowTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-bugflow-" + Guid.NewGuid().ToString("N"));

    public BugReportFlowTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class PathSaver(string? path) : IFileSaver
    {
        public string? PickSavePath(string filter, string suggestedName) => path;
    }

    private sealed class RecordingStrip : IMessageStrip
    {
        public List<(string Kind, string Title, string Body)> Shown { get; } = [];

        public void Info(string title, string body) => Shown.Add(("info", title, body));

        public void Success(string title, string body) => Shown.Add(("success", title, body));

        public void Warn(string title, string body) => Shown.Add(("warn", title, body));
    }

    private sealed record Harness(BugReportFlow Flow, ClosePreview Preview, NoUrlOpener Urls, NoClipboard Clipboard, RecordingStrip Strip, string Zip);

    private async Task<Harness> BuildAsync(ScratchLedger s, BugReportChoice choice, bool cancel = false, BugBundleOptions? options = null)
    {
        await File.WriteAllTextAsync(Path.Combine(_dir, "ui-20260914.log"), "[12:00:00.000 INF] hello\n", Ct).ConfigureAwait(false);
        var builder = new BugBundleBuilder(_dir, new RegisteredSettings(new SqliteSettingsStore(s.Db)), crashDumpDirectory: Path.Combine(_dir, "crashdumps"));
        string zip = Path.Combine(_dir, "bundle.zip");
        var preview = new ClosePreview(choice, options);
        var urls = new NoUrlOpener();
        var clipboard = new NoClipboard();
        var strip = new RecordingStrip();
        var agent = new FakeAgentLink { Hello = new HelloAck("9.9.9", IpcProtocol.Version, 4, false, "build-z", false, "l1", false, "consent-dialog/1") };
        var lastSession = new LastSessionSummary(s.Sessions, s.Games, new SqliteHardwareSnapshotRepository(s.Db));
        return new Harness(new BugReportFlow(builder, new PathSaver(cancel ? null : zip), agent, preview, urls, clipboard, strip, lastSession: lastSession), preview, urls, clipboard, strip, zip);
    }

    [Fact]
    public async Task TheZipIsWrittenThePreviewListsItAndOpenIssueOpensTheFormPrefilledByFieldId()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        Harness h = await BuildAsync(s, BugReportChoice.OpenIssue);

        BugReportOutcome o = await h.Flow.RunAsync(Ct);

        o.Written.Should().BeTrue();
        o.Choice.Should().Be(BugReportChoice.OpenIssue);
        File.Exists(h.Zip).Should().BeTrue();
        BugReportPreviewModel shown = h.Preview.Shown.Should().ContainSingle().Subject;
        shown.ZipPath.Should().Be(h.Zip);
        using (ZipArchive archive = await ZipFile.OpenReadAsync(h.Zip, Ct))
        {
            shown.Entries.Should().BeEquivalentTo(archive.Entries.Select(static e => e.FullName), "the preview lists exactly what the zip holds");
        }

        shown.Entries.Should().Contain("logs/ui-20260914.log").And.Contain("sysinfo.json");
        h.Urls.Opened.Should().ContainSingle().Which.AbsoluteUri.Should().StartWith(IssueLink.Repository + "/issues/new?template=bug_report.yml&title=%5BBug%5D%20&labels=bug&app-version=")
            .And.Contain("&os=Windows%20", "the form's field ids are app-version and os, hyphenated");
        h.Clipboard.Texts.Should().BeEmpty();
        h.Strip.Shown.Should().BeEmpty("the browser opened; there is nothing to say");
        h.Preview.Offers.Should().BeEmpty("no crash dump and no session: no question");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARecentCrashDumpIsOfferedAndGoesInOnlyWhenTicked(bool included)
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        string dump = WriteDump();
        Harness h = await BuildAsync(s, BugReportChoice.Close, options: new BugBundleOptions(Cancelled: false, IncludeCrashDump: included, IncludeLastSession: false));

        BugReportOutcome o = await h.Flow.RunAsync(Ct);

        o.Written.Should().BeTrue();
        h.Preview.Offers.Should().ContainSingle().Which.CrashDump!.Path.Should().Be(dump);
        IReadOnlyList<string> entries = h.Preview.Shown.Should().ContainSingle().Subject.Entries;
        string entry = "crashdumps/" + Path.GetFileName(dump);
        if (included)
        {
            entries.Should().Contain(entry);
        }
        else
        {
            entries.Should().NotContain(entry, "the box starts clear and was left so");
        }
    }

    [Fact]
    public async Task ClosingTheCrashDumpQuestionWritesNothing()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        WriteDump();
        Harness h = await BuildAsync(s, BugReportChoice.OpenIssue, options: BugBundleOptions.Cancel);

        BugReportOutcome o = await h.Flow.RunAsync(Ct);

        o.Written.Should().BeFalse();
        o.ZipPath.Should().BeNull();
        File.Exists(h.Zip).Should().BeFalse("the question comes before the save dialog");
        h.Preview.Shown.Should().BeEmpty();
        h.Urls.Opened.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheLastSessionIsOfferedAndGoesInOnlyWhenTickedWithItsPathsRedacted(bool included)
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Title", @"C:\Users\tester\Games\Title\title.exe");
        long id = await s.SessionAsync(game.Id, new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero));
        Harness h = await BuildAsync(s, BugReportChoice.Close, options: new BugBundleOptions(Cancelled: false, IncludeCrashDump: false, IncludeLastSession: included));

        BugReportOutcome o = await h.Flow.RunAsync(Ct);

        o.Written.Should().BeTrue();
        BugBundleOffer offer = h.Preview.Offers.Should().ContainSingle().Subject;
        offer.CrashDump.Should().BeNull();
        offer.LastSession.Should().Be(new LastSessionInfo(id, "Title", new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero)));
        IReadOnlyList<string> entries = h.Preview.Shown.Should().ContainSingle().Subject.Entries;
        if (!included)
        {
            entries.Should().NotContain("session.json", "the box starts clear and was left so");
            return;
        }

        entries.Should().Contain("session.json");
        using ZipArchive archive = await ZipFile.OpenReadAsync(h.Zip, Ct);
        using var reader = new StreamReader(await archive.GetEntry("session.json")!.OpenAsync(Ct));
        string json = await reader.ReadToEndAsync(Ct);
        json.Should().Contain("Title").And.NotContain("tester", "a path in the summary is redacted like a log");
    }

    private string WriteDump()
    {
        string dumps = Path.Combine(_dir, "crashdumps");
        Directory.CreateDirectory(dumps);
        string path = Path.Combine(dumps, "ui-20260915-010203-42.dmp");
        File.WriteAllBytes(path, "MDMP"u8.ToArray());
        return path;
    }

    [Fact]
    public async Task CopyMarkdownPutsTheEnvironmentSummaryOnTheClipboard()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        Harness h = await BuildAsync(s, BugReportChoice.CopyMarkdown);

        await h.Flow.RunAsync(Ct);

        h.Urls.Opened.Should().BeEmpty();
        string md = h.Clipboard.Texts.Should().ContainSingle().Subject;
        md.Should().StartWith("| Key | Value |").And.Contain("| `app_version` | ").And.Contain("| `agent_version` | 9.9.9 |").And.Contain("| `overlay_build_id` | build-z |");
        h.Strip.Shown.Should().ContainSingle().Which.Kind.Should().Be("info");
    }

    [Fact]
    public async Task ClosingKeepsTheZipAndSaysSoAndACancelledSaveWritesNothing()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        Harness closed = await BuildAsync(s, BugReportChoice.Close);
        (await closed.Flow.RunAsync(Ct)).Choice.Should().Be(BugReportChoice.Close);
        File.Exists(closed.Zip).Should().BeTrue();
        closed.Urls.Opened.Should().BeEmpty();
        closed.Strip.Shown.Should().ContainSingle().Which.Kind.Should().Be("success");

        Harness cancelled = await BuildAsync(s, BugReportChoice.OpenIssue, cancel: true);
        BugReportOutcome o = await cancelled.Flow.RunAsync(Ct);
        o.Written.Should().BeFalse();
        cancelled.Preview.Shown.Should().BeEmpty("no zip, no preview");
        cancelled.Strip.Shown.Should().BeEmpty();
    }

    [Fact]
    public void TheIssueLinkAndTheOsLineAreTheFormsSpelling()
    {
        IssueLink.NewIssue("0.1.0", "Windows 11 26100.2314").AbsoluteUri.Should().Be(
            "https://github.com/poli0981/frameledger/issues/new?template=bug_report.yml&title=%5BBug%5D%20&labels=bug&app-version=0.1.0&os=Windows%2011%2026100.2314");
        IssueLink.OsText(new Version(10, 0, 26100, 2314)).Should().Be("Windows 11 26100.2314");
        IssueLink.OsText(new Version(10, 0, 19045, 0)).Should().Be("Windows 10 19045");
        IssueLink.Documentation.AbsoluteUri.Should().Be("https://github.com/poli0981/frameledger#readme");
        IssueLink.Markdown(new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "x|y" }).Should().Contain("| `a` | x\\|y |", "a pipe in a value must not break the table");
    }
}
