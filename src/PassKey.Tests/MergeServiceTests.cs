using PassKey.Core.Models;
using PassKey.Core.Services;

namespace PassKey.Tests;

public class MergeServiceTests
{
    private readonly MergeService _merge = new();

    [Fact]
    public void SkipDuplicates_SamePassword_Skipped()
    {
        var target = new Vault();
        target.Passwords.Add(new() { Username = "user", Password = "pass", Url = "https://example.com" });

        var source = new Vault();
        source.Passwords.Add(new() { Username = "user", Password = "pass", Url = "https://example.com" });

        var result = _merge.MergeInto(target, source, ImportMergeStrategy.SkipDuplicates);

        Assert.Equal(0, result.PasswordsImported);
        Assert.Equal(1, result.Skipped);
        Assert.Single(target.Passwords);
    }

    [Fact]
    public void SkipDuplicates_DifferentPassword_Imported()
    {
        var target = new Vault();
        target.Passwords.Add(new() { Username = "user1", Password = "pass1", Url = "https://example.com" });

        var source = new Vault();
        source.Passwords.Add(new() { Username = "user2", Password = "pass2", Url = "https://other.com" });

        var result = _merge.MergeInto(target, source, ImportMergeStrategy.SkipDuplicates);

        Assert.Equal(1, result.PasswordsImported);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(2, target.Passwords.Count);
    }

    [Fact]
    public void Overwrite_DuplicatePassword_Overwritten()
    {
        var target = new Vault();
        target.Passwords.Add(new() { Title = "Old", Username = "user", Password = "pass", Url = "https://example.com" });

        var source = new Vault();
        source.Passwords.Add(new() { Title = "New", Username = "user", Password = "pass", Url = "https://example.com" });

        var result = _merge.MergeInto(target, source, ImportMergeStrategy.Overwrite);

        Assert.Equal(1, result.Overwritten);
        Assert.Single(target.Passwords);
        Assert.Equal("New", target.Passwords[0].Title);
    }

    [Fact]
    public void KeepBoth_DuplicatePassword_BothPresent()
    {
        var target = new Vault();
        target.Passwords.Add(new() { Title = "Original", Username = "user", Password = "pass", Url = "https://example.com" });

        var source = new Vault();
        source.Passwords.Add(new() { Title = "Duplicate", Username = "user", Password = "pass", Url = "https://example.com" });

        var result = _merge.MergeInto(target, source, ImportMergeStrategy.KeepBoth);

        Assert.Equal(1, result.PasswordsImported);
        Assert.Equal(2, target.Passwords.Count);
    }

    [Fact]
    public void UrlNormalization_HttpVsHttps_RecognizedAsDuplicate()
    {
        var target = new Vault();
        target.Passwords.Add(new() { Username = "user", Password = "pass", Url = "http://www.example.com/" });

        var source = new Vault();
        source.Passwords.Add(new() { Username = "user", Password = "pass", Url = "https://example.com" });

        var result = _merge.MergeInto(target, source, ImportMergeStrategy.SkipDuplicates);

        Assert.Equal(1, result.Skipped);
        Assert.Single(target.Passwords);
    }

    [Fact]
    public void EmptySource_NoChanges()
    {
        var target = new Vault();
        target.Passwords.Add(new() { Title = "Existing", Username = "u", Password = "p" });

        var source = new Vault();

        var result = _merge.MergeInto(target, source, ImportMergeStrategy.SkipDuplicates);

        Assert.Equal(0, result.PasswordsImported);
        Assert.Equal(0, result.Skipped);
        Assert.Single(target.Passwords);
    }

    [Fact]
    public void MergeAllTypes_CountsCorrect()
    {
        var target = new Vault();
        var source = new Vault();
        source.Passwords.Add(new() { Title = "PW", Username = "u", Password = "p" });
        source.CreditCards.Add(new() { CardNumber = "4111111111111111", CardholderName = "John", ExpiryMonth = 1, ExpiryYear = 2030 });
        source.Identities.Add(new() { FirstName = "Jane", LastName = "Doe", Email = "jane@test.com" });
        source.SecureNotes.Add(new() { Title = "Note", Content = "Content" });

        var result = _merge.MergeInto(target, source, ImportMergeStrategy.KeepBoth);

        Assert.Equal(1, result.PasswordsImported);
        Assert.Equal(1, result.CardsImported);
        Assert.Equal(1, result.IdentitiesImported);
        Assert.Equal(1, result.NotesImported);
    }

    [Fact]
    public void ImportedEntries_GetNewGuids()
    {
        var target = new Vault();
        var source = new Vault();
        var originalId = Guid.NewGuid();
        source.Passwords.Add(new() { Id = originalId, Title = "PW", Username = "u", Password = "p" });

        _merge.MergeInto(target, source, ImportMergeStrategy.KeepBoth);

        Assert.Single(target.Passwords);
        Assert.NotEqual(originalId, target.Passwords[0].Id);
        Assert.NotEqual(Guid.Empty, target.Passwords[0].Id);
    }

    [Theory]
    [InlineData("https://example.com", "example.com")]
    [InlineData("http://www.example.com/", "example.com")]
    [InlineData("HTTPS://WWW.Example.COM/path", "example.com/path")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    public void NormalizeUrl_VariousInputs_Correct(string input, string expected)
    {
        Assert.Equal(expected, MergeService.NormalizeUrl(input));
    }
}
