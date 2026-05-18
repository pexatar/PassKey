using System.Web;
using OtpNet;
using PassKey.Core.Models;

namespace PassKey.Core.Services;

/// <summary>
/// RFC 6238 TOTP implementation backed by <see href="https://github.com/kspearrin/Otp.NET"/>.
/// </summary>
public sealed class TotpService : ITotpService
{
    /// <inheritdoc/>
    public string GenerateCode(PasswordEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(entry.TotpSecret))
            return string.Empty;

        var secretBytes = TryDecodeBase32(entry.TotpSecret);
        if (secretBytes is null) return string.Empty;

        var totp = new Totp(
            secretKey: secretBytes,
            step: entry.TotpPeriod,
            mode: MapAlgorithm(entry.TotpAlgorithm),
            totpSize: entry.TotpDigits);

        return totp.ComputeTotp(DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public int RemainingSeconds(PasswordEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.TotpPeriod <= 0) return 0;

        var period = entry.TotpPeriod;
        var nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var inWindow = (int)(nowEpoch % period);
        // Remaining is always in [1, period] — when inWindow is 0 we have a full period left.
        return period - inWindow;
    }

    /// <inheritdoc/>
    public PasswordEntry? ParseOtpAuthUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return null;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)) return null;

        if (!string.Equals(parsed.Scheme, "otpauth", StringComparison.OrdinalIgnoreCase))
            return null;

        // Host = the OTP kind. We only support TOTP here (HOTP is not part of PassKey 2.0).
        if (!string.Equals(parsed.Host, "totp", StringComparison.OrdinalIgnoreCase))
            return null;

        // Path is "/Issuer:Account" (or "/Account") — both parts will become the entry title.
        var label = Uri.UnescapeDataString(parsed.AbsolutePath.TrimStart('/'));
        var (issuerFromLabel, account) = SplitLabel(label);

        var query = HttpUtility.ParseQueryString(parsed.Query);

        var secret = query["secret"];
        if (string.IsNullOrWhiteSpace(secret) || !IsValidBase32(secret))
            return null;

        var issuer = query["issuer"] ?? issuerFromLabel ?? string.Empty;
        var algorithm = (query["algorithm"] ?? "SHA1").ToUpperInvariant();
        if (algorithm is not ("SHA1" or "SHA256" or "SHA512"))
            algorithm = "SHA1";

        if (!int.TryParse(query["digits"], out var digits) || digits is < 6 or > 10)
            digits = 6;

        if (!int.TryParse(query["period"], out var period) || period <= 0)
            period = 30;

        // Title falls back to whatever piece is most informative. Most exporters embed
        // both the issuer and the account; prefer "Issuer (Account)" when both exist.
        var title = (issuer, account) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{issuer} ({account})",
            ({ Length: > 0 }, _)               => issuer,
            (_, { Length: > 0 })               => account,
            _                                  => "TOTP",
        };

        return new PasswordEntry
        {
            Title = title,
            Username = account ?? string.Empty,
            TotpSecret = NormaliseBase32(secret),
            TotpAlgorithm = algorithm,
            TotpDigits = digits,
            TotpPeriod = period,
        };
    }

    /// <inheritdoc/>
    public bool IsValidBase32(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return false;

        // Allow lowercase / whitespace / padding; reject anything else.
        foreach (var c in secret)
        {
            if (c is ' ' or '\t' or '\r' or '\n' or '=') continue;
            var u = char.ToUpperInvariant(c);
            var isLetter = u is >= 'A' and <= 'Z';
            var isDigit2to7 = u is >= '2' and <= '7';
            if (!isLetter && !isDigit2to7) return false;
        }
        return true;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static byte[]? TryDecodeBase32(string secret)
    {
        try
        {
            return Base32Encoding.ToBytes(NormaliseBase32(secret));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Strips whitespace, removes padding, and uppercases — what <see cref="Base32Encoding"/> expects.</summary>
    private static string NormaliseBase32(string secret)
    {
        Span<char> buf = secret.Length <= 256
            ? stackalloc char[secret.Length]
            : new char[secret.Length];
        int j = 0;
        foreach (var c in secret)
        {
            if (c is ' ' or '\t' or '\r' or '\n' or '=') continue;
            buf[j++] = char.ToUpperInvariant(c);
        }
        return new string(buf[..j]);
    }

    private static (string? issuer, string? account) SplitLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return (null, null);
        var colon = label.IndexOf(':');
        return colon < 0
            ? (null, label)
            : (label[..colon].Trim(), label[(colon + 1)..].Trim());
    }

    private static OtpHashMode MapAlgorithm(string algorithm) =>
        algorithm.ToUpperInvariant() switch
        {
            "SHA256" => OtpHashMode.Sha256,
            "SHA512" => OtpHashMode.Sha512,
            _        => OtpHashMode.Sha1, // default per RFC 6238 §1.2
        };
}
