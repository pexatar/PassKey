using System.Diagnostics;
using PassKey.Core.Constants;
using PassKey.Core.Models;
using PassKey.Core.Services;

namespace PassKey.Tests;

public class PerformanceTests
{
    private readonly CryptoService _crypto = new();
    private readonly VaultService _vaultService;
    private readonly PasswordStrengthAnalyzer _analyzer = new();

    public PerformanceTests()
    {
        _vaultService = new VaultService(_crypto);
    }

    [Fact]
    public void EncryptDecrypt_1000Entries_Under2Seconds()
    {
        var (_, dek) = _vaultService.InitializeVault("PerfT3st!Pass".AsSpan());
        using (dek)
        {
            var vault = new Vault();

            // 250 entries per type = 1000 total
            for (var i = 0; i < 250; i++)
            {
                vault.Passwords.Add(new PasswordEntry
                {
                    Title = $"Password_{i}",
                    Username = $"user{i}@example.com",
                    Password = $"P@ss{i:D4}!word",
                    Url = $"https://site{i}.example.com",
                    Notes = $"Notes for entry {i} with some padding."
                });
                vault.CreditCards.Add(new CreditCardEntry
                {
                    Label = $"Card_{i}",
                    CardNumber = $"411111111111{i:D4}",
                    CardholderName = $"Cardholder {i}",
                    Cvv = "123",
                    Notes = $"Card notes {i}"
                });
                vault.Identities.Add(new IdentityEntry
                {
                    Label = $"Identity_{i}",
                    FirstName = $"First{i}",
                    LastName = $"Last{i}",
                    Email = $"id{i}@example.com",
                    Phone = $"+1-555-{i:D4}",
                    Street = $"{i} Main St",
                    City = "TestCity"
                });
                vault.SecureNotes.Add(new SecureNoteEntry
                {
                    Title = $"Note_{i}",
                    Content = $"This is the content for secure note {i}. It contains multiple lines\nof text to simulate real usage."
                });
            }

            var sw = Stopwatch.StartNew();

            var encrypted = _vaultService.EncryptVault(vault, dek.ReadOnlySpan);
            var decrypted = _vaultService.DecryptVault(encrypted, dek.ReadOnlySpan);

            sw.Stop();

            Assert.Equal(250, decrypted.Passwords.Count);
            Assert.True(sw.ElapsedMilliseconds < 2000,
                $"EncryptVault + DecryptVault took {sw.ElapsedMilliseconds}ms, expected < 2000ms");
        }
    }

    [Fact]
    public void KdfDerivation_600kIterations_Under5Seconds()
    {
        var salt = _crypto.GenerateRandomBytes(CryptoConstants.SaltSizeBytes);

        var sw = Stopwatch.StartNew();

        using var key = _crypto.DeriveKeyFromPassword(
            "TestP@ssword!".AsSpan(),
            salt,
            CryptoConstants.DefaultKdfIterations);

        sw.Stop();

        Assert.Equal(CryptoConstants.KeySizeBytes, key.Length);
        Assert.True(sw.ElapsedMilliseconds >= 50,
            $"KDF took only {sw.ElapsedMilliseconds}ms — suspiciously fast, might not be using 600k iterations");
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"KDF took {sw.ElapsedMilliseconds}ms, expected < 5000ms");
    }

    [Fact]
    public void StrengthAnalysis_500Passwords_Under50ms()
    {
        var generator = new PasswordGenerator();
        var passwords = new string[500];
        for (var i = 0; i < 500; i++)
        {
            passwords[i] = generator.Generate(new PasswordGeneratorOptions { Length = 16 });
        }

        var sw = Stopwatch.StartNew();

        foreach (var pw in passwords)
        {
            _analyzer.Analyze(pw.AsSpan());
        }

        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 50,
            $"Analyzing 500 passwords took {sw.ElapsedMilliseconds}ms, expected < 50ms");
    }

    [Fact]
    public void UrlMatching_500Entries_Under50ms()
    {
        var entries = new List<PasswordEntry>();
        for (var i = 0; i < 500; i++)
        {
            entries.Add(new PasswordEntry
            {
                Title = $"Site {i}",
                Username = $"user{i}",
                Password = "pass",
                Url = $"https://site{i}.example.com"
            });
        }

        var queryUrls = new[]
        {
            "https://site0.example.com/login",
            "https://site100.example.com/dashboard",
            "https://site250.example.com",
            "https://site499.example.com/auth",
            "https://unknown.example.com",
            "https://www.site50.example.com",
            "https://sub.site75.example.com",
            "https://site300.example.com:8080/path",
            "https://site150.example.com?q=test",
            "https://nomatch.different.org"
        };

        var sw = Stopwatch.StartNew();

        foreach (var url in queryUrls)
        {
            UrlMatcher.FindMatchingCredentials(entries, url);
        }

        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 50,
            $"URL matching 10 queries × 500 entries took {sw.ElapsedMilliseconds}ms, expected < 50ms");
    }
}
