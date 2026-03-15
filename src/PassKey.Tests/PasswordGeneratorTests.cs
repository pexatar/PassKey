using PassKey.Core.Services;

namespace PassKey.Tests;

public class PasswordGeneratorTests
{
    private readonly PasswordGenerator _generator = new();

    [Fact]
    public void Generate_ReturnsCorrectLength()
    {
        var options = new PasswordGeneratorOptions { Length = 24 };
        var password = _generator.Generate(options);
        Assert.Equal(24, password.Length);
    }

    [Fact]
    public void Generate_IncludesAllEnabledCharacterSets()
    {
        var options = new PasswordGeneratorOptions
        {
            Length = 32,
            IncludeUppercase = true,
            IncludeLowercase = true,
            IncludeDigits = true,
            IncludeSymbols = true
        };

        var password = _generator.Generate(options);

        Assert.Contains(password, c => char.IsUpper(c));
        Assert.Contains(password, c => char.IsLower(c));
        Assert.Contains(password, c => char.IsDigit(c));
        Assert.Contains(password, c => !char.IsLetterOrDigit(c));
    }

    [Fact]
    public void Generate_OnlyLowercase()
    {
        var options = new PasswordGeneratorOptions
        {
            Length = 16,
            IncludeUppercase = false,
            IncludeLowercase = true,
            IncludeDigits = false,
            IncludeSymbols = false
        };

        var password = _generator.Generate(options);

        Assert.All(password, c => Assert.True(char.IsLower(c)));
    }

    [Fact]
    public void Generate_ThrowsOnInvalidLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _generator.Generate(new PasswordGeneratorOptions { Length = 5 }));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _generator.Generate(new PasswordGeneratorOptions { Length = 200 }));
    }

    [Fact]
    public void Generate_ProducesUniquePasswords()
    {
        var options = new PasswordGeneratorOptions { Length = 16 };
        var passwords = Enumerable.Range(0, 10).Select(_ => _generator.Generate(options)).ToList();
        Assert.Equal(passwords.Count, passwords.Distinct().Count());
    }
}
