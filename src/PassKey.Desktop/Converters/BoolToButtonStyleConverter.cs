using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PassKey.Desktop.Converters;

/// <summary>
/// Picks the accent or the subtle button style from a boolean, so a two-button segmented
/// control can show which half is active through a binding instead of code-behind.
/// </summary>
/// <remarks>
/// Pass "Invert" as the converter parameter for the other half of the pair. This replaces
/// assignments to <c>Button.Style</c> from a click handler, which is the same class of
/// hand-written UI state that let a disabled Save button look enabled (R2).
/// </remarks>
public sealed partial class BoolToButtonStyleConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        var active = value is true;
        if (invert) active = !active;

        return (Style)Application.Current.Resources[active ? "AccentButtonStyle" : "SubtleButtonStyle"];
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException("BoolToButtonStyleConverter is one-way.");
}
