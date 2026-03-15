namespace PassKey.Core.Models;

/// <summary>
/// Represents a saved website credential in the PassKey vault.
/// Instances are serialized to JSON, encrypted with AES-GCM, and stored as a
/// single encrypted blob in the VaultData SQLite table.
/// </summary>
public sealed class PasswordEntry
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
}
