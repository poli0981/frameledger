using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Tests;

/// <summary>The page numbers as text, in English: durations, playtime, resolutions, the token → product-name maps, and N/A where a value is missing.</summary>
public sealed class FormatsTests
{
    private static T InEnglish<T>(Func<T> f)
    {
        CultureInfo? previous = Strings.Culture;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo("en");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
            return f();
        }
        finally
        {
            Strings.Culture = previous;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void DurationsAndPlaytime()
    {
        InEnglish(() => Formats.Duration(5025)).Should().Be("1h 23m");
        InEnglish(() => Formats.Duration(245)).Should().Be("4m 05s");
        InEnglish(() => Formats.Playtime(9000)).Should().Be("2.5 h");
        InEnglish(() => Formats.Playtime(1500)).Should().Be("25 min");
        InEnglish(() => Formats.Duration(-3)).Should().Be("0m 00s");
    }

    [Fact]
    public void ResolutionsUpscalersAndFrameGeneration()
    {
        InEnglish(() => Formats.Resolution(1485, 835, 2560, 1440)).Should().Be("1485×835 → 2560×1440");
        InEnglish(() => Formats.Resolution(2560, 1440, 2560, 1440)).Should().Be("2560×1440", "no upscale: one pair");
        InEnglish(() => Formats.Resolution(null, null, 2560, 1440)).Should().Be("2560×1440");
        InEnglish(() => Formats.Resolution(null, null, null, null)).Should().Be("N/A");
        Formats.Upscaler("dlss").Should().Be("DLSS");
        Formats.Upscaler("fsr3").Should().Be("FSR 3");
        InEnglish(() => Formats.Upscaler("none")).Should().Be("No upscaler");
        InEnglish(() => Formats.Upscaler(null)).Should().Be("N/A");
        Formats.FrameGeneration("dlssg").Should().Be("DLSS-G");
        InEnglish(() => Formats.FrameGeneration("active")).Should().Contain("not identified");
        InEnglish(() => Formats.FrameGeneration("na")).Should().Be("N/A");
        Formats.Api("d3d12").Should().Be("D3D12");
        InEnglish(() => Formats.Api("weird")).Should().Be("N/A");
    }

    [Fact]
    public void FpsAndTemperaturesAndExitStatus()
    {
        InEnglish(() => Formats.Fps(61.6)).Should().Be("62");
        InEnglish(() => Formats.Fps(null)).Should().Be("N/A");
        InEnglish(() => Formats.FpsOrDash(null)).Should().Be("—");
        InEnglish(() => Formats.Temperature(71.4)).Should().Be("71 °C");
        InEnglish(() => Formats.Temperature(null)).Should().Be("N/A");
        InEnglish(() => Formats.ExitStatusText(ExitStatus.Crashed)).Should().Be("crashed");
        InEnglish(() => Formats.ExitStatusText(ExitStatus.UnhookedSafety)).Should().Be("unhooked (safety)");
        Formats.Platform("steam").Should().Be("Steam");
        Formats.Platform("none").Should().BeEmpty();
    }
}
