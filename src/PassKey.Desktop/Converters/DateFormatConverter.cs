using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace PassKey.Desktop.Converters;

/// <summary>
/// WinUI 3 value converter that formats a <see cref="DateTime"/> as a localized short date
/// and short time string (e.g., "20/02/2026 14:30" for it-IT, "2/20/2026 2:30 PM" for en-US).
/// UTC values are automatically converted to local time before formatting.
/// </summary>
public sealed class DateFormatConverter : IValueConverter
{
    /// <summary>
    /// Formats a <see cref="DateTime"/> value using the "g" (general short date/time) format.
    /// </summary>
    /// <param name="value">
    /// The value to format. Must be a <see cref="DateTime"/>; other types return an empty string.
    /// UTC <see cref="DateTime"/> values are converted to local time before formatting.
    /// </param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="language">
    /// BCP-47 language tag used to select the culture for formatting (e.g., "it-IT").
    /// Falls back to <see cref="CultureInfo.CurrentCulture"/> if null or empty.
    /// </param>
    /// <returns>
    /// A localized short date and time string, or <see cref="string.Empty"/> if
    /// <paramref name="value"/> is not a <see cref="DateTime"/>.
    /// </returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime dt)
        {
            var local = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
            var culture = string.IsNullOrEmpty(language)
                ? CultureInfo.CurrentCulture
                : new CultureInfo(language);

            // Short date + short time (e.g. "20/02/2026 14:30" for it-IT)
            return local.ToString("g", culture);
        }

        return string.Empty;
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
