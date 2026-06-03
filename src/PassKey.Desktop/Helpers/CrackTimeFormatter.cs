using Microsoft.Windows.ApplicationModel.Resources;

namespace PassKey.Desktop.Helpers;

/// <summary>
/// Localizes the brute-force crack-time tokens produced by
/// <c>PasswordStrengthAnalyzer.EstimateCrackTime</c> (e.g. "instant", "5 minutes",
/// "12 thousandyears", "trillionyears"). Shared by the Generator and Verifier pages so
/// the two always show consistent wording.
/// </summary>
internal static class CrackTimeFormatter
{
    private static readonly ResourceLoader Res = new();

    public static string Localize(string token) => token switch
    {
        "instant" => Res.GetString("CrackTimeInstant"),
        "seconds" => Res.GetString("CrackTimeSeconds"),
        "trillionyears" => Res.GetString("CrackTimeTrillionYears"),
        _ => LocalizeWithNumber(token)
    };

    private static string LocalizeWithNumber(string token)
    {
        var parts = token.Split(' ', 2);
        if (parts.Length != 2) return token;

        var number = parts[0];
        return parts[1].ToLowerInvariant() switch
        {
            "minutes" or "minute" => string.Format(Res.GetString("CrackTimeMinutes"), number),
            "hours" or "hour" => string.Format(Res.GetString("CrackTimeHours"), number),
            "days" or "day" => string.Format(Res.GetString("CrackTimeDays"), number),
            "years" or "year" => string.Format(Res.GetString("CrackTimeYears"), number),
            "thousandyears" => string.Format(Res.GetString("CrackTimeThousandYears"), number),
            "millionyears" => string.Format(Res.GetString("CrackTimeMillionYears"), number),
            "billionyears" => string.Format(Res.GetString("CrackTimeBillionYears"), number),
            _ => token
        };
    }
}
