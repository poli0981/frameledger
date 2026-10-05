// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;

namespace FrameLedger.App.Tests;

/// <summary>
/// Visual-tree helpers for the rendered checks (beta.14), all run on <see cref="PagesLoadTests.OnStaAsync{T}"/>'s thread:
/// find elements, pump the dispatcher, lay out at a size, swap the theme for one check, and the colour actually painted
/// under an element — the stack of backgrounds a pixel shows, composited, rather than one control's Background property.
/// </summary>
internal static class Visuals
{
    public static T? Find<T>(DependencyObject root)
        where T : DependencyObject => FindAll<T>(root).FirstOrDefault();

    public static IEnumerable<T> FindAll<T>(DependencyObject root)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit)
            {
                yield return hit;
            }

            foreach (T deeper in FindAll<T>(child))
            {
                yield return deeper;
            }
        }
    }

    /// <summary>The innermost element under <paramref name="root"/> along first children — where a pointer over it would land.</summary>
    public static UIElement Leaf(UIElement root)
    {
        UIElement current = root;
        while (VisualTreeHelper.GetChildrenCount(current) > 0 && VisualTreeHelper.GetChild(current, 0) is UIElement child)
        {
            current = child;
        }

        return current;
    }

    /// <summary>Runs the dispatcher's queue down to background priority — a Frame's navigation, Loaded, a deferred layout.</summary>
    public static void Pump()
    {
        for (int i = 0; i < 3; i++)
        {
            var frame = new DispatcherFrame();
#pragma warning disable VSTHRD001 // a test's own STA thread: no JoinableTaskFactory exists here, and the frame is what is waited on
            _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(_ =>
            {
                frame.Continue = false;
                return null;
            }), null);
#pragma warning restore VSTHRD001
            Dispatcher.PushFrame(frame);
        }
    }

    public static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    /// <summary>
    /// Runs <paramref name="work"/> with the App's theme dictionary swapped for <paramref name="theme"/>, and puts Dark back:
    /// elements made inside take the theme's resources when they are created.
    /// </summary>
    public static T UnderTheme<T>(ApplicationTheme theme, Func<T> work)
    {
        System.Collections.ObjectModel.Collection<ResourceDictionary> merged = System.Windows.Application.Current.Resources.MergedDictionaries;
        int index = merged.ToList().FindIndex(static d => d is ThemesDictionary);
        ResourceDictionary original = merged[index];
        merged[index] = new ThemesDictionary { Theme = theme };
        try
        {
            return work();
        }
        finally
        {
            merged[index] = original;
        }
    }

    /// <summary>
    /// The colour painted under <paramref name="element"/>: every Border's and Panel's background from <paramref name="root"/>
    /// down to the element, composited over <paramref name="window"/> — what a template's parts draw, which is what the eye sees.
    /// </summary>
    public static Color PaintedUnder(DependencyObject element, DependencyObject root, Color window)
    {
        var chain = new List<Brush?>();
        for (DependencyObject? at = element; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            chain.Add(at switch
            {
                Border b => b.Background,
                Panel p => p.Background,
                _ => null,
            });
            if (ReferenceEquals(at, root))
            {
                break;
            }
        }

        Color painted = window;
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            if (chain[i] is SolidColorBrush { Color.A: > 0 } solid)
            {
                painted = Wcag.Over(Color.FromArgb((byte)Math.Round(solid.Color.A * solid.Opacity), solid.Color.R, solid.Color.G, solid.Color.B), painted);
            }
        }

        return painted;
    }

    /// <summary>A TextBlock's text colour as drawn over <paramref name="under"/>.</summary>
    public static Color TextOver(TextBlock text, Color under) =>
        text.Foreground is SolidColorBrush brush ? Wcag.Over(brush.Color, under) : throw new InvalidOperationException("a text whose foreground is not a solid colour");
}
