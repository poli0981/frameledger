using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.TriState;
using FrameLedger.Domain.Metrics;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.App.Tests;

/// <summary>The summary over a scratch ledger: stat cards from the stored aggregates, the override writing the annotation (and the game default), tags/notes saved, exports through a scripted saver.</summary>
[Collection(StringsCultureCollection.Name)]
public sealed class SessionSummaryViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class ScriptedOverride(TriStateOverrideChoice? answer) : ITriStateOverridePrompt
    {
        public Task<TriStateOverrideChoice?> AskAsync(TriStateChipModel chip, CancellationToken ct = default) => Task.FromResult(answer);
    }

    private sealed class ScriptedSaver(string? path) : IFileSaver
    {
        public string? PickSavePath(string filter, string suggestedName) => path;
    }

    private sealed class FakeStrip : IMessageStrip
    {
        public List<string> Lines { get; } = [];

        public void Info(string title, string body) => Lines.Add("info:" + body);

        public void Success(string title, string body) => Lines.Add("success:" + body);

        public void Warn(string title, string body) => Lines.Add("warn:" + body);
    }

    private static SessionSummaryViewModel Build(ScratchLedger s, TriStateOverrideChoice? overrideAnswer = null, string? savePath = null, FakeStrip? strip = null) =>
        new(s.Sessions, s.Games, s.Annotations, new SqliteHardwareSnapshotRepository(s.Db), new SessionSeriesLoader(s.Sessions),
            new ScriptedOverride(overrideAnswer), new ScriptedSaver(savePath), strip ?? new FakeStrip());

    [Fact]
    public async Task StatCardsComeFromTheStoredRowAndATierTwoSessionSaysSo()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow game = await s.GameAsync("Alpha");
            long hooked = await s.SessionWithFramesAsync(game.Id, DateTimeOffset.UtcNow, frames: 300, spikeEvery: 0);
            long tier2 = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-1), hooked: false);

            SessionSummaryViewModel vm = Build(s);
            await vm.LoadAsync(hooked, Ct);
            vm.NotFound.Should().BeFalse();
            vm.IsHooked.Should().BeTrue();
            vm.Series.Should().NotBeNull();
            vm.Stats.Should().HaveCount(10, "eight frame statistics, then the machine: GPU and CPU (2026-09-21)");
            vm.Stats.Single(c => string.Equals(c.Label, "CPU", StringComparison.Ordinal)).Value.Should().Be("N/A avg · N/A max", "this row carries no CPU reading: N/A, never 0%");
            vm.Stats.Single(c => string.Equals(c.Label, "Median", StringComparison.Ordinal)).Value.Should().Be("60");
            vm.Stats.Single(c => string.Equals(c.Label, "1% Low", StringComparison.Ordinal)).Value.Should().Be("48");
            vm.Stats.Single(c => string.Equals(c.Label, "Duration", StringComparison.Ordinal)).Value.Should().Be(Formats.Duration(vm.Row!.DurationSeconds));
            vm.Chips.Should().HaveCount(3);
            vm.Chips[0].IsYes.Should().BeTrue();
            vm.DecimationNote.Should().Contain("300");

            SessionSummaryViewModel two = Build(s);
            await two.LoadAsync(tier2, Ct);
            two.IsHooked.Should().BeFalse();
            two.Series.Should().BeNull();
            two.Stats.Single(c => string.Equals(c.Label, "Median", StringComparison.Ordinal)).Value.Should().Be("N/A");
            two.Stats.Single(c => string.Equals(c.Label, "Average", StringComparison.Ordinal)).Value.Should().Be("N/A");

            SessionSummaryViewModel missing = Build(s);
            await missing.LoadAsync(tier2 + 99, Ct);
            missing.NotFound.Should().BeTrue();
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    [Fact]
    public async Task AnOverrideWritesTheAnnotationBesideTheMeasurementAndOptionallyTheGameDefault()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        long id = await s.SessionWithFramesAsync(game.Id, DateTimeOffset.UtcNow, frames: 40, spikeEvery: 0);
        SessionSummaryViewModel vm = Build(s, overrideAnswer: new TriStateOverrideChoice(Tri.No, SetAsGameDefault: true));
        await vm.LoadAsync(id, Ct);
        vm.Chips[0].Source.Should().Be(TriStateSource.Measured);

        vm.OverrideCommand.Execute(vm.Chips[0]);
        Task pending = vm.Pending;
        await pending;

        vm.Chips[0].Should().BeEquivalentTo(new { Value = Tri.No, Source = TriStateSource.Manual });
        (await s.Annotations.FindAsync(id, Ct))!.RtOverride.Should().Be(Tri.No);
        (await s.Sessions.FindByIdAsync(id, Ct))!.RtFlag.Should().Be("yes", "the measurement stays");
        (await s.Games.FindByIdAsync(game.Id, Ct))!.RtDefault.Should().Be(Tri.No);

        SessionSummaryViewModel clear = Build(s, overrideAnswer: new TriStateOverrideChoice(null, false));
        await clear.LoadAsync(id, Ct);
        clear.OverrideCommand.Execute(clear.Chips[0]);
        Task pending2 = clear.Pending;
        await pending2;
        clear.Chips[0].Source.Should().Be(TriStateSource.Measured, "clearing restores the measurement");
    }

    [Fact]
    public async Task TagsAndNotesSaveAndTheExportsWriteWhereTheSaverSays()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        long id = await s.SessionWithFramesAsync(game.Id, DateTimeOffset.UtcNow, frames: 40, spikeEvery: 0);
        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fl-export-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var strip = new FakeStrip();
            SessionSummaryViewModel vm = Build(s, savePath: System.IO.Path.Combine(dir, "out.csv"), strip: strip);
            await vm.LoadAsync(id, Ct);
            vm.Tags = "bench, 1440p, bench";
            vm.Notes = "  clean run ";
            vm.SaveAnnotationCommand.Execute(null);
            Task pending = vm.Pending;
            await pending;
            SessionAnnotation saved = (await s.Annotations.FindAsync(id, Ct))!;
            saved.Tags.Should().Equal("bench", "1440p");
            saved.Notes.Should().Be("clean run");

            vm.ExportCsvCommand.Execute(null);
            Task pending2 = vm.Pending;
            await pending2;
            (await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(dir, "out.csv"), Ct)).Should().Contain(SessionExporter.CsvColumns);
            strip.Lines.Should().Contain(static l => l.StartsWith("success:", StringComparison.Ordinal));

            SessionSummaryViewModel cancelled = Build(s, savePath: null, strip: strip);
            await cancelled.LoadAsync(id, Ct);
            cancelled.ExportJsonCommand.Execute(null);
            Task pending3 = cancelled.Pending;
            await pending3;
            System.IO.Directory.GetFiles(dir).Should().HaveCount(1, "a cancelled picker writes nothing");
        }
        finally
        {
            System.IO.Directory.Delete(dir, recursive: true);
        }
    }
}
