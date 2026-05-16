using PassKey.Core.Constants;

namespace PassKey.Core.Services;

public static class CardTypeDetector
{
    /// <summary>
    /// BIN (Bank Identification Number) / IIN prefix ranges used for card-type detection.
    /// Sources: EMVCo IIN Registry; updated ranges as of 2024.
    /// </summary>
    private static class BinRanges
    {
        // MasterCard: standard range 51-55 is checked inline with a 2-digit prefix;
        // the expanded range covers re-issued BINs introduced in 2017.
        internal const int MasterCardStandardStart = 51;
        internal const int MasterCardStandardEnd   = 55;
        internal const int MasterCardExpandedStart = 2221;
        internal const int MasterCardExpandedEnd   = 2720;

        // Discover: 3-digit prefix range (644-649)
        internal const int DiscoverRange3Start = 644;
        internal const int DiscoverRange3End   = 649;

        // Discover: 6-digit IIN range (co-branded UnionPay; 622126-622925)
        internal const int DiscoverRange6Start = 622126;
        internal const int DiscoverRange6End   = 622925;

        // JCB: 4-digit prefix range (3528-3589)
        internal const int JcbRangeStart = 3528;
        internal const int JcbRangeEnd   = 3589;

        // Diners Club Classic: 3-digit prefix range (300-305)
        internal const int DinersRange3Start = 300;
        internal const int DinersRange3End   = 305;
    }

    /// <summary>Network-specific literal prefixes used for card-type detection (string-startswith match).</summary>
    private static class BinPrefixes
    {
        /// <summary>American Express: 34 or 37.</summary>
        internal static readonly string[] Amex = ["34", "37"];

        /// <summary>Visa always starts with 4.</summary>
        internal const string Visa = "4";

        /// <summary>Discover: 6011 or 65 (in addition to the 3- and 6-digit ranges in <see cref="BinRanges"/>).</summary>
        internal static readonly string[] Discover = ["6011", "65"];

        /// <summary>Diners Club: 36 or 38 (in addition to the 300-305 range in <see cref="BinRanges"/>).</summary>
        internal static readonly string[] Diners = ["36", "38"];

        /// <summary>Maestro: 5018, 5020, 5038, 6304, 6759, 6761, 6762, 6763.</summary>
        internal static readonly string[] Maestro = ["5018", "5020", "5038", "6304", "6759", "6761", "6762", "6763"];
    }

    /// <summary>
    /// Detects the card type from the card number using BIN prefix tables.
    /// </summary>
    public static CardType Detect(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return CardType.Unknown;

        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
        if (digits.Length < 4)
            return CardType.Unknown;

        // Amex: starts with 34 or 37.
        if (StartsWithAny(digits, BinPrefixes.Amex))
            return CardType.Amex;

        // Visa: starts with 4.
        if (digits.StartsWith(BinPrefixes.Visa))
            return CardType.Visa;

        // MasterCard: 51-55 or expanded range 2221-2720.
        if (digits.Length >= 2)
        {
            var prefix2 = int.Parse(digits[..2]);
            if (prefix2 >= BinRanges.MasterCardStandardStart && prefix2 <= BinRanges.MasterCardStandardEnd)
                return CardType.MasterCard;
        }
        if (digits.Length >= 4)
        {
            var prefix4 = int.Parse(digits[..4]);
            if (prefix4 >= BinRanges.MasterCardExpandedStart && prefix4 <= BinRanges.MasterCardExpandedEnd)
                return CardType.MasterCard;
        }

        // Discover: 6011, 622126-622925, 644-649, 65.
        if (StartsWithAny(digits, BinPrefixes.Discover))
            return CardType.Discover;
        if (digits.Length >= 3)
        {
            var prefix3 = int.Parse(digits[..3]);
            if (prefix3 >= BinRanges.DiscoverRange3Start && prefix3 <= BinRanges.DiscoverRange3End)
                return CardType.Discover;
        }
        if (digits.Length >= 6)
        {
            var prefix6 = int.Parse(digits[..6]);
            if (prefix6 >= BinRanges.DiscoverRange6Start && prefix6 <= BinRanges.DiscoverRange6End)
                return CardType.Discover;
        }

        // JCB: 3528-3589.
        if (digits.Length >= 4)
        {
            var prefix4 = int.Parse(digits[..4]);
            if (prefix4 >= BinRanges.JcbRangeStart && prefix4 <= BinRanges.JcbRangeEnd)
                return CardType.JCB;
        }

        // Diners Club: 300-305, 36, 38.
        if (StartsWithAny(digits, BinPrefixes.Diners))
            return CardType.DinersClub;
        if (digits.Length >= 3)
        {
            var prefix3 = int.Parse(digits[..3]);
            if (prefix3 >= BinRanges.DinersRange3Start && prefix3 <= BinRanges.DinersRange3End)
                return CardType.DinersClub;
        }

        // Maestro: 5018, 5020, 5038, 6304, 6759, 6761, 6762, 6763.
        if (digits.Length >= 4 && StartsWithAny(digits, BinPrefixes.Maestro))
            return CardType.Maestro;

        return CardType.Unknown;
    }

    /// <summary>Returns true if the supplied digit string starts with any of the supplied prefixes.</summary>
    private static bool StartsWithAny(string digits, string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (digits.StartsWith(prefix)) return true;
        }
        return false;
    }

    /// <summary>
    /// Validates a card number using the Luhn algorithm.
    /// </summary>
    public static bool ValidateLuhn(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return false;

        var digits = cardNumber.Where(char.IsDigit).ToArray();
        if (digits.Length < 8)
            return false;

        var sum = 0;
        var alternate = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    /// <summary>
    /// Masks the card number showing only the last 4 digits,
    /// formatted according to the card network grouping.
    /// </summary>
    public static string MaskCardNumber(string cardNumber, CardType cardType)
    {
        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
        if (digits.Length < 4)
            return cardNumber;

        var last4 = digits[^4..];

        return cardType switch
        {
            // Amex: 4-6-5 grouping
            CardType.Amex => $"•••• •••••• •{last4[..1]}{last4[1..]}",
            // Diners: 4-6-4 grouping
            CardType.DinersClub => $"•••• •••••• {last4}",
            // Default: 4-4-4-4 grouping (Visa, MC, Discover, JCB, Maestro)
            _ => $"•••• •••• •••• {last4}"
        };
    }

    /// <summary>
    /// Formats a card number with appropriate grouping for display during input.
    /// </summary>
    public static string FormatCardNumber(string cardNumber, CardType cardType)
    {
        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());

        return cardType switch
        {
            // Amex: 4-6-5
            CardType.Amex => FormatWithGroups(digits, [4, 6, 5]),
            // Diners: 4-6-4
            CardType.DinersClub => FormatWithGroups(digits, [4, 6, 4]),
            // Default: 4-4-4-4
            _ => FormatWithGroups(digits, [4, 4, 4, 4])
        };
    }

    private static string FormatWithGroups(string digits, int[] groups)
    {
        var result = new System.Text.StringBuilder();
        var pos = 0;
        foreach (var groupSize in groups)
        {
            if (pos >= digits.Length) break;
            if (result.Length > 0) result.Append(' ');
            var take = Math.Min(groupSize, digits.Length - pos);
            result.Append(digits.AsSpan(pos, take));
            pos += take;
        }
        return result.ToString();
    }
}
