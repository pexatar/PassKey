using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using PassKey.Core.Constants;
using PassKey.Desktop.Controls;

namespace PassKey.Desktop.Converters;

/// <summary>
/// Converts a <see cref="CardColor"/> into the brush used for the card's icon disc in the
/// list view, so a card keeps the same visual identity in both the card and the list view.
/// </summary>
/// <remarks>
/// <para>
/// The colour the user picks is <i>data</i>, not decoration: showing it in one view and
/// dropping it in the other made the same card look like two different things.
/// </para>
/// <para>
/// The base colour is the gradient start already used to paint the card, reused rather than
/// duplicated so a future palette change cannot desynchronise the two views. It is then
/// lightened on dark theme and darkened on light theme — the same adjustment already proven
/// on the card's hover border. Without it the two near-black swatches (Black #212121,
/// Default charcoal #37474F) would merge into a dark background; the glyph stays white and
/// legible either way, but the disc would lose its edge.
/// </para>
/// </remarks>
public sealed partial class CardAccentBrushConverter : IValueConverter
{
    /// <summary>Shift applied to separate the disc from the page background. Matches the hover effect.</summary>
    private const int ThemeShift = 90;

    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not CardColor color)
            return new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        var (start, _) = CreditCardControl.GetGradientColors(color);

        // Resolved from the live visual tree rather than Application.RequestedTheme, because the
        // app also supports "follow the system", where the requested value is not the effective one.
        var isLight = App.MainWindow?.Content is FrameworkElement root
            ? root.ActualTheme == ElementTheme.Light
            : true;

        var adjusted = isLight
            ? CreditCardControl.Darken(start, ThemeShift)
            : CreditCardControl.Lighten(start, ThemeShift);

        return new SolidColorBrush(adjusted);
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException("CardAccentBrushConverter is one-way.");
}
