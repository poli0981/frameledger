// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Recording;

namespace FrameLedger.App.Tests;

/// <summary>
/// What the Advanced page shows, from what was read or stored (owner decision D101): every catalog entry has its
/// words in every language, a value keeps its unit and its age, and an entry that is not read says why.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture")]
public sealed class AdvancedPresenterTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("vi")]
    [InlineData("ja")]
    public void EveryCatalogEntryGroupReasonStateAndTokenHasItsWordsInEveryLanguage(string culture)
    {
        CultureInfo c = CultureInfo.GetCultureInfo(culture);
        var missing = new List<string>();
        void Need(string key)
        {
            if (string.IsNullOrEmpty(Strings.ResourceManager.GetString(key, c)))
            {
                missing.Add(key);
            }
        }

        foreach (AdvancedMetric metric in AdvancedMetricCatalog.All)
        {
            Need(metric.LabelKey);
            Need("AdvGroup_" + metric.Group);
            Need("AdvScope_" + metric.Scope);
            if (!metric.IsRead)
            {
                Need("AdvReach_" + metric.Reach);
            }
        }

        foreach (AdvancedSourceStatus status in Enum.GetValues<AdvancedSourceStatus>())
        {
            Need("AdvSource_" + status);
        }

        foreach (string token in Infrastructure.Telemetry.NvapiAdvancedTokens.All)
        {
            Need("AdvToken_" + token);
        }

        missing.Should().BeEmpty("the page shows a label, never a key - and a token the driver's answer maps to has a word in " + culture);
    }

    [Fact]
    public void TheEnglishLabelOfAnEntryIsItsSummaryInTheCatalog()
    {
        // One text, in one place: the catalog's summary is what docs/18's table prints and what the page shows in English.
        CultureInfo en = CultureInfo.GetCultureInfo("en");
        foreach (AdvancedMetric metric in AdvancedMetricCatalog.All)
        {
            Strings.ResourceManager.GetString(metric.LabelKey, en).Should().Be(metric.Summary, metric.Id);
        }
    }

    [Fact]
    public void FactsStandUnderTheirGroupsInTheCatalogsOrderWithTheirDisplayAndTheirRows()
    {
        IReadOnlyList<AdvancedFactGroup> groups = AdvancedPresenter.Groups(AdvancedFixtures.MachineFacts);

        groups.Select(static g => g.Title).Should().Equal(AdvancedLabels.Group(AdvancedGroup.Gpu), AdvancedLabels.Group(AdvancedGroup.Driver), AdvancedLabels.Group(AdvancedGroup.Display));
        AdvancedFactGroup card = groups[0];
        card.Rows.Select(static r => r.Label).Should().Equal(
            Label("nvapi.gpu.type"), Label("nvapi.gpu.vbios_version"), Label("nvapi.gpu.rt_cores"));
        card.Rows[0].Value.Should().Be(AdvancedLabels.Token("discrete"), "a token is shown in this language");
        card.Rows[1].Value.Should().Be("98.01.2F.00.6B");
        card.Rows[2].Value.Should().Be(84.ToString("0.###", CultureInfo.CurrentCulture));

        AdvancedFactRow profile = groups[1].Rows.Should().ContainSingle().Subject;
        profile.HasDetails.Should().BeTrue();
        profile.Value.Should().Be("2", "a table reads as how many rows it has, and unfolds");
        profile.Details.Should().Equal(
            new AdvancedDetailRow("Vertical sync", AdvancedLabels.Token("driver_default")),
            new AdvancedDetailRow("Power management mode", "1"));

        AdvancedFactRow luminance = groups[2].Rows.Should().ContainSingle().Subject;
        luminance.Label.Should().Be(Label("nvapi.display.luminance_max") + " — DISPLAY1", "a fact about one display says which");
        luminance.Value.Should().Be(AdvancedLabels.Number(417, AdvancedUnit.Nits));
    }

    [Fact]
    public void AFactThisBuildDoesNotKnowIsShownUnderItsOwnIdNotDropped()
    {
        IReadOnlyList<AdvancedFactGroup> groups = AdvancedPresenter.Groups([new AdvancedFact("nvapi.from.a.newer.build", Instance: null, AdvancedValue.OfText("x"))]);

        groups.Should().ContainSingle().Which.Rows.Should().ContainSingle().Which.Label.Should().Be("nvapi.from.a.newer.build");
    }

    [Fact]
    public void ASeriesIsNamedByTheDirectoryThenByTheCatalogThenByItsFamilyAndElseKeepsItsName()
    {
        AdvancedPresenter.Resolve(AdvancedFixtures.Fan1, AdvancedFixtures.Directory).Should().Be(
            (AdvancedMetricCatalog.Find("lhm.gpu.fan"), Label("lhm.gpu.fan") + " — GPU Fan 1"));
        AdvancedPresenter.Resolve(AdvancedFixtures.Vram, []).Should().Be((AdvancedMetricCatalog.Find("nvapi.vram.in_use"), Label("nvapi.vram.in_use")));
        AdvancedPresenter.Resolve(AdvancedFixtures.Fan2, []).Should().Be(
            (AdvancedMetricCatalog.Find("lhm.gpu.fan"), Label("lhm.gpu.fan") + " — gpu_nvidia_0_2"), "a member whose directory entry is gone still belongs to its family");
        AdvancedPresenter.Resolve("adv.nobody.knows", []).Should().Be((null, "adv.nobody.knows"));
        AdvancedPresenter.Resolve("gpu_temp", []).Should().Be((null, "gpu_temp"));
    }

    [Fact]
    public void ALiveRowCarriesItsUnitAndTheTimeItWasReadAt()
    {
        IReadOnlyList<AdvancedLiveRow> rows = AdvancedPresenter.Live(AdvancedFixtures.View());

        rows.Select(static r => r.Label).Should().Equal(
            Label("nvapi.vram.in_use"), Label("lhm.gpu.fan") + " — GPU Fan 1", Label("lhm.gpu.fan") + " — GPU Fan 2");
        rows[0].Value.Should().Be(AdvancedLabels.Number(5200, AdvancedUnit.Mebibytes));
        rows[1].Value.Should().Be(AdvancedLabels.Number(1400, AdvancedUnit.Rpm));
        rows[0].ReadAt.Should().Be(AdvancedFixtures.T0.AddSeconds(1).ToLocalTime().ToString("T", CultureInfo.CurrentCulture));
        rows[1].ReadAt.Should().Be(AdvancedFixtures.T0.ToLocalTime().ToString("T", CultureInfo.CurrentCulture),
            "a fan read every fifth second is as old as its time says, and the row says it");
    }

    [Fact]
    public void TheSelectorOffersTheEntriesThatHaveASeriesOnceEachInTheCatalogsOrder()
    {
        IReadOnlyList<AdvancedMetricOption> metrics = AdvancedPresenter.Metrics(
            [AdvancedFixtures.Fan2, AdvancedFixtures.Rail, AdvancedFixtures.Vram, AdvancedFixtures.Fan1, "adv.nobody.knows"], AdvancedFixtures.Directory);

        metrics.Select(static m => m.Id).Should().Equal("nvapi.vram.in_use", "lhm.gpu.fan", "lhm.board.voltage");
        metrics[1].Label.Should().Be(Label("lhm.gpu.fan"));
    }

    [Fact]
    public void AnEntrysLinesAreItsSeriesAndAboveEightTheirLowestAverageAndHighest()
    {
        IReadOnlyList<SensorSeries> series = SessionSeriesLoader.DecodeAdvanced(AdvancedFixtures.Blobs());

        (IReadOnlyList<AdvancedChartLine> fans, bool band) = AdvancedPresenter.Lines("lhm.gpu.fan", series, AdvancedFixtures.Directory);

        band.Should().BeFalse();
        fans.Select(static l => l.Label).Should().Equal(Label("lhm.gpu.fan") + " — GPU Fan 1", Label("lhm.gpu.fan") + " — GPU Fan 2");
        fans[0].Xs.Should().Equal(0, 5);
        fans[0].Ys.Should().Equal(1400, 1500);

        // Twelve cores: twelve lines cannot be told apart, so the chart draws what can be read off it.
        var cores = new List<SensorSeries>();
        var directory = new List<AdvancedSeriesInfo>();
        for (int i = 0; i < 12; i++)
        {
            string name = "adv.lhm.cpu.load.cpu_0_" + i.ToString(CultureInfo.InvariantCulture);
            cores.Add(new SensorSeries(name, [0, 5], [i, i * 2]));
            directory.Add(new AdvancedSeriesInfo(name, "lhm.cpu.load", "Core " + i.ToString(CultureInfo.InvariantCulture)));
        }

        (IReadOnlyList<AdvancedChartLine> lines, bool isBand) = AdvancedPresenter.Lines("lhm.cpu.load", cores, directory);

        isBand.Should().BeTrue();
        lines.Select(static l => l.Label).Should().Equal(Strings.Advanced_Min, Strings.Advanced_Mean, Strings.Advanced_Max);
        lines[0].Ys.Should().Equal(0, 0);
        lines[1].Ys.Should().Equal(5.5, 11);
        lines[2].Ys.Should().Equal(11, 22);
    }

    [Fact]
    public void AnAdvancedSeriesKeepsItsNegativeReadingsAndLeavesOutTheTicksWithNone()
    {
        IReadOnlyList<SensorSeries> series = SessionSeriesLoader.DecodeAdvanced(AdvancedFixtures.Blobs());

        series.Select(static s => s.Name).Should().Equal(AdvancedFixtures.Vram, AdvancedFixtures.Fan1, AdvancedFixtures.Fan2, AdvancedFixtures.Rail);
        SensorSeries rail = series[3];
        rail.TimesS.Should().Equal(0, 5);
        rail.Values[0].Should().BeApproximately(-11.97, 1e-4, "a -12 V rail is a reading; the fixed sensors' rule would have dropped it");
        series[0].TimesS.Should().Equal(0, 1, 2, 3, 4, 5);
        SessionSeriesLoader.DecodeAdvanced([]).Should().BeEmpty();
    }

    [Fact]
    public void AStoredSeriesStatisticsKeepTheirUnit()
    {
        var stats = new Dictionary<string, SensorSeriesStats>(StringComparer.Ordinal)
        {
            [AdvancedFixtures.Rail] = new(2, -12, -12, -12.03, -11.97),
            [AdvancedFixtures.Vram] = new(6, 5250, 5250, 5000, 5500),
        };

        IReadOnlyList<AdvancedStatRow> rows = AdvancedPresenter.Stats(stats, AdvancedFixtures.Directory);

        rows.Select(static r => r.Label).Should().Equal(Label("nvapi.vram.in_use"), Label("lhm.board.voltage") + " — -12V");
        rows[0].Should().Be(new AdvancedStatRow(rows[0].Label, 6.ToString("N0", CultureInfo.CurrentCulture),
            AdvancedLabels.Number(5000, AdvancedUnit.Mebibytes), AdvancedLabels.Number(5250, AdvancedUnit.Mebibytes), AdvancedLabels.Number(5500, AdvancedUnit.Mebibytes)));
        rows[1].Min.Should().Be(AdvancedLabels.Number(-12.03, AdvancedUnit.Volts));
    }

    [Fact]
    public void EveryCatalogEntryIsOnTheSourcesListReadOrWithItsReason()
    {
        IReadOnlyList<AdvancedSourceRow> unknown = AdvancedPresenter.Sources(now: null);
        IReadOnlyList<AdvancedSourceRow> live = AdvancedPresenter.Sources(AdvancedFixtures.Sources);

        unknown.Should().HaveCount(AdvancedMetricCatalog.All.Count);
        AdvancedSourceRow At(IReadOnlyList<AdvancedSourceRow> rows, string id) => rows[AdvancedMetricCatalog.All.ToList().FindIndex(m => string.Equals(m.Id, id, StringComparison.Ordinal))];

        At(unknown, "nvapi.gpu.rt_cores").Status.Should().Be(Strings.Advanced_Status_Readable, "with nobody asking, the page does not claim that it is being read");
        At(live, "nvapi.gpu.rt_cores").Status.Should().Be(Strings.Advanced_Status_Read);
        At(live, "lhm.gpu.fan").Status.Should().Be(Strings.Advanced_Status_Read);
        At(live, "lhm.cpu.temperature").Status.Should().Be(
            string.Format(CultureInfo.CurrentCulture, Strings.Advanced_Status_SourceSilent, AdvancedLabels.Status(AdvancedSourceStatus.NotElevated)),
            "the processor's sensors need the agent as administrator, and the row says so in words");
        At(live, "nvapi.gpu.board_serial").Should().Be(new AdvancedSourceRow(
            Label("nvapi.gpu.board_serial"), AdvancedLabels.Scope(AdvancedScope.MachineFact), "NVAPI", AdvancedLabels.Reach(AdvancedReach.Identifier), IsRead: false));
        At(live, "sl.constants.camera").Status.Should().Be(AdvancedLabels.Reach(AdvancedReach.PlayerState));
        live.Where(static r => r.IsRead).Should().HaveCount(AdvancedMetricCatalog.All.Count(static m => m.IsRead));
    }

    [Fact]
    public void AValueIsShownInItsUnitAndAFlagAsYesOrNo()
    {
        AdvancedLabels.Number(1, AdvancedUnit.Flag).Should().Be(Strings.Common_Yes);
        AdvancedLabels.Number(0, AdvancedUnit.Flag).Should().Be(Strings.Common_No);
        AdvancedLabels.Number(-11.97, AdvancedUnit.Volts).Should().Be((-11.97).ToString("0.###", CultureInfo.CurrentCulture) + " V");
        AdvancedLabels.Number(3 * 1024 * 1024, AdvancedUnit.BytesPerSecond).Should().Be(3.ToString("0.#", CultureInfo.CurrentCulture) + " MiB/s");
        AdvancedLabels.Number(512, AdvancedUnit.BytesPerSecond).Should().Be("512 B/s");
        AdvancedLabels.Token("a_token_from_a_newer_build").Should().Be("a_token_from_a_newer_build", "what was stored is shown, not nothing");
        AdvancedLabels.Value(AdvancedValue.OfTokens("hdr10", "dolby_vision"), AdvancedUnit.Text).Should().Be(AdvancedLabels.Token("hdr10") + ", " + AdvancedLabels.Token("dolby_vision"));
        AdvancedPresenter.SourcesLine(AdvancedFixtures.Sources).Should().Contain(AdvancedLabels.Status(AdvancedSourceStatus.NotElevated));
    }

    private static string Label(string id) => AdvancedLabels.Of(AdvancedMetricCatalog.Find(id)!);
}
