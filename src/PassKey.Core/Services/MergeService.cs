using System.Security.Cryptography;
using System.Text;
using PassKey.Core.Models;

namespace PassKey.Core.Services;

public sealed class MergeService : IMergeService
{
    public ImportResult MergeInto(Vault target, Vault source, ImportMergeStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);

        var (pwImported,   pwSkipped,   pwOverwritten)   = MergeCollection(target.Passwords,   source.Passwords,   strategy, ComputePasswordHash);
        var (cardImported, cardSkipped, cardOverwritten) = MergeCollection(target.CreditCards, source.CreditCards, strategy, ComputeCardHash);
        var (idImported,   idSkipped,   idOverwritten)   = MergeCollection(target.Identities,  source.Identities,  strategy, ComputeIdentityHash);
        var (noteImported, noteSkipped, noteOverwritten) = MergeCollection(target.SecureNotes, source.SecureNotes, strategy, ComputeNoteHash);

        target.LastModified = DateTime.UtcNow;

        return new ImportResult
        {
            PasswordsImported  = pwImported,
            CardsImported      = cardImported,
            IdentitiesImported = idImported,
            NotesImported      = noteImported,
            Skipped    = pwSkipped    + cardSkipped    + idSkipped    + noteSkipped,
            Overwritten = pwOverwritten + cardOverwritten + idOverwritten + noteOverwritten
        };
    }

    private enum MergeAction { Imported, Skipped, Overwritten }

    private const int NoteHashSnippetLength = 256;

    /// <summary>
    /// Merges all entries from <paramref name="sourceList"/> into <paramref name="targetList"/>
    /// applying the given <paramref name="strategy"/> for duplicates.
    /// Returns a tuple of (imported, skipped, overwritten) counts.
    /// </summary>
    private static (int imported, int skipped, int overwritten) MergeCollection<T>(
        List<T> targetList,
        List<T> sourceList,
        ImportMergeStrategy strategy,
        Func<T, string> hashFunc) where T : class
    {
        int imported = 0, skipped = 0, overwritten = 0;
        var existingHashes = BuildHashSet(targetList, hashFunc);

        foreach (var entry in sourceList)
        {
            var hash = hashFunc(entry);
            var result = MergeEntry(targetList, entry, hash, existingHashes, strategy, hashFunc);
            switch (result)
            {
                case MergeAction.Imported:    imported++;    break;
                case MergeAction.Skipped:     skipped++;     break;
                case MergeAction.Overwritten: overwritten++; break;
            }
        }
        return (imported, skipped, overwritten);
    }

    private static MergeAction MergeEntry<T>(
        List<T> targetList, T entry, string hash,
        Dictionary<string, int> existingHashes,
        ImportMergeStrategy strategy,
        Func<T, string> hashFunc) where T : class
    {
        if (existingHashes.ContainsKey(hash))
        {
            switch (strategy)
            {
                case ImportMergeStrategy.SkipDuplicates:
                    return MergeAction.Skipped;

                case ImportMergeStrategy.Overwrite:
                    var idx = existingHashes[hash];
                    targetList[idx] = entry;
                    AssignNewId(entry);
                    return MergeAction.Overwritten;

                case ImportMergeStrategy.KeepBoth:
                    AssignNewId(entry);
                    targetList.Add(entry);
                    existingHashes[hashFunc(entry)] = targetList.Count - 1;
                    return MergeAction.Imported;
            }
        }

        AssignNewId(entry);
        targetList.Add(entry);
        existingHashes[hash] = targetList.Count - 1;
        return MergeAction.Imported;
    }

    private static void AssignNewId(object entry)
    {
        switch (entry)
        {
            case PasswordEntry pw: pw.Id = Guid.NewGuid(); break;
            case CreditCardEntry cc: cc.Id = Guid.NewGuid(); break;
            case IdentityEntry id: id.Id = Guid.NewGuid(); break;
            case SecureNoteEntry sn: sn.Id = Guid.NewGuid(); break;
        }
    }

    private static Dictionary<string, int> BuildHashSet<T>(List<T> entries, Func<T, string> hashFunc)
    {
        var dict = new Dictionary<string, int>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var hash = hashFunc(entries[i]);
            dict.TryAdd(hash, i);
        }
        return dict;
    }

    internal static string ComputePasswordHash(PasswordEntry entry)
    {
        var input = $"{entry.Username.ToLowerInvariant()}|{NormalizeUrl(entry.Url)}|{entry.Password}";
        return ComputeSha256(input);
    }

    internal static string ComputeCardHash(CreditCardEntry entry)
    {
        var last4 = entry.CardNumber.Length >= 4
            ? entry.CardNumber[^4..]
            : entry.CardNumber;
        var input = $"{last4}|{entry.ExpiryMonth}|{entry.ExpiryYear}|{entry.CardholderName.ToLowerInvariant()}";
        return ComputeSha256(input);
    }

    internal static string ComputeIdentityHash(IdentityEntry entry)
    {
        var input = $"{entry.FirstName.ToLowerInvariant()}|{entry.LastName.ToLowerInvariant()}|{entry.Email.ToLowerInvariant()}";
        return ComputeSha256(input);
    }

    internal static string ComputeNoteHash(SecureNoteEntry entry)
    {
        var contentSnippet = entry.Content.Length > NoteHashSnippetLength
            ? entry.Content[..NoteHashSnippetLength]
            : entry.Content;
        var input = $"{entry.Title.ToLowerInvariant()}|{contentSnippet}";
        return ComputeSha256(input);
    }

    internal static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        var normalized = url.ToLowerInvariant().Trim();

        // Strip protocol
        if (normalized.StartsWith("https://")) normalized = normalized[8..];
        else if (normalized.StartsWith("http://")) normalized = normalized[7..];

        // Strip www.
        if (normalized.StartsWith("www.")) normalized = normalized[4..];

        // Strip trailing slash
        normalized = normalized.TrimEnd('/');

        return normalized;
    }

    private static string ComputeSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
