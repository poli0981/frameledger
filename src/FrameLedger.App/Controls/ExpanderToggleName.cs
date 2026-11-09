// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FrameLedger.App.Controls;

/// <summary>
/// beta.35 (D88): WPF UI 4.3.0's <c>CardExpander</c> draws its expand button, the template part <c>ExpanderToggleButton</c>,
/// apart from its header and with no name, so a screen reader reached a Tab stop it could only call "toggle button". Set on
/// every CardExpander by the App's implicit style, this names that button after the header — its text, or the first line of
/// text a composed header shows — once the template exists.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime")]
public static class ExpanderToggleName
{
    /// <summary>The template part WPF UI's CardExpander template names its expand button.</summary>
    public const string PartName = "ExpanderToggleButton";

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ExpanderToggleName), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsEnabledProperty, value);
    }

    /// <summary>The words for an expander's header: the text itself, or the first non-empty text a composed header holds.</summary>
    public static string? HeaderText(object? header) => header switch
    {
        string text when !string.IsNullOrWhiteSpace(text) => text,
        DependencyObject element => FirstText(element),
        _ => null,
    };

    /// <summary>Names the expander's button now, if its template has been applied; false while it has not.</summary>
    public static bool TryName(Control expander)
    {
        ArgumentNullException.ThrowIfNull(expander);
        if (expander.Template?.FindName(PartName, expander) is not ToggleButton toggle)
        {
            return false;
        }

        if (HeaderText((expander as HeaderedContentControl)?.Header) is { } header)
        {
            AutomationProperties.SetName(toggle, string.Format(CultureInfo.CurrentCulture, Strings.A11y_ToggleSection_Format, header));
        }

        return true;
    }

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Control expander || e.NewValue is not true || TryName(expander))
        {
            return;
        }

        // The template is applied in the first measure; name the button after the first layout that made it.
        void OnLayoutUpdated(object? sender, EventArgs args)
        {
            if (TryName(expander))
            {
                expander.LayoutUpdated -= OnLayoutUpdated;
            }
        }

        expander.LayoutUpdated += OnLayoutUpdated;
    }

    private static string? FirstText(DependencyObject root)
    {
        if (root is TextBlock { Text.Length: > 0 } text)
        {
            return text.Text;
        }

        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject element && FirstText(element) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
