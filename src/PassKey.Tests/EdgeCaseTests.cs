using PassKey.Core.Constants;
using PassKey.Core.Models;
using PassKey.Core.Services;

namespace PassKey.Tests;

public class EdgeCaseTests
{
    private readonly CryptoService _crypto = new();
    private readonly VaultService _vaultService;

    public EdgeCaseTests()
    {
        _vaultService = new VaultService(_crypto);
    }

    [Fact]
    public void Decrypt_TruncatedBlob_Throws()
    {
        var key = _crypto.GenerateRandomBytes(CryptoConstants.KeySizeBytes);

        // A valid blob is at least nonce(12) + tag(16) = 28 bytes
        var truncatedBlob = new byte[20];

        Assert.ThrowsAny<Exception>(() => _crypto.Decrypt(truncatedBlob, key));
    }

    [Fact]
    public void Decrypt_EmptyBlob_Throws()
    {
        var key = _crypto.GenerateRandomBytes(CryptoConstants.KeySizeBytes);

        Assert.ThrowsAny<Exception>(() => _crypto.Decrypt([], key));
    }

    [Fact]
    public void Encrypt_EmptyPlaintext_RoundTrips()
    {
        var key = _crypto.GenerateRandomBytes(CryptoConstants.KeySizeBytes);

        var blob = _crypto.Encrypt([], key);
        var decrypted = _crypto.Decrypt(blob, key);

        Assert.Empty(decrypted);
    }

    [Fact]
    public void PinnedSecureBuffer_WriteExceedsSize_Throws()
    {
        var buffer = new PinnedSecureBuffer(32);

        Assert.ThrowsAny<Exception>(() =>
            buffer.Write(new byte[64]));

        buffer.Dispose();
    }

    [Fact]
    public void PinnedSecureBuffer_DoubleDispose_NoThrow()
    {
        var buffer = new PinnedSecureBuffer(32);
        buffer.Write(_crypto.GenerateRandomBytes(32));

        buffer.Dispose();

        // Second dispose should not throw
        var ex = Record.Exception(() => buffer.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void PinnedSecureBuffer_AccessAfterDispose_Throws()
    {
        var buffer = new PinnedSecureBuffer(32);
        buffer.Write(_crypto.GenerateRandomBytes(32));
        buffer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => buffer.Span.ToArray());
        Assert.Throws<ObjectDisposedException>(() => buffer.ReadOnlySpan.ToArray());
    }

    [Fact]
    public void VaultService_DecryptWithWrongDek_Throws()
    {
        var (_, dek1) = _vaultService.InitializeVault("Pass1!word".AsSpan());
        var (_, dek2) = _vaultService.InitializeVault("Pass2!word".AsSpan());

        using (dek1)
        using (dek2)
        {
            var vault = new Vault();
            vault.Passwords.Add(new PasswordEntry { Title = "Test", Password = "secret" });

            var encrypted = _vaultService.EncryptVault(vault, dek1.ReadOnlySpan);

            // Decrypting with a different DEK should fail
            Assert.ThrowsAny<Exception>(() =>
                _vaultService.DecryptVault(encrypted, dek2.ReadOnlySpan));
        }
    }

    [Fact]
    public void CardTypeDetector_AllWhitespace_ReturnsUnknown()
    {
        var result = CardTypeDetector.Detect("   ");

        Assert.Equal(CardType.Unknown, result);
    }
}
