// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Recording;

namespace FrameLedger.Application.Tests.Advanced;

/// <summary>
/// The stored form of Advanced metrics (owner decision D102): what is written reads back as it was, what does not fit
/// is left out rather than thrown or guessed at, and only what the catalog says is read may be written at all.
/// </summary>
public sealed class AdvancedJsonTests
{
    private static readonly AdvancedFact[] _facts =
    [
        new("nvapi.gpu.rt_cores", Instance: null, AdvancedValue.Of(84)),
        new("nvapi.gpu.external", Instance: null, AdvancedValue.Of(false)),
        new("nvapi.gpu.vbios_version", Instance: null, AdvancedValue.OfText("98.03.6c.00.68")),
        new("nvapi.display.hdr_formats", "1", AdvancedValue.OfTokens("hdr10", "dolby_vision")),
        new("nvapi.driver.package", Instance: null, AdvancedValue.OfTokens()),
        new("nvapi.driver.global_profile", Instance: null, AdvancedValue.OfRows([new AdvancedRow("Vertical Sync (0x00A879CF)", "0x60925292", "user_set"), new AdvancedRow("0x10E41DF3", string.Empty)])),
    ];

    private static readonly AdvancedSources _sources = new(AdvancedSourceStatus.NotPresent, NvapiCode: -6, AdvancedSourceStatus.Reading, AdvancedSourceStatus.PawnIoMissing);

    [Fact]
    public void FactsReadBackAsTheyWereInEveryShape()
    {
        IReadOnlyList<AdvancedFact> back = AdvancedJson.ParseFacts(AdvancedJson.OfFacts(_facts));

        back.Should().HaveCount(_facts.Length);
        back.Select(static f => (f.Id, f.Instance)).Should().Equal(_facts.Select(static f => (f.Id, f.Instance)));
        back[0].Value.Number.Should().Be(84);
        back[1].Value.Number.Should().Be(0, "a flag that is off was read");
        back[2].Value.Text.Should().Be("98.03.6c.00.68");
        back[3].Value.Tokens.Should().Equal("hdr10", "dolby_vision");
        back[4].Value.Tokens.Should().BeEmpty("the source answered, and named nothing: that is kept apart from not answering");
        back[5].Value.Rows.Should().Equal(new AdvancedRow("Vertical Sync (0x00A879CF)", "0x60925292", "user_set"), new AdvancedRow("0x10E41DF3", string.Empty));
    }

    [Fact]
    public void TheFormIsCamelCaseLeavesOutWhatIsNullAndSpellsAStatusByItsName()
    {
        string facts = AdvancedJson.OfFacts([_facts[0]]);
        string session = AdvancedJson.OfSession([], _sources);

        facts.Should().Be("[{\"id\":\"nvapi.gpu.rt_cores\",\"number\":84}]", "a hash is taken of this text, so it has one form");
        session.Should().Be("{\"facts\":[],\"sources\":{\"nvapi\":\"NotPresent\",\"nvapiCode\":-6,\"sensors\":\"Reading\",\"elevatedSensors\":\"PawnIoMissing\"}}");
    }

    [Fact]
    public void ASessionsFactsAndTheStateOfEachSourceReadBack()
    {
        (IReadOnlyList<AdvancedFact> facts, AdvancedSources sources) = AdvancedJson.ParseSession(AdvancedJson.OfSession([_facts[0], _facts[3]], _sources));

        facts.Select(static f => f.Id).Should().Equal("nvapi.gpu.rt_cores", "nvapi.display.hdr_formats");
        sources.Should().Be(_sources);
    }

