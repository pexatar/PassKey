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

        for (int i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
            if (fields.Count == 0) continue;

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

            // Skip rows with no meaningful data
            if (string.IsNullOrWhiteSpace(entry.Title) &&
                string.IsNullOrWhiteSpace(entry.Username) &&
                string.IsNullOrWhiteSpace(entry.Password))
                continue;

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
            var header = headers[i].Trim().ToLowerInvariant();
            switch (header)
            {
                case "title" or "name" or "service" or "login_name":
                    map.TryAdd(ColumnType.Title, i);
                    break;
                case "username" or "email" or "login" or "user" or "login_username":
                    map.TryAdd(ColumnType.Username, i);
                    break;
                case "password" or "pass" or "login_password":
                    map.TryAdd(ColumnType.Password, i);
                    break;
                case "url" or "uri" or "website" or "login_uri":
                    map.TryAdd(ColumnType.Url, i);
                    break;
                case "notes" or "comment" or "comments" or "extra":
                    map.TryAdd(ColumnType.Notes, i);
                    break;
            }
        }

        return map;
    }

    private static string GetField(List<string> fields, Dictionary<ColumnType, int> map, ColumnType type)
    {
        if (!map.TryGetValue(type, out var idx) || idx >= fields.Count)
            return string.Empty;
        return fields[idx].Trim();
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
