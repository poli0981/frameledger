using FluentAssertions;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Domain.Tests.Metrics;

public sealed class UpscalerNamesTests
{
    [Theory]
    [InlineData("1", "Performance")]
    [InlineData("2", "Balanced")]
    [InlineData("3", "Quality")]
    [InlineData("4", "Ultra Performance")]
    [InlineData("5", "Ultra Quality")]
    [InlineData("6", "DLAA")]
    public void ADlssModeIsTheNameTheMenuUses(string stored, string name) =>
        UpscalerNames.Quality("dlss", stored).Should().Be(name, "sl::DLSSMode, as fl_sl_inputs.h QualityFromMode writes it");

    [Theory]
    [InlineData("dlss", "7")]
    [InlineData("dlss", "0")]
    [InlineData("fsr3", "2")]
    [InlineData("xess", "3")]
    [InlineData(null, "2")]
    public void AValueWithNoNameIsShownAsStored(string? upscaler, string stored) =>
        UpscalerNames.Quality(upscaler, stored).Should().Be(stored, "a number nobody can name is still what was measured");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void NoQualityIsNoName(string? stored) =>
        UpscalerNames.Quality("dlss", stored).Should().BeNull();
}
