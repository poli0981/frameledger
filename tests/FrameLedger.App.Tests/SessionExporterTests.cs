// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Tests;

/// <summary>FR-9: the CSV's header block and column list are <c>03_METRICS</c> §Export schema's, every row is InvariantCulture, a Tier-2 export has no rows; the JSON carries metadata, aggregates, segments and the annotation.</summary>
public sealed class SessionExporterTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheCsvFollowsTheSchemaAndIsCultureInvariant()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");   // a comma decimal separator, to prove invariance
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow game = await s.GameAsync("Alpha");
            long id = await s.SessionWithFramesAsync(game.Id, DateTimeOffset.UtcNow, frames: 40, spikeEvery: 0);
            SessionRow row = (await s.Sessions.FindByIdAsync(id, Ct))!;
            IReadOnlyList<SegmentRow> segments = await s.Sessions.FindSegmentsAsync(id, Ct);
            SessionSeries series = (await new SessionSeriesLoader(s.Sessions).LoadAsync(id, Ct))!;

            using var writer = new StringWriter();
            SessionExporter.WriteCsv(writer, row, game, new HardwareSnapshot { GpuName = "RTX 5080" }, segments, series);
            string[] lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            lines.Should().Contain(static l => l.StartsWith("# capture_tier: 1", StringComparison.Ordinal));
            lines.Should().Contain(static l => l.StartsWith("# hardware: RTX 5080", StringComparison.Ordinal));
            lines.Should().Contain(static l => l.StartsWith("# segment: frames 0-39", StringComparison.Ordinal));
            string header = lines.First(static l => !l.StartsWith('#'));
            header.Should().Be(SessionExporter.CsvColumns);
            string[] rows = [.. lines.SkipWhile(static l => l.StartsWith('#')).Skip(1)];
            rows.Should().HaveCount(40);
            string[] second = rows[1].Split(',');
            second.Should().HaveCount(16);
            second[0].Should().Be("1");
            second[1].Should().Be("16.667", "qpc_ms is relative to the first present, invariant");
            second[2].Should().Be("16.667");
            second[3].Should().Be("native");
            second[8].Should().Be("dlss", "joined from the segment by frame index");
            rows.Should().OnlyContain(static r => !r.Contains("16,667", StringComparison.Ordinal), "never the vi-VN decimal comma");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task ATierTwoCsvHasTheHeaderAndNoRowsAndTheJsonCarriesTheRecord()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        long id = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow, hooked: false);
        SessionRow row = (await s.Sessions.FindByIdAsync(id, Ct))!;
        await s.Annotations.UpsertAsync(new SessionAnnotation { SessionId = id, Tags = ["bench"], Notes = "n" }, Ct);

        using var writer = new StringWriter();
        SessionExporter.WriteCsv(writer, row, game, null, [], null);
        string[] lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().Contain(static l => l.StartsWith("# capture_tier: 2", StringComparison.Ordinal) && l.Contains("no per-frame rows", StringComparison.Ordinal));
        lines[^1].Should().Be(SessionExporter.CsvColumns, "the column line is the last line: nothing was measured");
        lines.Should().Contain("# display: N/A (not recorded: a session recorded before beta.10, or recovered from its crash file)");

        SessionExportDocument doc = SessionExporter.Document(row, game, null, [], await s.Annotations.FindAsync(id, Ct));
        using var stream = new MemoryStream();
        SessionExporter.WriteJson(stream, doc);
        using JsonDocument json = JsonDocument.Parse(stream.ToArray());
        json.RootElement.GetProperty("schema").GetString().Should().Be("frameledger-session/1");
        json.RootElement.GetProperty("capture_tier").GetInt32().Should().Be(2);
        json.RootElement.GetProperty("exit_status").GetString().Should().Be("normal");
        json.RootElement.GetProperty("tags")[0].GetString().Should().Be("bench");
        json.RootElement.GetProperty("aggregates").GetProperty("session_guid").GetGuid().Should().Be(row.SessionGuid);
        json.RootElement.GetProperty("aggregates").TryGetProperty("native_fps", out _).Should().BeFalse("N/A is omitted, never written as 0");
        json.RootElement.GetProperty("game").GetString().Should().Be("Alpha");
        _ = CaptureTier.NotHooked;
    }

    /// <summary>beta.10: the CSV's <c># display:</c> line is the stored milliseconds by name, invariant in every UI language.</summary>
    [Fact]
    public async Task TheDisplayHeaderLineIsTheStoredMillisecondsByName()
    {
        CultureInfo? previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow game = await s.GameAsync("Alpha");
            long id = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow, display: new FrameLedger.Domain.Display.DisplaySummary
            {
                ExclusiveMs = 1_500,
                BorderlessMs = 60_000,
                Changes = 2,
                Source = FrameLedger.Domain.Display.DisplaySource.SwapChain,
                WindowWidth = 2560,
                WindowHeight = 1440,
                BufferWidth = 2560,
                BufferHeight = 1440,
                MonitorWidth = 2560,
                MonitorHeight = 1440,
                MonitorHz = 165,
                SwapEffect = "flip_discard",
            });
            SessionRow row = (await s.Sessions.FindByIdAsync(id, Ct))!;

            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            SessionExporter.WriteCsv(writer, row, game, null, [], null);

            writer.ToString().Split(Environment.NewLine).Should().Contain(
                "# display: exclusive_ms=1500; borderless_ms=60000; covers_ms=0; windowed_ms=0; minimized_ms=0; nowindow_ms=0; changes=2; source=swapchain;"
                + " window=2560x1440; buffer=2560x1440; monitor=2560x1440@165");
            writer.ToString().Should().Contain("swap_effect: flip_discard", "the swap chain's own description is sessions.swap_effect's first writer");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
