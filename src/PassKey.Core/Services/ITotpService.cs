using PassKey.Core.Models;

namespace PassKey.Core.Services;

/// <summary>
/// Generates RFC 6238 TOTP (Time-based One-Time Password) codes for vault entries.
/// </summary>
/// <remarks>
/// The service is stateless: each method derives the current code from the entry's
/// stored seed and parameters together with the current UTC time. The standard
/// settings (SHA1, 6 digits, 30 s) match Google Authenticator and the overwhelming
/// majority of online services; <see cref="PasswordEntry.TotpAlgorithm"/> /
/// <see cref="PasswordEntry.TotpDigits"/> / <see cref="PasswordEntry.TotpPeriod"/>
/// can be customised per entry for services that deviate (e.g. Steam, some bank tokens).
/// </remarks>
public interface ITotpService
{
    /// <summary>
    /// Computes the current TOTP code for <paramref name="entry"/>.
    /// </summary>
    /// <param name="entry">The vault entry whose <see cref="PasswordEntry.TotpSecret"/> drives the derivation.</param>
    /// <returns>
    /// A zero-padded numeric string of length <see cref="PasswordEntry.TotpDigits"/>
    /// (e.g. <c>"123456"</c>), or <see cref="string.Empty"/> if the entry has no TOTP
    /// configured or the stored seed is malformed.
    /// </returns>
    string GenerateCode(PasswordEntry entry);

    /// <summary>
    /// Returns the number of seconds remaining before the current code rolls over
    /// to the next one (always between 1 and <see cref="PasswordEntry.TotpPeriod"/>).
    /// </summary>
    int RemainingSeconds(PasswordEntry entry);

    /// <summary>
    /// Parses an <c>otpauth://</c> URI (as encoded inside a QR code) into the
    /// TOTP fields of a brand-new <see cref="PasswordEntry"/>. Returns the parsed
    /// entry or <see langword="null"/> if the URI is not a recognisable TOTP otpauth URL.
    /// </summary>
    /// <remarks>
    /// The format follows <see href="https://github.com/google/google-authenticator/wiki/Key-Uri-Format"/>:
    /// <code>otpauth://totp/Issuer:Account?secret=BASE32&amp;issuer=Issuer&amp;algorithm=SHA1&amp;digits=6&amp;period=30</code>
    /// Only the <c>secret</c> parameter is required.
    /// </remarks>
    PasswordEntry? ParseOtpAuthUri(string uri);

    /// <summary>
    /// Validates that <paramref name="secret"/> is a syntactically correct Base32 string
    /// (RFC 4648, uppercase A–Z and 2–7, optional <c>=</c> padding, whitespace ignored).
    /// </summary>
    bool IsValidBase32(string secret);
}
