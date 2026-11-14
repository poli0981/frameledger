// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.IO;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Input;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.App.Tests;

/// <summary>
/// A session's input series as the charts read them (0.1.0-beta.50): the series decoded with their gaps, the marks made
/// of them, the two charts, the marks on the frame-time chart, the summary's view model over a stored session, the two
/// trend metrics and the export's line. Over <see cref="InputFixtures"/> - synthetic; no controller was read.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class InputChartsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class NoOverride : ITriStateOverridePrompt
    {
        public Task<TriStateOverrideChoice?> AskAsync(TriStateChipModel chip, CancellationToken ct = default) =>
            Task.FromResult<TriStateOverrideChoice?>(null);
    }

    private sealed class NoSaver : IFileSaver
    {
        public string? PickSavePath(string filter, string suggestedName) => null;
    }

    private sealed class NoStrip : IMessageStrip
    {
        public void Info(string title, string body)
        {
        }

        public void Success(string title, string body)
        {
        }

        public void Warn(string title, string body)
        {
        }
    }

    private static SessionSummaryViewModel Summary(ScratchLedger s) =>
        new(s.Sessions, s.Games, s.Annotations, new SqliteHardwareSnapshotRepository(s.Db), new SessionSeriesLoader(s.Sessions), new NoOverride(), new NoSaver(), new NoStrip());

    [Fact]
    public void TheSeriesAreDecodedOnTheSensorsClockWithTheirGapsKept()
    {
        InputSeries series = InputSeries.Decode(InputFixtures.Blobs())!;

        series.TimesS.Should().Equal(0, 1, 2, 3, 4, 5, 6, 7);
        series.Class.Take(6).Should().Equal(1f, 1f, 3f, 2f, 2f, 0f);
        float.IsNaN(series.Class[6]).Should().BeTrue("the game was not in front in the seventh second, and that is kept");
        series.Devices.Select(static d => d.Index).Should().Equal(1, 2);
        series.Devices[0].Battery.Should().NotBeNull();
        float.IsNaN(series.Devices[1].Active[0]).Should().BeTrue("the second controller was not connected yet");

        InputSeries.Decode([]).Should().BeNull("a session that stored no input series has none to draw");
        InputSeries.Decode([.. InputFixtures.Blobs().Where(static b => !string.Equals(b.Series, InputSessionBuilder.ClassSeries, StringComparison.Ordinal))])
            .Should().BeNull();

        // With no clock the stored rate is the fallback, as for every other series.
        InputSeries.Decode([.. InputFixtures.Blobs().Skip(1)])!.TimesS.Should().Equal(0, 1, 2, 3, 4, 5, 6, 7);
    }

    [Fact]
    public void TheMarksAreTheConnectionsTheDropsAndTheBatteriesThatFellToLow()
    {
        InputSeries series = InputSeries.Decode(InputFixtures.Blobs())!;

        series.Events(InputFixtures.Record()).Should().Equal(
            new InputEvent(2, InputEventKind.Connected, 2),
            new InputEvent(4, InputEventKind.Disconnected, 2),
            new InputEvent(5, InputEventKind.BatteryLow, 1),
            new InputEvent(6, InputEventKind.Connected, 2),
            new InputEvent(6, InputEventKind.BatteryLow, 2));

        // The row counted one connection for a controller whose series rises twice: the first rise is the session's start.
        InputDevicesRecord oneConnect = InputFixtures.Record() with { Devices = [.. InputFixtures.Record().Devices.Select(static d => d.Index == 2 ? d with { Connects = 1 } : d)] };
        series.Events(oneConnect).Where(static e => e.Kind == InputEventKind.Connected).Should().Equal(new InputEvent(6, InputEventKind.Connected, 2));

        // With no stored row: a controller there on the first tick did not connect, and no battery scale means no low mark.
        series.Events(null).Select(static e => (e.TimeS, e.Kind, e.Device)).Should().Equal(
            (2d, InputEventKind.Connected, 2), (4d, InputEventKind.Disconnected, 2), (6d, InputEventKind.Connected, 2));
    }

    [Fact]
    public void AFlappingConnectionIsCappedAndAFourLevelBatteryIsDrawnAtThirds()
    {
        float[] flapping = [.. Enumerable.Range(0, 400).Select(static i => i % 2 == 0 ? float.NaN : 0f)];
        var series = new InputSeries([.. Enumerable.Range(0, 400).Select(static i => (double)i)], new float[400], [new InputDeviceSeries(1, flapping, null)]);

        series.Events(null).Should().HaveCount(InputSeries.MaxEvents, "a chart is not buried under its own marks; the table has the counts");
        Enumerable.Range(0, 4).Select(static l => InputSeries.ToPercent(l, BatteryScale.FourLevels)).Should().Equal(0, 33, 67, 100);
        InputSeries.ToPercent(57f, BatteryScale.Percent).Should().Be(57);
        InputSeries.ToPercent(140f, BatteryScale.Percent).Should().Be(100);
    }

    [Fact]
    public async Task TheTwoChartsDrawWhatTheSessionStoredAndSayWhenItStoredNothing()
    {
        InputSeries series = InputSeries.Decode(InputFixtures.Blobs())!;
        IReadOnlyList<InputEvent> events = series.Events(InputFixtures.Record());

        (int points, int marks, int empty, int lines, int noBattery) = await PagesLoadTests.OnStaAsync(() =>
        {
            var timeline = new InputTimelineChart();
            timeline.Show(series, events);
            int drawn = timeline.DrawnPoints;
            int drawnMarks = timeline.DrawnEvents;
            timeline.Show(null, []);
            var battery = new InputBatteryChart();
            battery.Show(series, InputFixtures.Record());
            int drawnLines = battery.DrawnLines;
            battery.Show(series, null);
            return (drawn, drawnMarks, timeline.DrawnPoints, drawnLines, battery.DrawnLines);
        });

        points.Should().Be(7, "seven of the eight seconds had the game in front");
        marks.Should().Be(5);
        empty.Should().Be(0);
        lines.Should().Be(2, "both controllers reported a battery");
        noBattery.Should().Be(0, "without the stored scale a level cannot be put on the axis, so nothing is");
    }

    [Fact]
    public async Task TheFrameTimeChartCarriesTheMarksOnlyWhereTheFramesPlaceOnTheClockIsKnown()
    {
        InputSeries input = InputSeries.Decode(InputFixtures.Blobs())!;
        IReadOnlyList<InputEvent> events = input.Events(InputFixtures.Record());
        SessionSeries Frames(double? offset) => new()
        {
            TimesS = [.. Enumerable.Range(0, 60).Select(static i => i * 0.1)],
            FrameTimesMs = [.. Enumerable.Repeat(16.7f, 60)],
            Generated = new bool[60],
            Gap = new bool[60],
            AppTimesS = [.. Enumerable.Range(0, 60).Select(static i => i * 0.1)],
            AppFrameTimesMs = [.. Enumerable.Repeat(16.7, 60)],
            AppPresentIndex = [.. Enumerable.Range(0, 60)],
            Segments = [],
            Sensors = [],
            SensorsAligned = offset is not null,
            SensorOffsetS = offset,
        };

        (int aligned, int shifted, int hidden, int unaligned) = await PagesLoadTests.OnStaAsync(() =>
        {
            var chart = new FrametimeChart();
            chart.SetInputEvents(events, show: true);
            chart.Show(Frames(0), displayed: false, sensors: false);
            int atZero = chart.DrawnInputEvents;
            chart.Show(Frames(3), displayed: false, sensors: false);
            int fromThree = chart.DrawnInputEvents;
            chart.SetInputEvents(events, show: false);
            int off = chart.DrawnInputEvents;
            chart.SetInputEvents(events, show: true);
            chart.Show(Frames(null), displayed: false, sensors: false);
            return (atZero, fromThree, off, chart.DrawnInputEvents);
        });

        aligned.Should().Be(3, "the frames last 5.9 s: the marks at 2 s, 4 s and 5 s are on the axis, the two at 6 s are past its end");
        shifted.Should().Be(4, "with the first present three seconds in, the marks at 4, 5, 6 and 6 s are");
        hidden.Should().Be(0);
        unaligned.Should().Be(0, "a session that does not say where its frames sit on the sensors' clock draws no mark out of step");
    }

    [Fact]
    public void TheGapNoteSpeaksOfTheMarksWhenTheyAreWhatRunsAhead()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            var series = new SessionSeries
            {
                TimesS = [0, 0.1, 0.2],
                FrameTimesMs = [0, 16.7f, 0],
                Generated = new bool[3],
                Gap = [true, false, true],
                AppTimesS = [0.1],
                AppFrameTimesMs = [16.7],
                AppPresentIndex = [1],
                Segments = [],
                Sensors = [],
                SensorsAligned = true,
                SensorOffsetS = 0,
            };

            FrametimeChart.NoteFor(series, displayed: false, sensors: false).Should().BeEmpty();
            FrametimeChart.NoteFor(series, displayed: false, sensors: false, marks: true).Should().Contain("the controller marks run ahead");
            FrametimeChart.NoteFor(series, displayed: false, sensors: true, marks: true).Should().Contain("the sensor lines run ahead").And.NotContain("controller marks");
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    [Fact]
    public async Task TheSummaryShowsTheBlockInBothTiersAndSaysWhyASessionHasNone()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow game = await s.GameAsync("Alpha");
            long hooked = await s.SessionWithFramesAsync(game.Id, DateTimeOffset.UtcNow, frames: 600, spikeEvery: 0, shape: InputFixtures.With, sensors: InputFixtures.BlobsWithSensors());
            long tier2 = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-1), hooked: false, sensors: InputFixtures.Blobs(), shape: InputFixtures.With);
            long older = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-2));

            SessionSummaryViewModel vm = Summary(s);
            await vm.LoadAsync(hooked, Ct);
            vm.HasInput.Should().BeTrue();
            vm.InputRows.Select(static r => r.Label).Should().Equal("Controller", "Keyboard and mouse", "Both at once", "No input");
            vm.InputSegments.Should().HaveCount(4);
            vm.InputName.Should().StartWith("Share of the time in front");
            vm.InputControllers.Should().HaveCount(2);
            vm.HasInputControllers.Should().BeTrue();
            vm.InputNoControllers.Should().BeNull();
            vm.InputMakers.Should().Contain("PlayStation 80.0%");
            vm.InputNotes.Should().BeNull();
            vm.InputMissing.Should().BeNull();
            vm.HasInputCharts.Should().BeTrue();
            vm.HasInputBattery.Should().BeTrue();
            vm.InputEvents.Should().HaveCount(5);
            vm.HasInputEvents.Should().BeTrue("the session has frames and says where they sit on the sensors' clock");
            vm.ShowInputEvents.Should().BeTrue();

            await vm.LoadAsync(tier2, Ct);
            vm.HasInput.Should().BeTrue("what a session was played with is read from outside the game (D155): a session that was not hooked has it");
            vm.InputControllers.Should().HaveCount(2);
            vm.HasInputCharts.Should().BeTrue();
            vm.HasInputEvents.Should().BeFalse("there is no frame-time chart to carry the marks; the input timeline has them");

            await vm.LoadAsync(older, Ct);
            vm.HasInput.Should().BeFalse();
            vm.InputRows.Should().BeEmpty();
            vm.HasInputControllers.Should().BeFalse();
            vm.InputNoControllers.Should().BeNull("nothing was read, so nothing is said about controllers");
            vm.HasInputCharts.Should().BeFalse();
            vm.InputSeries.Should().BeNull();
            vm.InputMissing.Should().StartWith("Not recorded for this session");
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    [Fact]
    public async Task ASessionReadWithNoControllerSaysNoneWasConnected()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow game = await s.GameAsync("Alpha");
            long id = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow, shape: static r => r with
            {
                InputObservedMs = 60_000,
                InputForegroundMs = 60_000,
                InputKeyboardMouseMs = 50_000,
                InputIdleMs = 10_000,
                InputControllerMs = 0,
                InputBothMs = 0,
                InputMethodSwitches = 0,
                InputDevicesJson = InputDevicesRecord.ToJson(new InputDevicesRecord { Health = new Application.Input.InputHealth { RawInputRegistered = true, XInputAvailable = true, LastInputAvailable = true } }),
            });

            SessionSummaryViewModel vm = Summary(s);
            await vm.LoadAsync(id, Ct);

            vm.HasInput.Should().BeTrue();
            vm.InputRows[1].Share.Should().Be("83.3%");
            vm.HasInputControllers.Should().BeFalse();
            vm.InputNoControllers.Should().Be("No controller was connected during this session.");
            vm.InputMakers.Should().BeNull();
            vm.HasInputCharts.Should().BeFalse("this row was written without its series");
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    [Fact]
    public void TheTwoTrendMetricsAreSharesOfTheTimeInFrontInEitherTier()
    {
        SessionRow hooked = InputFixtures.With(Row(Domain.Sessions.CaptureTier.Hooked));
        SessionRow tier2 = InputFixtures.With(Row(Domain.Sessions.CaptureTier.NotHooked));

        TrendSeriesBuilder.ValueOf(hooked, TrendMetric.ControllerShare).Should().BeApproximately(42.857, 0.001);
        TrendSeriesBuilder.ValueOf(tier2, TrendMetric.KeyboardMouseShare).Should().BeApproximately(28.571, 0.001);
        TrendSeriesBuilder.ValueOf(Row(Domain.Sessions.CaptureTier.Hooked), TrendMetric.ControllerShare).Should().BeNull("a session that recorded nothing has no point (FR-4.9)");
        TrendSeriesBuilder.ValueOf(hooked with { InputForegroundMs = 0 }, TrendMetric.ControllerShare).Should().BeNull("the game was never in front");
        TrendSeriesBuilder.Counts(tier2, TrendMetric.ControllerShare).Should().BeTrue();
        TrendSeriesBuilder.IsInputMetric(TrendMetric.KeyboardMouseShare).Should().BeTrue();
        TrendSeriesBuilder.IsInputMetric(TrendMetric.DisplayWindowedShare).Should().BeFalse();
        TrendSeriesBuilder.IsPartial(hooked with { SettingsChangedMidSession = true }, TrendMetric.ControllerShare).Should().BeFalse("what a session was played with is the whole session's");
        TrendSeriesBuilder.UnitOf(TrendMetric.ControllerShare).Should().StartWith("%");
    }

    [Fact]
    public async Task TheCsvHeaderCarriesTheInputLineAndCompareHasTheTwoRows()
    {
        CultureInfo? previous = Strings.Culture;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow game = await s.GameAsync("Alpha");
            long with = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow, hooked: false, shape: InputFixtures.With);
            long without = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-1), hooked: false);
            SessionRow played = (await s.Sessions.FindByIdAsync(with, Ct))!;
            SessionRow bare = (await s.Sessions.FindByIdAsync(without, Ct))!;

            using var writer = new StringWriter();
            SessionExporter.WriteCsv(writer, played, game, null, [], null);
            writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Should().Contain(static l => l.StartsWith("# input: observed_ms 8000; foreground_ms 7000; controller_ms 3000;", StringComparison.Ordinal));
            using var none = new StringWriter();
            SessionExporter.WriteCsv(none, bare, game, null, [], null);
            none.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Should().Contain("# input: N/A");

            // The JSON export is the stored row (Privacy Policy 3A): the totals and the controllers ride it as they are.
            played.InputDevicesJson.Should().Contain("DualSense Wireless Controller").And.NotContainAny("serial", "address", "path");

            InputPresentation.SharesText(played).Should().Be("Controller 42.9% · keyboard and mouse 28.6% · both 14.3% · no input 14.3%");
            InputPresentation.SharesText(bare).Should().Be("N/A");
        }
        finally
        {
            Strings.Culture = previous;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static SessionRow Row(Domain.Sessions.CaptureTier tier) => new()
    {
        SessionGuid = Guid.NewGuid(),
        GameId = 1,
        SnapshotId = 1,
        StartedAt = DateTimeOffset.UnixEpoch,
        EndedAt = DateTimeOffset.UnixEpoch.AddMinutes(10),
        QpcEpoch = 0,
        QpcFrequency = 1,
        Tier = tier,
        Mode = Domain.Sessions.CaptureMode.Launch,
        ExitStatus = Domain.Sessions.ExitStatus.Normal,
        FrameCount = 0,
        FgMode = "na",
    };
}
