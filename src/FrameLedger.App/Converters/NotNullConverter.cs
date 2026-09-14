using System.Globalization;
using System.Windows.Data;

namespace FrameLedger.App.Converters;

/// <summary>Null or an empty string → false; anything else → true (an <c>InfoBar.IsOpen</c> from an optional message).</summary>
public sealed class NotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && !(value is string s && s.Length == 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
