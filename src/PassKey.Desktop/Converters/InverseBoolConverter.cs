using Microsoft.UI.Xaml.Data;

namespace PassKey.Desktop.Converters;

/// <summary>
/// WinUI 3 value converter that negates a boolean value.
/// Useful for binding <c>IsEnabled</c> or <c>IsChecked</c> to an inverted condition
/// without adding a dedicated property to the view model.
/// Supports two-way binding: <see cref="ConvertBack"/> applies the same negation.
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    /// <summary>
    /// Converts a boolean value to its logical negation.
    /// </summary>
    /// <param name="value">The boolean value to negate. Non-boolean values are treated as false (not true).</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="language">Unused.</param>
    /// <returns>True if <paramref name="value"/> is not true; false otherwise.</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is not true;
    }

    /// <summary>
    /// Converts a boolean value back to its logical negation (same as <see cref="Convert"/>).
    /// </summary>
    /// <param name="value">The boolean value to negate.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="language">Unused.</param>
    /// <returns>True if <paramref name="value"/> is not true; false otherwise.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is not true;
    }
}
