using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace PassKey.Desktop.Converters;

/// <summary>
/// Maps a raw activity-action string ("Created", "Modified", "Deleted", …) to either a
/// Segoe MDL2 glyph or a semantic <see cref="Brush"/>, selected by the converter parameter
/// (<c>"Glyph"</c> or <c>"Brush"</c>).
/// </summary>
/// <remarks>
/// Used by the recent-activity list (Dashboard) and the activity-log viewer to give each
/// action an accessible indicator — shape <em>and</em> colour, not colour alone — so a
/// destructive "Deleted" is instantly distinguishable from a constructive "Created".
/// </remarks>
public sealed class ActionIndicatorConverter : IValueConverter
{
    /// <summary>
    /// Converts the action string to a glyph or brush.
    /// </summary>
    /// <param name="value">The raw action string.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Pass <c>"Brush"</c> for the semantic colour; anything else yields the glyph.</param>
    /// <param name="language">Unused.</param>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var action = value as string ?? string.Empty;
        var wantBrush = parameter is string s && s.Equals("Brush", StringComparison.OrdinalIgnoreCase);

        if (wantBrush)
        {
            var key = action switch
            {
                "Created" => "StatAddedBrush",
                "Modified" or "Updated" => "StatModifiedBrush",
                "Deleted" => "StatRemovedBrush",
                _ => "TextFillColorSecondaryBrush"
            };
            return (Brush)Application.Current.Resources[key];
        }

        return action switch
        {
            "Created" => "",               // Add
            "Modified" or "Updated" => "", // Edit
            "Deleted" => "",               // Delete
            _ => ""                        // History (neutral — Copied/Unlocked/Locked/…)
        };
    }

    /// <summary>Not supported. Throws <see cref="NotSupportedException"/>.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
