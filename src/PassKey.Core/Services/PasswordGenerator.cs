using System.Security.Cryptography;

namespace PassKey.Core.Services;

public sealed class PasswordGenerator : IPasswordGenerator
{
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{}|;:',.<>?/~`";
    private const string AmbiguousChars = "0O1lI|";

    public string Generate(PasswordGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Length < 8 || options.Length > 128)
            throw new ArgumentOutOfRangeException(nameof(options), "Length must be between 8 and 128.");

        var charPool = BuildCharPool(options);
        if (charPool.Length == 0)
            throw new ArgumentException("At least one character set must be enabled.", nameof(options));

        // Generate password ensuring at least one char from each enabled set
        char[] password;
        do
        {
            password = new char[options.Length];
            for (var i = 0; i < options.Length; i++)
            {
                password[i] = charPool[RandomNumberGenerator.GetInt32(charPool.Length)];
            }
        } while (!MeetsRequirements(password, options));

        return new string(password);
    }

    private static string BuildCharPool(PasswordGeneratorOptions options)
    {
        var pool = string.Empty;
        if (options.IncludeUppercase) pool += Uppercase;
        if (options.IncludeLowercase) pool += Lowercase;
        if (options.IncludeDigits) pool += Digits;
        if (options.IncludeSymbols) pool += Symbols;

        if (options.ExcludeAmbiguous && pool.Length > 0)
            pool = new string(pool.Where(c => !AmbiguousChars.Contains(c)).ToArray());

        return pool;
    }

    private static bool MeetsRequirements(char[] password, PasswordGeneratorOptions options)
    {
        var exclude = options.ExcludeAmbiguous;
        if (options.IncludeUppercase && !password.Any(c => Uppercase.Contains(c) && (!exclude || !AmbiguousChars.Contains(c)))) return false;
        if (options.IncludeLowercase && !password.Any(c => Lowercase.Contains(c) && (!exclude || !AmbiguousChars.Contains(c)))) return false;
        if (options.IncludeDigits && !password.Any(c => Digits.Contains(c) && (!exclude || !AmbiguousChars.Contains(c)))) return false;
        if (options.IncludeSymbols && !password.Any(c => Symbols.Contains(c) && (!exclude || !AmbiguousChars.Contains(c)))) return false;
        return true;
    }
}
