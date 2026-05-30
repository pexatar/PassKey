using PassKey.Core.Models;

namespace PassKey.Core.Services;

public sealed class CsvImporter : ICsvImporter
{
    public Vault ParseCsv(string csvContent)
    {
        ArgumentNullException.ThrowIfNull(csvContent);

        var vault = new Vault();
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2) return vault; // Need at least header + 1 data row

        var headers = ParseCsvLine(lines[0]);
        var columnMap = MapHeaders(headers);

        if (columnMap.Count == 0) return vault;

        // NordPass and a few other exporters include a "type" column distinguishing
        // password / credit_card / identity / note rows in a single CSV. When present,
        // only "password" (or unrecognised/empty types) are imported here as
        // PasswordEntries; richer cross-type import is tracked for a future release.
        var typeColumnIndex = TryFindTypeColumn(headers);

        for (int i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
            if (fields.Count == 0) continue;

            // Skip non-password rows when a "type" column is present so card/identity/note
            // labels don't pollute the imported password list.
            if (typeColumnIndex >= 0 && typeColumnIndex < fields.Count)
            {
                var rowType = fields[typeColumnIndex].Trim().ToLowerInvariant();
                if (rowType.Length > 0 && rowType != "password" && rowType != "login")
                    continue;
            }

            var entry = new PasswordEntry
            {
                Id = Guid.NewGuid(),
                Title = GetField(fields, columnMap, ColumnType.Title),
                Username = GetField(fields, columnMap, ColumnType.Username),
                Password = GetField(fields, columnMap, ColumnType.Password),
                Url = GetField(fields, columnMap, ColumnType.Url),
                Notes = GetField(fields, columnMap, ColumnType.Notes),
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };

            // Skip rows with no meaningful data (judged on the raw imported fields).
            if (string.IsNullOrWhiteSpace(entry.Title) &&
                string.IsNullOrWhiteSpace(entry.Username) &&
                string.IsNullOrWhiteSpace(entry.Password))
                continue;

            // FU6a: exporters like Firefox omit a title column and identify logins only
            // by URL. Fall back to the URL host so the surviving entry isn't anonymous.
            if (string.IsNullOrWhiteSpace(entry.Title))
                entry.Title = DeriveTitleFromUrl(entry.Url);

            vault.Passwords.Add(entry);
        }

        vault.LastModified = DateTime.UtcNow;
        return vault;
    }

    private enum ColumnType { Title, Username, Password, Url, Notes }

    private static Dictionary<ColumnType, int> MapHeaders(List<string> headers)
    {
        var map = new Dictionary<ColumnType, int>();

        for (int i = 0; i < headers.Count; i++)
        {
            // Normalize first so that aliases differing only by separators/case match:
            // "Login Name", "login_name" and "login name" all collapse to "login name".
            var header = NormalizeHeader(headers[i]);
            switch (header)
            {
                // "account" is KeePass's title column; "login name" is now treated as a
                // username (it is KeePass's username field), not a title.
                case "title" or "name" or "service" or "account":
                    map.TryAdd(ColumnType.Title, i);
                    break;
                case "username" or "email" or "login" or "user" or "user name"
                    or "login username" or "login name":
                    map.TryAdd(ColumnType.Username, i);
                    break;
                case "password" or "pass" or "login password":
                    map.TryAdd(ColumnType.Password, i);
                    break;
                case "url" or "uri" or "website" or "web site" or "login uri":
                    map.TryAdd(ColumnType.Url, i);
                    break;
                case "notes" or "note" or "comment" or "comments" or "extra":
                    // "note" (singular) is required for NordPass exports.
                    map.TryAdd(ColumnType.Notes, i);
                    break;
            }
        }

        return map;
    }

    /// <summary>
    /// Normalizes a CSV header for alias matching: lowercased, trimmed, underscores
    /// treated as spaces, and runs of whitespace collapsed to a single space. This lets
    /// a single alias list cover exporters that write "login_name", "Login Name" or
    /// "login name" interchangeably (FU6b).
    /// </summary>
    private static string NormalizeHeader(string header)
    {
        var lowered = header.Trim().ToLowerInvariant().Replace('_', ' ');
        return string.Join(' ', lowered.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string GetField(List<string> fields, Dictionary<ColumnType, int> map, ColumnType type)
    {
        if (!map.TryGetValue(type, out var idx) || idx >= fields.Count)
            return string.Empty;
        return fields[idx].Trim();
    }

    /// <summary>
    /// Returns the zero-based index of a "type"-style column in the header row, or -1 if absent.
    /// Used to detect mixed-format exports (e.g. NordPass) so non-password rows can be skipped.
    /// </summary>
    private static int TryFindTypeColumn(List<string> headers)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            var name = headers[i].Trim().ToLowerInvariant();
            if (name == "type" || name == "item_type" || name == "entry_type")
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Derives a display title from a URL when the CSV has no usable title column
    /// (FU6a). Returns the host part of an absolute URL (e.g. "accounts.google.com"
    /// from "https://accounts.google.com/"), or the raw URL when it cannot be parsed
    /// as an absolute URI, or an empty string when no URL is available.
    /// </summary>
    internal static string DeriveTitleFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        var trimmed = url.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
            return uri.Host;

        return trimmed;
    }

    /// <summary>
    /// Parses a CSV line handling RFC 4180 quoted fields.
    /// </summary>
    internal static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Check for escaped quote ""
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else if (c == '\r')
                {
                    // Skip carriage return
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
