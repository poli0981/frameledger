// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;

namespace FrameLedger.App.Controls;

/// <summary>
/// The library grid's panel (beta.11, owner request 2026-10-03: "the empty band on the right"): as many columns as
/// <see cref="MinItemWidth"/> allows across the width it is given, every item stretched to an equal share of it, so a row
/// is always full — a plain <see cref="WrapPanel"/> of 300 px cards left up to a card's width unused at the right. Each row
/// is as tall as its tallest item. Not virtualizing: a library is hundreds of rows at most.
/// </summary>
public sealed class UniformWrapPanel : Panel
{
    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(UniformWrapPanel), new FrameworkPropertyMetadata(300.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(UniformWrapPanel), new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>The narrowest an item may be; the column count is the most that fit at this width or wider.</summary>
    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    /// <summary>The gap between columns and between rows.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>How many columns of at least <paramref name="minItemWidth"/> fit in <paramref name="width"/>, never fewer than one.</summary>
    public static int ColumnsFor(double width, double minItemWidth, double spacing)
    {
        if (double.IsNaN(width) || double.IsInfinity(width) || minItemWidth <= 0)
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Floor((width + spacing) / (minItemWidth + spacing)));
    }

    /// <summary>Each item's width when <paramref name="columns"/> share <paramref name="width"/> with gaps between them.</summary>
    public static double ItemWidthFor(double width, int columns, double spacing) =>
        columns <= 0 ? 0 : Math.Max(0, (width - ((columns - 1) * spacing)) / columns);

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? MinItemWidth : availableSize.Width;
        int columns = ColumnsFor(width, MinItemWidth, Spacing);
        double itemWidth = ItemWidthFor(width, columns, Spacing);
        double height = 0;
        double rowHeight = 0;
        int column = 0;
        int rows = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(itemWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            if (++column == columns)
            {
                height += rowHeight;
                rows++;
                rowHeight = 0;
                column = 0;
            }
        }

        if (column > 0)
        {
            height += rowHeight;
            rows++;
        }

        return new Size(width, height + (Math.Max(0, rows - 1) * Spacing));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int columns = ColumnsFor(finalSize.Width, MinItemWidth, Spacing);
        double itemWidth = ItemWidthFor(finalSize.Width, columns, Spacing);
        UIElement[] children = [.. InternalChildren.Cast<UIElement>()];
        double y = 0;
        for (int start = 0; start < children.Length; start += columns)
        {
            int end = Math.Min(start + columns, children.Length);
            double rowHeight = 0;
            for (int i = start; i < end; i++)
            {
                rowHeight = Math.Max(rowHeight, children[i].DesiredSize.Height);
            }

            for (int i = start; i < end; i++)
            {
                children[i].Arrange(new Rect((i - start) * (itemWidth + Spacing), y, itemWidth, rowHeight));
            }

            y += rowHeight + Spacing;
        }

        return finalSize;
    }
}
