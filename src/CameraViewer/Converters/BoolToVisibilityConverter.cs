using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CameraViewer.Converters;

/// <summary>
/// Converts a <see cref="bool"/> to <see cref="Visibility"/>.
/// <c>true</c> → <see cref="Visibility.Visible"/>;  <c>false</c> → <see cref="Visibility.Collapsed"/>.
/// Use <c>ConverterParameter=Invert</c> to reverse the mapping.
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool bv && bv;
        bool invert = parameter is string s && s == "Invert";
        if (invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}
