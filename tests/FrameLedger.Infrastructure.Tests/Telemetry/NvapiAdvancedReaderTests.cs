// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Reflection;
using FluentAssertions;
using FrameLedger.Application.Advanced;
using FrameLedger.Infrastructure.Telemetry;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>
/// <see cref="NvapiAdvancedReader"/> on a scripted bridge: what it emits is the catalog's <c>nvapi.</c> entries of the
/// Agent's reach — all of them and nothing else — and what the driver did not answer is absent, never a zero.
/// </summary>
public sealed class NvapiAdvancedReaderTests
{
    private static (NvapiAdvancedReader Reader, FakeNvapiBridge Bridge, FakeNvapiAdvancedBridge Advanced) Started(int init = 0)
    {
        var bridge = new FakeNvapiBridge { InitResult = init };
        var advanced = new FakeNvapiAdvancedBridge();
        var reader = new NvapiAdvancedReader(bridge, advanced);
        reader.Start();
        return (reader, bridge, advanced);
    }

    private static NvapiProfileList ListOf(int status, uint count) => new() { Status = status, Count = count, Total = count, ProfileName = "Base Profile" };

    private static FakeNvapiAdvancedBridge Everything(FakeNvapiAdvancedBridge advanced)
    {
        advanced.Facts = FakeNvapiAdvancedBridge.FullFacts();
        advanced.Live = FakeNvapiAdvancedBridge.FullLive();
        advanced.Displays = FakeNvapiAdvancedBridge.TwoDisplays();
        advanced.Session = new NvapiSessionDisplay { NvapiStatus = 0, Present = NvapiSessionDisplay.HasVrr | NvapiSessionDisplay.HasColorSpace, VrrFlags = NvapiDisplay.VrrEnabled | NvapiDisplay.VrrPossible, ColorSpace = 12 };
        NvapiProfileEntry[] entries = [new() { Id = 0x00A879CF, Type = 0, Value = 0x60925292, Predefined = 0, Name = "Vertical Sync" }];
        advanced.GlobalProfile = (ListOf(NvapiDriverProfile.Global, 1), entries);
        advanced.GameProfile = (ListOf(NvapiDriverProfile.Application, 1), entries);
        return advanced;
    }

