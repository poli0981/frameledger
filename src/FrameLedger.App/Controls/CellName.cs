// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;

namespace FrameLedger.App.Controls;

/// <summary>
/// beta.35 (D88): the words a screen reader reads for a template column's cells. A <c>DataGridTemplateColumn</c>'s cell has no
/// text of its own — its template draws it — so WPF names it <c>Item: FrameLedger.App.ViewModels.SessionItemViewModel, Column
/// Display Index: 3</c>. Set on the column, <see cref="PathProperty"/> gives every cell of it the row's text at that path as its
/// name, through a cell style based on WPF UI's own (<c>DefaultDataGridCellStyle</c>, the one its DataGrid gives every cell), so
/// the cells look as they did.
/// </summary>
public static class CellName
{
    /// <summary>The key of WPF UI 4.3.0's cell style, which its DataGrid sets as <c>CellStyle</c>.</summary>
    public const string WpfUiCellStyleKey = "DefaultDataGridCellStyle";

    public static readonly DependencyProperty PathProperty = DependencyProperty.RegisterAttached(
        "Path", typeof(string), typeof(CellName), new PropertyMetadata(null, OnPathChanged));

    public static string? GetPath(DataGridColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        return (string?)column.GetValue(PathProperty);
    }

    public static void SetPath(DataGridColumn column, string? value)
    {
        ArgumentNullException.ThrowIfNull(column);
        column.SetValue(PathProperty, value);
    }

    /// <summary>
    /// The cell style that names a cell by the row's text at <paramref name="path"/>, based on <paramref name="basedOn"/>. An
    /// empty text is read as the word for empty: a cell WPF finds no text in is named by its fallback again.
    /// </summary>
    public static Style Named(string path, Style? basedOn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var style = new Style(typeof(DataGridCell), basedOn);
        style.Setters.Add(new Setter(AutomationProperties.NameProperty, new Binding(path) { Mode = BindingMode.OneWay, Converter = SaysEmpty.Instance }));
        return style;
    }

    /// <summary>A cell's text, or the word for empty when there is none.</summary>
    public static string Spoken(object? value) => value?.ToString() is { } text && !string.IsNullOrWhiteSpace(text) ? text : Strings.A11y_Empty;

    private sealed class SaysEmpty : IValueConverter
    {
        public static readonly SaysEmpty Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => Spoken(value);

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
    }

    private static void OnPathChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is DataGridColumn column && e.NewValue is string { Length: > 0 } path)
        {
            column.CellStyle = Named(path, column.CellStyle ?? System.Windows.Application.Current?.TryFindResource(WpfUiCellStyleKey) as Style);
        }
    }
}
