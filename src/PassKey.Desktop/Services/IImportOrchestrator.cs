using PassKey.Core.Models;

namespace PassKey.Desktop.Services;

/// <summary>
/// Routes import requests to the appropriate format-specific importer
/// (CSV, KDBX, 1PUX, Bitwarden) and returns a normalised <see cref="Vault"/> object
/// ready to be merged into the active vault via <c>IMergeService</c>.
/// </summary>
public interface IImportOrchestrator
{
    /// <summary>
    /// Parses the file at <paramref name="filePath"/> according to the specified <paramref name="format"/>
    /// and returns a <see cref="Vault"/> populated with the imported entries.
    /// </summary>
    /// <param name="filePath">Absolute path to the import file.</param>
    /// <param name="format">The source format to use for parsing.</param>
    /// <param name="password">
    /// Optional decryption password required for KDBX and password-protected Bitwarden exports.
    /// Pass <c>null</c> for unencrypted sources.
    /// </param>
    /// <returns>A <see cref="Vault"/> containing the parsed credentials.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the file format is invalid or the password is incorrect.</exception>
    Task<Vault> ParseFileAsync(string filePath, ImportFormat format, string? password = null);
}
