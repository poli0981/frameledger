// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text.RegularExpressions;

using FluentAssertions;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Recording;

namespace FrameLedger.Application.Tests.Advanced;

/// <summary>
/// The catalog is the one list every consumer of "Advanced metrics" reads (D101), so what it may hold is held here:
/// ids that can go on disk, a scope and a reason on every entry, and the table in
/// <c>docs/18_GPU_VENDOR_APIS.md</c> §The inventory equal to it row for row.
/// </summary>
public sealed partial class AdvancedMetricCatalogTests
{
    private static readonly IReadOnlyList<AdvancedMetric> _all = AdvancedMetricCatalog.All;

    [Fact]
    public void EveryIdIsUniqueLowerCaseDottedAndStartsWithItsSourcesPrefix()
    {
        _all.Select(static m => m.Id).Should().OnlyHaveUniqueItems();
        foreach (AdvancedMetric m in _all)
        {
            IdShape().IsMatch(m.Id).Should().BeTrue("'{0}' goes on disk as a series name or a JSON key", m.Id);
            m.Id.Should().StartWith(PrefixOf(m.Source), "an id says where its value comes from");
        }
    }

    [Fact]
    public void NoEntryLeavesAnEnumAtItsZeroOrItsSummaryEmpty()
    {
        foreach (AdvancedMetric m in _all)
        {
            m.Scope.Should().NotBe(AdvancedScope.None, m.Id);
            m.Source.Should().NotBe(AdvancedSource.None, m.Id);
            m.Reach.Should().NotBe(AdvancedReach.None, m.Id);
            m.Unit.Should().NotBe(AdvancedUnit.None, m.Id);
            m.Group.Should().NotBe(AdvancedGroup.None, m.Id);
            m.Summary.Should().NotBeNullOrWhiteSpace(m.Id);
            m.Summary.Should().NotContain("|", "{0}'s summary is a cell of a Markdown table", m.Id);
            m.Summary.Should().NotEndWith(".", "{0}'s summary is a label, not a sentence", m.Id);
        }
    }

    [Fact]
    public void ASeriesIsNamedByThePrefixAndItsIdAndNothingElseHasASeriesName()
    {
        foreach (AdvancedMetric m in _all)
        {
            if (m.Scope == AdvancedScope.Series)
            {
                m.SeriesName.Should().Be("adv." + m.Id);
            }
            else
            {
                m.SeriesName.Should().BeNull(m.Id);
            }
        }
    }

