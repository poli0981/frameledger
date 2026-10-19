// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Windows;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.Controls;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Metrics;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Tests;

/// <summary>
/// The summary's frame-generation mix (beta.24, D62) in words: each state with its factor, its own Native → Displayed and its
/// share to two decimals, then no frame generation, then the time no state claims — the stored rows, never recomputed; and
/// the bar those shares draw.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class FgMixPresentationTests
{
    private static T In<T>(string culture, Func<T> f)
    {
        CultureInfo? previous = Strings.Culture;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            return f();
        }
        finally
        {
            Strings.Culture = previous;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static string Mix(params FgMixRow[] rows) => FgMixRecord.ToJson(new FgMix(rows))!;

    // The owner's example (2026-10-19): ×2 10%, ×3 25.75%, ×4 35.33%, then the time without frame generation.
    private static readonly string _owners = Mix(
        new FgMixRow(FgMixKind.Generated, 2.0, 62, 124, 60, 0.1, 12),
        new FgMixRow(FgMixKind.Generated, 3.0, 60, 180, 154.5, 0.2575, 30),
        new FgMixRow(FgMixKind.Generated, 4.0, 70.5, 282, 212, 0.3533, 42),
        new FgMixRow(FgMixKind.None, null, 118, 118, 171, 0.285, 34),
        new FgMixRow(FgMixKind.Unclassified, null, null, null, 2.5, 0.0042, 1));

    private static SessionRow Hooked(string fgMode = "dlssg", double? factor = 1.9, string? refusal = null, string? mix = null, CaptureTier tier = CaptureTier.Hooked) => new()
    {
        SessionGuid = Guid.NewGuid(),
        GameId = 1,
        SnapshotId = 1,
        StartedAt = DateTimeOffset.UnixEpoch,
        EndedAt = DateTimeOffset.UnixEpoch.AddMinutes(10),
        QpcEpoch = 0,
        QpcFrequency = 1,
        Tier = tier,
        Mode = CaptureMode.Launch,
        ExitStatus = ExitStatus.Normal,
        FrameCount = 1000,
        FgMode = fgMode,
        FgFactor = factor,
        FgRefusal = refusal,
        FgMixJson = mix,
    };

    [Fact]
    public void TheOwnersExampleReadsAsOneRowPerStateEachWithItsOwnRatesAndTwoDecimals() => In("en", () =>
    {
        IReadOnlyList<FgMixRowModel> rows = FgMixPresentation.Rows(Hooked(mix: _owners));

        rows.Select(static r => r.Label).Should().Equal("×2.0 frame generation", "×3.0 frame generation", "×4.0 frame generation", "No frame generation", "Not classified");
        rows.Select(static r => r.Share).Should().Equal("10.00%", "25.75%", "35.33%", "28.50%", "0.42%");
        rows[0].Rates.Should().Be(Formats.Fps(62) + " → " + Formats.Fps(124) + " FPS", "a state's factor never stands without its Native and Displayed (rule 6)");
        rows[2].Rates.Should().Be(Formats.Fps(70.5) + " → " + Formats.Fps(282) + " FPS");
        rows[3].Rates.Should().Be(Formats.Fps(118) + " FPS", "no frame generation: what was rendered is what was shown");
        rows[4].Rates.Should().BeEmpty("no state was read over that time, so it has no rate of its own");
        rows[1].Duration.Should().Be(Formats.Duration(154.5));
        rows.Select(static r => r.Segment.Kind).Should().Equal(FgMixKind.Generated, FgMixKind.Generated, FgMixKind.Generated, FgMixKind.None, FgMixKind.Unclassified);
        rows.Select(static r => r.Segment.Factor).Should().Equal(2.0, 3.0, 4.0, null, null);
        rows[2].Swatch.Should().ContainSingle().Which.Should().Be(new FgMixSegment(FgMixKind.Generated, 4.0, 1), "the swatch is the row's colour, filled");

        FgMixPresentation.AccessibleName(rows).Should().Be("Share of the session's time by frame-generation state: ×2.0 frame generation 10.00%, "
            + "×3.0 frame generation 25.75%, ×4.0 frame generation 35.33%, No frame generation 28.50%, Not classified 0.42%");
        return 0;
    });

    [Fact]
    public void OneStateNoMixAndASessionThatWasNotHookedShowNoBlock()
    {
        FgMixPresentation.Rows(Hooked(mix: Mix(new FgMixRow(FgMixKind.Generated, 2.0, 60, 120, 600, 1, 120))))
            .Should().BeEmpty("one state throughout is what the readout already says");
        FgMixPresentation.Rows(Hooked()).Should().BeEmpty("no mix stored");
        FgMixPresentation.Rows(Hooked(mix: "{not json")).Should().BeEmpty("a hand-edited column reads as none");
        FgMixPresentation.Rows(Hooked(mix: _owners, tier: CaptureTier.NotHooked)).Should().BeEmpty("a session that was not hooked measured nothing");
    }

    [Fact]
    public void AStateWithNoFactorAndAKindThisBuildDoesNotKnowReadAsNotClassified() => In("en", () =>
    {
        FgMixModel(new FgMixRecordRow("generated", null, 60, 120, 10, 0.5, 2)).Should().Be(("Not classified", FgMixKind.Unclassified));
        FgMixModel(new FgMixRecordRow("dynamic", 3.0, 60, 180, 10, 0.5, 2)).Should().Be(("Not classified", FgMixKind.Unclassified), "a token from a later writer");
        FgMixRecordRow.KindOf(null).Should().Be(FgMixKind.Unclassified);
        FgMixRecordRow.KindOf("none").Should().Be(FgMixKind.None);
        return 0;

        static (string, FgMixKind) FgMixModel(FgMixRecordRow stored)
        {
            FgMixRowModel row = FgMixPresentation.Row(stored);
            row.Rates.Should().BeEmpty();
            return (row.Label, row.Segment.Kind);
        }
    });

    [Fact]
    public void AShareTooSmallToShowReadsAsUnderOneHundredthAndNeverAsZero()
    {
        In("en", () => FgMixPresentation.ShareText(0.35333)).Should().Be("35.33%");
        In("en", () => FgMixPresentation.ShareText(0.00004)).Should().Be("<0.01%");
        In("en", () => FgMixPresentation.ShareText(0.00006)).Should().Be("0.01%");
        In("en", () => FgMixPresentation.ShareText(0)).Should().Be("0.00%");
        In("en", () => FgMixPresentation.ShareText(1)).Should().Be("100.00%");
        In("vi", () => FgMixPresentation.ShareText(0.2575)).Should().Be("25,75%", "the reader's own decimal separator");
        In("vi", () => FgMixPresentation.ShareText(0.00001)).Should().Be("<0,01%");
    }

    [Fact]
    public void ASessionWithoutAMixSaysWhyOnlyWhereOneCouldHaveBeenStored() => In("en", () =>
    {
        string notStored = Strings.FgMix_NotStored;
        FgMixPresentation.Missing(Hooked()).Should().Be(notStored, "a counted factor, recorded before the mix was stored");
        FgMixPresentation.Missing(Hooked(factor: null, refusal: "non_uniform")).Should().Be(notStored, "the count was refused only because the state changed");
        FgMixPresentation.Missing(Hooked(factor: null, refusal: "ambiguous_band")).Should().Be(notStored);
        FgMixPresentation.Missing(Hooked(factor: null, refusal: "not_counted")).Should().BeNull("the readout names that refusal; a mix was never possible");
        FgMixPresentation.Missing(Hooked(fgMode: "none", factor: null)).Should().BeNull("no frame generation");
        FgMixPresentation.Missing(Hooked(fgMode: "na", factor: null)).Should().BeNull("not measured");
        FgMixPresentation.Missing(Hooked(tier: CaptureTier.NotHooked)).Should().BeNull("not hooked");
        FgMixPresentation.Missing(Hooked(mix: _owners)).Should().BeNull("it has one");
        notStored.Should().Contain("0.1.0-beta.24");
        return 0;
    });

    [Fact]
    public void TheExportLineIsTheSameInEveryLanguage()
    {
        string line = In("vi", () => FgMixPresentation.ExportLine(Hooked(mix: _owners)));
        line.Should().Be("generated x2.00 10.00% native 62.0 displayed 124.0 60.0 s; generated x3.00 25.75% native 60.0 displayed 180.0 154.5 s; "
            + "generated x4.00 35.33% native 70.5 displayed 282.0 212.0 s; none 28.50% native 118.0 displayed 118.0 171.0 s; unclassified 0.42% 2.5 s");
        FgMixPresentation.ExportLine(Hooked()).Should().Be("N/A");
    }

    [Fact]
    public void TheBarDrawsEachShareInProportionWithAGapBetweenTwoSegments()
    {
        FgMixSegment[] segments = [new(FgMixKind.Generated, 2.0, 0.25), new(FgMixKind.None, null, 0), new(FgMixKind.Generated, 4.0, 0.75)];
        IReadOnlyList<Rect> rects = FgMixBar.Layout(segments, 400 + FgMixBar.Gap, 14);

        rects[0].Should().Be(new Rect(0, 0, 100, 14));
        rects[1].Width.Should().Be(0, "a share of nothing draws nothing and takes no gap");
        rects[2].Should().Be(new Rect(100 + FgMixBar.Gap, 0, 300, 14));
        FgMixBar.Layout([new(FgMixKind.None, null, 1)], 12, 12).Should().Equal(new Rect(0, 0, 12, 12));
        FgMixBar.Layout([new(FgMixKind.None, null, 0)], 100, 14).Should().OnlyContain(static r => r.Width == 0);
        FgMixBar.Layout(segments, 0, 14).Should().OnlyContain(static r => r.Width == 0, "a bar not yet laid out");
        FgMixBar.Layout([], 100, 14).Should().BeEmpty();
    }

    [Fact]
    public void AStateTakesItsMultipliersColourSoTheSameFactorReadsTheSameInEverySession()
    {
        ScottPlot.Color[] c = [.. Enumerable.Range(1, 16).Select(static i => ScottPlot.Color.FromARGB((uint)(0xFF000000 | i)))];
        var p = new ChartPalette(c[0], c[1], c[2], c[3], c[4], c[5], c[6], c[7], c[8], c[9], c[10], c[11], c[12], c[13], c[14], c[15]);

        p.MixFill(FgMixKind.Generated, 1.6).Should().Be(p.Native, "an FSR-style ×1.6 is the ×2 family");
        p.MixFill(FgMixKind.Generated, 2.0).Should().Be(p.Native);
        p.MixFill(FgMixKind.Generated, 2.6).Should().Be(p.Sensor);
        p.MixFill(FgMixKind.Generated, 3.0).Should().Be(p.Sensor);
        p.MixFill(FgMixKind.Generated, 4.0).Should().Be(p.Displayed);
        p.MixFill(FgMixKind.Generated, 5.0).Should().Be(p.SensorSecondary);
        p.MixFill(FgMixKind.Generated, 6.0).Should().Be(p.Percentile);
        p.MixFill(FgMixKind.Generated, 8.0).Should().Be(p.SensorTertiary);
        p.MixFill(FgMixKind.None, null).Should().Be(p.MixNone);
        p.MixFill(FgMixKind.Unclassified, null).Should().Be(p.MixUnclassified);
        p.ByKey("Chart_MixNone").Should().Be(p.MixNone);
        p.ByKey("Chart_MixUnclassified").Should().Be(p.MixUnclassified);
        Enumerable.Range(0, 80).Select(i => p.MixFill(FgMixKind.Generated, 1.5 + (i * 0.1)))
            .Should().NotContain(fill => fill == p.Stutter || fill == p.StutterPso, "a state is never drawn in the stutter colours");
    }
}
