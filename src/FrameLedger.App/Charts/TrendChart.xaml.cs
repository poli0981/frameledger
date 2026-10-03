// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Controls;
using System.Windows.Input;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>
/// The trend: one point per session, oldest first, with FR-6.3's hardware-change markers as labelled vertical lines. Since
/// beta.11 (owner decision D39) it draws several metrics at once — every line in the first unit against the left axis, a
/// second unit against the right — and a click near a session's points raises <see cref="SessionClicked"/>, which the
/// Sessions tab's overview uses to select that session.
/// </summary>
public partial class TrendChart : UserControl
{
    /// <summary>How near a click must land to a session's points, in device-independent pixels.</summary>
    private const double _clickReach = 14;

    private TrendPoint[] _all = [];

    public TrendChart()
    {
        InitializeComponent();
        ChartTheme.Attach(Plot);
        Plot.MouseLeftButtonUp += OnPlotClicked;
    }

    /// <summary>The session whose points a click landed on (<see cref="TrendPoint.SessionId"/>).</summary>
    public event EventHandler<SessionClickedEventArgs>? SessionClicked;

    public ScottPlot.Plot ScottPlot => Plot.Plot;

    public int DrawnPoints { get; private set; }

    /// <summary>How many of <see cref="DrawnPoints"/> were drawn hollow (a part of their session).</summary>
    public int HollowPoints { get; private set; }

    /// <summary>How many lines were drawn against the right axis (a second unit).</summary>
    public int RightAxisLines { get; private set; }

    /// <summary>One metric — what the Trend drew before beta.11.</summary>
    public void Show(IReadOnlyList<TrendPoint> points, IReadOnlyList<HardwareChange> changes, string metricLabel) =>
        Show([new TrendLine(metricLabel, metricLabel, points)], changes);

    /// <summary>
    /// Every line in <paramref name="lines"/>: the first line's unit on the left axis, the first other unit on the right,
    /// and a line in any further unit is not drawn (the selector allows two).
    /// </summary>
    public void Show(IReadOnlyList<TrendLine> lines, IReadOnlyList<HardwareChange> changes)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(changes);
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

        _all = [.. drawn];
        DrawnPoints = drawn.Count;
        HollowPoints = hollow;
        RightAxisLines = onRight;
        if (drawn.Count > 0)
        {
            foreach (HardwareChange change in TrendSeriesBuilder.MergedByDay(changes))
            {
                VerticalLine marker = plot.Add.VerticalLine(change.At.LocalDateTime.ToOADate(), 1, p.StutterPso, LinePattern.Dashed);
                marker.LabelText = change.Text;
                marker.LabelFontSize = 11;
                marker.LabelOppositeAxis = true;
            }

            plot.Axes.AutoScale();
            plot.ShowLegend(Alignment.UpperLeft);
        }

        Plot.Refresh();
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

    /// <summary>The session whose points are nearest the click along the date axis, if any is within <see cref="_clickReach"/>.</summary>
    private void OnPlotClicked(object sender, MouseButtonEventArgs e)
    {
        if (_all.Length == 0 || SessionClicked is null)
        {
            return;
        }

        System.Windows.Point at = e.GetPosition(Plot);
        float scale = Plot.DisplayScale;
        double clickX = at.X * scale;
        ScottPlot.Plot plot = Plot.Plot;
        TrendPoint? nearest = null;
        double best = double.MaxValue;
        foreach (TrendPoint point in _all)
        {
            double distance = Math.Abs(plot.GetPixel(new Coordinates(point.At.LocalDateTime.ToOADate(), 0)).X - clickX);
            if (distance < best)
            {
                best = distance;
                nearest = point;
            }
        }

        if (nearest is { } hit && best <= _clickReach * scale)
        {
            SessionClicked.Invoke(this, new SessionClickedEventArgs(hit.SessionId));
        }
    }
}
