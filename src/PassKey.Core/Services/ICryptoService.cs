using PassKey.Core.Constants;

namespace PassKey.Core.Services;

/// <summary>
/// Provides cryptographic primitives used by the vault: key derivation (PBKDF2-SHA256 and Argon2id),
/// symmetric authenticated encryption (AES-256-GCM), and secure random number generation.
/// </summary>
public interface ICryptoService
{
    /// <summary>
    /// Derives a 32-byte key from the master password using the specified KDF algorithm.
    /// The caller owns the returned <see cref="PinnedSecureBuffer"/> and must dispose it when done.
    /// </summary>
    /// <param name="password">The master password as a read-only character span. Cleared by the caller after use.</param>
    /// <param name="salt">The KDF salt (at least 32 bytes, unique per vault).</param>
    /// <param name="iterations">
    /// Number of KDF iterations. Used only for PBKDF2-SHA256.
    /// For Argon2id this parameter is ignored — the OWASP constants defined in
    /// <see cref="CryptoConstants"/> are used unconditionally.
    /// </param>
    /// <param name="kdfAlgorithm">
    /// The key derivation algorithm identifier. Use <see cref="CryptoConstants.KdfAlgorithmPbkdf2"/>
    /// (default) or <see cref="CryptoConstants.KdfAlgorithmArgon2Id"/>.
    /// </param>
    /// <returns>A <see cref="PinnedSecureBuffer"/> containing the 32-byte derived key.</returns>
    PinnedSecureBuffer DeriveKeyFromPassword(ReadOnlySpan<char> password, byte[] salt, int iterations,
        string kdfAlgorithm = CryptoConstants.KdfAlgorithmPbkdf2);

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> using AES-256-GCM with a randomly generated nonce.
    /// </summary>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <param name="key">A 32-byte AES key (e.g., the DEK from a <see cref="PinnedSecureBuffer"/>).</param>
    /// <returns>
    /// A self-contained blob with the format: <c>[nonce (12 bytes) || ciphertext || auth tag (16 bytes)]</c>.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is not 32 bytes.</exception>
    byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> key);

    /// <summary>
    /// Decrypts an AES-256-GCM blob produced by <see cref="Encrypt"/>.
    /// </summary>
    /// <param name="blob">
    /// The encrypted blob in the format: <c>[nonce (12 bytes) || ciphertext || auth tag (16 bytes)]</c>.
    /// </param>
    /// <param name="key">The 32-byte AES key used during encryption.</param>
    /// <returns>The decrypted plaintext bytes.</returns>
    /// <exception cref="CryptographicException">
    /// Thrown if the authentication tag does not match — indicating a wrong key, corruption, or tampering.
    /// </exception>
    byte[] Decrypt(ReadOnlySpan<byte> blob, ReadOnlySpan<byte> key);

    /// <summary>
    /// Generates a cryptographically secure random byte array using <see cref="System.Security.Cryptography.RandomNumberGenerator"/>.
    /// </summary>
    /// <param name="count">The number of random bytes to generate.</param>
    /// <returns>A new byte array filled with cryptographically secure random data.</returns>
    byte[] GenerateRandomBytes(int count);
}
