namespace PassKey.Desktop.Services;

/// <summary>
/// Discriminates between clipboard copy operations that differ in their auto-clear behaviour.
/// </summary>
public enum CopyType
{
    /// <summary>
    /// Content that does not require aggressive clearing (e.g. a username or URL).
    /// Auto-clear still applies after the global timeout.
    /// </summary>
    Standard,

    /// <summary>
    /// Sensitive content such as a password or CVV. Triggers auto-clear after 30 seconds
    /// and is excluded from Windows clipboard history via <c>ClipboardContentOptions.IsAllowedInHistory = false</c>.
    /// </summary>
    Sensitive
}

/// <summary>
/// Copies text to the Windows clipboard with optional 30-second auto-clear
/// and clipboard-history suppression for sensitive data.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Places <paramref name="content"/> on the clipboard.
    /// If <paramref name="type"/> is <see cref="CopyType.Sensitive"/>, the content is excluded
    /// from Windows clipboard history and is automatically cleared after 30 seconds.
    /// </summary>
    /// <param name="content">The text to copy.</param>
    /// <param name="type">The sensitivity level, controlling auto-clear and history suppression.</param>
    void Copy(string content, CopyType type);

    /// <summary>Raised immediately after content is placed on the clipboard.</summary>
    event Action<CopyType>? ContentCopied;

    /// <summary>Raised after the auto-clear timer expires and the clipboard is wiped.</summary>
    event Action? ContentCleared;
}
