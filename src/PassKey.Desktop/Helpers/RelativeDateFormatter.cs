using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace PassKey.Desktop.Helpers;

/// <summary>
/// Formats a timestamp the way the note cards show it: "adesso", "2 ore fa", "ieri",
/// "3 giorni fa", then an absolute date.
/// </summary>
public static class RelativeDateFormatter
{
    private static readonly ResourceLoader s_res = new();

    /// <summary>Formats a UTC timestamp as a localized relative date.</summary>
    public static string Format(DateTime utcDate)
    {
        var local = utcDate.ToLocalTime();
        var now = DateTime.Now;
        var diff = now - local;
        var culture = new CultureInfo(s_res.GetString("NoteDateCulture"));

        if (diff.TotalMinutes < 1) return s_res.GetString("NoteTimeNow");
        if (diff.TotalMinutes < 60) return string.Format(s_res.GetString("NoteTimeMinutes"), (int)diff.TotalMinutes);
        if (diff.TotalHours < 24 && local.Date == now.Date) return string.Format(s_res.GetString("NoteTimeHours"), (int)diff.TotalHours);
        if (local.Date == now.Date.AddDays(-1)) return s_res.GetString("NoteTimeYesterday");
        if (diff.TotalDays < 7) return string.Format(s_res.GetString("NoteTimeDays"), (int)diff.TotalDays);
        if (local.Year == now.Year) return local.ToString("d MMM", culture);
        return local.ToString("d MMM yyyy", culture);
    }
}
