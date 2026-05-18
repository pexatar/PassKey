namespace PassKey.Core.Models;

/// <summary>
/// Represents a saved website credential in the PassKey vault.
/// Instances are serialized to JSON, encrypted with AES-GCM, and stored as a
/// single encrypted blob in the VaultData SQLite table.
/// </summary>
public sealed class PasswordEntry : IVaultEntry
{
    /// <summary>Gets or sets the unique identifier for this entry.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the display title (e.g., website name or service).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the login username or email address.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the plaintext password (encrypted at rest inside the vault blob).</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets the URL of the associated website or service.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets optional free-text notes for this entry.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the favicon / icon data using a three-way convention:
    /// <c>null</c> or empty string — display a letter avatar from <see cref="Title"/>;
    /// <c>"glyph:XXXX"</c> — display a Segoe MDL2 Assets FontIcon with the given hex code;
    /// any other value — Base64-encoded PNG/JPG/ICO image data (max 64 KB).
    /// </summary>
    public string? FaviconBase64 { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this entry was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the UTC timestamp of the last modification to this entry.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    // ─── TOTP / RFC 6238 fields (added in PassKey 2.0) ─────────────────────────
    //
    // These fields are *optional*: a v1.x vault deserialised by 2.0 simply has
    // TotpSecret == null (the JSON property is absent), and the UI hides the 2FA
    // section for entries without a configured secret. Conversely, 2.0 vaults
    // written back to disk include the fields whenever the user has imported a
    // QR / otpauth URI / Bitwarden seed for the entry.

    /// <summary>
    /// Base32-encoded shared secret (the "seed") used to derive TOTP codes,
    /// or <see langword="null"/> when this entry has no 2FA configured.
    /// Encrypted at rest like the rest of the entry (the field lives inside the
    /// encrypted vault blob — there is no plaintext exposure on disk).
    /// </summary>
    public string? TotpSecret { get; set; }

    /// <summary>
    /// HMAC algorithm used to compute the TOTP HOTP value. Defaults to <c>"SHA1"</c>
    /// for compatibility with Google Authenticator, Microsoft Authenticator and the
    /// vast majority of services (RFC 6238 §1.2). Other accepted values: <c>"SHA256"</c>,
    /// <c>"SHA512"</c>.
    /// </summary>
    public string TotpAlgorithm { get; set; } = "SHA1";

    /// <summary>Number of digits in the generated code (RFC 6238 §5.3). Default 6.</summary>
    public int TotpDigits { get; set; } = 6;

    /// <summary>Time-step in seconds (RFC 6238 §5.2). Default 30.</summary>
    public int TotpPeriod { get; set; } = 30;
}
