using System.Windows;
using ScottPlot;
using Wpf.Ui.Appearance;

namespace FrameLedger.App.Charts;

/// <summary>
/// The chart colours for one theme, read from <c>Styles/ChartPalette.{Dark,Light}.xaml</c> — the one place hex
/// colours are allowed (<c>16_WPFUI_SYNTAX</c> §Theming), because ScottPlot draws with Skia and cannot read a
/// WPF brush. Loaded once per theme and cached.
/// </summary>
public sealed record ChartPalette(
    Color Figure,
    Color Data,
    Color Axis,
    Color Grid,
    Color Native,
    Color Displayed,
    Color Stutter,
    Color StutterPso,
    Color Sensor,
    Color SensorSecondary,
    Color Segment,
    Color Histogram,
    Color Percentile)
{
    /// <summary>The keys both dictionaries must carry, in the record's order.</summary>
    public static readonly IReadOnlyList<string> Keys =
    [
        "Chart_Figure", "Chart_Data", "Chart_Axis", "Chart_Grid", "Chart_Native", "Chart_Displayed", "Chart_Stutter",
        "Chart_StutterPso", "Chart_Sensor", "Chart_SensorSecondary", "Chart_Segment", "Chart_Histogram", "Chart_Percentile",
    ];

    private static readonly Dictionary<ApplicationTheme, ChartPalette> _cache = [];
    private static readonly Lock _lock = new();

    public static ChartPalette For(ApplicationTheme theme)
    {
        ApplicationTheme key = theme == ApplicationTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark;
        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out ChartPalette? palette))
            {
                palette = Load(key);
                _cache[key] = palette;
            }

            return palette;
        }
    }

    /// <summary>A palette from any dictionary carrying the keys (the tests hand in a parsed file).</summary>
    public static ChartPalette FromDictionary(ResourceDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        Color[] c = [.. Keys.Select(k => Convert(dictionary, k))];
        return new ChartPalette(c[0], c[1], c[2], c[3], c[4], c[5], c[6], c[7], c[8], c[9], c[10], c[11], c[12]);
    }

    private static ChartPalette Load(ApplicationTheme theme)
    {
        string file = theme == ApplicationTheme.Light ? "ChartPalette.Light.xaml" : "ChartPalette.Dark.xaml";
        var dictionary = new ResourceDictionary { Source = new Uri("pack://application:,,,/FrameLedger;component/Styles/" + file) };
        return FromDictionary(dictionary);
    }

    private static Color Convert(ResourceDictionary dictionary, string key)
    {
        if (dictionary[key] is not System.Windows.Media.Color color)
        {
            throw new InvalidOperationException($"chart palette: '{key}' is missing or not a Color");
        }

        return Color.FromARGB((uint)(color.A << 24 | color.R << 16 | color.G << 8 | color.B));
    }
}
