using PassKey.Core.Constants;

namespace PassKey.Core.Models;

/// <summary>
/// Represents a saved credit or debit card in the PassKey vault.
/// Fields follow the physical card flow: Number → Name → Expiry → CVV → metadata.
/// Stored as part of the encrypted vault blob in the VaultData SQLite table.
/// </summary>
public sealed class CreditCardEntry
{
    /// <summary>Gets or sets the unique identifier for this entry.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the user-defined display label for this card (e.g., "My Visa Platinum").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the category of use for this card (Personal, Work, Travel, Online).</summary>
    public CardCategory Category { get; set; } = CardCategory.Personal;

    /// <summary>Gets or sets the accent color used in the skeuomorphic card visual control.</summary>
    public CardColor AccentColor { get; set; } = CardColor.Default;

    /// <summary>Gets or sets the cardholder name as it appears on the physical card.</summary>
    public string CardholderName { get; set; } = string.Empty;

    /// <summary>Gets or sets the Primary Account Number (PAN) of the card.</summary>
    public string CardNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets the expiry month (1–12).</summary>
    public int ExpiryMonth { get; set; }

    /// <summary>Gets or sets the four-digit expiry year (e.g., 2026).</summary>
    public int ExpiryYear { get; set; }

    /// <summary>Gets the expiry date formatted as MM/YY for display (e.g., "03/26"). Returns an empty string if month or year is not set.</summary>
    public string ExpiryFormatted => ExpiryMonth > 0 && ExpiryYear > 0
        ? $"{ExpiryMonth:D2}/{ExpiryYear % 100:D2}"
        : string.Empty;

    /// <summary>Gets or sets the Card Verification Value (CVV/CVC/CID) printed on the card.</summary>
    public string Cvv { get; set; } = string.Empty;

    /// <summary>Gets or sets the card PIN (stored encrypted at rest).</summary>
    public string Pin { get; set; } = string.Empty;

    /// <summary>Gets or sets the detected or user-selected card network type (Visa, MasterCard, Amex, etc.).</summary>
    public CardType CardType { get; set; } = CardType.Unknown;

    /// <summary>Gets or sets optional free-text notes for this card entry.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when this entry was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the UTC timestamp of the last modification to this entry.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
