namespace PassKey.Core.Constants;

/// <summary>
/// Centralised cryptographic constants used throughout PassKey.
/// All values are chosen to meet or exceed current OWASP and NIST recommendations.
/// </summary>
public static class CryptoConstants
{
    /// <summary>AES key size in bytes (256-bit key for AES-256-GCM).</summary>
    public const int KeySizeBytes = 32;

    /// <summary>
    /// AES-GCM nonce size in bytes.
    /// 96-bit (12-byte) nonces are the recommended size per NIST SP 800-38D
    /// and provide the best performance with the GCM construction.
    /// </summary>
    public const int NonceSizeBytes = 12;

    /// <summary>
    /// AES-GCM authentication tag size in bytes (128-bit tag).
    /// A 128-bit tag is the maximum — and recommended — size per NIST SP 800-38D.
    /// </summary>
    public const int TagSizeBytes = 16;

    /// <summary>
    /// KDF salt size in bytes (256-bit salt).
    /// A unique random salt is generated for every new vault and every master password change.
    /// </summary>
    public const int SaltSizeBytes = 32;

    /// <summary>
    /// PBKDF2-SHA256 iteration count for legacy vault unlock.
    /// 600,000 iterations meets the OWASP 2023 minimum for PBKDF2-HMAC-SHA256.
    /// New vaults use Argon2id instead.
    /// </summary>
    public const int DefaultKdfIterations = 600_000;

    /// <summary>Identifier string for the PBKDF2-HMAC-SHA256 key derivation algorithm.</summary>
    public const string KdfAlgorithmPbkdf2 = "PBKDF2-SHA256";

    /// <summary>Identifier string for the Argon2id key derivation algorithm.</summary>
    public const string KdfAlgorithmArgon2Id = "Argon2id";

    // ─── Argon2id parameters — OWASP 2023 recommendation ─────────────────────
    // Reference: https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html
    // Minimum recommendation: m=19456 (19 MB), t=2, p=1.
    // PassKey uses stronger parameters: m=65536 (64 MB), t=3, p=4.

    /// <summary>
    /// Argon2id memory cost in kibibytes (64 MiB).
    /// Exceeds the OWASP 2023 minimum of 19 MiB. Raising this value increases resistance
    /// to GPU-based attacks at the cost of higher memory usage during unlock.
    /// </summary>
    public const int Argon2MemoryCostKiB = 65_536;

    /// <summary>
    /// Argon2id time cost (number of passes over memory).
    /// 3 passes exceeds the OWASP 2023 minimum of 2.
    /// </summary>
    public const int Argon2TimeCost = 3;

    /// <summary>
    /// Argon2id degree of parallelism (number of independent memory lanes).
    /// Set to 4 to utilise multiple CPU cores while remaining safe on single-core machines.
    /// </summary>
    public const int Argon2Parallelism = 4;
}