    [Fact]
    public void NoAdvancedSeriesCanBeTakenForAFixedOne()
    {
        // The fixed series store a missing tick as -1 and their readers drop every negative value; an advanced series
        // may be negative (a -12 V rail) and stores NaN (D102). The prefix is how a reader tells which rule applies.
        string[] fixedNames = [.. SensorSeriesCatalog.All.Select(static s => s.Name), "t_ms"];

        fixedNames.Should().NotContain(static n => n.StartsWith(AdvancedMetric.SeriesPrefix, StringComparison.Ordinal));
        _all.Where(static m => m.SeriesName is not null).Should().OnlyContain(static m => m.SeriesName!.StartsWith(AdvancedMetric.SeriesPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void TheLabelKeyIsAnIdentifierTheResourceAccessorCanCarry()
    {
        foreach (AdvancedMetric m in _all)
        {
            LabelKeyShape().IsMatch(m.LabelKey).Should().BeTrue(m.LabelKey);
        }

        _all.Select(static m => m.LabelKey).Should().OnlyHaveUniqueItems("two ids that differ only by '.' and '_' would share a label");
    }

    [Fact]
    public void OnlyWhatTheAgentOrTheOverlayReadsCountsAsRead()
    {
        _all.Where(static m => m.IsRead).Should().OnlyContain(static m => m.Reach == AdvancedReach.Agent || m.Reach == AdvancedReach.AgentElevated || m.Reach == AdvancedReach.Game);
        _all.Where(static m => !m.IsRead).Should().NotContain(static m => m.Reach == AdvancedReach.Agent || m.Reach == AdvancedReach.AgentElevated || m.Reach == AdvancedReach.Game);
        _all.Should().Contain(static m => m.IsRead);
        _all.Should().Contain(static m => !m.IsRead);
    }

    [Fact]
    public void TheElevatedReachIsLibreHardwareMonitorsAlone()
    {
        // D103: the administrator's rights buy the PawnIO groups and nothing else. NVAPI needs none, and nothing read
        // inside a game is ever a matter of the Agent's rights.
        _all.Where(static m => m.Reach == AdvancedReach.AgentElevated).Should().OnlyContain(static m => m.Source == AdvancedSource.Lhm);
        _all.Where(static m => m.Reach == AdvancedReach.AgentElevated).Should().NotBeEmpty();
    }

    [Fact]
    public void WhatIsReadInsideAGameIsWhatTheOverlayOfThisBuildReadsAndNothingMore()
    {
        // Train B, 0.1.0-beta.40: thirteen entries, each a member of a call the Overlay already hooked - and each
        // matched by a name on tools/overlay-allowed-fields.txt, which a build gate holds to the Overlay's own code.
        // 0.1.0-beta.43: six more, from the two calls hooked only for Advanced metrics (slSetConstants, ffxConfigure).
        // Everything else of Streamline and FidelityFX is still named, not read; NGX and XeSS are never read.
        string[] inGame = [.. _all.Where(static m => m.Reach == AdvancedReach.Game).Select(static m => m.Id)];
        inGame.Should().Equal(
            "sl.dlss.output_size", "sl.dlss.presets", "sl.dlss.hdr_input", "sl.dlss.auto_exposure", "sl.dlss.alpha_upscaling", "sl.dlss.exposure",
            "ffx.upscale.sharpening", "ffx.upscale.jitter", "ffx.upscale.exposure", "ffx.upscale.depth", "ffx.upscale.flags",
            "ffx.upscale.frame_time_delta", "ffx.memory_usage",
            "sl.constants.jitter", "sl.constants.motion_vectors", "sl.constants.depth", "sl.constants.projection", "sl.constants.resets",
            "ffx.framegen.config");
        // The camera's position, axes and matrices are in the same struct as the constants read, and are the player's.
        AdvancedMetricCatalog.Find("sl.constants.camera")!.Reach.Should().Be(AdvancedReach.PlayerState);

        _all.Where(static m => m.Reach == AdvancedReach.Game).Should().OnlyContain(static m => m.Source == AdvancedSource.Streamline || m.Source == AdvancedSource.FidelityFx);
        _all.Where(static m => m.Reach == AdvancedReach.Game).Should().OnlyContain(static m => m.Scope != AdvancedScope.MachineFact, "what a game says is of a session, never of the PC");
        _all.Where(static m => m.Source is AdvancedSource.Streamline or AdvancedSource.FidelityFx or AdvancedSource.Ngx or AdvancedSource.Xess)
            .Where(static m => m.Reach != AdvancedReach.Game)
            .Should().OnlyContain(static m => !m.IsRead);
        _all.Where(static m => m.Source is AdvancedSource.Ngx or AdvancedSource.Xess).Should().OnlyContain(static m => !m.IsRead);
    }

    [Theory]
    [InlineData("nvapi.gpu.board_serial")]
    [InlineData("nvapi.gpu.uuid")]
    [InlineData("nvapi.display.edid")]
    [InlineData("nvapi.encoder.session_processes")]
    public void AnIdentifierIsNamedAndNeverRead(string id)
    {
        // D104. The entry exists so the page can say "never read, and why"; a reach that reads it would be the defect.
        AdvancedMetric m = AdvancedMetricCatalog.Find(id).Should().BeOfType<AdvancedMetric>("the catalog names '{0}'", id).Subject;

        m.Reach.Should().Be(AdvancedReach.Identifier);
        m.IsRead.Should().BeFalse();
    }

    [Fact]
    public void TheCameraIsPlayerStateAndTheOnlyEntryOfThatReach()
    {
        // CLAUDE.md rule 4: where the camera is, is where the player is.
        _all.Where(static m => m.Reach == AdvancedReach.PlayerState).Select(static m => m.Id).Should().Equal("sl.constants.camera");
    }

    [Fact]
    public void FindReturnsTheEntryOrNull()
    {
        AdvancedMetricCatalog.Find("nvapi.gpu.vbios_version").Should().BeSameAs(_all.Single(static m => string.Equals(m.Id, "nvapi.gpu.vbios_version", StringComparison.Ordinal)));
        AdvancedMetricCatalog.Find("nvapi.gpu.VBIOS_VERSION").Should().BeNull("ids are compared exactly");
        AdvancedMetricCatalog.Find("no.such.metric").Should().BeNull();
        FluentActions.Invoking(static () => AdvancedMetricCatalog.Find(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AnInstanceOfAFamilyIsNamedByItsStemAndItsKey()
    {
        AdvancedMetric cores = AdvancedMetricCatalog.Find("lhm.cpu.temperature")!;

        cores.IsFamily.Should().BeTrue();
        AdvancedMetricCatalog.InstanceName(cores, "amdcpu_0_3").Should().Be("adv.lhm.cpu.temperature.amdcpu_0_3");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Core 3")]
    [InlineData("core.3")]
    [InlineData("core-3")]
    [InlineData("CORE3")]
    public void AnInstanceKeyIsLowerCaseLettersDigitsAndUnderscores(string key)
    {
        AdvancedMetric cores = AdvancedMetricCatalog.Find("lhm.cpu.temperature")!;

        AdvancedMetricCatalog.IsInstanceKey(key).Should().BeFalse();
        FluentActions.Invoking(() => AdvancedMetricCatalog.InstanceName(cores, key)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AnInstanceKeyHasALongestLength()
    {
        AdvancedMetricCatalog.IsInstanceKey(new string('a', AdvancedMetricCatalog.MaxInstanceKeyLength)).Should().BeTrue();
        AdvancedMetricCatalog.IsInstanceKey(new string('a', AdvancedMetricCatalog.MaxInstanceKeyLength + 1)).Should().BeFalse();
        FluentActions.Invoking(static () => AdvancedMetricCatalog.IsInstanceKey(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OnlyAFamilyOfSeriesHasInstances()
    {
        AdvancedMetric single = AdvancedMetricCatalog.Find("nvapi.pstate")!;
        AdvancedMetric factFamily = AdvancedMetricCatalog.Find("nvapi.display.hdr_formats")!;

        single.IsFamily.Should().BeFalse();
        factFamily.IsFamily.Should().BeTrue();
        FluentActions.Invoking(() => AdvancedMetricCatalog.InstanceName(single, "a")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => AdvancedMetricCatalog.InstanceName(factFamily, "a")).Should().Throw<ArgumentException>("a family of facts is stored as JSON, not as series");
        FluentActions.Invoking(static () => AdvancedMetricCatalog.InstanceName(null!, "a")).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => AdvancedMetricCatalog.InstanceName(single, null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TheInventoryTableOfDoc18IsTheCatalogRowForRow()
    {
        // The document is where a person reads what is read and what is not; the catalog is what the code does. A row
        // on one side alone is a claim nobody checks - the defect this repository's documents keep being corrected for.
        string doc = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "18_GPU_VENDOR_APIS.md"));
        int start = doc.IndexOf("### The inventory", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "docs/18_GPU_VENDOR_APIS.md has a section '### The inventory'");
        int end = doc.IndexOf("\n## ", start, StringComparison.Ordinal);
        string section = end < 0 ? doc[start..] : doc[start..end];

        string[] documented = [.. InventoryRow().Matches(section).Select(static r => r.Value.Trim())];
        string[] expected = [.. _all.Select(RowOf)];

        // Row by row, so a failure names the one row that differs instead of printing both tables.
        for (int i = 0; i < Math.Min(documented.Length, expected.Length); i++)
        {
            documented[i].Should().Be(expected[i], "row {0} of the inventory is the catalog's entry {0}", i);
        }

        documented.Should().HaveCount(expected.Length, "the table has a row for every entry of the catalog and no other");
    }

    /// <summary>The row of the inventory table an entry is documented by.</summary>
    private static string RowOf(AdvancedMetric m)
        => $"| `{m.Id}` | {m.Summary} | {m.Scope}{(m.IsFamily ? ", one per device" : string.Empty)} | {m.Source} | {m.Reach} | {m.Unit} |";

    private static string PrefixOf(AdvancedSource source) => source switch
    {
        AdvancedSource.Nvapi => "nvapi.",
        AdvancedSource.Lhm => "lhm.",
        AdvancedSource.Streamline => "sl.",
        AdvancedSource.FidelityFx => "ffx.",
        AdvancedSource.Ngx => "ngx.",
        AdvancedSource.Xess => "xess.",
        _ => throw new InvalidOperationException($"no prefix for {source}"),
    };

    private static string RepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FrameLedger.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("the repository root (FrameLedger.slnx) is not above " + AppContext.BaseDirectory);
    }

    [GeneratedRegex(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9_]*)+$", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex IdShape();

    [GeneratedRegex("^Adv_[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex LabelKeyShape();

    [GeneratedRegex(@"^\| `[a-z0-9_.]+` \|.*\|\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex InventoryRow();
}
