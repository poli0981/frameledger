// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Domain.Tests.Detection;

/// <summary>D64 (beta.24): a DLSS generation only from what the session observed, strongest evidence first.</summary>
public sealed class DlssGenerationTests
{
    private static readonly Version _dlss4Library = new(310, 9, 1, 0);

    [Theory]
    [InlineData(12u, DlssGenerationKind.Dlss45)]
    [InlineData(13u, DlssGenerationKind.Dlss45)]
    [InlineData(10u, DlssGenerationKind.Dlss4)]
    [InlineData(11u, DlssGenerationKind.Dlss4)]
    public void ThePresetTheDriverReportsNamesTheModelsGeneration(uint preset, DlssGenerationKind kind) =>
        DlssGeneration.Of(new DlssEvidence(_dlss4Library, null, preset, null)).Should().Be(new DlssGeneration(kind, DlssBasis.Preset));

    [Theory]
    [InlineData(6.0, DlssGenerationKind.Dlss45)]
    [InlineData(5.0, DlssGenerationKind.Dlss45)]
    [InlineData(4.0, DlssGenerationKind.Dlss4)]
    [InlineData(3.0, DlssGenerationKind.Dlss4)]
    public void AMeasuredMultiFrameStateNamesItsGeneration(double factor, DlssGenerationKind kind) =>
        DlssGeneration.Of(new DlssEvidence(null, _dlss4Library, null, factor)).Should().Be(new DlssGeneration(kind, DlssBasis.FrameGenerationFactor));

    [Fact]
    public void TheStrongerEvidenceWins()
    {
        DlssGeneration.Of(new DlssEvidence(_dlss4Library, _dlss4Library, 11, 6.0)).Should().Be(new DlssGeneration(DlssGenerationKind.Dlss45, DlssBasis.FrameGenerationFactor),
            "a ×6 state is DLSS 4.5's even under a DLSS 4 preset");
        DlssGeneration.Of(new DlssEvidence(_dlss4Library, _dlss4Library, 12, 4.0)).Should().Be(new DlssGeneration(DlssGenerationKind.Dlss45, DlssBasis.Preset));
        DlssGeneration.Of(new DlssEvidence(new Version(3, 7, 10, 0), null, 10, null)).Should().Be(new DlssGeneration(DlssGenerationKind.Dlss4, DlssBasis.Preset),
            "the driver's override can run a newer model than the game's library");
    }

    [Fact]
    public void ALibraryVersionNamesAGenerationOnlyWhereItsMajorNumberDoes()
    {
        DlssGeneration.Of(new DlssEvidence(new Version(2, 5, 1, 0), null, null, null)).Should().Be(new DlssGeneration(DlssGenerationKind.Dlss2, DlssBasis.LibraryVersion));
        DlssGeneration.Of(new DlssEvidence(new Version(3, 7, 10, 0), null, null, 2.0)).Should().Be(new DlssGeneration(DlssGenerationKind.Dlss3, DlssBasis.LibraryVersion));
        DlssGeneration.Of(new DlssEvidence(null, new Version(1, 0, 3, 0), null, 2.0)).Should().Be(new DlssGeneration(DlssGenerationKind.Dlss3, DlssBasis.LibraryVersion));
        DlssGeneration.Of(new DlssEvidence(_dlss4Library, _dlss4Library, null, 2.0)).Should().BeNull("310.x is DLSS 4's and 4.5's alike, and ×2 is any generation's");
        DlssGeneration.Of(new DlssEvidence(_dlss4Library, null, 5, null)).Should().BeNull("a CNN preset names no generation");
        DlssGeneration.Of(new DlssEvidence(null, null, null, null)).Should().BeNull();
        FluentActions.Invoking(static () => DlssGeneration.Of(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void APresetIsALetterAndAModel()
    {
        DlssGeneration.PresetLetter(1).Should().Be('A');
        DlssGeneration.PresetLetter(12).Should().Be('L');
        DlssGeneration.PresetLetter(0).Should().BeNull();
        DlssGeneration.PresetLetter(27).Should().BeNull();
        DlssGeneration.PresetLetter(null).Should().BeNull();
        DlssGeneration.ModelOf(5).Should().Be(DlssModel.Cnn);
        DlssGeneration.ModelOf(11).Should().Be(DlssModel.Transformer);
        DlssGeneration.ModelOf(12).Should().Be(DlssModel.SecondGenerationTransformer);
        DlssGeneration.ModelOf(8).Should().BeNull("G–I are not placed");
        DlssGeneration.ModelOf(null).Should().BeNull();
    }
}
