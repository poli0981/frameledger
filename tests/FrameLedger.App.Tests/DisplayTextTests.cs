using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Domain.Display;

namespace FrameLedger.App.Tests;

/// <summary>
/// The display facts as the pages say them (beta.10, <c>03_METRICS</c> §Display mode): never more certain than the source,
/// a share that was there never rounded to nothing, and N/A for a row that has none.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class DisplayTextTests
{
    private static T InEnglish<T>(Func<T> read)
    {
        CultureInfo? previous = Strings.Culture;
        CultureInfo current = CultureInfo.CurrentCulture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            return read();
        }
        finally
        {
            Strings.Culture = previous;
            CultureInfo.CurrentCulture = current;
        }
    }

    [Fact]
    public void TheHeadlineIsTheModeShownLongestWithItsShare() => InEnglish(() =>
    {
        DisplayText.Headline(new DisplaySummary { ExclusiveMs = 30_000, BorderlessMs = 70_000 }).Should().Be("Borderless 70%");
        DisplayText.Headline(new DisplaySummary { CoversMs = 10_000 }).Should().Be("Fullscreen or borderless 100%");
        DisplayText.Headline(new DisplaySummary { MinimizedMs = 5_000 }).Should().Be("Minimized 100%", "a session spent minimised says so rather than 'no window'");
        DisplayText.Headline(new DisplaySummary { NoWindowMs = 5_000 }).Should().Be("No window read");
        DisplayText.Headline(null).Should().Be("N/A");
        return 0;
    });

    [Fact]
    public void SharesAreSaidFullscreenEndFirstAndATraceIsNeverRoundedAway() => InEnglish(() =>
    {
        var display = new DisplaySummary { WindowedMs = 99_800, ExclusiveMs = 200 };

        DisplayText.Shares(display).Should().Be("Exclusive fullscreen <1% · Windowed 100%");
        DisplayText.Shares(null).Should().Be("N/A");
        return 0;
    });

    [Fact]
    public void TheSizesAreSaidOnlyWhenTheyWereRead() => InEnglish(() =>
    {
        var display = new DisplaySummary { WindowedMs = 1, WindowWidth = 1280, WindowHeight = 720, MonitorWidth = 2560, MonitorHeight = 1440 };

        DisplayText.Window(display).Should().Be("1280×720");
        DisplayText.Monitor(display).Should().Be("2560×1440", "no refresh rate was read");
        DisplayText.Monitor(display with { MonitorHz = 60 }).Should().Be("2560×1440 @ 60 Hz");
        DisplayText.Window(new DisplaySummary()).Should().Be("N/A");
        DisplayText.Monitor(null).Should().Be("N/A");
        return 0;
    });

    [Fact]
    public void TheLineNamesWhatTheFactsRestedOn() => InEnglish(() =>
    {
        var display = new DisplaySummary
        {
            ExclusiveMs = 60_000,
            Changes = 3,
            Source = DisplaySource.SwapChain,
            BufferWidth = 3840,
            BufferHeight = 2160,
            SwapEffect = "flip_discard",
        };

        DisplayText.Line(display).Should().Be("Display: Exclusive fullscreen 100% · changed 3 times · back buffer 3840×2160 (flip_discard)"
            + " · the swap chain said whether it was exclusive");
        DisplayText.Line(null).Should().BeNull();
        return 0;
    });

    [Fact]
    public void TheExportLineIsInvariant()
    {
        CultureInfo current = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            DisplayText.ExportLine(new DisplaySummary { WindowedMs = 1_234_567, MonitorWidth = 1920, MonitorHeight = 1080 })
                .Should().Be("exclusive_ms=0; borderless_ms=0; covers_ms=0; windowed_ms=1234567; minimized_ms=0; nowindow_ms=0; changes=0; source=n/a;"
                    + " window=n/a; buffer=n/a; monitor=1920x1080");
        }
        finally
        {
            CultureInfo.CurrentCulture = current;
        }
    }
}
