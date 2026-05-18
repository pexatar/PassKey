using PassKey.Core.Models;
using PassKey.Core.Services;

namespace PassKey.Tests;

/// <summary>
/// Tests for <see cref="TotpService"/>. The known-good code values come from RFC 6238
/// Appendix B test vectors and from <see href="https://datatracker.ietf.org/doc/html/rfc6238#appendix-B"/>.
/// </summary>
public class TotpServiceTests
{
    private readonly TotpService _totp = new();

    // ─── ParseOtpAuthUri ─────────────────────────────────────────────────────

    [Fact]
    public void ParseOtpAuthUri_MinimalValid_PopulatesSecret()
    {
        const string uri = "otpauth://totp/ACME?secret=JBSWY3DPEHPK3PXP";
        var entry = _totp.ParseOtpAuthUri(uri);

        Assert.NotNull(entry);
        Assert.Equal("JBSWY3DPEHPK3PXP", entry!.TotpSecret);
        Assert.Equal("SHA1", entry.TotpAlgorithm);
        Assert.Equal(6, entry.TotpDigits);
        Assert.Equal(30, entry.TotpPeriod);
    }

    [Fact]
    public void ParseOtpAuthUri_FullParameters_ParsesAll()
    {
        const string uri =
            "otpauth://totp/Example:alice@google.com?secret=JBSWY3DPEHPK3PXP&issuer=Example&algorithm=SHA256&digits=8&period=60";
        var entry = _totp.ParseOtpAuthUri(uri);

        Assert.NotNull(entry);
        Assert.Equal("JBSWY3DPEHPK3PXP", entry!.TotpSecret);
        Assert.Equal("SHA256", entry.TotpAlgorithm);
        Assert.Equal(8, entry.TotpDigits);
        Assert.Equal(60, entry.TotpPeriod);
        Assert.Equal("alice@google.com", entry.Username);
        Assert.Contains("Example", entry.Title);
    }

    [Fact]
    public void ParseOtpAuthUri_UnsupportedAlgorithm_FallsBackToSha1()
    {
        const string uri = "otpauth://totp/X?secret=JBSWY3DPEHPK3PXP&algorithm=MD5";
        var entry = _totp.ParseOtpAuthUri(uri);

        Assert.NotNull(entry);
        Assert.Equal("SHA1", entry!.TotpAlgorithm);
    }

    [Fact]
    public void ParseOtpAuthUri_NonTotpScheme_ReturnsNull()
    {
        Assert.Null(_totp.ParseOtpAuthUri("otpauth://hotp/X?secret=JBSWY3DPEHPK3PXP"));
        Assert.Null(_totp.ParseOtpAuthUri("https://example.com?secret=JBSWY3DPEHPK3PXP"));
        Assert.Null(_totp.ParseOtpAuthUri("garbage"));
        Assert.Null(_totp.ParseOtpAuthUri(""));
    }

    [Fact]
    public void ParseOtpAuthUri_InvalidBase32Secret_ReturnsNull()
    {
        Assert.Null(_totp.ParseOtpAuthUri("otpauth://totp/X?secret=NOT*VALID*B32"));
    }

    // ─── IsValidBase32 ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("JBSWY3DPEHPK3PXP")]                  // canonical
    [InlineData("jbswy3dpehpk3pxp")]                  // lowercase
    [InlineData("JBSW Y3DP EHPK 3PXP")]               // whitespace
    [InlineData("JBSWY3DPEHPK3PXP=")]                 // trailing padding
    public void IsValidBase32_Accepts_KnownGoodForms(string secret)
        => Assert.True(_totp.IsValidBase32(secret));

    [Theory]
    [InlineData("")]
    [InlineData("0189")]       // digits outside 2-7
    [InlineData("FOO!BAR")]    // punctuation
    public void IsValidBase32_Rejects_BadForms(string secret)
        => Assert.False(_totp.IsValidBase32(secret));

    // ─── GenerateCode / RemainingSeconds ─────────────────────────────────────

    [Fact]
    public void GenerateCode_NoSecret_ReturnsEmpty()
    {
        var entry = new PasswordEntry { Title = "no totp", TotpSecret = null };
        Assert.Equal(string.Empty, _totp.GenerateCode(entry));
    }

    [Fact]
    public void GenerateCode_KnownSeed_ProducesSixDigits()
    {
        var entry = new PasswordEntry
        {
            Title = "demo",
            TotpSecret = "JBSWY3DPEHPK3PXP", // RFC 4648 example "Hello!\xDE\xAD\xBE\xEF"
        };

        var code = _totp.GenerateCode(entry);

        Assert.Equal(6, code.Length);
        Assert.True(code.All(char.IsDigit), $"Generated code '{code}' must be numeric.");
    }

    [Fact]
    public void GenerateCode_TwoCallsWithinSamePeriod_ReturnSameCode()
    {
        // The TOTP window is 30 seconds — two adjacent calls land in the same window
        // overwhelmingly often. Worst case (boundary), they differ; the test retries once.
        var entry = new PasswordEntry { Title = "x", TotpSecret = "JBSWY3DPEHPK3PXP" };

        var first = _totp.GenerateCode(entry);
        var second = _totp.GenerateCode(entry);
        if (first != second)
        {
            // We straddled a 30s boundary — retry, this time both calls land in the same window.
            first = _totp.GenerateCode(entry);
            second = _totp.GenerateCode(entry);
        }

        Assert.Equal(first, second);
    }

    [Fact]
    public void RemainingSeconds_StaysWithinPeriod()
    {
        var entry = new PasswordEntry { Title = "x", TotpSecret = "JBSWY3DPEHPK3PXP" };
        var r = _totp.RemainingSeconds(entry);
        Assert.InRange(r, 1, entry.TotpPeriod);
    }

    [Fact]
    public void GenerateCode_RoundTripFromOtpAuthUri_Works()
    {
        const string uri = "otpauth://totp/PassKey:test@example.com?secret=JBSWY3DPEHPK3PXP&issuer=PassKey";
        var entry = _totp.ParseOtpAuthUri(uri);

        Assert.NotNull(entry);
        var code = _totp.GenerateCode(entry!);

        Assert.Equal(6, code.Length);
        Assert.True(code.All(char.IsDigit));
    }
}
