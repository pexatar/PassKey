using PassKey.Core.Constants;

namespace PassKey.Core.Models;

/// <summary>
/// Stores the cryptographic parameters required to derive the Key Encryption Key (KEK)
/// and decrypt the Data Encryption Key (DEK).  This record is stored in plaintext in the
/// VaultMetadata SQLite table so that the app can prompt for the master password without
/// needing to load the encrypted vault blob first.
/// </summary>
/// <remarks>
/// Two-layer key architecture:
/// <list type="number">
///   <item>Master password + <see cref="KekSalt"/> → KEK (via KDF specified by <see cref="KdfAlgorithm"/>)</item>
///   <item>KEK decrypts <see cref="EncryptedDek"/> (using <see cref="DekNonce"/>) → DEK</item>
///   <item>DEK decrypts the vault blob in VaultData</item>
/// </list>
/// Changing the master password re-wraps the DEK without re-encrypting the vault blob.
/// </remarks>
public sealed class VaultMetadata
{
    /// <summary>
    /// Gets or sets the random 32-byte salt used when deriving the KEK from the master password.
    /// A new salt is generated each time <see cref="KdfAlgorithm"/> is changed or the master password is set.
    /// </summary>
    public byte[] KekSalt { get; set; } = [];

    /// <summary>
    /// Gets or sets the AES-GCM encrypted DEK blob.
    /// Format: [nonce 12 B || ciphertext 32 B || tag 16 B] = 60 bytes total.
    /// </summary>
    public byte[] EncryptedDek { get; set; } = [];

    /// <summary>
    /// Gets or sets the 12-byte AES-GCM nonce used to encrypt the DEK.
    /// Stored separately from <see cref="EncryptedDek"/> for clarity and backward compatibility.
    /// </summary>
    public byte[] DekNonce { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of KDF iterations.
    /// Defaults to <see cref="CryptoConstants.DefaultKdfIterations"/> (600 000) for PBKDF2-SHA256.
    /// Not used by Argon2id (which uses fixed OWASP parameters).
    /// </summary>
    public int KdfIterations { get; set; } = CryptoConstants.DefaultKdfIterations;

    /// <summary>
    /// Gets or sets the key derivation algorithm identifier.
    /// Supported values: <c>"PBKDF2-SHA256"</c> (MVP default) and <c>"Argon2id"</c> (post-MVP).
    /// </summary>
    public string KdfAlgorithm { get; set; } = CryptoConstants.KdfAlgorithmPbkdf2;

    /// <summary>Gets or sets the vault schema version. Currently always 1.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the UTC timestamp when this vault was first created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
