using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FrameLedger.App.Converters;

/// <summary>A count of zero → <see cref="Visibility.Visible"/> (an empty-state line); anything else → Collapsed.</summary>
public sealed class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
