using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Interfaces;
using PassKey.Core.Models;
using PassKey.Core.Services;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Dashboard ViewModel: greeting, 4 stat cards, 15 recent activity items, search.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IVaultStateService _vaultState;
    private readonly IVaultRepository _repository;
    private readonly IPasswordStrengthAnalyzer _strengthAnalyzer;
    private readonly IClipboardService _clipboard;

    // Localized greeting resources (set by View code-behind)
    private string _greetingMorning = "Buongiorno!";
    private string _greetingAfternoon = "Bentornato!";
    private string _greetingEvening = "Buonasera!";

    // Localized action labels (set by View code-behind)
    private string _labelCreated = "Aggiunto";
    private string _labelModified = "Modificato";
    private string _labelDeleted = "Eliminato";

    // Localized "deleted entity" fallback labels
    private string _deletedPassword = "Password eliminata";
    private string _deletedCard = "Carta eliminata";
    private string _deletedIdentity = "Identità eliminata";
    private string _deletedNote = "Nota eliminata";

    // Greeting
    [ObservableProperty]
    public partial string GreetingMessage { get; set; } = string.Empty;

    // Stat card totals
    [ObservableProperty]
    public partial int TotalPasswords { get; set; }

    [ObservableProperty]
    public partial int TotalCards { get; set; }

    [ObservableProperty]
    public partial int TotalIdentities { get; set; }

    [ObservableProperty]
    public partial int TotalNotes { get; set; }

    // Per-card weekly activity (4 types × 3 actions = 12 properties)
    [ObservableProperty]
    public partial int PasswordsAdded { get; set; }

    [ObservableProperty]
    public partial int PasswordsRemoved { get; set; }

    [ObservableProperty]
    public partial int PasswordsModified { get; set; }

    [ObservableProperty]
    public partial int CardsAdded { get; set; }

    [ObservableProperty]
    public partial int CardsRemoved { get; set; }

    [ObservableProperty]
    public partial int CardsModified { get; set; }

    [ObservableProperty]
    public partial int IdentitiesAdded { get; set; }

    [ObservableProperty]
    public partial int IdentitiesRemoved { get; set; }

    [ObservableProperty]
    public partial int IdentitiesModified { get; set; }

    [ObservableProperty]
    public partial int NotesAdded { get; set; }

    [ObservableProperty]
    public partial int NotesRemoved { get; set; }

    [ObservableProperty]
    public partial int NotesModified { get; set; }

    // Password health
    [ObservableProperty]
    public partial int VaultHealthScore { get; set; }

    [ObservableProperty]
    public partial int WeakPasswordCount { get; set; }

    // Expiring cards
    [ObservableProperty]
    public partial int ExpiringCardsCount { get; set; }

    [ObservableProperty]
    public partial bool HasExpiringCards { get; set; }

    // Recent activity
    public ObservableCollection<RecentActivityItem> RecentItems { get; } = [];

    // Empty state
    [ObservableProperty]
    public partial bool IsVaultEmpty { get; set; }

    // Search
    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    public ObservableCollection<SearchResultItem> SearchResults { get; } = [];

    [ObservableProperty]
    public partial bool HasSearchResults { get; set; }

    // Navigation event: fired when user clicks a recent item or search result
    public event Action<string, Guid>? NavigateToItemRequested;

    public DashboardViewModel(
        IVaultStateService vaultState,
        IVaultRepository repository,
        IPasswordStrengthAnalyzer strengthAnalyzer,
        IClipboardService clipboard)
    {
        _vaultState = vaultState;
        _repository = repository;
        _strengthAnalyzer = strengthAnalyzer;
        _clipboard = clipboard;
    }

    /// <summary>
    /// Set localized greeting resources from View code-behind (ResourceLoader).
    /// </summary>
    public void SetGreetingResources(string morning, string afternoon, string evening)
    {
        _greetingMorning = morning;
        _greetingAfternoon = afternoon;
        _greetingEvening = evening;
    }

    /// <summary>
    /// Set localized action labels from View code-behind (ResourceLoader).
    /// </summary>
    public void SetActionLabels(string created, string modified, string deleted)
    {
        _labelCreated = created;
        _labelModified = modified;
        _labelDeleted = deleted;
    }

    /// <summary>
    /// Set localized "deleted entity" fallback labels from View code-behind.
    /// </summary>
    public void SetDeletedLabels(string password, string card, string identity, string note)
    {
        _deletedPassword = password;
        _deletedCard = card;
        _deletedIdentity = identity;
        _deletedNote = note;
    }

    [RelayCommand]
    public async Task LoadDashboardAsync()
    {
        UpdateGreeting();
        UpdateStatCards();
        UpdatePasswordHealth();
        UpdateExpiringCards();
        await LoadRecentActivityAsync();
    }

    private void UpdateGreeting()
    {
        var hour = DateTime.Now.Hour;
        GreetingMessage = hour switch
        {
            >= 5 and < 12 => _greetingMorning,
            >= 12 and < 18 => _greetingAfternoon,
            _ => _greetingEvening
        };
    }

    private void UpdateStatCards()
    {
        var vault = _vaultState.CurrentVault;
        if (vault is null)
        {
            TotalPasswords = 0;
            TotalCards = 0;
            TotalIdentities = 0;
            TotalNotes = 0;
            IsVaultEmpty = true;
            return;
        }

        TotalPasswords = vault.Passwords.Count;
        TotalCards = vault.CreditCards.Count;
        TotalIdentities = vault.Identities.Count;
        TotalNotes = vault.SecureNotes.Count;

        IsVaultEmpty = TotalPasswords == 0 && TotalCards == 0 &&
                       TotalIdentities == 0 && TotalNotes == 0;
    }

    private async Task LoadRecentActivityAsync()
    {
        RecentItems.Clear();

        var activities = await _repository.GetRecentActivityAsync(15);
        var weekAgo = DateTime.UtcNow.AddDays(-7);

        int pwAdd = 0, pwRem = 0, pwMod = 0;
        int ccAdd = 0, ccRem = 0, ccMod = 0;
        int idAdd = 0, idRem = 0, idMod = 0;
        int snAdd = 0, snRem = 0, snMod = 0;

        foreach (var entry in activities)
        {
            RecentItems.Add(new RecentActivityItem
            {
                EntityType = entry.EntityType,
                EntityId = entry.EntityId,
                Action = entry.Action,
                Timestamp = entry.Timestamp,
                Title = ResolveTitle(entry),
                Subtitle = ResolveSubtitle(entry),
                IconGlyph = GetEntityIcon(entry.EntityType),
                ActionLabel = ResolveActionLabel(entry.Action),
                CopyValue = ResolveCopyValue(entry),
                Url = ResolveUrl(entry)
            });

            // Count weekly deltas per entity type
            if (entry.Timestamp >= weekAgo)
            {
                switch ((entry.EntityType, entry.Action))
                {
                    case ("PasswordEntry", "Created"): pwAdd++; break;
                    case ("PasswordEntry", "Deleted"): pwRem++; break;
                    case ("PasswordEntry", "Modified"): pwMod++; break;
                    case ("CreditCardEntry", "Created"): ccAdd++; break;
                    case ("CreditCardEntry", "Deleted"): ccRem++; break;
                    case ("CreditCardEntry", "Modified"): ccMod++; break;
                    case ("IdentityEntry", "Created"): idAdd++; break;
                    case ("IdentityEntry", "Deleted"): idRem++; break;
                    case ("IdentityEntry", "Modified"): idMod++; break;
                    case ("SecureNoteEntry", "Created"): snAdd++; break;
                    case ("SecureNoteEntry", "Deleted"): snRem++; break;
                    case ("SecureNoteEntry", "Modified"): snMod++; break;
                }
            }
        }

        PasswordsAdded = pwAdd; PasswordsRemoved = pwRem; PasswordsModified = pwMod;
        CardsAdded = ccAdd; CardsRemoved = ccRem; CardsModified = ccMod;
        IdentitiesAdded = idAdd; IdentitiesRemoved = idRem; IdentitiesModified = idMod;
        NotesAdded = snAdd; NotesRemoved = snRem; NotesModified = snMod;
    }

    private void UpdatePasswordHealth()
    {
        var vault = _vaultState.CurrentVault;
        if (vault is null || vault.Passwords.Count == 0)
        {
            VaultHealthScore = 0;
            WeakPasswordCount = 0;
            return;
        }

        int totalScore = 0, weak = 0;
        foreach (var pw in vault.Passwords)
        {
            var result = _strengthAnalyzer.Analyze(pw.Password.AsSpan());
            totalScore += result.Score;
            if (result.Score < 40) weak++;
        }

        VaultHealthScore = totalScore / vault.Passwords.Count;
        WeakPasswordCount = weak;
    }

    private void UpdateExpiringCards()
    {
        var vault = _vaultState.CurrentVault;
        if (vault is null || vault.CreditCards.Count == 0)
        {
            ExpiringCardsCount = 0;
            HasExpiringCards = false;
            return;
        }

        var now = DateTime.Now;
        var threshold = new DateTime(now.Year, now.Month, 1).AddMonths(2);
        ExpiringCardsCount = vault.CreditCards.Count(c =>
        {
            if (c.ExpiryYear <= 0 || c.ExpiryMonth <= 0) return false;
            var expiry = new DateTime(c.ExpiryYear, c.ExpiryMonth, 1).AddMonths(1).AddDays(-1);
            return expiry <= threshold && expiry >= now.AddDays(-1);
        });
        HasExpiringCards = ExpiringCardsCount > 0;
    }

    /// <summary>
    /// Copy a value to clipboard (used by View hover actions).
    /// </summary>
    public void CopyToClipboard(string value, CopyType copyType = CopyType.Standard)
    {
        if (!string.IsNullOrEmpty(value))
            _clipboard.Copy(value, copyType);
    }

    private string ResolveCopyValue(ActivityLogEntry entry)
    {
        var vault = _vaultState.CurrentVault;
        if (vault is null) return string.Empty;

        return entry.EntityType switch
        {
            "PasswordEntry" => vault.Passwords.FirstOrDefault(p => p.Id == entry.EntityId)?.Username ?? string.Empty,
            "CreditCardEntry" => vault.CreditCards.FirstOrDefault(c => c.Id == entry.EntityId)?.CardholderName ?? string.Empty,
            "IdentityEntry" => vault.Identities.FirstOrDefault(i => i.Id == entry.EntityId)?.Email ?? string.Empty,
            "SecureNoteEntry" => vault.SecureNotes.FirstOrDefault(n => n.Id == entry.EntityId)?.Title ?? string.Empty,
            _ => string.Empty
        };
    }

    private string? ResolveUrl(ActivityLogEntry entry)
    {
        if (entry.EntityType != "PasswordEntry") return null;
        var vault = _vaultState.CurrentVault;
        var url = vault?.Passwords.FirstOrDefault(p => p.Id == entry.EntityId)?.Url;
        return string.IsNullOrWhiteSpace(url) ? null : url;
    }

    /// <summary>
    /// Execute search across all vault types. Called by View with debounce.
    /// </summary>
    public void ExecuteSearch(string query)
    {
        SearchResults.Clear();
        HasSearchResults = false;

        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return;

        var vault = _vaultState.CurrentVault;
        if (vault is null) return;

        var comparison = StringComparison.OrdinalIgnoreCase;

        // Passwords (max 5)
        foreach (var p in vault.Passwords
            .Where(p => p.Title.Contains(query, comparison) ||
                        p.Username.Contains(query, comparison) ||
                        (p.Url?.Contains(query, comparison) ?? false))
            .Take(5))
        {
            SearchResults.Add(new SearchResultItem
            {
                EntityType = "PasswordEntry",
                EntityId = p.Id,
                Title = p.Title,
                Subtitle = p.Username,
                IconGlyph = "\uE8D7"
            });
        }

        // Cards (max 5)
        foreach (var c in vault.CreditCards
            .Where(c => c.Label.Contains(query, comparison) ||
                        c.CardholderName.Contains(query, comparison))
            .Take(5))
        {
            SearchResults.Add(new SearchResultItem
            {
                EntityType = "CreditCardEntry",
                EntityId = c.Id,
                Title = c.Label,
                Subtitle = c.CardholderName,
                IconGlyph = "\uE8C7"
            });
        }

        // Identities (max 5)
        foreach (var i in vault.Identities
            .Where(i => (i.Label?.Contains(query, comparison) ?? false) ||
                        i.FirstName.Contains(query, comparison) ||
                        i.LastName.Contains(query, comparison) ||
                        (i.Email?.Contains(query, comparison) ?? false))
            .Take(5))
        {
            SearchResults.Add(new SearchResultItem
            {
                EntityType = "IdentityEntry",
                EntityId = i.Id,
                Title = !string.IsNullOrEmpty(i.Label) ? i.Label : $"{i.FirstName} {i.LastName}",
                Subtitle = i.Email ?? string.Empty,
                IconGlyph = "\uE77B"
            });
        }

        // Notes (max 5)
        foreach (var n in vault.SecureNotes
            .Where(n => n.Title.Contains(query, comparison) ||
                        (n.Content?.Contains(query, comparison) ?? false))
            .Take(5))
        {
            SearchResults.Add(new SearchResultItem
            {
                EntityType = "SecureNoteEntry",
                EntityId = n.Id,
                Title = n.Title,
                Subtitle = n.Category.ToString(),
                IconGlyph = "\uE70B"
            });
        }

        HasSearchResults = SearchResults.Count > 0;
    }

    /// <summary>
    /// Navigate to an item from recent activity or search results.
    /// </summary>
    public void NavigateToItem(string entityType, Guid entityId)
    {
        NavigateToItemRequested?.Invoke(entityType, entityId);
    }

    private string ResolveTitle(ActivityLogEntry entry)
    {
        var vault = _vaultState.CurrentVault;
        if (vault is null) return entry.EntityType;

        return entry.EntityType switch
        {
            "PasswordEntry" => vault.Passwords.FirstOrDefault(p => p.Id == entry.EntityId)?.Title ?? _deletedPassword,
            "CreditCardEntry" => vault.CreditCards.FirstOrDefault(c => c.Id == entry.EntityId)?.Label ?? _deletedCard,
            "IdentityEntry" => vault.Identities.FirstOrDefault(i => i.Id == entry.EntityId)?.Label ?? _deletedIdentity,
            "SecureNoteEntry" => vault.SecureNotes.FirstOrDefault(n => n.Id == entry.EntityId)?.Title ?? _deletedNote,
            _ => entry.EntityType
        };
    }

    private string ResolveSubtitle(ActivityLogEntry entry)
    {
        var vault = _vaultState.CurrentVault;
        if (vault is null) return string.Empty;

        return entry.EntityType switch
        {
            "PasswordEntry" => vault.Passwords.FirstOrDefault(p => p.Id == entry.EntityId)?.Username ?? string.Empty,
            "CreditCardEntry" => vault.CreditCards.FirstOrDefault(c => c.Id == entry.EntityId)?.CardholderName ?? string.Empty,
            "IdentityEntry" => vault.Identities.FirstOrDefault(i => i.Id == entry.EntityId)?.Email ?? string.Empty,
            "SecureNoteEntry" => vault.SecureNotes.FirstOrDefault(n => n.Id == entry.EntityId)?.Category.ToString() ?? string.Empty,
            _ => string.Empty
        };
    }

    private string ResolveActionLabel(string action) => action switch
    {
        "Created" => _labelCreated,
        "Modified" => _labelModified,
        "Deleted" => _labelDeleted,
        _ => action
    };

    private static string GetEntityIcon(string entityType) => entityType switch
    {
        "PasswordEntry" => "\uE8D7",    // Key
        "CreditCardEntry" => "\uE8C7",  // ContactInfo (card)
        "IdentityEntry" => "\uE77B",    // Contact
        "SecureNoteEntry" => "\uE70B",  // Page
        _ => "\uE7C3"                   // List
    };
}

/// <summary>
/// Display model for recent activity list items.
/// </summary>
public sealed class RecentActivityItem
{
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public string Action { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = string.Empty;
    public string ActionLabel { get; init; } = string.Empty;
    public string CopyValue { get; init; } = string.Empty;
    public string? Url { get; init; }
    public bool HasUrl => !string.IsNullOrEmpty(Url);

    public string FormattedTime => Timestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}

/// <summary>
/// Display model for search result items.
/// </summary>
public sealed class SearchResultItem
{
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = string.Empty;

    public override string ToString() => Title;
}
