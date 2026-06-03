namespace PassKey.Core.Services;

/// <summary>
/// Thrown when an import file cannot be processed for a reason worth surfacing to the
/// user verbatim — an encrypted export, an unsupported legacy format, or a malformed
/// archive (FU3). The <see cref="System.Exception.Message"/> is a ready-to-display,
/// user-facing string: the import flow shows it directly instead of importing an empty
/// vault silently or surfacing a raw technical error.
/// </summary>
public sealed class ImportFileException : Exception
{
    public ImportFileException(string message) : base(message) { }
}
