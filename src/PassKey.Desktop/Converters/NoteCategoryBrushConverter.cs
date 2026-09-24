using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using PassKey.Core.Constants;
using PassKey.Desktop.Helpers;

namespace PassKey.Desktop.Converters;

/// <summary>
/// Converts a <see cref="NoteCategory"/> into the brush painting the card's 4px accent bar
/// and the category dots.
/// </summary>
/// <remarks>
/// The colour used to be pushed onto the border by hand while recycling list containers,
/// which is why an edited note kept the previous category's colour until the list was
/// rebuilt. As a converter the colour is part of the binding, so it follows the data.
/// </remarks>
public sealed partial class NoteCategoryBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not NoteCategory category)
            return new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        return new SolidColorBrush(ColorHex.Parse(NoteCategoryInfo.GetColor(category)));
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException("NoteCategoryBrushConverter is one-way.");
}
