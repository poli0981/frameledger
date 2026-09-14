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
