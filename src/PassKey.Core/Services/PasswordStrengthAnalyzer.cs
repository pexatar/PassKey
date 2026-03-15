using PassKey.Core.Models;

namespace PassKey.Core.Services;

public sealed class PasswordStrengthAnalyzer : IPasswordStrengthAnalyzer
{
    private static readonly string[] CommonPatterns =
    [
        "password", "123456", "qwerty", "abc123", "letmein",
        "admin", "welcome", "monkey", "dragon", "master",
        "login", "princess", "football", "shadow", "sunshine",
        "trustno1", "iloveyou"
    ];

    public PasswordStrengthResult Analyze(ReadOnlySpan<char> password)
    {
        if (password.IsEmpty)
        {
            return new PasswordStrengthResult
            {
                Score = 0,
                Label = "Empty",
                EstimatedCrackTime = "instant"
            };
        }

        var pw = password.ToString();
        var hasUpper = false;
        var hasLower = false;
        var hasDigit = false;
        var hasSymbol = false;

        foreach (var c in password)
        {
            if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else hasSymbol = true;
        }

        var hasMinLength = password.Length >= 8;
        var hasRecommendedLength = password.Length >= 12;
        var hasNoCommonPatterns = !ContainsCommonPattern(pw);

        // Calculate score (0-100)
        var score = 0;
        if (hasMinLength) score += 10;
        if (hasRecommendedLength) score += 15;
        if (password.Length >= 16) score += 10;
        if (hasUpper) score += 10;
        if (hasLower) score += 10;
        if (hasDigit) score += 10;
        if (hasSymbol) score += 15;
        if (hasNoCommonPatterns) score += 10;

        // Bonus for length
        score += Math.Min(10, password.Length - 8);
        score = Math.Clamp(score, 0, 100);

        var label = score switch
        {
            < 20 => "VeryWeak",
            < 40 => "Weak",
            < 60 => "Medium",
            < 80 => "Strong",
            _ => "VeryStrong"
        };

        var crackTime = EstimateCrackTime(password.Length, hasUpper, hasLower, hasDigit, hasSymbol);

        var suggestions = new List<string>();
        if (!hasMinLength) suggestions.Add("UseAtLeast8Characters");
        if (!hasRecommendedLength) suggestions.Add("UseAtLeast12Characters");
        if (!hasUpper) suggestions.Add("AddUppercaseLetters");
        if (!hasLower) suggestions.Add("AddLowercaseLetters");
        if (!hasDigit) suggestions.Add("AddNumbers");
        if (!hasSymbol) suggestions.Add("AddSpecialCharacters");
        if (!hasNoCommonPatterns) suggestions.Add("AvoidCommonPatterns");

        return new PasswordStrengthResult
        {
            Score = score,
            Label = label,
            EstimatedCrackTime = crackTime,
            HasMinLength = hasMinLength,
            HasRecommendedLength = hasRecommendedLength,
            HasUppercase = hasUpper,
            HasLowercase = hasLower,
            HasDigits = hasDigit,
            HasSymbols = hasSymbol,
            HasNoCommonPatterns = hasNoCommonPatterns,
            Suggestions = suggestions
        };
    }

    private static bool ContainsCommonPattern(string password)
    {
        var lower = password.ToLowerInvariant();
        foreach (var pattern in CommonPatterns)
        {
            if (lower.Contains(pattern, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string EstimateCrackTime(int length, bool hasUpper, bool hasLower, bool hasDigit, bool hasSymbol)
    {
        var charsetSize = 0;
        if (hasUpper) charsetSize += 26;
        if (hasLower) charsetSize += 26;
        if (hasDigit) charsetSize += 10;
        if (hasSymbol) charsetSize += 30;
        if (charsetSize == 0) charsetSize = 26;

        // Assume 10 billion guesses/sec (modern GPU cluster)
        var guessesPerSecond = 10_000_000_000.0;
        var combinations = Math.Pow(charsetSize, length);
        var seconds = combinations / guessesPerSecond / 2; // average case

        return seconds switch
        {
            < 1 => "instant",
            < 60 => "seconds",
            < 3600 => $"{(int)(seconds / 60)} minutes",
            < 86400 => $"{(int)(seconds / 3600)} hours",
            < 31536000 => $"{(int)(seconds / 86400)} days",
            < 31536000.0 * 100 => $"{(int)(seconds / 31536000)} years",
            < 31536000.0 * 1000 => "centuries",
            _ => "millennia"
        };
    }
}
