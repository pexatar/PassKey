using System.IO.Compression;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using PassKey.Core.Models;
using PassKey.Core.Services;

namespace PassKey.Desktop.Services;

/// <summary>
/// Coordinates import of password data from multiple external formats into a <see cref="Vault"/>.
/// Supported formats: CSV (generic), Bitwarden JSON export, 1Password .1pux archive, and KeePass KDBX.
/// Each format is handled by a dedicated importer injected via constructor DI.
/// </summary>
public sealed class ImportOrchestrator : IImportOrchestrator
{
    private readonly ICsvImporter _csvImporter;
    private readonly IBitwardenImporter _bitwardenImporter;
    private readonly IOnePuxImporter _onePuxImporter;

    /// <summary>
    /// Initializes a new instance of <see cref="ImportOrchestrator"/>.
    /// </summary>
    /// <param name="csvImporter">Importer for generic CSV files.</param>
    /// <param name="bitwardenImporter">Importer for Bitwarden JSON export files.</param>
    /// <param name="onePuxImporter">Importer for 1Password .1pux archive files.</param>
    public ImportOrchestrator(
        ICsvImporter csvImporter,
        IBitwardenImporter bitwardenImporter,
        IOnePuxImporter onePuxImporter)
    {
        _csvImporter = csvImporter;
        _bitwardenImporter = bitwardenImporter;
        _onePuxImporter = onePuxImporter;
    }

    /// <summary>
    /// Parses the file at <paramref name="filePath"/> according to the specified <paramref name="format"/>
    /// and returns a <see cref="Vault"/> populated with the imported entries.
    /// </summary>
    /// <param name="filePath">Absolute path to the file to import.</param>
    /// <param name="format">The file format to use for parsing.</param>
    /// <param name="password">
    /// Optional password for encrypted formats (required for KDBX; ignored for CSV, Bitwarden, 1PUX).
    /// Defaults to an empty string.
    /// </param>
    /// <returns>A <see cref="Vault"/> containing the imported entries.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="format"/> is not supported.</exception>
    /// <exception cref="InvalidDataException">Thrown if the .1pux archive does not contain an <c>export.data</c> entry.</exception>
    public async Task<Vault> ParseFileAsync(string filePath, ImportFormat format, string? password = null)
    {
        return format switch
        {
            ImportFormat.Csv => await ParseCsvAsync(filePath),
            ImportFormat.Bitwarden => await ParseBitwardenAsync(filePath),
            ImportFormat.OnePux => await ParseOnePuxAsync(filePath),
            ImportFormat.Kdbx => await ParseKdbxAsync(filePath, password ?? string.Empty),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    /// <summary>
    /// Reads a CSV file and delegates parsing to <see cref="ICsvImporter.ParseCsv"/>.
    /// </summary>
    private async Task<Vault> ParseCsvAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        return _csvImporter.ParseCsv(content);
    }

    /// <summary>
    /// Reads a Bitwarden JSON export file and delegates parsing to <see cref="IBitwardenImporter.ParseBitwarden"/>.
    /// </summary>
    private async Task<Vault> ParseBitwardenAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        return _bitwardenImporter.ParseBitwarden(content);
    }

