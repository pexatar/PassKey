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
    /// A Bitwarden "export with attachments" is a ZIP wrapping a plaintext <c>data.json</c>
    /// (plus an <c>attachments/</c> folder PassKey doesn't handle); such files are unwrapped
    /// transparently so they import like a plain JSON export (FU3).
    /// </summary>
    private async Task<Vault> ParseBitwardenAsync(string filePath)
    {
        var content = IsZipFile(filePath)
            ? await ExtractBitwardenDataJsonAsync(filePath)
            : await File.ReadAllTextAsync(filePath);

        return _bitwardenImporter.ParseBitwarden(content);
    }

    /// <summary>Returns true if the file begins with the ZIP local-file-header magic "PK".</summary>
    private static bool IsZipFile(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            return fs.ReadByte() == 0x50 && fs.ReadByte() == 0x4B; // 'P','K'
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts the <c>data.json</c> entry from a Bitwarden ZIP export. Throws an
    /// <see cref="ImportFileException"/> with a user-facing message if it is absent.
    /// </summary>
    private static async Task<string> ExtractBitwardenDataJsonAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var entry = archive.GetEntry("data.json")
            ?? throw new ImportFileException("IMPORT_BW_ZIP");

        using var reader = new StreamReader(entry.Open());
        return await reader.ReadToEndAsync();
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
                ?? throw new ImportFileException("IMPORT_1PUX");

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
            // FU3: a KeePass 1.x database (.kdb) shares the first four signature bytes with
            // KDBX but differs on the 5th (0x65 vs 0x67). KeePassLib cannot read it, so give
            // a clear, actionable message instead of an opaque library exception.
            if (IsKeePass1File(filePath))
                throw new ImportFileException("IMPORT_KEEPASS_1X");

            var ioConnInfo = new IOConnectionInfo { Path = filePath };
            var compositeKey = new CompositeKey();
            compositeKey.AddUserKey(new KcpPassword(password));

            var db = new PwDatabase();
            try
            {
                db.Open(ioConnInfo, compositeKey, null);
                return MapKdbxToVault(db);
            }
            catch (ImportFileException)
            {
                throw;
            }
            catch (Exception)
            {
                // Wrong password or a file that isn't a valid KDBX 2.x database.
                throw new ImportFileException("IMPORT_KEEPASS_OPEN");
            }
            finally
            {
                try { db.Close(); } catch { /* never opened, or already closed */ }
            }
        });
    }

    /// <summary>
    /// Detects the legacy KeePass 1.x (.kdb) file format by its 8-byte signature
    /// <c>03 D9 A2 9A 65 FB 4B B5</c> — identical to KDBX except the 5th byte is 0x65
    /// (KDBX uses 0x67).
    /// </summary>
    private static bool IsKeePass1File(string filePath)
    {
        try
        {
            Span<byte> sig = stackalloc byte[8];
            using var fs = File.OpenRead(filePath);
            if (fs.Read(sig) < 8) return false;
            return sig[0] == 0x03 && sig[1] == 0xD9 && sig[2] == 0xA2 && sig[3] == 0x9A
                && sig[4] == 0x65 && sig[5] == 0xFB && sig[6] == 0x4B && sig[7] == 0xB5;
        }
        catch
        {
            return false;
        }
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
