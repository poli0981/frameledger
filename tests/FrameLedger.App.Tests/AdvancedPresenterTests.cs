// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Capture;
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

        // Train B: what a session says of the entries read inside the game, and the words its facts carry.
        Need("AdvReach_" + AdvancedReach.Game);
        Need(nameof(Strings.Advanced_Status_InGame));
        Need(nameof(Strings.Advanced_InGameLine));
        foreach (AdvancedInGameStatus status in Enum.GetValues<AdvancedInGameStatus>())
        {
            Need("AdvInGame_" + status);
        }

        foreach (string token in InGameAdvancedSampler.Tokens)
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

        // Train B: read inside a game being measured, when the game makes the call - which is not a state of this PC,
        // so the row says the same with the page live and with nobody asking.
        foreach (string id in new[] { "sl.dlss.presets", "ffx.upscale.depth", "ffx.memory_usage" })
        {
            At(unknown, id).Should().Be(new AdvancedSourceRow(
                Label(id), AdvancedLabels.Scope(AdvancedMetricCatalog.Find(id)!.Scope), AdvancedLabels.Source(AdvancedMetricCatalog.Find(id)!.Source), Strings.Advanced_Status_InGame, IsRead: true));
            At(live, id).Status.Should().Be(Strings.Advanced_Status_InGame);
        }

        At(live, "sl.init.preferences").Status.Should().Be(AdvancedLabels.Reach(AdvancedReach.BeforeAttach), "a call made before FrameLedger is in the game is never read, and the page says why (D129)");
        AdvancedLabels.Reach(AdvancedReach.BeforeAttach).Should().NotBe(nameof(AdvancedReach.BeforeAttach), "the reason has words of its own in every language");
        live.Where(static r => r.IsRead).Should().HaveCount(AdvancedMetricCatalog.All.Count(static m => m.IsRead));
    }

    [Fact]
    public void WhatTheGameToldItsUpscalerIsShownInTheVendorsWordsWithTheSwitchTranslated()
    {
        AdvancedFact[] said =
        [
            new("sl.dlss.presets", Instance: null, AdvancedValue.OfRows([new AdvancedRow("DLAA", "K"), new AdvancedRow("Quality", "Default")])),
            new("sl.dlss.hdr_input", Instance: null, AdvancedValue.Of(false)),
            new("ffx.upscale.sharpening", Instance: null, AdvancedValue.OfRows([new AdvancedRow("enableSharpening", "on", "on"), new AdvancedRow("sharpness", "0.35")])),
            new("ffx.upscale.flags", Instance: null, AdvancedValue.OfTokens("non_linear_color_pq", "bit_5")),
        ];

        AdvancedFactGroup group = AdvancedPresenter.Groups(said).Should().ContainSingle("all four stand under Upscaling").Subject;

        group.Title.Should().Be(AdvancedLabels.Group(AdvancedGroup.Upscaling));
        group.Rows.Select(static r => r.Label).Should().Equal(Label("sl.dlss.presets"), Label("sl.dlss.hdr_input"), Label("ffx.upscale.sharpening"), Label("ffx.upscale.flags"));
        group.Rows[0].Details.Should().Equal(new AdvancedDetailRow("DLAA", "K"), new AdvancedDetailRow("Quality", "Default"));
        group.Rows[1].Value.Should().Be(Strings.Common_No);
        group.Rows[2].Details.Should().Equal(new AdvancedDetailRow("enableSharpening", Strings.AdvToken_on), new AdvancedDetailRow("sharpness", "0.35"));
        group.Rows[3].Value.Should().Be(Strings.AdvToken_non_linear_color_pq + ", bit_5", "a flag nobody named is shown as it is");
    }

    [Fact]
    public void ASessionsSourcesLineSaysWhatBecameOfTheInGameEntriesAndAnOlderRowsDoesNot()
    {
        string before = AdvancedPresenter.SourcesLine(AdvancedFixtures.Sources);
        before.Should().NotContain(AdvancedLabels.InGame(AdvancedInGameStatus.NothingSeen)).And.NotContain(AdvancedLabels.InGame(AdvancedInGameStatus.Reading));

        foreach (AdvancedInGameStatus status in new[] { AdvancedInGameStatus.Reading, AdvancedInGameStatus.NothingSeen, AdvancedInGameStatus.NotHooked })
        {
            string line = AdvancedPresenter.SourcesLine(AdvancedFixtures.Sources with { InGame = status });

            line.Should().StartWith(before, "the three sources are said as before");
            line.Should().EndWith(string.Format(CultureInfo.CurrentCulture, Strings.Advanced_InGameLine, AdvancedLabels.InGame(status)));
        }
    }

    [Fact]
    public void ASessionsSourcesLineSaysForEachCallHookedOnlyForAdvancedMetricsWhichOfThreeThingsHappened()
    {
        // 0.1.0-beta.43: read, hooked and never made by the game, or not hooked. A row stored before has neither
        // member, and a session not measured inside the game has none to speak of: the sentence is not there.
        AdvancedSources hooked = AdvancedFixtures.Sources with { InGame = AdvancedInGameStatus.Reading };
        string before = AdvancedPresenter.SourcesLine(hooked);
        before.Should().NotContain(AdvancedLabels.InGameCall(AdvancedInGameCall.NeverCalled)).And.NotContain(AdvancedLabels.InGameCall(AdvancedInGameCall.NotHooked));

        string line = AdvancedPresenter.SourcesLine(hooked with
        {
            StreamlineConstants = AdvancedInGameCall.NeverCalled,
            FsrFrameGeneration = AdvancedInGameCall.Read,
        });

        line.Should().StartWith(before, "what was said before is said as before");
        line.Should().EndWith(string.Format(
            CultureInfo.CurrentCulture,
            Strings.Advanced_InGameCallsLine,
            AdvancedLabels.InGameCall(AdvancedInGameCall.NeverCalled),
            AdvancedLabels.InGameCall(AdvancedInGameCall.Read)));

        foreach (AdvancedInGameCall call in Enum.GetValues<AdvancedInGameCall>())
        {
            AdvancedLabels.InGameCall(call).Should().NotBe(call.ToString(), "every state has words of its own in every language");
        }

        AdvancedLabels.InGameCall(AdvancedInGameCall.NeverCalled).Should().NotBe(AdvancedLabels.InGameCall(AdvancedInGameCall.NotHooked), "the two reasons a value is absent are not one");
    }

    [Fact]
    public void ASessionsSourcesLineNamesTheSixCallsOfBeta45UnderWhatBecameOfEach()
    {
        // 0.1.0-beta.45: six calls would be six sentences; they are one, each call by its own name under which of the
        // three things happened to it, and "none" where a group is empty. A row stored before has none of the six.
        AdvancedSources beta44 = AdvancedFixtures.Sources with
        {
            InGame = AdvancedInGameStatus.Reading,
            StreamlineConstants = AdvancedInGameCall.Read,
            FsrFrameGeneration = AdvancedInGameCall.NotHooked,
            StreamlineFeatureVersion = AdvancedInGameCall.Read,
            StreamlineFeatureRequirements = AdvancedInGameCall.Read,
        };
        string before = AdvancedPresenter.SourcesLine(beta44);
        before.Should().NotContain("slIsFeatureSupported");

        AdvancedSources all = beta44 with
        {
            StreamlineFeatureSupported = AdvancedInGameCall.Read,
            StreamlineFeatureLoaded = AdvancedInGameCall.Read,
            StreamlineSetFeatureLoaded = AdvancedInGameCall.NeverCalled,
            StreamlineAllocateResources = AdvancedInGameCall.Read,
            StreamlineFreeResources = AdvancedInGameCall.NeverCalled,
            StreamlineShutdown = AdvancedInGameCall.NeverCalled,
        };
        string line = AdvancedPresenter.SourcesLine(all);
        line.Should().StartWith(before, "what was said before is said as before");
        line.Should().EndWith(string.Format(
            CultureInfo.CurrentCulture,
            Strings.Advanced_InGameMoreCallsLine,
            "slIsFeatureSupported, slIsFeatureLoaded, slAllocateResources",
            "slSetFeatureLoaded, slFreeResources, slShutdown",
            AdvancedLabels.Token("none")));

        // One of the six missing - a row no build writes - and the sentence is not there at all.
        AdvancedPresenter.SourcesLine(all with { StreamlineShutdown = null }).Should().Be(before);
    }

    [Fact]
    public void ASessionsSourcesLineSaysOfTheTwoQuestionsAboutAFeatureThatAGameAsksThemWhileItStarts()
    {
        // 0.1.0-beta.44. A game asks which version a feature is and what it requires while it starts; a session that
        // attached later finds the hook in and the call not made. That is said in words of its own - "did not ask
        // again" - and not as the finding "the game did not make the call" is for a call made every frame.
        AdvancedSources beta43 = AdvancedFixtures.Sources with
        {
            InGame = AdvancedInGameStatus.Reading,
            StreamlineConstants = AdvancedInGameCall.Read,
            FsrFrameGeneration = AdvancedInGameCall.NotHooked,
        };
        string before = AdvancedPresenter.SourcesLine(beta43);
        before.Should().NotContain(Strings.AdvInGameCall_NotAskedAgain, "a row stored before says nothing of the two");

        string line = AdvancedPresenter.SourcesLine(beta43 with
        {
            StreamlineFeatureVersion = AdvancedInGameCall.Read,
            StreamlineFeatureRequirements = AdvancedInGameCall.NeverCalled,
        });

        line.Should().StartWith(before, "what was said before is said as before");
        line.Should().EndWith(string.Format(
            CultureInfo.CurrentCulture,
            Strings.Advanced_InGameFeatureCallsLine,
            AdvancedLabels.InGameCall(AdvancedInGameCall.Read),
            Strings.AdvInGameCall_NotAskedAgain));

        AdvancedLabels.InGameStartupCall(AdvancedInGameCall.NeverCalled).Should().NotBe(AdvancedLabels.InGameCall(AdvancedInGameCall.NeverCalled));
        AdvancedLabels.InGameStartupCall(AdvancedInGameCall.NotHooked).Should().Be(AdvancedLabels.InGameCall(AdvancedInGameCall.NotHooked), "not hooked is not hooked, whenever the call is made");
        AdvancedLabels.InGameStartupCall(AdvancedInGameCall.Read).Should().Be(AdvancedLabels.InGameCall(AdvancedInGameCall.Read));
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

    // ---- The driver's latency report (train C): a call FrameLedger makes itself, so words of its own -------------

    [Fact]
    public void TheDriversLatencyReportHasASentenceOfItsOwnAndARowStoredBeforeHasNone()
    {
        AdvancedSources before = AdvancedSources.Unknown with { InGame = AdvancedInGameStatus.Reading };
        string without = AdvancedPresenter.SourcesLine(before);

        string read = AdvancedPresenter.SourcesLine(before with { DriverLatency = AdvancedDriverRead.Read, DriverLatencySlowestUs = 3 });
        read.Should().StartWith(without, "what was said before is said as before");
        read.Should().EndWith(string.Format(CultureInfo.CurrentCulture, Strings.Advanced_InGameDriverLine, Strings.AdvDriverRead_Read));

        // Stopped for being slow says how slow: that number is what tells a driver that held a call from a pause.
        string slow = AdvancedPresenter.SourcesLine(before with { DriverLatency = AdvancedDriverRead.Slow, DriverLatencySlowestUs = 2400 });
        slow.Should().Contain(2400.ToString("N0", CultureInfo.CurrentCulture));
        slow.Should().NotContain("{0");

        // A row that says nothing about it - stored before, or a game the capture side publishes nothing for.
        AdvancedPresenter.SourcesLine(before with { DriverLatency = AdvancedDriverRead.Unknown }).Should().Be(without);
        AdvancedPresenter.SourcesLine(before with { DriverLatency = null }).Should().Be(without);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("vi")]
    [InlineData("ja")]
    public void EveryOutcomeOfTheDriversReadHasItsWordsInEveryLanguage(string culture)
    {
        CultureInfo c = CultureInfo.GetCultureInfo(culture);
        foreach (AdvancedDriverRead outcome in Enum.GetValues<AdvancedDriverRead>())
        {
            string? words = Strings.ResourceManager.GetString("AdvDriverRead_" + outcome, c);
            words.Should().NotBeNullOrEmpty(outcome.ToString());
            (words ?? string.Empty).Contains("{0", StringComparison.Ordinal).Should().Be(outcome == AdvancedDriverRead.Slow, "only the slow stop carries its microseconds");
        }

        Strings.ResourceManager.GetString(nameof(Strings.Advanced_InGameDriverLine), c).Should().Contain("{0}");
        Strings.ResourceManager.GetString(nameof(Strings.Advanced_Status_InGameDriver), c).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TheDriversTwoOwnTimesAreLinesOfAnEntryOfTheirOwnSoTheSevenSpansStayLines()
    {
        (AdvancedMetric? metric, string label) = AdvancedPresenter.Resolve("adv.nvapi.reflex.gpu_times.active_render", []);

        metric!.Id.Should().Be("nvapi.reflex.gpu_times");
        label.Should().Be(AdvancedLabels.Member(AdvancedLabels.Of(metric), Strings.AdvToken_active_render));
        label.Should().NotContain("active_render", "the member is one of ours and is said in words");

        // Nine series, two entries: neither chart is past what it draws as lines.
        string[] series =
        [
            .. InGameAdvancedSampler.LatencySpans.Select(static span => InGameAdvancedSampler.LatencySeriesStem + span),
            .. InGameAdvancedSampler.GpuTimes.Select(static time => InGameAdvancedSampler.GpuTimesSeriesStem + time),
        ];
        AdvancedPresenter.Metrics(series, []).Select(static m => m.Id).Should().BeEquivalentTo("nvapi.reflex.frame_report", "nvapi.reflex.gpu_times");

        // The span between the driver's two GPU marks says what it is, in every language (D143) - and neither label says
        // which of the two holds the other (D152): in Cyberpunk 2077 the driver's active time is the longer.
        foreach (string culture in new[] { "", "vi", "ja" })
        {
            string render = Strings.ResourceManager.GetString(nameof(Strings.AdvToken_gpu_render), CultureInfo.GetCultureInfo(culture))!;
            string active = Strings.ResourceManager.GetString(nameof(Strings.AdvToken_active_render), CultureInfo.GetCultureInfo(culture))!;
            render.Should().Contain("(", "the label carries what the span is");
            render.Should().NotBe(active);
            render.Should().NotContain("idle", "the span is not said to include the active time's idle time");
            active.Should().NotContain("idle", "the active time is not said to be the span less its idle time");
        }
    }

    [Fact]
    public void TheSevenSpansOfTheDriversReportAreSeriesOfOneEntryLabelledInWords()
    {
        (AdvancedMetric? metric, string label) = AdvancedPresenter.Resolve("adv.nvapi.reflex.frame_report.os_queue", []);

        metric!.Id.Should().Be("nvapi.reflex.frame_report");
        label.Should().Be(AdvancedLabels.Member(AdvancedLabels.Of(metric), Strings.AdvToken_os_queue));
        label.Should().NotContain("os_queue", "the member is one of ours and is said in words");

        // Seven lines of one entry: within what a chart can show, so no band.
        string[] series = [.. InGameAdvancedSampler.LatencySpans.Select(static span => InGameAdvancedSampler.LatencySeriesStem + span)];
        AdvancedPresenter.Metrics(series, []).Should().ContainSingle().Which.Id.Should().Be("nvapi.reflex.frame_report");
        series.Length.Should().BeLessThanOrEqualTo(AdvancedPresenter.MaxLines);

        // On the Sources tab the two entries say where they are read, and it is not "when the game makes the call".
        AdvancedPresenter.Sources(now: null)
            .Where(static row => string.Equals(row.Label, AdvancedLabels.Of(AdvancedMetricCatalog.Find("nvapi.reflex.frame_report")!), StringComparison.Ordinal))
            .Should().ContainSingle().Which.Status.Should().Be(Strings.Advanced_Status_InGameDriver);
    }
}
