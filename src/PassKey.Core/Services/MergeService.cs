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

        int pwImported = 0, cardImported = 0, idImported = 0, noteImported = 0;
        int skipped = 0, overwritten = 0;

        // Build hash sets of existing entries
        var existingPwHashes = BuildHashSet(target.Passwords, ComputePasswordHash);
        var existingCardHashes = BuildHashSet(target.CreditCards, ComputeCardHash);
        var existingIdHashes = BuildHashSet(target.Identities, ComputeIdentityHash);
        var existingNoteHashes = BuildHashSet(target.SecureNotes, ComputeNoteHash);

        // Merge passwords
        foreach (var entry in source.Passwords)
        {
            var hash = ComputePasswordHash(entry);
            var result = MergeEntry(target.Passwords, entry, hash, existingPwHashes, strategy, ComputePasswordHash);
            switch (result)
            {
                case MergeAction.Imported: pwImported++; break;
                case MergeAction.Skipped: skipped++; break;
                case MergeAction.Overwritten: overwritten++; break;
            }
        }

        // Merge credit cards
        foreach (var entry in source.CreditCards)
        {
            var hash = ComputeCardHash(entry);
            var result = MergeEntry(target.CreditCards, entry, hash, existingCardHashes, strategy, ComputeCardHash);
            switch (result)
            {
                case MergeAction.Imported: cardImported++; break;
                case MergeAction.Skipped: skipped++; break;
                case MergeAction.Overwritten: overwritten++; break;
            }
        }

        // Merge identities
        foreach (var entry in source.Identities)
        {
            var hash = ComputeIdentityHash(entry);
            var result = MergeEntry(target.Identities, entry, hash, existingIdHashes, strategy, ComputeIdentityHash);
            switch (result)
            {
                case MergeAction.Imported: idImported++; break;
                case MergeAction.Skipped: skipped++; break;
                case MergeAction.Overwritten: overwritten++; break;
            }
        }

        // Merge secure notes
        foreach (var entry in source.SecureNotes)
        {
            var hash = ComputeNoteHash(entry);
            var result = MergeEntry(target.SecureNotes, entry, hash, existingNoteHashes, strategy, ComputeNoteHash);
            switch (result)
            {
                case MergeAction.Imported: noteImported++; break;
                case MergeAction.Skipped: skipped++; break;
                case MergeAction.Overwritten: overwritten++; break;
            }
        }

        target.LastModified = DateTime.UtcNow;

        return new ImportResult
        {
            PasswordsImported = pwImported,
            CardsImported = cardImported,
            IdentitiesImported = idImported,
            NotesImported = noteImported,
            Skipped = skipped,
            Overwritten = overwritten
        };
    }

    private enum MergeAction { Imported, Skipped, Overwritten }

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
        var contentSnippet = entry.Content.Length > 256
            ? entry.Content[..256]
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
