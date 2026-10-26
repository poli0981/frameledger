// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using ScottPlot;

namespace FrameLedger.App.Charts;

/// <summary>
/// FR-6.3's markers on a trend chart since beta.30 (owner decision D70): a dashed line for each day that changed, and above
/// the data area, in the band <see cref="BandHeight"/> keeps free, a numbered badge for each — joined to its line, dark or
/// light text by contrast on the palette's orange, never cut off and never over another (<see cref="ChangeBadgeLayout"/>).
/// </summary>
/// <remarks>
/// It replaces ScottPlot's <c>VerticalLine</c> label, which grew up from the data area into a top panel nothing sized for
/// it (about 15 px for a 25 px box: the owner's report of 2026-10-26, the top of "Driver GPU: … → …" cut off). The lines are
/// drawn in <see cref="Render"/>, clipped to the data area; the badges in <see cref="RenderLast"/>, after the axes and
/// unclipped. The colours are read at every render, so a theme switch recolours them with the axes.
/// </remarks>
internal sealed class ChangeMarkerPlottable : IPlottable, IRenderLast
{
    internal const float FontSize = 12;

    /// <summary>Between two badges.</summary>
    internal const float Gap = 3;

    /// <summary>Between a badge and the data area: the badge's connector to its line starts here.</summary>
    internal const float Stem = 6;

    /// <summary>Above a badge, inside the band.</summary>
    internal const float Margin = 3;

    private static readonly PixelPadding _padding = new(5, 5, 2, 2);

    private readonly (double X, ChangeMarker Marker)[] _markers;
    private readonly LabelStyle _badge = new()
    {
        FontSize = FontSize,
        Bold = true,
        Alignment = Alignment.LowerCenter,
        PixelPadding = _padding,
        BorderRadius = 3,
        ShadowOffset = new PixelOffset(0, 0),
    };

    private readonly LineStyle _line = new(1, Colors.Black, LinePattern.Dashed);
    private readonly LineStyle _connector = new(1, Colors.Black);
    private ChangeBadgeLayout.Anchor[] _lines = [];
    private PixelRect _data;

    public ChangeMarkerPlottable(IReadOnlyList<ChangeMarker> markers)
    {
        ArgumentNullException.ThrowIfNull(markers);
        _markers = [.. markers.Select(static m => (m.At.LocalDateTime.ToOADate(), m))];
    }

    /// <summary>A badge as last drawn: its rectangle in the plot's pixels, its label, and the markers it stands for.</summary>
    internal sealed record DrawnBadge(PixelRect Rect, string Label, IReadOnlyList<ChangeMarker> Markers);

    public bool IsVisible { get; set; } = true;

    public IAxes Axes { get; set; } = new Axes();

    public IEnumerable<LegendItem> LegendItems => LegendItem.None;

    /// <summary>The badges of the last render, left to right.</summary>
    public IReadOnlyList<DrawnBadge> LastBadges { get; private set; } = [];

    /// <summary>The markers count toward the date axis's autoscale, as ScottPlot's lines did.</summary>
    public AxisLimits GetAxisLimits() => _markers.Length == 0
        ? AxisLimits.NoLimits
        : AxisLimits.HorizontalOnly(_markers.Min(static m => m.X), _markers.Max(static m => m.X));

    /// <summary>The height the badges need above the data area: a margin, a line of text and its padding, the stem.</summary>
    public float BandHeight()
    {
        using Paint paint = Paint.NewDisposablePaint();
        return MathF.Ceiling(Margin + _badge.Measure("0", paint).Height + _padding.Vertical + Stem);
    }

    public void Render(RenderPack rp)
    {
        ArgumentNullException.ThrowIfNull(rp);
        _line.Color = ChartTheme.Current.StutterPso;
        PixelRect data = rp.DataRect;
        var lines = new List<ChangeBadgeLayout.Anchor>(_markers.Length);
        foreach ((double x, ChangeMarker marker) in _markers)
        {
            float px = Axes.GetPixelX(x);
            if (!data.ContainsX(px))
            {
                continue;   // zoomed or panned out of the data area: no line, and no badge for it
            }

            _line.Render(rp.Canvas, new PixelLine(px, data.Top, px, data.Bottom), rp.Paint);
            lines.Add(new ChangeBadgeLayout.Anchor(px, marker));
        }

        _lines = [.. lines.OrderBy(static a => a.X)];
        _data = data;
    }

    public void RenderLast(RenderPack rp)
    {
        ArgumentNullException.ThrowIfNull(rp);
        Color fill = ChartTheme.Current.StutterPso;
        _badge.BackgroundColor = fill;
        _badge.ForeColor = ChartTheme.TextOn(fill);
        _connector.Color = fill;
        var widths = new Dictionary<string, float>(StringComparer.Ordinal);
        float WidthOf(string label)
        {
            if (!widths.TryGetValue(label, out float width))
            {
                width = _badge.Measure(label, rp.Paint).Width + _padding.Horizontal;
                widths[label] = width;
            }

            return width;
        }

        PixelRect data = rp.DataRect;
        rp.CanvasState.DisableClipping();
        float bottom = data.Top - Stem;
        LastBadges = [.. ChangeBadgeLayout.Place(_lines, data.Left, data.Right, Gap, WidthOf).Select(badge => Draw(rp, badge, bottom, data.Top))];
    }

    /// <summary>
    /// The markers under <paramref name="at"/> (the plot's pixels): a badge's, or else the one line within
    /// <paramref name="reach"/> along the date axis. Empty when nothing is near.
    /// </summary>
    internal IReadOnlyList<ChangeMarker> HitTest(Pixel at, float reach)
    {
        foreach (DrawnBadge badge in LastBadges)
        {
            if (badge.Rect.Expand(2).Contains(at))
            {
                return badge.Markers;
            }
        }

        if (_lines.Length == 0 || at.Y > _data.Bottom)
        {
            return [];
        }

        ChangeBadgeLayout.Anchor nearest = _lines.MinBy(a => Math.Abs(a.X - at.X));
        return Math.Abs(nearest.X - at.X) <= reach ? [nearest.Marker] : [];
    }

    /// <summary>One badge and its connectors: straight lines from under it to each of its lines, which cannot cross another's.</summary>
    private DrawnBadge Draw(RenderPack rp, ChangeBadgeLayout.Placed badge, float bottom, float dataTop)
    {
        foreach (ChangeBadgeLayout.Anchor member in badge.Members)
        {
            _connector.Render(rp.Canvas, new PixelLine(badge.Center, bottom, member.X, dataTop), rp.Paint);
        }

        float height = _badge.Measure(badge.Label, rp.Paint).Height + _padding.Vertical;
        _badge.Render(rp.Canvas, new Pixel(badge.Center, bottom - _padding.Bottom), rp.Paint, badge.Label);
        return new DrawnBadge(new PixelRect(badge.Left, badge.Right, bottom, bottom - height), badge.Label, [.. badge.Members.Select(static m => m.Marker)]);
    }
}
