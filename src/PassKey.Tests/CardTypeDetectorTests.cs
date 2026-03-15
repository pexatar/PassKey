using PassKey.Core.Constants;
using PassKey.Core.Services;

namespace PassKey.Tests;

public class CardTypeDetectorTests
{
    [Theory]
    [InlineData("4111111111111111", CardType.Visa)]
    [InlineData("4012888888881881", CardType.Visa)]
    [InlineData("5500000000000004", CardType.MasterCard)]
    [InlineData("5105105105105100", CardType.MasterCard)]
    [InlineData("2221000000000009", CardType.MasterCard)]
    [InlineData("340000000000009", CardType.Amex)]
    [InlineData("371449635398431", CardType.Amex)]
    [InlineData("6011111111111117", CardType.Discover)]
    [InlineData("6500000000000002", CardType.Discover)]
    [InlineData("3530111333300000", CardType.JCB)]
    [InlineData("36000000000008", CardType.DinersClub)]
    [InlineData("30569309025904", CardType.DinersClub)]
    public void Detect_ReturnsCorrectCardType(string cardNumber, CardType expected)
    {
        Assert.Equal(expected, CardTypeDetector.Detect(cardNumber));
    }

    [Theory]
    [InlineData("", CardType.Unknown)]
    [InlineData("123", CardType.Unknown)]
    [InlineData("9999999999999999", CardType.Unknown)]
    public void Detect_ReturnsUnknown_ForInvalidInput(string cardNumber, CardType expected)
    {
        Assert.Equal(expected, CardTypeDetector.Detect(cardNumber));
    }

    [Theory]
    [InlineData("4111111111111111", true)]
    [InlineData("5500000000000004", true)]
    [InlineData("340000000000009", true)]
    [InlineData("1234567890123456", false)]
    [InlineData("0000000000000000", true)] // Luhn valid
    public void ValidateLuhn_ReturnsCorrectResult(string cardNumber, bool expected)
    {
        Assert.Equal(expected, CardTypeDetector.ValidateLuhn(cardNumber));
    }

    [Fact]
    public void MaskCardNumber_Visa_Shows4444Format()
    {
        var masked = CardTypeDetector.MaskCardNumber("4111111111111111", CardType.Visa);
        Assert.EndsWith("1111", masked);
        Assert.Contains("••••", masked);
    }

    [Fact]
    public void MaskCardNumber_Amex_ShowsAmexFormat()
    {
        var masked = CardTypeDetector.MaskCardNumber("371449635398431", CardType.Amex);
        Assert.Contains("••••", masked);
    }

    [Fact]
    public void FormatCardNumber_Visa_Groups4444()
    {
        var formatted = CardTypeDetector.FormatCardNumber("4111111111111111", CardType.Visa);
        Assert.Equal("4111 1111 1111 1111", formatted);
    }

    [Fact]
    public void FormatCardNumber_Amex_Groups465()
    {
        var formatted = CardTypeDetector.FormatCardNumber("371449635398431", CardType.Amex);
        Assert.Equal("3714 496353 98431", formatted);
    }
}
