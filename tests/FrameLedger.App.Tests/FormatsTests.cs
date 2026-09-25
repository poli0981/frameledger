using System.Globalization;
using System.IO;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Domain.Detection;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Tests;

/// <summary>The page numbers as text, in English: durations, playtime, resolutions, the token → product-name maps, and N/A where a value is missing.</summary>
[Collection(StringsCultureCollection.Name)]
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

    /// <summary>
    /// 03_METRICS §Upscaling's ladder as the UI states it (2026-09-21): a hook's name outranks the driver's, the driver's
    /// name says it is the driver's and never takes a quality, and a DLSS quality byte is the preset's name.
    /// </summary>
    [Fact]
    public void TheUpscalerLadderNamesTheDriverOnlyWhereNoHookDid()
    {
        InEnglish(() => Formats.Upscaler("unknown", null, "dlss")).Should().Be("DLSS (driver-reported)", "an NGX-direct title: the hook ran and saw nothing, the driver saw the feature evaluated");
        InEnglish(() => Formats.Upscaler(null, null, "dlss")).Should().Be("DLSS (driver-reported)");
        InEnglish(() => Formats.Upscaler("unknown", "2", "dlss")).Should().Be("DLSS (driver-reported)", "the driver's word is identity only");
        InEnglish(() => Formats.Upscaler("fsr3", null, "dlss")).Should().Be("FSR 3", "a hook's name always outranks the driver's");
        InEnglish(() => Formats.Upscaler("dlss", "2", "dlss")).Should().Be("DLSS Balanced");
        InEnglish(() => Formats.Upscaler("dlss", "6", null)).Should().Be("DLSS DLAA");
        InEnglish(() => Formats.Upscaler("dlss", "9", null)).Should().Be("DLSS 9", "a value with no name is shown as stored");
        InEnglish(() => Formats.Upscaler("unknown", null, null)).Should().Be("Upscaler not identified");
        InEnglish(() => Formats.Upscaler("unknown", "2", null)).Should().Be("Upscaler not identified", "a quality without a name to hang it on is not printed");
        InEnglish(() => Formats.Upscaler("none", null, null)).Should().Be("No upscaler");
        InEnglish(() => Formats.Upscaler(null, null, null)).Should().Be("N/A");
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

    /// <summary>The engine ids as names (2026-09-23): proper nouns in every language; an id this table does not know passes through.</summary>
    /// <summary>beta.8: what an executable runs as, in words — an id this build does not know reads as "could not be read".</summary>
    [Fact]
    public void ArchitecturesAreSaidInWords() => InEnglish(() =>
    {
        Formats.Architecture(ExecutableArchitecture.X64).Should().Be("64-bit (x64)");
        Formats.Architecture(ExecutableArchitecture.X86).Should().Be("32-bit (x86)");
        Formats.Architecture(ExecutableArchitecture.AnyCpu).Should().Be(".NET, any CPU (runs as 64-bit)");
        Formats.Architecture(ExecutableArchitecture.AnyCpu32).Should().Contain("runs as 32-bit");
        Formats.Architecture(ExecutableArchitecture.Arm64).Should().Be("ARM64");
        Formats.Architecture(ExecutableArchitecture.Other).Should().Be("another architecture");
        Formats.Architecture(ExecutableArchitecture.Unknown).Should().Be("could not be read");
        Formats.Architecture("mips").Should().Be("could not be read");
        return 0;
    });

    /// <summary>beta.8: a store's version is said as one; a version the user typed is shown as typed; none is none.</summary>
    [Fact]
    public void AStoreVersionIsLabelledWithItsStore() => InEnglish(() =>
    {
        Formats.StoreVersion("steam", "20374416", detected: true).Should().Be("Steam build 20374416");
        Formats.StoreVersion("gog", "1.2.3", detected: true).Should().Be("GOG version 1.2.3");
        Formats.StoreVersion("epic", "++Fortnite+Release-31.10", detected: true).Should().Be("Epic version ++Fortnite+Release-31.10");
        Formats.StoreVersion("itch", "0.9", detected: true).Should().Be("0.9", "itch has no store version to label");
        Formats.StoreVersion("steam", "1.04 (my patch)", detected: false).Should().Be("1.04 (my patch)", "the user's words are theirs");
        Formats.StoreVersion("steam", "  ", detected: true).Should().BeNull();
        return 0;
    });

    [Theory]
    [InlineData("3.7.10.0", "3.7.10.0", true)]
    [InlineData("2,8,0,0", "2.8.0.0", true)]
    [InlineData("3.7.10.0 built by: dlss-release", "3.7.10.0", true)]
    [InlineData("3.7.10", "3.7.10.0", true)]
    [InlineData("310.2.1.0", "3.7.10.0", false)]
    [InlineData(null, "3.7.10.0", null)]
    [InlineData("3.7.10.0", null, null)]
    [InlineData("n/a", "3.7.10.0", null)]
    public void TwoFileVersionsAreTheSameFourNumbers(string? a, string? b, bool? expected) =>
        Formats.SameVersion(a, b).Should().Be(expected);

    /// <summary>
    /// beta.8 (owner request): two decimals, always, when <c>ui.fps_decimals</c> is on; a whole number otherwise. Half away
    /// from zero both ways — 62.125 is exactly representable, and rounding half to even would say 62.12.
    /// </summary>
    [Fact]
    public void FpsHasTwoDecimalsWhenAskedAndRoundsHalfAwayFromZero() => InEnglish(() =>
    {
        Formats.FpsNumber(62.4, twoDecimals: true).Should().Be("62.40");
        Formats.FpsNumber(62, twoDecimals: true).Should().Be("62.00");
        Formats.FpsNumber(62.125, twoDecimals: true).Should().Be("62.13");
        Formats.FpsNumber(62.5, twoDecimals: false).Should().Be("63");
        Formats.FpsNumber(61.5, twoDecimals: false).Should().Be("62");
        Formats.FpsNumber(62.4, twoDecimals: false).Should().Be("62");
        return 0;
    });

    [Fact]
    public void EnginesAreNamedAndAnUnknownIdPassesThrough()
    {
        Formats.Engine("re_engine").Should().Be("RE Engine");
        Formats.Engine("fromsoftware").Should().Be("FromSoftware");
        Formats.Engine("rpgmaker_mv").Should().Be("RPG Maker MV/MZ");
        Formats.Engine("unreal").Should().Be("Unreal Engine");
        Formats.Engine("My Own Engine").Should().Be("My Own Engine", "an engine the user typed is shown as typed");
        Formats.Engine(null).Should().BeEmpty();
    }

    /// <summary>
    /// Every engine the shipped rules can detect has a name here, and it is the rule's own <c>name</c> (2026-09-23): a rule
    /// added without one would show its raw id on every card that engine lands on — which is what every engine did until
    /// this date.
    /// </summary>
    [Fact]
    public void EveryShippedEngineIdIsShownByItsRulesName()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FrameLedger.slnx")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the check needs the repository's rules file");
        using System.Text.Json.JsonDocument rules = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "rules", "detection-rules.json")));
        List<(string Id, string Name)> engines = [.. rules.RootElement.GetProperty("engines").EnumerateArray()
            .Select(static e => (e.GetProperty("id").GetString()!, e.GetProperty("name").GetString()!))];

        engines.Should().NotBeEmpty();
        engines.Should().AllSatisfy(static e => Formats.Engine(e.Id).Should().Be(e.Name, $"the rule '{e.Id}' names its engine"));
    }
}
