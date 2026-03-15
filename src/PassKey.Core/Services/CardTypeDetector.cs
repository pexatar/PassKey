using PassKey.Core.Constants;

namespace PassKey.Core.Services;

public static class CardTypeDetector
{
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

        // Amex: starts with 34 or 37
        if (digits.StartsWith("34") || digits.StartsWith("37"))
            return CardType.Amex;

        // Visa: starts with 4
        if (digits.StartsWith('4'))
            return CardType.Visa;

        // MasterCard: 51-55 or 2221-2720
        if (digits.Length >= 2)
        {
            var prefix2 = int.Parse(digits[..2]);
            if (prefix2 >= 51 && prefix2 <= 55)
                return CardType.MasterCard;
        }
        if (digits.Length >= 4)
        {
            var prefix4 = int.Parse(digits[..4]);
            if (prefix4 >= 2221 && prefix4 <= 2720)
                return CardType.MasterCard;
        }

        // Discover: 6011, 622126-622925, 644-649, 65
        if (digits.StartsWith("6011") || digits.StartsWith("65"))
            return CardType.Discover;
        if (digits.Length >= 3)
        {
            var prefix3 = int.Parse(digits[..3]);
            if (prefix3 >= 644 && prefix3 <= 649)
                return CardType.Discover;
        }
        if (digits.Length >= 6)
        {
            var prefix6 = int.Parse(digits[..6]);
            if (prefix6 >= 622126 && prefix6 <= 622925)
                return CardType.Discover;
        }

        // JCB: 3528-3589
        if (digits.Length >= 4)
        {
            var prefix4 = int.Parse(digits[..4]);
            if (prefix4 >= 3528 && prefix4 <= 3589)
                return CardType.JCB;
        }

        // Diners Club: 300-305, 36, 38
        if (digits.StartsWith("36") || digits.StartsWith("38"))
            return CardType.DinersClub;
        if (digits.Length >= 3)
        {
            var prefix3 = int.Parse(digits[..3]);
            if (prefix3 >= 300 && prefix3 <= 305)
                return CardType.DinersClub;
        }

        // Maestro: 5018, 5020, 5038, 6304, 6759, 6761, 6762, 6763
        string[] maestroPrefixes = ["5018", "5020", "5038", "6304", "6759", "6761", "6762", "6763"];
        if (digits.Length >= 4 && maestroPrefixes.Any(p => digits.StartsWith(p)))
            return CardType.Maestro;

        return CardType.Unknown;
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
