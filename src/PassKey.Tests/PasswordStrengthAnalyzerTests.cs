using PassKey.Core.Services;

namespace PassKey.Tests;

public class PasswordStrengthAnalyzerTests
{
    private readonly PasswordStrengthAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_EmptyPassword_ReturnsZeroScore()
    {
        var result = _analyzer.Analyze(ReadOnlySpan<char>.Empty);

        Assert.Equal(0, result.Score);
        Assert.Equal("Empty", result.Label);
        Assert.Equal("instant", result.EstimatedCrackTime);
    }

    [Fact]
    public void Analyze_ShortPassword_VeryWeak()
    {
        var result = _analyzer.Analyze("abc".AsSpan());

        Assert.True(result.Score < 20);
        Assert.Equal("VeryWeak", result.Label);
        Assert.False(result.HasMinLength);
    }

    [Fact]
    public void Analyze_OnlyLowercase8Chars_Weak()
    {
        var result = _analyzer.Analyze("abcdefgh".AsSpan());

        Assert.True(result.Score < 40, $"Expected Score < 40, got {result.Score}");
        Assert.True(result.HasMinLength);
        Assert.True(result.HasLowercase);
        Assert.False(result.HasUppercase);
        Assert.False(result.HasDigits);
        Assert.False(result.HasSymbols);
    }

    [Fact]
    public void Analyze_MixedButShort_ReportsFlags()
    {
        var result = _analyzer.Analyze("Ab1!x".AsSpan());

        Assert.False(result.HasMinLength);
        Assert.True(result.HasUppercase);
        Assert.True(result.HasLowercase);
        Assert.True(result.HasDigits);
        Assert.True(result.HasSymbols);
    }

    [Fact]
    public void Analyze_AllCharSets12Chars_AtLeastStrong()
    {
        var result = _analyzer.Analyze("MyP@ssw0rd!X".AsSpan());

        Assert.True(result.Score >= 60, $"Expected Score >= 60, got {result.Score}");
        Assert.True(result.HasMinLength);
        Assert.True(result.HasRecommendedLength);
        Assert.True(result.HasUppercase);
        Assert.True(result.HasLowercase);
        Assert.True(result.HasDigits);
        Assert.True(result.HasSymbols);
    }

    [Fact]
    public void Analyze_Long17Chars_VeryStrong()
    {
        var result = _analyzer.Analyze("Xy7$kQm2rPw9@NzLq".AsSpan());

        Assert.True(result.Score >= 80, $"Expected Score >= 80, got {result.Score}");
        Assert.Equal("VeryStrong", result.Label);
    }

    [Fact]
    public void Analyze_CommonPattern_Detected()
    {
        var result = _analyzer.Analyze("password123!A".AsSpan());

        Assert.False(result.HasNoCommonPatterns);
        Assert.Contains("AvoidCommonPatterns", result.Suggestions);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("123456")]
    [InlineData("qwerty")]
    [InlineData("abc123")]
    [InlineData("letmein")]
    [InlineData("admin")]
    [InlineData("welcome")]
    [InlineData("monkey")]
    [InlineData("dragon")]
    [InlineData("master")]
    [InlineData("login")]
    [InlineData("princess")]
    [InlineData("football")]
    [InlineData("shadow")]
    [InlineData("sunshine")]
    [InlineData("trustno1")]
    [InlineData("iloveyou")]
    public void Analyze_AllCommonPatterns_Detected(string pattern)
    {
        // Pad to at least 8 chars so the pattern detection is the focus
        var padded = pattern.Length < 8 ? pattern + new string('X', 8 - pattern.Length) : pattern;
        var result = _analyzer.Analyze(padded.AsSpan());

        Assert.False(result.HasNoCommonPatterns,
            $"Pattern '{pattern}' (padded: '{padded}') should be detected as common");
    }

    [Fact]
    public void Analyze_Flags_MatchContent()
    {
        var result = _analyzer.Analyze("aB3!".AsSpan());

        Assert.True(result.HasLowercase);
        Assert.True(result.HasUppercase);
        Assert.True(result.HasDigits);
        Assert.True(result.HasSymbols);
        Assert.False(result.HasMinLength);
        Assert.True(result.HasNoCommonPatterns);
    }

    [Fact]
    public void Analyze_Suggestions_IncludeMissing()
    {
        var result = _analyzer.Analyze("abc".AsSpan());

        Assert.Contains("UseAtLeast8Characters", result.Suggestions);
        Assert.Contains("UseAtLeast12Characters", result.Suggestions);
        Assert.Contains("AddUppercaseLetters", result.Suggestions);
        Assert.Contains("AddNumbers", result.Suggestions);
        Assert.Contains("AddSpecialCharacters", result.Suggestions);
        // Has lowercase, so should NOT suggest adding it
        Assert.DoesNotContain("AddLowercaseLetters", result.Suggestions);
    }

    [Fact]
    public void Analyze_CrackTime_ShortIsInstant()
    {
        var result = _analyzer.Analyze("abcd".AsSpan());

        Assert.True(
            result.EstimatedCrackTime == "instant" || result.EstimatedCrackTime == "seconds",
            $"Expected 'instant' or 'seconds', got '{result.EstimatedCrackTime}'");
    }

    [Fact]
    public void Analyze_CrackTime_LongComplexIsExtreme()
    {
        // 22 chars with all character sets → should be centuries or millennia
        var result = _analyzer.Analyze("Xy7$kQm2rPw9@NzLqJf!8Wv".AsSpan());

        Assert.True(
            result.EstimatedCrackTime == "centuries" || result.EstimatedCrackTime == "millennia",
            $"Expected 'centuries' or 'millennia', got '{result.EstimatedCrackTime}'");
    }
}
