using System.Security.Cryptography;
using Konscious.Security.Cryptography;
using PassKey.Core.Constants;

namespace PassKey.Core.Services;

public sealed class CryptoService : ICryptoService
{
    public PinnedSecureBuffer DeriveKeyFromPassword(ReadOnlySpan<char> password, byte[] salt, int iterations,
        string kdfAlgorithm = CryptoConstants.KdfAlgorithmPbkdf2)
    {
        ArgumentNullException.ThrowIfNull(salt);

        var buffer = new PinnedSecureBuffer(CryptoConstants.KeySizeBytes);
        try
        {
            var byteCount = System.Text.Encoding.UTF8.GetByteCount(password);
            var passwordBytes = new byte[byteCount];
            try
            {
                System.Text.Encoding.UTF8.GetBytes(password, passwordBytes);

                if (kdfAlgorithm == CryptoConstants.KdfAlgorithmArgon2Id)
                {
                    using var argon2 = new Argon2id(passwordBytes)
                    {
                        Salt                = salt,
                        DegreeOfParallelism = CryptoConstants.Argon2Parallelism,
                        MemorySize          = CryptoConstants.Argon2MemoryCostKiB,
                        Iterations          = CryptoConstants.Argon2TimeCost
                    };
                    var derived = argon2.GetBytes(CryptoConstants.KeySizeBytes);
                    try   { derived.CopyTo(buffer.Span); }
                    finally { CryptographicOperations.ZeroMemory(derived); }
                }
                else
                {
                    // PBKDF2-SHA256 (legacy + backward compat)
                    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
                    Rfc2898DeriveBytes.Pbkdf2(
                        passwordBytes,
                        salt,
                        buffer.Span,
                        iterations,
                        HashAlgorithmName.SHA256);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(passwordBytes);
            }

            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    public byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> key)
    {
        if (key.Length != CryptoConstants.KeySizeBytes)
            throw new ArgumentException($"Key must be {CryptoConstants.KeySizeBytes} bytes.", nameof(key));

        // Output: [nonce 12B || ciphertext || tag 16B]
        var nonce = new byte[CryptoConstants.NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[CryptoConstants.TagSizeBytes];

        using var aes = new AesGcm(key, CryptoConstants.TagSizeBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        // Assemble self-describing blob
        var blob = new byte[CryptoConstants.NonceSizeBytes + ciphertext.Length + CryptoConstants.TagSizeBytes];
        nonce.CopyTo(blob, 0);
        ciphertext.CopyTo(blob, CryptoConstants.NonceSizeBytes);
        tag.CopyTo(blob, CryptoConstants.NonceSizeBytes + ciphertext.Length);

        return blob;
    }

    public byte[] Decrypt(ReadOnlySpan<byte> blob, ReadOnlySpan<byte> key)
    {
        if (key.Length != CryptoConstants.KeySizeBytes)
            throw new ArgumentException($"Key must be {CryptoConstants.KeySizeBytes} bytes.", nameof(key));

        var minBlobSize = CryptoConstants.NonceSizeBytes + CryptoConstants.TagSizeBytes;
        if (blob.Length < minBlobSize)
            throw new ArgumentException("Blob is too small to contain nonce and tag.", nameof(blob));

        var nonce = blob[..CryptoConstants.NonceSizeBytes];
        var ciphertextLength = blob.Length - CryptoConstants.NonceSizeBytes - CryptoConstants.TagSizeBytes;
        var ciphertext = blob.Slice(CryptoConstants.NonceSizeBytes, ciphertextLength);
        var tag = blob[^CryptoConstants.TagSizeBytes..];

        var plaintext = new byte[ciphertextLength];

        using var aes = new AesGcm(key, CryptoConstants.TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }

    public byte[] GenerateRandomBytes(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var bytes = new byte[count];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }
}
