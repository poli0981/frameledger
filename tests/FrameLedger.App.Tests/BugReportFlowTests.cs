using System.IO;
using System.IO.Compression;
using FluentAssertions;
using FrameLedger.App.Services;
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

    private async Task<Harness> BuildAsync(ScratchLedger s, BugReportChoice choice, bool cancel = false)
    {
        await File.WriteAllTextAsync(Path.Combine(_dir, "ui-20260914.log"), "[12:00:00.000 INF] hello\n", Ct).ConfigureAwait(false);
        var builder = new BugBundleBuilder(_dir, new RegisteredSettings(new SqliteSettingsStore(s.Db)));
        string zip = Path.Combine(_dir, "bundle.zip");
        var preview = new ClosePreview(choice);
        var urls = new NoUrlOpener();
        var clipboard = new NoClipboard();
        var strip = new RecordingStrip();
        var agent = new FakeAgentLink { Hello = new HelloAck("9.9.9", IpcProtocol.Version, 4, false, "build-z", false, "l1", false, "consent-dialog/1") };
        return new Harness(new BugReportFlow(builder, new PathSaver(cancel ? null : zip), agent, preview, urls, clipboard, strip), preview, urls, clipboard, strip, zip);
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
