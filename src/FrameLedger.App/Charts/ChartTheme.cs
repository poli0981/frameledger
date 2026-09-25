using ScottPlot;
using ScottPlot.WPF;
using Wpf.Ui.Appearance;

namespace FrameLedger.App.Charts;

/// <summary>
/// <c>16_WPFUI_SYNTAX</c> §ScottPlot theme sync: on startup and on <c>ApplicationThemeManager.Changed</c>, every
/// live plot gets the current palette's figure/data backgrounds, axis, grid and legend colours, then refreshes.
/// Pages never colour plots ad hoc — they call <see cref="Attach"/> once and <see cref="Apply"/> after loading data.
/// </summary>
public static class ChartTheme
{
    private static readonly List<WeakReference<WpfPlot>> _live = [];
    private static readonly Lock _lock = new();
    private static bool _subscribed;

    /// <summary>The palette of the theme WPF UI has applied.</summary>
    public static ChartPalette Current => ChartPalette.For(ApplicationThemeManager.GetAppTheme());

    /// <summary>Colours one plot for the current theme; the series keep their own palette colours.</summary>
    public static void Apply(Plot plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        ChartPalette p = Current;
        plot.FigureBackground.Color = p.Figure;
        plot.DataBackground.Color = p.Data;
        plot.Axes.Color(p.Axis);
        plot.Grid.MajorLineColor = p.Grid;
        plot.Legend.BackgroundColor = p.Data;
        plot.Legend.OutlineColor = p.Grid;
        plot.Legend.FontColor = p.Axis;
    }

    /// <summary>
    /// Save the plot as a PNG with an OPAQUE background (beta.8): on screen the figure is transparent over its card, but an
    /// exported image has no card behind it, and a viewer painted its own colour under the theme's text — white axis labels
    /// on a white page. The window's colour is painted for the file and the plot restored.
    /// </summary>
    public static void SavePng(Plot plot, string path, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(plot);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Color figure = plot.FigureBackground.Color;
        Color data = plot.DataBackground.Color;
        Color window = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Light ? Color.FromHex("#F3F3F3") : Color.FromHex("#202020");
        try
        {
            plot.FigureBackground.Color = Over(figure, window);
            plot.DataBackground.Color = Over(data, Over(figure, window));
            plot.SavePng(path, width, height);
        }
        finally
        {
            plot.FigureBackground.Color = figure;
            plot.DataBackground.Color = data;
        }
    }

    /// <summary>Source-over: <paramref name="top"/> composited on an opaque <paramref name="under"/>.</summary>
    internal static Color Over(Color top, Color under)
    {
        double a = top.A / 255.0;
        return new Color(
            (byte)Math.Round((top.R * a) + (under.R * (1 - a))),
            (byte)Math.Round((top.G * a) + (under.G * (1 - a))),
            (byte)Math.Round((top.B * a) + (under.B * (1 - a))));
    }

    /// <summary>Registers a control so a theme change re-applies and refreshes it (weakly held; a closed window is forgotten).</summary>
    public static void Attach(WpfPlot control)
    {
        ArgumentNullException.ThrowIfNull(control);
        lock (_lock)
        {
            _live.RemoveAll(static w => !w.TryGetTarget(out _));
            _live.Add(new WeakReference<WpfPlot>(control));
            if (!_subscribed)
            {
                ApplicationThemeManager.Changed += OnThemeChanged;
                _subscribed = true;
            }
        }

        Apply(control.Plot);
    }

    private static void OnThemeChanged(ApplicationTheme theme, System.Windows.Media.Color accent)
    {
        WpfPlot[] plots;
        lock (_lock)
        {
            plots = [.. _live.Select(static w => w.TryGetTarget(out WpfPlot? p) ? p : null).OfType<WpfPlot>()];
        }

        foreach (WpfPlot plot in plots)
        {
            Apply(plot.Plot);
            plot.Refresh();
        }
    }
}
