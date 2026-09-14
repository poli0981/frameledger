using System.IO;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.App.Tests;

/// <summary>File ▸ Export from the shell (P4 PR-3): the selected session as CSV or JSON, the same writers the summary window uses; no selection or a cancelled save writes nothing.</summary>
public sealed class SessionExportServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-export-" + Guid.NewGuid().ToString("N"));

    public SessionExportServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class PathSaver(string? path) : IFileSaver
    {
        public string? Suggested { get; private set; }

        public string? PickSavePath(string filter, string suggestedName)
        {
            Suggested = suggestedName;
            return path;
        }
    }

    private sealed class RecordingStrip : IMessageStrip
    {
        public List<string> Kinds { get; } = [];

        public void Info(string title, string body) => Kinds.Add("info");

        public void Success(string title, string body) => Kinds.Add("success");

        public void Warn(string title, string body) => Kinds.Add("warn");
    }

    private static SessionExportService Build(ScratchLedger s, IFileSaver saver, IMessageStrip strip) => new(
        s.Sessions, s.Games, new SqliteSessionAnnotationRepository(s.Db), new SqliteHardwareSnapshotRepository(s.Db), new SessionSeriesLoader(s.Sessions), saver, strip);

    [Fact]
    public async Task TheSelectedSessionIsWrittenAsCsvAndJsonWhereTheUserSays()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        long id = await s.SessionAsync(game.Id, DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000), hooked: true, fgMode: "dlssg", native: 62, displayed: 118, factor: 1.9);
        var strip = new RecordingStrip();

        string csv = Path.Combine(_dir, "out.csv");
        var csvSaver = new PathSaver(csv);
        (await Build(s, csvSaver, strip).ExportCsvAsync(id, Ct)).Should().Be(csv);
        csvSaver.Suggested.Should().StartWith("Alpha-").And.EndWith(".csv");
        (await File.ReadAllTextAsync(csv, Ct)).Should().Contain(SessionExporter.CsvColumns, "the same writer the summary window uses");

        string json = Path.Combine(_dir, "out.json");
        (await Build(s, new PathSaver(json), strip).ExportJsonAsync(id, Ct)).Should().Be(json);
        (await File.ReadAllTextAsync(json, Ct)).Should().Contain("\"Alpha\"");
        strip.Kinds.Should().Equal("success", "success");
    }

    [Fact]
    public async Task AnUnknownSessionOrACancelledSaveWritesNothing()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        long id = await s.SessionAsync(game.Id, DateTimeOffset.UnixEpoch);
        var strip = new RecordingStrip();

        (await Build(s, new PathSaver(Path.Combine(_dir, "x.csv")), strip).ExportCsvAsync(id + 99, Ct)).Should().BeNull();
        strip.Kinds.Should().Equal("warn");

        (await Build(s, new PathSaver(null), strip).ExportJsonAsync(id, Ct)).Should().BeNull("cancelled");
        Directory.EnumerateFiles(_dir).Should().BeEmpty();
        strip.Kinds.Should().Equal("warn"); // a cancelled save says nothing
    }

    [Fact]
    public void TheSelectionRemembersTheLastPickAndRaisesOnce()
    {
        var selection = new SessionSelection();
        int raised = 0;
        selection.Changed += (_, _) => raised++;
        selection.Set(7);
        selection.Set(7);
        selection.Set(null);
        selection.SessionId.Should().BeNull();
        raised.Should().Be(2);
    }
}
