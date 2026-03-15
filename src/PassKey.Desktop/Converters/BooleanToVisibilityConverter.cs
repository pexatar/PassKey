using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PassKey.Desktop.Converters;

/// <summary>
/// WinUI 3 value converter that maps a <see cref="bool"/> to a <see cref="Visibility"/> value.
/// Supports an optional "Invert" converter parameter to reverse the mapping.
/// </summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts a boolean value to <see cref="Visibility"/>.
    /// </summary>
    /// <param name="value">The boolean value to convert. Non-boolean values are treated as false.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">
    /// Pass "Invert" (case-insensitive) to reverse the mapping:
    /// true → <see cref="Visibility.Collapsed"/>, false → <see cref="Visibility.Visible"/>.
    /// </param>
    /// <param name="language">Unused.</param>
    /// <returns>
    /// <see cref="Visibility.Visible"/> when the effective boolean is true;
    /// <see cref="Visibility.Collapsed"/> when false.
    /// </returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        var boolValue = value is true;
        if (invert) boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Not supported. Throws <see cref="NotSupportedException"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