    [Fact]
    public void TheDirectoryAndTheStatisticsReadBack()
    {
        AdvancedSeriesInfo[] series = [new("adv.lhm.cpu.temperature.intelcpu_0_3", "lhm.cpu.temperature", "Intel Core i7-14700K - CPU Core #3")];
        var stats = new Dictionary<string, SensorSeriesStats>(StringComparer.Ordinal) { ["adv.nvapi.pstate"] = new SensorSeriesStats(60, 0.5, 0, 0, 8) };

        (IReadOnlyList<AdvancedSeriesInfo> backSeries, int dropped) = AdvancedJson.ParseSeries(AdvancedJson.OfSeries(series, dropped: 3));
        IReadOnlyDictionary<string, SensorSeriesStats> backStats = AdvancedJson.ParseStats(AdvancedJson.OfStats(stats));

        backSeries.Should().Equal(series);
        dropped.Should().Be(3);
        backStats.Should().ContainKey("adv.nvapi.pstate").WhoseValue.Should().Be(new SensorSeriesStats(60, 0.5, 0, 0, 8));
        FluentActions.Invoking(static () => AdvancedJson.OfSeries([], dropped: -1)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AWholeContextReadsBackForTheRecoveryOfASession()
    {
        var data = new AdvancedSessionData([_facts[0]], [_facts[3]], [new AdvancedSeriesInfo("adv.lhm.gpu.fan.gpu_nvidia_0_1", "lhm.gpu.fan", "RTX 5080 - GPU Fan 1")], _sources);

        AdvancedSessionData back = AdvancedJson.ParseContext(AdvancedJson.OfContext(data))!;

        back.MachineFacts.Should().ContainSingle().Which.Value.Number.Should().Be(84);
        back.SessionFacts.Should().ContainSingle().Which.Instance.Should().Be("1");
        back.Series.Should().Equal(data.Series);
        back.Sources.Should().Be(_sources);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("not json")]
    [InlineData("{\"facts\":")]
    [InlineData("42")]
    public void WhatIsNotThereOrDoesNotParseReadsAsNothingAndNeverThrows(string? json)
    {
        AdvancedJson.ParseFacts(json).Should().BeEmpty();
        (IReadOnlyList<AdvancedFact> sessionFacts, AdvancedSources sources) = AdvancedJson.ParseSession(json);
        sessionFacts.Should().BeEmpty();
        sources.Should().Be(AdvancedSources.Unknown, "nobody said: never read as a source that answers");
        AdvancedJson.ParseSeries(json).Series.Should().BeEmpty();
        AdvancedJson.ParseStats(json).Should().BeEmpty();
        AdvancedJson.ParseContext(json).Should().BeNull();
    }

    [Theory]
    [InlineData("[{\"id\":\"nvapi.gpu.rt_cores\",\"number\":84,\"text\":\"84\"}]", "a value in two shapes")]
    [InlineData("[{\"id\":\"nvapi.gpu.rt_cores\"}]", "a value in no shape")]
    [InlineData("[{\"id\":\"\",\"number\":1}]", "no id")]
    [InlineData("[{\"number\":1}]", "no id at all")]
    [InlineData("[{\"id\":\"nvapi.gpu.type\",\"tokens\":[\"Discrete\"]}]", "a word that is not a token")]
    [InlineData("[{\"id\":\"nvapi.gpu.vbios_version\",\"text\":\"  \"}]", "empty words")]
    [InlineData("[{\"id\":\"nvapi.driver.global_profile\",\"rows\":[{\"name\":null,\"value\":\"1\"}]}]", "a row with no name")]
    [InlineData("[null]", "a null entry")]
    public void AStoredFactThatDoesNotFitIsLeftOutAndTheOnesBesideItAreKept(string broken, string why)
    {
        string json = "[{\"id\":\"nvapi.gpu.core_count\",\"number\":10752}," + broken[1..];

        IReadOnlyList<AdvancedFact> facts = AdvancedJson.ParseFacts(json);

        facts.Should().ContainSingle(why).Which.Id.Should().Be("nvapi.gpu.core_count");
    }

    [Fact]
    public void ADirectoryLineThatIsNotAnAdvancedSeriesIsLeftOutAndANegativeCountReadsAsNone()
    {
        string json = "{\"series\":[{\"series\":\"gpu_temp\",\"metricId\":\"x\",\"label\":\"y\"},{\"series\":\"adv.nvapi.pstate\",\"metricId\":\"nvapi.pstate\",\"label\":\"P-state\"},null],\"dropped\":-4}";

        (IReadOnlyList<AdvancedSeriesInfo> series, int dropped) = AdvancedJson.ParseSeries(json);

        series.Should().ContainSingle().Which.Series.Should().Be("adv.nvapi.pstate");
        dropped.Should().Be(0);
    }

    [Fact]
    public void OnlyWhatTheCatalogSaysIsReadMayBeStored()
    {
        // D104, held once more at the door of the ledger. The readers cannot produce the first three; if one ever
        // could, it would stop here.
        AdvancedFact[] mixed =
        [
            new("nvapi.gpu.rt_cores", Instance: null, AdvancedValue.Of(84)),
            new("nvapi.gpu.board_serial", Instance: null, AdvancedValue.OfText("0123456789")),
            new("nvapi.display.edid", "1", AdvancedValue.OfText("00ffffffffffff00")),
            new("sl.constants.camera", Instance: null, AdvancedValue.OfText("1.0 2.0 3.0")),
            new("sl.dlss.presets", Instance: null, AdvancedValue.OfText("K")),
            new("no.such.metric", Instance: null, AdvancedValue.Of(1)),
            new("lhm.board.model", Instance: null, AdvancedValue.OfText("ASUS ROG STRIX Z790-E")),
        ];

        AdvancedJson.Storable(mixed).Select(static f => f.Id).Should().Equal("nvapi.gpu.rt_cores", "lhm.board.model");
        FluentActions.Invoking(static () => AdvancedJson.Storable(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TheWritersRefuseNull()
    {
        FluentActions.Invoking(static () => AdvancedJson.OfFacts(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(static () => AdvancedJson.OfSession([], null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(static () => AdvancedJson.OfSeries(null!, 0)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(static () => AdvancedJson.OfStats(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(static () => AdvancedJson.OfContext(null!)).Should().Throw<ArgumentNullException>();
    }
}
