using PassKey.Core.Models;

namespace PassKey.Core.Services;

public interface IPasswordStrengthAnalyzer
{
    PasswordStrengthResult Analyze(ReadOnlySpan<char> password);
}
