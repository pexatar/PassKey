using PassKey.Core.Constants;
using PassKey.Core.Services;

namespace PassKey.Tests;

public class CryptoServiceTests
{
    private readonly CryptoService _crypto = new();

    [Fact]
    public void GenerateRandomBytes_ReturnsCorrectLength()
    {
        var bytes = _crypto.GenerateRandomBytes(32);
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void GenerateRandomBytes_ReturnsUniqueValues()
    {
        var a = _crypto.GenerateRandomBytes(32);
        var b = _crypto.GenerateRandomBytes(32);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Encrypt_Decrypt_RoundTrip()
    {
        var key = _crypto.GenerateRandomBytes(CryptoConstants.KeySizeBytes);
        var plaintext = "Hello, PassKey PK4!"u8.ToArray();

        var blob = _crypto.Encrypt(plaintext, key);
        var decrypted = _crypto.Decrypt(blob, key);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_ProducesSelfDescribingBlob()
    {
        var key = _crypto.GenerateRandomBytes(CryptoConstants.KeySizeBytes);
        var plaintext = new byte[100];

        var blob = _crypto.Encrypt(plaintext, key);

        // blob = nonce(12) + ciphertext(100) + tag(16)
        Assert.Equal(
            CryptoConstants.NonceSizeBytes + 100 + CryptoConstants.TagSizeBytes,
            blob.Length);
    }

    [Fact]
    public void Decrypt_WithWrongKey_Throws()
    {
        var key1 = _crypto.GenerateRandomBytes(CryptoConstants.KeySizeBytes);
        var key2 = _crypto.GenerateRandomBytes(CryptoConstants.KeySizeBytes);
        var plaintext = "secret data"u8.ToArray();

        var blob = _crypto.Encrypt(plaintext, key1);

        Assert.ThrowsAny<Exception>(() => _crypto.Decrypt(blob, key2));
    }

    [Fact]
    public void Decrypt_WithTamperedBlob_Throws()
    {
        var key = _crypto.GenerateRandomBytes(CryptoConstants.KeySizeBytes);
        var plaintext = "secret data"u8.ToArray();

        var blob = _crypto.Encrypt(plaintext, key);
        blob[CryptoConstants.NonceSizeBytes + 5] ^= 0xFF; // tamper ciphertext

        Assert.ThrowsAny<Exception>(() => _crypto.Decrypt(blob, key));
    }

    [Fact]
    public void DeriveKeyFromPassword_ReturnsPinnedBuffer()
    {
        var salt = _crypto.GenerateRandomBytes(CryptoConstants.SaltSizeBytes);

        using var key = _crypto.DeriveKeyFromPassword("TestPassword123!".AsSpan(), salt, 1000);

        Assert.Equal(CryptoConstants.KeySizeBytes, key.Length);
        Assert.False(key.ReadOnlySpan.ToArray().All(b => b == 0));
    }

    [Fact]
    public void DeriveKeyFromPassword_SameInputProducesSameOutput()
    {
        var salt = _crypto.GenerateRandomBytes(CryptoConstants.SaltSizeBytes);

        using var key1 = _crypto.DeriveKeyFromPassword("TestPassword".AsSpan(), salt, 1000);
        using var key2 = _crypto.DeriveKeyFromPassword("TestPassword".AsSpan(), salt, 1000);

        Assert.Equal(key1.ReadOnlySpan.ToArray(), key2.ReadOnlySpan.ToArray());
    }

    [Fact]
    public void DeriveKeyFromPassword_DifferentSaltProducesDifferentOutput()
    {
        var salt1 = _crypto.GenerateRandomBytes(CryptoConstants.SaltSizeBytes);
        var salt2 = _crypto.GenerateRandomBytes(CryptoConstants.SaltSizeBytes);

        using var key1 = _crypto.DeriveKeyFromPassword("TestPassword".AsSpan(), salt1, 1000);
        using var key2 = _crypto.DeriveKeyFromPassword("TestPassword".AsSpan(), salt2, 1000);

        Assert.NotEqual(key1.ReadOnlySpan.ToArray(), key2.ReadOnlySpan.ToArray());
    }

    [Fact]
    public void DeriveKeyFromPassword_Argon2id_Deterministic()
    {
        var salt = _crypto.GenerateRandomBytes(CryptoConstants.SaltSizeBytes);

        using var key1 = _crypto.DeriveKeyFromPassword("TestPassword".AsSpan(), salt, 0, CryptoConstants.KdfAlgorithmArgon2Id);
        using var key2 = _crypto.DeriveKeyFromPassword("TestPassword".AsSpan(), salt, 0, CryptoConstants.KdfAlgorithmArgon2Id);

        Assert.Equal(key1.ReadOnlySpan.ToArray(), key2.ReadOnlySpan.ToArray());
        Assert.Equal(CryptoConstants.KeySizeBytes, key1.Length);
    }

    [Fact]
    public void DeriveKeyFromPassword_Argon2id_DifferentSalt_DifferentKey()
    {
        var salt1 = _crypto.GenerateRandomBytes(CryptoConstants.SaltSizeBytes);
        var salt2 = _crypto.GenerateRandomBytes(CryptoConstants.SaltSizeBytes);

        using var key1 = _crypto.DeriveKeyFromPassword("TestPassword".AsSpan(), salt1, 0, CryptoConstants.KdfAlgorithmArgon2Id);
        using var key2 = _crypto.DeriveKeyFromPassword("TestPassword".AsSpan(), salt2, 0, CryptoConstants.KdfAlgorithmArgon2Id);

        Assert.NotEqual(key1.ReadOnlySpan.ToArray(), key2.ReadOnlySpan.ToArray());
    }

    [Fact]
    public void PinnedSecureBuffer_ZerosOnDispose()
    {
        var buffer = new PinnedSecureBuffer(32);
        buffer.Write(_crypto.GenerateRandomBytes(32));

        var copy = buffer.ReadOnlySpan.ToArray();
        Assert.False(copy.All(b => b == 0));

        buffer.Dispose();

        // After dispose, accessing Span should throw
        Assert.Throws<ObjectDisposedException>(() => buffer.Span.ToArray());
    }
}
