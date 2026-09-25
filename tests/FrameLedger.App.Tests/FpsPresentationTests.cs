using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Sessions;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// CLAUDE.md rule 6 / <c>08_UI</c> §FPS display rule, the three shapes and the table's two negatives: a measured
/// none is <c>—</c>, not measured is <c>N/A</c>, and they never collapse; Presented FPS never says "Native" and
/// always carries its qualifier; a generated row shows Native first and the factor as a chip.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class FpsPresentationTests
{
    private static readonly CultureInfo _en = CultureInfo.GetCultureInfo("en");

    private static SessionRow Hooked(string fgMode, double? native = 62, double? displayed = null, double? factor = null, double? presented = null, string? qualifier = null, string? refusal = null, long? census = null) => new()
    {
        FgRefusal = refusal,
        FgRuntimeCensus = census,
        SessionGuid = Guid.NewGuid(),
        GameId = 1,
        SnapshotId = 1,
        StartedAt = DateTimeOffset.UnixEpoch,
        EndedAt = DateTimeOffset.UnixEpoch.AddMinutes(5),
        QpcEpoch = 0,
        QpcFrequency = 1,
        Tier = CaptureTier.Hooked,
        Mode = CaptureMode.Launch,
        ExitStatus = ExitStatus.Normal,
        FrameCount = 1000,
        FgMode = fgMode,
        NativeFps = native,
        DisplayedFps = displayed,
        FgFactor = factor,
        PresentedFps = presented,
        PresentedQualifier = qualifier,
    };

    private static T InEnglish<T>(Func<T> f)
    {
        CultureInfo? previous = Strings.Culture;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            Strings.Culture = _en;
            CultureInfo.CurrentCulture = _en;
            return f();
        }
        finally
        {
            Strings.Culture = previous;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void AGeneratedRowShowsNativeFirstDisplayedSecondAndTheFactorAsAChip()
    {
        SessionRow row = Hooked("dlssg", native: 62, displayed: 118, factor: 1.9);
        FpsReadoutModel m = InEnglish(() => FpsPresentation.FromRow(row));
        m.Kind.Should().Be(FpsReadoutKind.Generated);
        InEnglish(() => m.Line).Should().Be("62 → 118 FPS (×1.9 FG)");
        InEnglish(() => m.FactorChip).Should().Be("×1.9 FG");
        m.QualifierText.Should().BeNull();
        InEnglish(() => FpsPresentation.NativeColumn(row)).Should().Be("62");
        InEnglish(() => FpsPresentation.DisplayedColumn(row)).Should().Be("118");
        InEnglish(() => FpsPresentation.FactorColumn(row)).Should().Be("×1.9");
    }

    [Fact]
    public void AMeasuredNoneStandsAloneAndTheTableSaysDashNotNa()
    {
        SessionRow row = Hooked("none", native: 144);
        FpsReadoutModel m = InEnglish(() => FpsPresentation.FromRow(row));
        m.Kind.Should().Be(FpsReadoutKind.None);
        InEnglish(() => m.Line).Should().Be("144 FPS");
        m.FactorChip.Should().BeNull();
        m.QualifierText.Should().BeNull();
        InEnglish(() => FpsPresentation.DisplayedColumn(row)).Should().Be("—", "a measured negative");
        InEnglish(() => FpsPresentation.FactorColumn(row)).Should().Be("—");
        InEnglish(() => FpsPresentation.ColumnTooltip(row)).Should().Contain("measured as none");
    }

    [Theory]
    [InlineData("no_fg_runtime", "FG not observed", false)]
    [InlineData("fg_runtime_loaded", "FG runtime loaded — may include generated frames", true)]
    [InlineData("census_not_run", "FG census not run", false)]
    [InlineData("none_withheld", "Counts application frames", true)]
    [InlineData(null, "FG census not run", false)]
    public void NotMeasuredIsPresentedFpsWithItsQualifierAndTheTableSaysNa(string? qualifier, string chip, bool warning)
    {
        SessionRow row = Hooked("na", native: null, presented: 144, qualifier: qualifier);
        FpsReadoutModel m = InEnglish(() => FpsPresentation.FromRow(row));
        m.Kind.Should().Be(FpsReadoutKind.Presented);
        InEnglish(() => m.Line).Should().Be("144 FPS").And.NotContain("Native");
        InEnglish(() => m.QualifierText).Should().Be(chip);
        m.QualifierIsWarning.Should().Be(warning);
        InEnglish(() => FpsPresentation.NativeColumn(row)).Should().Be("144", "the Native column carries the Presented figure");
        InEnglish(() => FpsPresentation.DisplayedColumn(row)).Should().Be("N/A", "not measured is a different negative from a measured none");
        InEnglish(() => FpsPresentation.FactorColumn(row)).Should().Be("N/A");
        InEnglish(() => FpsPresentation.ColumnTooltip(row)).Should().Be(InEnglish(() => m.QualifierTooltip));
    }

    [Fact]
    public void ATierTwoRowIsUnavailableEverywhere()
    {
        SessionRow row = Hooked("na") with { Tier = CaptureTier.NotHooked, FrameCount = 0, NativeFps = null };
        FpsReadoutModel m = FpsPresentation.FromRow(row);
        m.Kind.Should().Be(FpsReadoutKind.Unavailable);
        InEnglish(() => m.Line).Should().Be("N/A");
        InEnglish(() => FpsPresentation.NativeColumn(row)).Should().Be("N/A");
        InEnglish(() => FpsPresentation.DisplayedColumn(row)).Should().Be("N/A");
    }

    [Fact]
    public void TheLiveCardFollowsTheSameRuleFromAProgressEvent()
    {
        var generated = new SessionProgressEvent
        {
            SessionGuid = Guid.NewGuid(),
            ElapsedS = 12,
            Presents5s = 590,
            PresentedFps5s = 118,
            PresentedQualifier = "fg_runtime_loaded",
            NativeFps5s = 62,
            DisplayedFps5s = 118,
            FgFactor = 1.9,
            FgMode = "dlssg",
        };
        InEnglish(() => FpsPresentation.FromProgress(generated).Line).Should().Be("62 → 118 FPS (×1.9 FG)");

        var presented = generated with { NativeFps5s = null, DisplayedFps5s = null, FgFactor = null, FgMode = "na", PresentedQualifier = "no_fg_runtime" };
        FpsReadoutModel m = InEnglish(() => FpsPresentation.FromProgress(presented));
        m.Kind.Should().Be(FpsReadoutKind.Presented);
        InEnglish(() => m.QualifierText).Should().Be("FG not observed");

        FpsPresentation.FromProgress(generated with { Presents5s = 0 }).Kind.Should().Be(FpsReadoutKind.Unavailable, "no frames yet");
    }

    /// <summary>
    /// The owner's rows 8 and 10 (2026-09-14): <c>fg_mode = dlssg</c>, <c>fg_source = api</c>, <c>fg_factor</c> NULL —
    /// the tags named DLSS-G and the count refused. Every FG surface said N/A and the headline printed
    /// <c>N/A → N/A FPS (×0.0 FG)</c>: a factor nobody counted. The shape is Presented FPS with a warning chip naming
    /// the technology and the refusal as its tooltip; the table's Displayed and FG× say N/A; "Native" never appears.
    /// </summary>
    [Fact]
    public void AnIdentifiedButUncountedRowIsPresentedFpsNamingTheTechnologyAndNeverAFactor()
    {
        SessionRow row = Hooked("dlssg", native: null, displayed: null, factor: null, presented: 304.35, qualifier: "fg_runtime_loaded", refusal: "no_evaluations");
        FpsReadoutModel m = InEnglish(() => FpsPresentation.FromRow(row));

        m.Kind.Should().Be(FpsReadoutKind.IdentifiedUncounted);
        InEnglish(() => m.Line).Should().Be("304 FPS").And.NotContain("Native").And.NotContain("×");
        InEnglish(() => m.QualifierText).Should().Be("DLSS-G active — factor not counted");
        m.QualifierIsWarning.Should().BeTrue("the number may include generated frames");
        InEnglish(() => m.QualifierTooltip).Should().Contain("no application-frame token was counted").And.Contain("Displayed, not Native");
        m.FactorChip.Should().BeNull();
        InEnglish(() => FpsPresentation.NativeColumn(row)).Should().Be("304");
        InEnglish(() => FpsPresentation.DisplayedColumn(row)).Should().Be("N/A");
        InEnglish(() => FpsPresentation.FactorColumn(row)).Should().Be("N/A");
        InEnglish(() => FpsPresentation.ColumnTooltip(row)).Should().Be(InEnglish(() => m.QualifierTooltip));
        InEnglish(() => FpsPresentation.FrameGenerationLabel(m, row.FgMode)).Should().Be("DLSS-G · factor not counted");

        var live = new SessionProgressEvent
        {
            SessionGuid = Guid.NewGuid(),
            ElapsedS = 30,
            Presents5s = 1500,
            PresentedFps5s = 300,
            PresentedQualifier = "fg_runtime_loaded",
            FgMode = "dlssg",
            FgRefusal = "non_uniform",
        };
        FpsReadoutModel lm = InEnglish(() => FpsPresentation.FromProgress(live));
        lm.Kind.Should().Be(FpsReadoutKind.IdentifiedUncounted);
        InEnglish(() => lm.Line).Should().Be("300 FPS");
        InEnglish(() => lm.QualifierTooltip).Should().Contain("changed mid-session");
        InEnglish(() => FpsPresentation.RefusalText("something_new")).Should().Be("the count did not resolve", "an unknown token is the honest unknown");
    }

    /// <summary>
    /// 2026-09-16: the owner's Hell Is Us, Black Myth: Wukong and Cronos rows refused <c>non_uniform</c> and Onimusha
    /// <c>multiple_streams</c>, and the tooltip could only repeat the kind. With <c>fg_refusal_detail</c> (schema 0005)
    /// it says which bucket, its ratio and the window's — the numbers the aggregator had and threw away.
    /// </summary>
    [Fact]
    public void TheIdentifiedTooltipCarriesTheRefusalsNumbersWhenTheRowStoredThem()
    {
        string detail = "{\"Kind\":\"non_uniform\",\"Subject\":\"factor\",\"Count\":53,\"BucketIndex\":2,\"BucketCount\":8,\"BucketValue\":2.25,\"Overall\":3.65}";
        SessionRow row = Hooked("dlssg", native: null, displayed: null, factor: null, presented: 304.35, qualifier: "fg_runtime_loaded", refusal: "non_uniform") with { FgRefusalDetail = detail };

        FpsReadoutModel m = InEnglish(() => FpsPresentation.FromRow(row));

        m.Kind.Should().Be(FpsReadoutKind.IdentifiedUncounted);
        InEnglish(() => m.QualifierTooltip).Should().Contain("changed mid-session").And.Contain("Bucket 3 of 8 measured 2.25 against 3.65 for the whole session, over 53 samples.");
        InEnglish(() => m.QualifierText).Should().Be("DLSS-G active — factor not counted", "the chip does not grow; the numbers are the tooltip's");

        string empty = "{\"Kind\":\"non_uniform\",\"Subject\":\"factor\",\"Count\":40,\"BucketIndex\":7,\"BucketCount\":8,\"BucketValue\":null,\"Overall\":3.9}";
        InEnglish(() => FpsPresentation.IdentifiedTooltip("DLSS-G", "non_uniform", empty)).Should().Contain("Bucket 8 of 8 measured no tokens against 3.90", "a bucket with no tokens is the loading-screen shape, not infinity");
        InEnglish(() => FpsPresentation.IdentifiedTooltip("DLSS-G", "multiple_streams", "{\"Kind\":\"multiple_streams\",\"Subject\":\"factor\",\"Count\":2,\"BucketIndex\":0,\"BucketCount\":0,\"BucketValue\":null,\"Overall\":0}"))
            .Should().Contain("2 swapchains presented during the session.");
        InEnglish(() => FpsPresentation.IdentifiedTooltip("DLSS-G", "no_evaluations", "{\"Kind\":\"no_evaluations\",\"Subject\":\"factor\",\"Count\":0,\"BucketIndex\":0,\"BucketCount\":0,\"BucketValue\":null,\"Overall\":0}"))
            .Should().Be(InEnglish(() => FpsPresentation.IdentifiedTooltip("DLSS-G", "no_evaluations")), "a kind with no numbers worth a sentence adds nothing");
        InEnglish(() => FpsPresentation.IdentifiedTooltip("DLSS-G", "non_uniform", "not json")).Should().Be(InEnglish(() => FpsPresentation.IdentifiedTooltip("DLSS-G", "non_uniform")), "an unparsable detail is the old tooltip, never an exception");
    }

    /// <summary>
    /// 2026-09-17: a steady-state factor (<c>fg_factor_scope = steady</c>) is the generated shape — Native, Displayed and
    /// the factor together, as rule 6 asks — and its chip ALWAYS says how much of the session it describes; the tooltip
    /// says why there is no session-wide number. A session-wide factor keeps the plain chip.
    /// </summary>
    [Fact]
    public void ASteadyStateFactorIsTheGeneratedShapeWithItsShareOnTheChip()
    {
        string detail = "{\"Kind\":\"non_uniform\",\"Subject\":\"factor\",\"Count\":8000,\"BucketIndex\":0,\"BucketCount\":8,\"BucketValue\":1.0,\"Overall\":1.6}";
        SessionRow row = Hooked("dlssg", native: 58.2, displayed: 116.4, factor: 2.0, presented: 101, qualifier: "fg_runtime_loaded", refusal: "non_uniform")
            with
        { FgFactorScope = "steady", FgSteadyShare = 0.75, FgRefusalDetail = detail };

        FpsReadoutModel m = InEnglish(() => FpsPresentation.FromRow(row));

        m.Kind.Should().Be(FpsReadoutKind.Generated);
        InEnglish(() => m.Line).Should().Be("58 → 116 FPS (×2.0 FG · 75%)", "rule 6: a steady factor is never shown without its share, the Average card included (beta.8)");
        InEnglish(() => m.FactorChip).Should().Be("×2.0 FG · 75%");
        InEnglish(() => m.QualifierTooltip).Should().Contain("steady state").And.Contain("75%").And.Contain("Bucket 1 of 8 measured 1.00 against 1.60");
        InEnglish(() => FpsPresentation.ColumnTooltip(row)).Should().Be(InEnglish(() => m.QualifierTooltip));
        InEnglish(() => FpsPresentation.FactorColumn(row)).Should().Be("×2.0 · 75%", "the grid carries the share too");

        SessionRow session = Hooked("dlssg", native: 58.2, displayed: 116.4, factor: 2.0) with { FgFactorScope = "session" };
        FpsReadoutModel s = InEnglish(() => FpsPresentation.FromRow(session));
        InEnglish(() => s.FactorChip).Should().Be("×2.0 FG", "a session-wide factor needs no share");
        s.QualifierTooltip.Should().BeNull();
    }

    /// <summary>A counted factor keeps the generated shape; the label carries the chip, without a trailing space.</summary>
    [Fact]
    public void ACountedFactorKeepsTheGeneratedLabelWithItsChip()
    {
        SessionRow row = Hooked("dlssg", native: 99.6, displayed: 399.9, factor: 4.01);
        FpsReadoutModel m = InEnglish(() => FpsPresentation.FromRow(row));
        m.Kind.Should().Be(FpsReadoutKind.Generated);
        InEnglish(() => FpsPresentation.FrameGenerationLabel(m, row.FgMode)).Should().Be("DLSS-G ×4.0 FG");
        InEnglish(() => FpsPresentation.FrameGenerationLabel(FpsPresentation.FromRow(Hooked("na", native: null, presented: 144)), "na")).Should().Be("N/A");
    }

    /// <summary><c>08_UI</c> §FPS display rule: the warning chip names the module when the census says which one was loaded.</summary>
    [Fact]
    public void TheRuntimeLoadedChipNamesTheModuleWhenTheCensusCarriesOne()
    {
        long census = (long)(Shared.FlRuntimeCensus.Ran | Shared.FlRuntimeCensus.SlInterposer | Shared.FlRuntimeCensus.SlDlssG | Shared.FlRuntimeCensus.NvngxDlssG);
        SessionRow named = Hooked("na", native: null, presented: 144, qualifier: "fg_runtime_loaded", census: census);
        InEnglish(() => FpsPresentation.FromRow(named).QualifierText).Should().Be("FG runtime loaded (sl.dlss_g.dll) — may include generated frames");

        SessionRow unnamed = Hooked("na", native: null, presented: 144, qualifier: "fg_runtime_loaded", census: (long)(Shared.FlRuntimeCensus.Ran | Shared.FlRuntimeCensus.SlInterposer));
        InEnglish(() => FpsPresentation.FromRow(unnamed).QualifierText).Should().Be("FG runtime loaded — may include generated frames", "the qualifier said loaded and the census names no FG family: the words stay, the name does not");

        FpsPresentation.FrameGenerationModule(null).Should().BeNull();
        FpsPresentation.FrameGenerationModule((long)Shared.FlRuntimeCensus.AmdFfxDx12).Should().Be("amd_fidelityfx_dx12.dll", "the FSR 3.1 facade MAY generate frames (03_METRICS §Rung 0's qualifier)");
    }
}
