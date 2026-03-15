namespace PassKey.Core.Models;

/// <summary>
/// Represents a saved personal identity profile in the PassKey vault.
/// Stores contact information, postal address, and government-issued document numbers.
/// Stored as part of the encrypted vault blob in the VaultData SQLite table.
/// </summary>
public sealed class IdentityEntry
{
    /// <summary>Gets or sets the unique identifier for this entry.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the user-defined display label for this identity profile (e.g., "Personal", "Work").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the person's first name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Gets or sets the person's last name (surname).</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Gets or sets the date of birth stored as a string (ISO 8601 format: yyyy-MM-dd).</summary>
    public string BirthDate { get; set; } = string.Empty;

    /// <summary>Gets or sets the primary email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets the primary phone number (including country code if applicable).</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Gets or sets the street address (number and street name).</summary>
    public string Street { get; set; } = string.Empty;

    /// <summary>Gets or sets the city of the postal address.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Gets or sets the province or state of the postal address.</summary>
    public string Province { get; set; } = string.Empty;

    /// <summary>Gets or sets the postal or ZIP code.</summary>
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the region or county within the country.</summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>Gets or sets the country of the postal address.</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>Gets or sets the national identity card number.</summary>
    public string IdCardNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets the national health insurance card number.</summary>
    public string HealthCardNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets the driving licence number.</summary>
    public string DrivingLicenseNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets the passport number.</summary>
    public string PassportNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets optional free-text notes for this identity entry.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when this entry was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the UTC timestamp of the last modification to this entry.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
