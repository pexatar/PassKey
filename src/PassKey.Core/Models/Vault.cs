namespace PassKey.Core.Models;

/// <summary>
/// Top-level in-memory vault container holding all four entry types.
/// At rest the vault is serialised to JSON, then encrypted with AES-GCM 256 using
/// the Data Encryption Key (DEK) and stored as a single blob in the VaultData table.
/// </summary>
public sealed class Vault
{
    /// <summary>Gets or sets the list of website credential entries.</summary>
    public List<PasswordEntry> Passwords { get; set; } = [];

    /// <summary>Gets or sets the list of credit/debit card entries.</summary>
    public List<CreditCardEntry> CreditCards { get; set; } = [];

    /// <summary>Gets or sets the list of personal identity profile entries.</summary>
    public List<IdentityEntry> Identities { get; set; } = [];

    /// <summary>Gets or sets the list of encrypted secure note entries.</summary>
    public List<SecureNoteEntry> SecureNotes { get; set; } = [];

    /// <summary>Gets or sets the UTC timestamp of the most recent modification to any entry in this vault.</summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