    [Fact]
    public void WhatItEmitsIsExactlyTheCatalogsNvapiEntriesOfTheAgentsReach()
    {
        // Both directions. An id the reader invents is a value no page shows and no document names; a catalog entry it
        // never emits is a row that reads N/A on every PC while the document says it is read.
        (NvapiAdvancedReader reader, _, FakeNvapiAdvancedBridge advanced) = Started();
        using NvapiAdvancedReader disposing = reader;
        Everything(advanced);

        string[] facts = [.. reader.ReadMachineFacts().Concat(reader.ReadSessionFacts(@"\\.\DISPLAY1", 4242, @"C:\Games\game.exe")).Select(static f => f.Id).Distinct(StringComparer.Ordinal)];
        string[] series = [.. reader.ReadLive().Select(static r => r.Series)];

        string[] expectedFacts = [.. AdvancedMetricCatalog.All.Where(static m => m.Source == AdvancedSource.Nvapi && m.Reach == AdvancedReach.Agent && m.Scope != AdvancedScope.Series).Select(static m => m.Id)];
        string[] expectedSeries = [.. AdvancedMetricCatalog.All.Where(static m => m.Source == AdvancedSource.Nvapi && m.Reach == AdvancedReach.Agent && m.Scope == AdvancedScope.Series).Select(static m => m.SeriesName!)];

        facts.Should().BeEquivalentTo(expectedFacts);
        series.Should().BeEquivalentTo(expectedSeries);
        series.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AMachineFactIsFiledUnderTheMachineAndASessionFactUnderTheSession()
    {
        (NvapiAdvancedReader reader, _, FakeNvapiAdvancedBridge advanced) = Started();
        using NvapiAdvancedReader disposing = reader;
        Everything(advanced);

        reader.ReadMachineFacts().Should().OnlyContain(static f => AdvancedMetricCatalog.Find(f.Id)!.Scope == AdvancedScope.MachineFact);
        reader.ReadSessionFacts(@"\\.\DISPLAY1", 4242, @"C:\Games\game.exe").Should().OnlyContain(static f => AdvancedMetricCatalog.Find(f.Id)!.Scope == AdvancedScope.SessionFact);
    }

    [Fact]
    public void TheCardsFactsAreTheDriversValuesInTheCatalogsUnits()
    {
        var facts = new List<AdvancedFact>();

        NvapiAdvancedReader.AddCardFacts(facts, FakeNvapiAdvancedBridge.FullFacts());

        AdvancedValue Of(string id) => facts.Single(f => string.Equals(f.Id, id, StringComparison.Ordinal)).Value;
        Of("nvapi.gpu.type").Tokens.Should().Equal("discrete");
        Of("nvapi.gpu.system_type").Tokens.Should().Equal("desktop");
        Of("nvapi.gpu.external").Number.Should().Be(0);
        Of("nvapi.gpu.bus_type").Tokens.Should().Equal("pci_express");
        Of("nvapi.gpu.pci_ids").Text.Should().Be("2C0210DE / 41961458 / A1");
        Of("nvapi.gpu.vbios_version").Text.Should().Be("98.03.6c.00.68");
        Of("nvapi.gpu.architecture").Text.Should().Be("0x1B0 / 0x3 / 0xA1");
        Of("nvapi.gpu.core_count").Number.Should().Be(10752);
        Of("nvapi.gpu.rt_cores").Number.Should().Be(84);
        Of("nvapi.gpu.tensor_cores").Number.Should().Be(336);
        Of("nvapi.gpu.ram_bus_width").Number.Should().Be(256);
        Of("nvapi.gpu.clock_boost_core").Number.Should().Be(2670);
        Of("nvapi.vram.dedicated").Number.Should().Be(16303, "bytes become MiB");
        Of("nvapi.vram.framebuffer_physical").Number.Should().Be(16666368 / 1024.0, "KiB become MiB");
        Of("nvapi.driver.package").Tokens.Should().Equal("game_ready");
        Of("nvapi.driver.dch").Number.Should().Be(1);
        Of("nvapi.driver.base_branch").Text.Should().Be("R620");
        Of("nvapi.driver.chipset").Text.Should().Be("Intel Intel-RaptorLake");
        facts.Should().OnlyContain(static f => f.Instance == null, "a card's facts are of the one card the bridge reads");
    }

    [Fact]
    public void AFieldTheDriverDidNotFillIsAbsentAndNeverAZero()
    {
        var facts = new List<AdvancedFact>();
        NvapiFacts card = FakeNvapiAdvancedBridge.FullFacts();
        card.Present &= ~(uint)(NvapiFactField.GpuInfo | NvapiFactField.Memory | NvapiFactField.Vbios);

        NvapiAdvancedReader.AddCardFacts(facts, card);

        facts.Select(static f => f.Id).Should().NotContain(["nvapi.gpu.external", "nvapi.gpu.rt_cores", "nvapi.gpu.tensor_cores", "nvapi.vram.dedicated", "nvapi.vram.dedicated_available", "nvapi.vram.system", "nvapi.vram.shared", "nvapi.gpu.vbios_version"]);
        facts.Should().Contain(static f => string.Equals(f.Id, "nvapi.gpu.core_count", StringComparison.Ordinal), "the fields the driver did fill are untouched");
    }

    [Fact]
    public void ACodeTheCatalogHasNoWordForIsAbsentRatherThanGuessed()
    {
        var facts = new List<AdvancedFact>();
        NvapiFacts card = FakeNvapiAdvancedBridge.FullFacts();
        card.GpuType = 0;
        card.SystemType = 7;
        card.BusType = 99;
        card.Vbios = " ";

        NvapiAdvancedReader.AddCardFacts(facts, card);

        facts.Select(static f => f.Id).Should().NotContain(["nvapi.gpu.type", "nvapi.gpu.system_type", "nvapi.gpu.bus_type", "nvapi.gpu.vbios_version"]);
    }

    [Fact]
    public void ADriverThatNamesNoPackageIsAnAnswerWithNoWordInIt()
    {
        var facts = new List<AdvancedFact>();
        NvapiFacts card = FakeNvapiAdvancedBridge.FullFacts();
        card.DriverFlags = 0;
        card.BaseBranch = string.Empty;

        NvapiAdvancedReader.AddCardFacts(facts, card);

        facts.Single(static f => string.Equals(f.Id, "nvapi.driver.package", StringComparison.Ordinal)).Value.Tokens.Should().BeEmpty("the driver answered, and named none");
        facts.Single(static f => string.Equals(f.Id, "nvapi.driver.dch", StringComparison.Ordinal)).Value.Number.Should().Be(0);
        facts.Should().NotContain(static f => string.Equals(f.Id, "nvapi.driver.base_branch", StringComparison.Ordinal));
    }

    [Fact]
    public void ADisplayIsNamedByItsPlaceInTheListAndNeverByTheDriversHandle()
    {
        // D104. NVAPI's display id is a handle of this boot's topology; storing it would store a number that means
        // nothing tomorrow and looks like an identity.
        var facts = new List<AdvancedFact>();

        NvapiAdvancedReader.AddDisplayFacts(facts, FakeNvapiAdvancedBridge.TwoDisplays());

        facts.Select(static f => f.Instance).Distinct(StringComparer.Ordinal).Should().Equal("1");
        facts.Should().NotContain(static f => f.Value.Number == 0x80061082 || f.Value.Number == 0x80061083);
        facts.Should().NotContain(static f => f.Value.Text != null && f.Value.Text.Contains("80061082", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ADisplaysFactsAreItsFormatsItsLuminancesAndItsVariableRefresh()
    {
        var facts = new List<AdvancedFact>();

        NvapiAdvancedReader.AddDisplayFacts(facts, FakeNvapiAdvancedBridge.TwoDisplays());

        AdvancedValue Of(string id) => facts.Single(f => string.Equals(f.Id, id, StringComparison.Ordinal)).Value;
        Of("nvapi.display.hdr_formats").Tokens.Should().Equal("hdr10", "hdr10_plus");
        Of("nvapi.display.luminance_max").Number.Should().Be(417);
        Of("nvapi.display.luminance_min").Number.Should().BeApproximately(0.0698, 1e-9, "the driver reports it in ten-thousandths of a nit");
        Of("nvapi.display.luminance_max_frame_average").Number.Should().Be(391);
        Of("nvapi.display.vrr_support").Tokens.Should().Equal("variable_refresh", "adaptive_sync", "usable_now");
        Of("nvapi.display.adaptive_sync_off").Number.Should().Be(0);
        Of("nvapi.display.max_frame_interval").Number.Should().Be(0, "an interval nobody set is the driver's answer, not an absence");
    }

    [Theory]
    [InlineData(0u, "not_supported")]
    [InlineData(NvapiDisplay.CapsVrr | NvapiDisplay.CapsTrueGsync | NvapiDisplay.CapsUlmb, "variable_refresh,gsync_module,ulmb")]
    [InlineData(NvapiDisplay.CapsTrueGsync, "not_supported")]
    public void VariableRefreshSupportIsSaidInWordsAndAModuleWithoutSupportIsNotSupport(uint caps, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var facts = new List<AdvancedFact>();
        var displays = new NvapiDisplay[NvapiDisplays.MaxDisplays];
        displays[0] = new NvapiDisplay { Present = NvapiDisplay.HasCaps, CapsFlags = caps };

        NvapiAdvancedReader.AddDisplayFacts(facts, new NvapiDisplays { Count = 1, Displays = displays });

        facts.Should().ContainSingle().Which.Value.Tokens.Should().Equal(expected.Split(','));
    }

    [Fact]
    public void ACountLargerThanTheListAndAListThatIsNotThereAreBothRead()
    {
        var facts = new List<AdvancedFact>();
        NvapiDisplays tooMany = FakeNvapiAdvancedBridge.TwoDisplays();
        tooMany.Count = 400;

        NvapiAdvancedReader.AddDisplayFacts(facts, tooMany);
        NvapiAdvancedReader.AddDisplayFacts(facts, new NvapiDisplays { Count = 3, Displays = null! });

        facts.Should().NotBeEmpty();
        facts.Select(static f => f.Instance).Distinct(StringComparer.Ordinal).Should().Equal("1");
    }

    [Fact]
    public void TheSessionsDisplayIsItsVariableRefreshStateAndTheGamesColourSpace()
    {
        var facts = new List<AdvancedFact>();

        NvapiAdvancedReader.AddSessionDisplayFacts(facts, new NvapiSessionDisplay { NvapiStatus = 0, Present = NvapiSessionDisplay.HasVrr | NvapiSessionDisplay.HasColorSpace, VrrFlags = NvapiDisplay.VrrEnabled | NvapiDisplay.VrrDisplayMode, ColorSpace = 12 });

        AdvancedValue Of(string id) => facts.Single(f => string.Equals(f.Id, id, StringComparison.Ordinal)).Value;
        Of("nvapi.session.vrr_enabled").Number.Should().Be(1);
        Of("nvapi.session.vrr_requested").Number.Should().Be(0);
        Of("nvapi.session.vrr_possible").Number.Should().Be(0);
        Of("nvapi.session.vrr_display_mode").Number.Should().Be(1);
        Of("nvapi.session.source_colorspace").Tokens.Should().Equal("bt2100_pq");
    }

    [Theory]
    [InlineData(0u, "srgb", null)]
    [InlineData(1u, "scrgb_linear", null)]
    [InlineData(7u, null, "7")]
    public void AColourSpaceTheHeaderDoesNotNameIsShownAsItsNumber(uint space, string? token, string? text)
    {
        var facts = new List<AdvancedFact>();

        NvapiAdvancedReader.AddSessionDisplayFacts(facts, new NvapiSessionDisplay { NvapiStatus = 0, Present = NvapiSessionDisplay.HasColorSpace, ColorSpace = space });

        AdvancedValue value = facts.Should().ContainSingle().Which.Value;
        value.Text.Should().Be(text);
        if (token is null)
        {
            value.Tokens.Should().BeNull();
        }
        else
        {
            value.Tokens.Should().Equal(token);
        }
    }

    [Fact]
    public void ADisplayTheDriverDidNotFindSaysNothingAboutTheSession()
    {
        var facts = new List<AdvancedFact>();

        NvapiAdvancedReader.AddSessionDisplayFacts(facts, new NvapiSessionDisplay { NvapiStatus = -5, Present = NvapiSessionDisplay.HasVrr | NvapiSessionDisplay.HasColorSpace, VrrFlags = 0x1F, ColorSpace = 12 });

        facts.Should().BeEmpty("with the display not found nothing else was asked: the bits are not a reading");
    }

    [Fact]
    public void AProfilesRowsAreTheDriversNamesItsValuesInHexAndWhoSetThem()
    {
        NvapiProfileEntry[] entries =
        [
            new() { Id = 0x00A879CF, Type = 0, Value = 0x60925292, Predefined = 0, Name = "Vertical Sync" },
            new() { Id = 0x10E41DF3, Type = 0, Value = 1, Predefined = 1, Name = string.Empty },
            new() { Id = 0x20FDD1F9, Type = 1, Value = 0, Predefined = 0, Name = "Shader cache" },
        ];

        IReadOnlyList<AdvancedRow> rows = NvapiAdvancedReader.RowsOf(entries);

        rows.Should().Equal(
            new AdvancedRow("Vertical Sync (0x00A879CF)", "0x60925292", "user_set"),
            new AdvancedRow("0x10E41DF3", "0x00000001", "driver_default"),
            new AdvancedRow("Shader cache (0x20FDD1F9)", string.Empty, "not_a_number"));
    }

    [Fact]
    public void TheGlobalProfileIsAMachineFactAndIsLeftOutWhenTheDriverRefuses()
    {
        (NvapiAdvancedReader reader, _, FakeNvapiAdvancedBridge advanced) = Started();
        using NvapiAdvancedReader disposing = reader;
        advanced.GlobalProfile = (ListOf(NvapiDriverProfile.Degraded, 0), []);

        reader.ReadMachineFacts().Should().NotContain(static f => string.Equals(f.Id, "nvapi.driver.global_profile", StringComparison.Ordinal));

        advanced.GlobalProfile = (ListOf(NvapiDriverProfile.Global, 0), []);

        reader.ReadMachineFacts().Single(static f => string.Equals(f.Id, "nvapi.driver.global_profile", StringComparison.Ordinal)).Value.Rows.Should().BeEmpty("a profile that sets nothing is an answer");
        advanced.Calls.Should().Contain("profile (global) own=False");
    }

    [Fact]
    public void AGameNoProfileNamesSaysSoAndIsNeverGivenThePcsSettings()
    {
        (NvapiAdvancedReader reader, _, FakeNvapiAdvancedBridge advanced) = Started();
        using NvapiAdvancedReader disposing = reader;
        advanced.GameProfile = (ListOf(NvapiDriverProfile.Global, 0), []);

        AdvancedFact fact = reader.ReadSessionFacts(gdiDeviceName: null, 4242, @"C:\Games\game.exe").Should().ContainSingle().Which;

        fact.Id.Should().Be("nvapi.session.driver_profile");
        fact.Value.Tokens.Should().Equal("no_profile_for_game");
        fact.Value.Rows.Should().BeNull();
        advanced.Calls.Should().Equal(@"profile C:\Games\game.exe own=True");
    }

    [Fact]
    public void WithNoDisplayNameAndNoPathASessionAsksTheDriverNothing()
    {
        (NvapiAdvancedReader reader, _, FakeNvapiAdvancedBridge advanced) = Started();
        using NvapiAdvancedReader disposing = reader;
        Everything(advanced);

        reader.ReadSessionFacts(gdiDeviceName: " ", 4242, exePath: null).Should().BeEmpty();

        advanced.Calls.Should().BeEmpty();
    }

    [Fact]
    public void TheReadingsAreTheDriversFiguresInMebibytesPercentAndTheHundredOrZeroOfALimit()
    {
        IReadOnlyList<AdvancedReading> readings = NvapiAdvancedReader.ReadingsOf(FakeNvapiAdvancedBridge.FullLive());

        double Of(string series) => readings.Single(r => string.Equals(r.Series, series, StringComparison.Ordinal)).Value;
        Of("adv.nvapi.vram.available").Should().Be(14714);
        Of("adv.nvapi.vram.in_use").Should().Be(15979 - 14714, "what surfaces can use, less what is free now");
        Of("adv.nvapi.vram.evictions_size").Should().Be(89);
        Of("adv.nvapi.vram.eviction_count").Should().Be(184);
        Of("adv.nvapi.vram.promotions_size").Should().Be(3);
        Of("adv.nvapi.vram.promotion_count").Should().Be(1);
        Of("adv.nvapi.load.framebuffer").Should().Be(1);
        Of("adv.nvapi.load.bus").Should().Be(3);
        Of("adv.nvapi.clock.video").Should().Be(2295);
        Of("adv.nvapi.pstate").Should().Be(0, "P0 is a state, not an absence");
        Of("adv.nvapi.temp.board").Should().Be(41);
        Of("adv.nvapi.limit.ac_battery").Should().Be(0);
        Of("adv.nvapi.limit.api").Should().Be(100);
        Of("adv.nvapi.limit.insufficient_power").Should().Be(0);
        Of("adv.nvapi.encoder.sessions").Should().Be(1);
        Of("adv.nvapi.encoder.latency").Should().Be(4200);
    }

    [Fact]
    public void WhatTheCardDoesNotHaveHasNoReading()
    {
        // The RTX 5080 answers neither temperature (2026-11-11: present = 0x33F). Its series are absent, not 0 degrees.
        NvapiLive live = FakeNvapiAdvancedBridge.FullLive();
        live.Present = 0x33F;

        IReadOnlyList<AdvancedReading> readings = NvapiAdvancedReader.ReadingsOf(live);

        readings.Select(static r => r.Series).Should().NotContain(["adv.nvapi.temp.board", "adv.nvapi.temp.power_supply"]);
        readings.Should().Contain(static r => string.Equals(r.Series, "adv.nvapi.pstate", StringComparison.Ordinal));
        NvapiAdvancedReader.ReadingsOf(default).Should().BeEmpty();
    }

    [Fact]
    public void MemoryInUseIsNotComputedFromFiguresThatContradictEachOther()
    {
        NvapiLive live = FakeNvapiAdvancedBridge.FullLive();
        live.VramAvailableBytes = live.VramDedicatedAvailableBytes + 1;

        IReadOnlyList<AdvancedReading> readings = NvapiAdvancedReader.ReadingsOf(live);

        readings.Should().NotContain(static r => string.Equals(r.Series, "adv.nvapi.vram.in_use", StringComparison.Ordinal), "more free than there is to use is not a figure to subtract");
        readings.Should().Contain(static r => string.Equals(r.Series, "adv.nvapi.vram.available", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(NativeNvapiBridge.NoGpu)]
    [InlineData(NativeNvapiBridge.Unavailable)]
    public void APcWhoseDriverDidNotInitialiseOrListsNoCardReadsNothingAndAsksNothing(int init)
    {
        (NvapiAdvancedReader reader, _, FakeNvapiAdvancedBridge advanced) = Started(init);
        using NvapiAdvancedReader disposing = reader;
        Everything(advanced);

        reader.IsAvailable.Should().BeFalse();
        reader.InitStatus.Should().Be(init);
        reader.ReadMachineFacts().Should().BeEmpty();
        reader.ReadSessionFacts(@"\\.\DISPLAY1", 4242, @"C:\Games\game.exe").Should().BeEmpty();
        reader.ReadLive().Should().BeEmpty();
        advanced.Calls.Should().BeEmpty("a source that is not there is not asked");
    }

    [Fact]
    public void BeforeStartNothingIsReadAndStartingTwiceInitialisesOnce()
    {
        using var bridge = new FakeNvapiBridge();
        var advanced = Everything(new FakeNvapiAdvancedBridge());
        using var reader = new NvapiAdvancedReader(bridge, advanced);

        reader.InitStatus.Should().BeNull();
        reader.ReadLive().Should().BeEmpty();
        reader.Start().Should().BeTrue();
        reader.Start().Should().BeTrue();

        bridge.Inits.Should().Be(1);
        reader.ReadLive().Should().NotBeEmpty();
    }

    [Fact]
    public void AReadTheBridgeRefusesLeavesTheOthersStanding()
    {
        (NvapiAdvancedReader reader, _, FakeNvapiAdvancedBridge advanced) = Started();
        using NvapiAdvancedReader disposing = reader;
        advanced.Displays = FakeNvapiAdvancedBridge.TwoDisplays();

        IReadOnlyList<AdvancedFact> facts = reader.ReadMachineFacts();

        facts.Should().OnlyContain(static f => f.Id.StartsWith("nvapi.display.", StringComparison.Ordinal));
        reader.ReadLive().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(NativeNvapiBridge.NoGpu, 1)]
    [InlineData(-1, 0)]
    public void DisposingReleasesTheReferenceItTookAndOnlyThat(int init, int shutdowns)
    {
        (NvapiAdvancedReader reader, FakeNvapiBridge bridge, _) = Started(init);

        reader.Dispose();
        reader.Dispose();

        bridge.Shutdowns.Should().Be(shutdowns);
        bridge.Disposed.Should().BeTrue();
        FluentActions.Invoking(reader.Start).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AReaderThatWasNeverStartedReleasesNothing()
    {
        using var bridge = new FakeNvapiBridge();
        var reader = new NvapiAdvancedReader(bridge, new FakeNvapiAdvancedBridge());

        reader.Dispose();

        bridge.Shutdowns.Should().Be(0);
        bridge.Disposed.Should().BeTrue();
    }

    [Fact]
    public void EveryWordItStoresIsATokenAndIsInTheListTheAppTranslatesFrom()
    {
        string[] declared = [.. typeof(NvapiAdvancedTokens).GetFields(BindingFlags.Public | BindingFlags.Static).Where(static f => f.IsLiteral).Select(static f => (string)f.GetRawConstantValue()!)];

        declared.Should().NotBeEmpty();
        declared.Should().OnlyContain(static t => AdvancedValue.IsToken(t));
        declared.Should().OnlyHaveUniqueItems();
        NvapiAdvancedTokens.All.Should().BeEquivalentTo(declared, "a word missing from All has no translation checked for it");
    }

    [Fact]
    public void TheRealBridgeEitherReadsOrSaysWhyAndNeverReturnsAnIdentifier()
    {
        // On the dev box this is the AVAILABLE branch (an RTX 5080); on a hosted runner the driver is absent and every
        // read is empty. Both are the contract.
        using NvapiAdvancedReader reader = NvapiAdvancedReader.OverTheNativeBridge();

        if (!reader.Start())
        {
            reader.InitStatus.Should().NotBe(0);
            reader.ReadMachineFacts().Should().BeEmpty();
            reader.ReadLive().Should().BeEmpty();
            return;
        }

        IReadOnlyList<AdvancedFact> facts = reader.ReadMachineFacts();
        IReadOnlyList<AdvancedReading> readings = reader.ReadLive();
        IReadOnlyList<AdvancedFact> session = reader.ReadSessionFacts(gdiDeviceName: null, (uint)Environment.ProcessId, Environment.ProcessPath);

        // What holds whatever the driver answered - and a read that answered NOTHING is within the contract: once in
        // about a dozen runs on the dev box (2026-11-11) the live read came back with no field although the facts
        // read just before it had answered (20_OPEN_QUESTIONS M12). So nothing here requires a reading to be there;
        // each "none of them is wrong" is written as an empty set of wrong ones, which an empty answer satisfies.
        facts.Where(static f => AdvancedMetricCatalog.Find(f.Id) is not { IsRead: true }).Should().BeEmpty("every fact is one the catalog says this build reads");
        facts.Select(static f => f.Id).Should().NotContain(["nvapi.gpu.board_serial", "nvapi.gpu.uuid", "nvapi.display.edid"]);
        readings.Where(static r => !r.Series.StartsWith(AdvancedMetric.SeriesPrefix, StringComparison.Ordinal) || !double.IsFinite(r.Value)).Should().BeEmpty();
        readings.Select(static r => r.Series).Should().OnlyHaveUniqueItems();
        session.Where(static f => !string.Equals(f.Id, "nvapi.session.driver_profile", StringComparison.Ordinal)).Should().BeEmpty("with no display named, only the profile is asked for");
    }
}
