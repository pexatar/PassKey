namespace PassKey.Core.Models;

/// <summary>
/// Immutable result produced by <see cref="PassKey.Core.Services.IPasswordStrengthAnalyzer"/>
/// after evaluating a candidate password.
/// </summary>
public sealed class PasswordStrengthResult
{
    /// <summary>
    /// Gets the overall strength score on a 0–4 scale.
    /// <list type="bullet">
    ///   <item>0 — Very weak</item>
    ///   <item>1 — Weak</item>
    ///   <item>2 — Moderate</item>
    ///   <item>3 — Strong</item>
    ///   <item>4 — Very strong</item>
    /// </list>
    /// </summary>
    public int Score { get; init; }

    /// <summary>
    /// Gets a short human-readable label corresponding to <see cref="Score"/>
    /// (e.g., <c>"Weak"</c>, <c>"Strong"</c>).
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets a human-readable estimate of how long it would take to crack this password
    /// by brute force (e.g., <c>"3 years"</c>, <c>"centuries"</c>).
    /// </summary>
    public string EstimatedCrackTime { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the password meets the minimum length requirement (8 characters).</summary>
    public bool HasMinLength { get; init; }

    /// <summary>Gets a value indicating whether the password meets the recommended length (16+ characters).</summary>
    public bool HasRecommendedLength { get; init; }

    /// <summary>Gets a value indicating whether the password contains at least one uppercase letter (A–Z).</summary>
    public bool HasUppercase { get; init; }

    /// <summary>Gets a value indicating whether the password contains at least one lowercase letter (a–z).</summary>
    public bool HasLowercase { get; init; }

    /// <summary>Gets a value indicating whether the password contains at least one digit (0–9).</summary>
    public bool HasDigits { get; init; }

    /// <summary>Gets a value indicating whether the password contains at least one non-alphanumeric symbol.</summary>
    public bool HasSymbols { get; init; }

    /// <summary>
    /// Gets a value indicating whether the password avoids common patterns
    /// (keyboard walks, repeated characters, dictionary words, date patterns).
    /// </summary>
    public bool HasNoCommonPatterns { get; init; }

    /// <summary>
    /// Gets a list of actionable improvement suggestions for the user.
    /// Empty when <see cref="Score"/> is 4.
    /// </summary>
    public List<string> Suggestions { get; init; } = [];
}