    /// <summary>
    /// Reads a 1Password .1pux archive (ZIP), extracts the <c>export.data</c> JSON entry,
    /// and delegates parsing to <see cref="IOnePuxImporter.ParseOnePux"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown if the archive does not contain an <c>export.data</c> entry.</exception>
    private async Task<Vault> ParseOnePuxAsync(string filePath)
    {
        // 1PUX is a ZIP containing export.data (JSON)
        string exportDataJson;
        await using (var stream = File.OpenRead(filePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            var entry = archive.GetEntry("export.data")
                ?? throw new InvalidDataException("The .1pux file does not contain 'export.data'.");

            using var reader = new StreamReader(entry.Open());
            exportDataJson = await reader.ReadToEndAsync();
        }

        return _onePuxImporter.ParseOnePux(exportDataJson);
    }

    /// <summary>
    /// Opens a KeePass KDBX database using the KeePassLib library, maps all non-recycled entries
    /// to <see cref="PasswordEntry"/> objects, and returns a <see cref="Vault"/>.
    /// Custom fields beyond the standard five (Title, UserName, Password, URL, Notes)
    /// are appended to the Notes text.
    /// Runs on a background thread via <see cref="Task.Run"/> because KeePassLib performs
    /// synchronous I/O and CPU-intensive key derivation.
    /// </summary>
    /// <param name="filePath">Path to the .kdbx file.</param>
    /// <param name="password">Master password for the KDBX database.</param>
    private Task<Vault> ParseKdbxAsync(string filePath, string password)
    {
        return Task.Run(() =>
        {
            var ioConnInfo = new IOConnectionInfo { Path = filePath };
            var compositeKey = new CompositeKey();
            compositeKey.AddUserKey(new KcpPassword(password));

            var db = new PwDatabase();
            db.Open(ioConnInfo, compositeKey, null);

            try
            {
                return MapKdbxToVault(db);
            }
            finally
            {
                db.Close();
            }
        });
    }

    /// <summary>
    /// Maps all non-recycled entries in a <see cref="PwDatabase"/> to a <see cref="Vault"/>.
    /// Entries in the Recycle Bin group are skipped. Completely empty entries (no title,
    /// username, or password) are also skipped.
    /// </summary>
    /// <param name="db">An open KeePass database.</param>
    /// <returns>A <see cref="Vault"/> populated with the mapped entries.</returns>
    private static Vault MapKdbxToVault(PwDatabase db)
    {
        var vault = new Vault();

        foreach (var entry in db.RootGroup.GetEntries(true))
        {
            // Skip deleted/recycled entries
            if (db.RecycleBinUuid != null &&
                db.RecycleBinUuid.Equals(entry.ParentGroup?.Uuid))
                continue;

            var pw = new PasswordEntry
            {
                Id = Guid.NewGuid(),
                Title = entry.Strings.ReadSafe("Title"),
                Username = entry.Strings.ReadSafe("UserName"),
                Password = entry.Strings.ReadSafe("Password"),
                Url = entry.Strings.ReadSafe("URL"),
                Notes = BuildKdbxNotes(entry),
                CreatedAt = entry.CreationTime.ToUniversalTime(),
                ModifiedAt = entry.LastModificationTime.ToUniversalTime()
            };

            // Skip completely empty entries
            if (string.IsNullOrEmpty(pw.Title) &&
                string.IsNullOrEmpty(pw.Username) &&
                string.IsNullOrEmpty(pw.Password))
                continue;

            vault.Passwords.Add(pw);
        }

        vault.LastModified = DateTime.UtcNow;
        return vault;
    }

    /// <summary>
    /// Builds the notes string for a KeePass entry by combining the standard Notes field
    /// with any custom fields (non-standard string entries) appended as key-value pairs.
    /// </summary>
    /// <param name="entry">The KeePass entry whose notes to build.</param>
    /// <returns>A formatted notes string, or an empty string if none.</returns>
    private static string BuildKdbxNotes(PwEntry entry)
    {
        var notes = entry.Strings.ReadSafe("Notes");

        // Append custom fields to notes
        var standardFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Title", "UserName", "Password", "URL", "Notes"
        };

        var customFields = new System.Text.StringBuilder();
        foreach (var kvp in entry.Strings)
        {
            if (standardFields.Contains(kvp.Key)) continue;
            var value = kvp.Value.ReadString();
            if (string.IsNullOrEmpty(value)) continue;
            customFields.AppendLine($"{kvp.Key}: {value}");
        }

        if (customFields.Length > 0)
        {
            if (!string.IsNullOrEmpty(notes)) notes += "\n\n";
            notes += customFields.ToString().TrimEnd();
        }

        return notes;
    }
}
