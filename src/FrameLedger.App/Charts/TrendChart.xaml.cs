// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>
/// The trend: one point per session, oldest first, with FR-6.3's hardware-change markers. Since beta.11 (owner decision D39)
/// it draws several metrics at once — every line in the first unit against the left axis, a second unit against the right —
/// and a click near a session's points raises <see cref="SessionClicked"/>, which the Sessions tab's overview uses to select
/// that session. Since beta.30 (owner decision D70) a marker is a dashed line with a numbered badge above the data area
/// (<see cref="ChangeMarkerPlottable"/>), its day's changes listed under the chart by the page, and the same text in a
/// tooltip near the line or the badge.
/// </summary>
public partial class TrendChart : UserControl
{
    /// <summary>How near a click must land to a session's points, or the pointer to a marker's line, in device-independent pixels.</summary>
    private const double _clickReach = 14;

    private readonly ToolTip _tip;
    private readonly TextBlock _tipText;
    private TrendPoint[] _all = [];
    private ChangeMarkerPlottable? _markers;

    public TrendChart()
    {
        InitializeComponent();
        ChartTheme.Attach(Plot);
        Plot.MouseLeftButtonUp += OnPlotClicked;

        // A code-made text takes the theme's brush by reference: WPF UI leaves a plain TextBlock's foreground black.
        _tipText = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 480 };
        _tipText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        _tip = new ToolTip { Content = _tipText, Placement = PlacementMode.Relative, PlacementTarget = Plot };
        Plot.MouseMove += OnPlotMouseMove;
        Plot.MouseLeave += (_, _) => CloseTip();
        // A press pans or zooms, and a wheel scrolls the page under a popup that would not follow it: either closes the tip.
        // ChartWheel marks a plain wheel handled, hence handledEventsToo.
        Plot.AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler((_, _) => CloseTip()), handledEventsToo: true);
        Plot.AddHandler(PreviewMouseWheelEvent, new MouseWheelEventHandler((_, _) => CloseTip()), handledEventsToo: true);
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is false)
            {
                CloseTip();
            }
        };
        Unloaded += (_, _) => CloseTip();
    }

    /// <summary>The session whose points a click landed on (<see cref="TrendPoint.SessionId"/>).</summary>
    public event EventHandler<SessionClickedEventArgs>? SessionClicked;

    public ScottPlot.Plot ScottPlot => Plot.Plot;

    public int DrawnPoints { get; private set; }

    /// <summary>How many of <see cref="DrawnPoints"/> were drawn hollow (a part of their session).</summary>
    public int HollowPoints { get; private set; }

    /// <summary>How many lines were drawn against the right axis (a second unit).</summary>
    public int RightAxisLines { get; private set; }

    /// <summary>The change markers on the chart, in the numbering the page's list shares; empty when no point was drawn.</summary>
    public IReadOnlyList<ChangeMarker> Markers { get; private set; } = [];

    /// <summary>The badges of the last render (beta.30), for a test to measure.</summary>
    internal IReadOnlyList<ChangeMarkerPlottable.DrawnBadge> Badges => _markers?.LastBadges ?? [];

    /// <summary>One metric — what the Trend drew before beta.11.</summary>
    public void Show(IReadOnlyList<TrendPoint> points, IReadOnlyList<ChangeMarker> changes, string metricLabel) =>
        Show([new TrendLine(metricLabel, metricLabel, points)], changes);

    /// <summary>
    /// Every line in <paramref name="lines"/>: the first line's unit on the left axis, the first other unit on the right,
    /// and a line in any further unit is not drawn (the selector allows two).
    /// </summary>
    public void Show(IReadOnlyList<TrendLine> lines, IReadOnlyList<ChangeMarker> changes)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(changes);
        CloseTip();
        ScottPlot.Plot plot = Plot.Plot;
        plot.Clear();
        // The date axis REPLACES the bottom axis, so it goes first (beta.8): made after the theme and the label, it dropped
        // both — an unlabelled axis in the default colours, unreadable in the dark theme.
        plot.Axes.DateTimeTicksBottom();
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Trend_Axis_Date);
        string? left = lines.Count > 0 ? lines[0].Unit : null;
        string? right = lines.Select(static l => l.Unit).FirstOrDefault(u => !string.Equals(u, left, StringComparison.Ordinal));
        plot.YLabel(left ?? string.Empty);
        plot.Axes.Right.Label.Text = right ?? string.Empty;

        List<TrendPoint> drawn = AddLines(plot, lines, left, right);
        _all = [.. drawn];
        DrawnPoints = drawn.Count;
        _markers = null;
        if (drawn.Count > 0)
        {
            AddMarkers(plot, changes);
            plot.Axes.AutoScale();
            plot.ShowLegend(Alignment.UpperLeft);
        }

        // The band the badges stand in, measured once: no markers, no band — Clear removes plottables, not the axis's size.
        plot.Axes.Top.MinimumSize = _markers?.BandHeight() ?? 0;
        Markers = _markers is null ? [] : changes;
        Plot.Refresh();
    }

    /// <summary>The text of the markers under <paramref name="at"/> (device pixels of the plot), within <paramref name="reach"/>; null when none.</summary>
    internal string? ToolTipAt(Pixel at, float reach)
    {
        if (_markers is null)
        {
            return null;
        }

        // The plottable's geometry is in the plot's own units; a ScaleFactor other than 1 (none is set today) scales both.
        float scale = (float)Plot.Plot.ScaleFactor;
        IReadOnlyList<ChangeMarker> hit = _markers.HitTest(new Pixel(at.X / scale, at.Y / scale), reach / scale);
        return hit.Count == 0 ? null : string.Join(Environment.NewLine + Environment.NewLine, hit.Select(static m => m.ToolTipText));
    }

    /// <summary>Every line, its points and hollow points; the points drawn, and the counts the tests read.</summary>
    private List<TrendPoint> AddLines(ScottPlot.Plot plot, IReadOnlyList<TrendLine> lines, string? left, string? right)
    {
        ChartPalette p = ChartTheme.Current;
        Color[] palette = [p.Native, p.Displayed, p.Percentile, p.Sensor, p.SensorSecondary, p.Stutter, p.Histogram, p.Segment];
        var drawn = new List<TrendPoint>();
        int hollow = 0;
        int onRight = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            TrendLine line = lines[i];
            bool isRight = right is not null && string.Equals(line.Unit, right, StringComparison.Ordinal);
            if (line.Points.Count == 0 || (!isRight && !string.Equals(line.Unit, left, StringComparison.Ordinal)))
            {
                continue;
            }

            hollow += AddLine(plot, line, palette[i % palette.Length], p.Data, isRight);
            onRight += isRight ? 1 : 0;
            drawn.AddRange(line.Points);
        }

        HollowPoints = hollow;
        RightAxisLines = onRight;
        return drawn;
    }

    /// <summary>The day markers (beta.30, D70), on the date axis <see cref="Show(IReadOnlyList{TrendLine}, IReadOnlyList{ChangeMarker})"/> made.</summary>
    private void AddMarkers(ScottPlot.Plot plot, IReadOnlyList<ChangeMarker> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        _markers = new ChangeMarkerPlottable(changes);
        _markers.Axes.XAxis = plot.Axes.Bottom;
        _markers.Axes.YAxis = plot.Axes.Left;
        plot.Add.Plottable(_markers);
    }

    /// <summary>One line, its hollow points, against the left or the right axis; how many were hollow.</summary>
    private static int AddLine(ScottPlot.Plot plot, TrendLine line, Color color, Color hollowColor, bool isRight)
    {
        double[] xs = [.. line.Points.Select(static t => t.At.LocalDateTime.ToOADate())];
        double[] ys = [.. line.Points.Select(static t => t.Value)];
        Scatter scatter = plot.Add.Scatter(xs, ys, color);
        scatter.LineWidth = 2;
        scatter.MarkerSize = 7;
        scatter.LegendText = isRight ? line.Label + " ▸" : line.Label;
        if (isRight)
        {
            scatter.Axes.YAxis = plot.Axes.Right;
        }

        // A point that describes part of its session (settings changed, or a steady state) is drawn hollow over its filled
        // one: the line still passes through it, and it cannot be read as the session's own number.
        TrendPoint[] partial = [.. line.Points.Where(static t => t.SettingsChangedMidSession)];
        if (partial.Length > 0)
        {
            double[] hx = [.. partial.Select(static t => t.At.LocalDateTime.ToOADate())];
            double[] hy = [.. partial.Select(static t => t.Value)];
            Scatter dots = plot.Add.ScatterPoints(hx, hy, hollowColor);
            dots.MarkerSize = 6;
            dots.MarkerShape = MarkerShape.FilledCircle;
            if (isRight)
            {
                dots.Axes.YAxis = plot.Axes.Right;
            }
        }

        return partial.Length;
    }

    /// <summary>
    /// The pointer in the plot's device pixels and the reach in the same pixels, from the element's DPI now: the control's
    /// <c>DisplayScale</c> is read once, when it is made, before it has a window — another monitor's DPI never reached it.
    /// </summary>
    private (Pixel At, float Reach) PlotPointer(MouseEventArgs e) =>
        (Plot.GetPlotPixelPosition(e), (float)(_clickReach * System.Windows.Media.VisualTreeHelper.GetDpi(Plot).DpiScaleX));

    /// <summary>The session whose points are nearest the click along the date axis, if any is within <see cref="_clickReach"/>.</summary>
    private void OnPlotClicked(object sender, MouseButtonEventArgs e)
    {
        if (_all.Length == 0 || SessionClicked is null)
        {
            return;
        }

        (Pixel at, float reach) = PlotPointer(e);
        ScottPlot.Plot plot = Plot.Plot;
        TrendPoint? nearest = null;
        double best = double.MaxValue;
        foreach (TrendPoint point in _all)
        {
            double distance = Math.Abs(plot.GetPixel(new Coordinates(point.At.LocalDateTime.ToOADate(), 0)).X - at.X);
            if (distance < best)
            {
                best = distance;
                nearest = point;
            }
        }

        if (nearest is { } hit && best <= reach)
        {
            SessionClicked.Invoke(this, new SessionClickedEventArgs(hit.SessionId));
        }
    }

    /// <summary>The marker under the pointer, as a tooltip beside it; nothing while a button is held (a pan or a zoom).</summary>
    private void OnPlotMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed)
        {
            CloseTip();
            return;
        }

        (Pixel at, float reach) = PlotPointer(e);
        string? text = ToolTipAt(at, reach);
        if (text is null)
        {
            CloseTip();
            return;
        }

        if (_tip.IsOpen && string.Equals(_tipText.Text, text, StringComparison.Ordinal))
        {
            return;   // the same marker: the tip stays where it opened
        }

        Point position = e.GetPosition(Plot);
        _tipText.Text = text;
        _tip.HorizontalOffset = position.X + 12;
        _tip.VerticalOffset = position.Y + 18;
        _tip.IsOpen = true;
    }

    private void CloseTip() => _tip.IsOpen = false;
}
