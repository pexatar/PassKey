using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Constants;
using PassKey.Core.Interfaces;
using PassKey.Core.Models;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Secure notes list ViewModel: master-detail layout with ComboBox category filter,
/// search, pin sorting, CRUD.
/// Left panel shows mini-cards with colored left border; right panel shows editor.
/// </summary>
public partial class SecureNotesListViewModel : ObservableObject
{
    private readonly IVaultStateService _vaultState;
    private readonly IDialogQueueService _dialogQueue;
    private readonly IVaultRepository _repository;

    private List<SecureNoteEntry> _allEntries = [];

    public ObservableCollection<SecureNoteEntry> Entries { get; } = [];

    [ObservableProperty]
    public partial SecureNoteEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial NoteCategory? FilterCategory { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial bool IsFilteredEmpty { get; set; }

    [ObservableProperty]
    public partial bool IsEditorOpen { get; set; }

    [ObservableProperty]
    public partial SecureNoteDetailViewModel? DetailViewModel { get; set; }

    /// <summary>Fired after a note is saved successfully (for toast notification).</summary>
    public event Action? SaveCompleted;

    private readonly SecureNoteDetailViewModel _detailVm;

    public SecureNotesListViewModel(
        IVaultStateService vaultState,
        IDialogQueueService dialogQueue,
        IVaultRepository repository,
        SecureNoteDetailViewModel detailViewModel)
    {
        _vaultState = vaultState;
        _dialogQueue = dialogQueue;
        _repository = repository;
        _detailVm = detailViewModel;
    }

    [RelayCommand]
    public Task LoadEntriesAsync()
    {
        var vault = _vaultState.CurrentVault;
        _allEntries = vault?.SecureNotes ?? [];
        ApplyFilterAndSort();
        return Task.CompletedTask;
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilterAndSort();
    }

    partial void OnFilterCategoryChanged(NoteCategory? value)
    {
        ApplyFilterAndSort();
    }

    /// <summary>
    /// Set category filter (null = all categories).
    /// Called by ComboBox SelectionChanged handler.
    /// </summary>
    public void SetFilter(NoteCategory? category)
    {
        FilterCategory = category;
    }

    private void ApplyFilterAndSort()
    {
        var filtered = _allEntries.AsEnumerable();

        // Category filter
        if (FilterCategory.HasValue)
            filtered = filtered.Where(e => e.Category == FilterCategory.Value);

        // Search filter
        if (!string.IsNullOrWhiteSpace(SearchQuery))
            filtered = filtered.Where(e =>
                e.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                e.Content.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

        // Sort: pinned first, then by most recently modified
        var sorted = filtered
            .OrderByDescending(e => e.IsPinned)
            .ThenByDescending(e => e.ModifiedAt);

        Entries.Clear();
        foreach (var entry in sorted)
            Entries.Add(entry);

        // Nessuna nota nel vault
        IsEmpty = _allEntries.Count == 0;
        // Filtri attivi ma 0 risultati
        IsFilteredEmpty = Entries.Count == 0 && !IsEmpty;
    }

    [RelayCommand]
    private void AddNew()
    {
        _detailVm.StartNew();
        _detailVm.Saved = OnEntrySaved;
        _detailVm.Deleted = OnEntryDeleted;
        _detailVm.Cancelled = CloseEditor;
        _detailVm.PinToggled = OnPinToggled;
        // Force PropertyChanged anche quando la stessa istanza VM viene riusata
        DetailViewModel = null;
        DetailViewModel = _detailVm;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void SelectNote(SecureNoteEntry? entry)
    {
        if (entry is null) return;
        SelectedEntry = entry;
        _detailVm.StartEdit(entry);
        _detailVm.Saved = OnEntrySaved;
        _detailVm.Deleted = OnEntryDeleted;
        _detailVm.Cancelled = CloseEditor;
        _detailVm.PinToggled = OnPinToggled;
        DetailViewModel = null;
        DetailViewModel = _detailVm;
        IsEditorOpen = true;
    }

    public void CloseEditor()
    {
        IsEditorOpen = false;
        DetailViewModel = null;
        SelectedEntry = null;
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedEntry is null) return;

        var result = await _dialogQueue.EnqueueAndWait(() =>
        {
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Elimina nota",
                Content = $"Eliminare \"{SelectedEntry.Title}\"?\nQuesta azione è irreversibile.",
                PrimaryButtonText = "Elimina",
                CloseButtonText = "Annulla",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close
            };
            return dialog.ShowAsync().AsTask();
        });

        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
        {
            var vault = _vaultState.CurrentVault;
            var entryId = SelectedEntry.Id;
            vault?.SecureNotes.Remove(SelectedEntry);
            await _vaultState.SaveVaultAsync();
            await _repository.LogActivityAsync(new ActivityLogEntry
            {
                EntityType = "SecureNoteEntry",
                EntityId = entryId,
                Action = "Deleted",
                Timestamp = DateTime.UtcNow
            });
            await LoadEntriesCommand.ExecuteAsync(null);
            CloseEditor();
        }
    }

    /// <summary>
    /// Called when pin is toggled instantly from the editor.
    /// Saves vault and refreshes list immediately (no Save button needed).
    /// </summary>
    private async void OnPinToggled()
    {
        await _vaultState.SaveVaultAsync();
        await LoadEntriesCommand.ExecuteAsync(null);
    }

    private async void OnEntrySaved(bool isNew, Guid entryId)
    {
        await _vaultState.SaveVaultAsync();
        await _repository.LogActivityAsync(new ActivityLogEntry
        {
            EntityType = "SecureNoteEntry",
            EntityId = entryId,
            Action = isNew ? "Created" : "Modified",
            Timestamp = DateTime.UtcNow
        });
        await LoadEntriesCommand.ExecuteAsync(null);
        SaveCompleted?.Invoke();
    }

    private async void OnEntryDeleted(Guid entryId)
    {
        await _vaultState.SaveVaultAsync();
        await _repository.LogActivityAsync(new ActivityLogEntry
        {
            EntityType = "SecureNoteEntry",
            EntityId = entryId,
            Action = "Deleted",
            Timestamp = DateTime.UtcNow
        });
        await LoadEntriesCommand.ExecuteAsync(null);
        CloseEditor();
    }

    // --- Static helpers ---

    /// <summary>
    /// Get the color hex for a note category.
    /// </summary>
    public static string GetCategoryColor(NoteCategory category)
    {
        return category switch
        {
            NoteCategory.General => "#607D8B",
            NoteCategory.Personal => "#2196F3",
            NoteCategory.Work => "#FF9800",
            NoteCategory.Financial => "#4CAF50",
            NoteCategory.Medical => "#F44336",
            NoteCategory.Travel => "#9C27B0",
            NoteCategory.Education => "#00BCD4",
            NoteCategory.Legal => "#795548",
            NoteCategory.Technical => "#3F51B5",
            NoteCategory.Other => "#9E9E9E",
            _ => "#607D8B"
        };
    }

    /// <summary>
    /// Get the localized display name for a note category.
    /// </summary>
    public static string GetCategoryName(NoteCategory category)
    {
        return category switch
        {
            NoteCategory.General => "Generale",
            NoteCategory.Personal => "Personale",
            NoteCategory.Work => "Lavoro",
            NoteCategory.Financial => "Finanziario",
            NoteCategory.Medical => "Medico",
            NoteCategory.Travel => "Viaggio",
            NoteCategory.Education => "Educazione",
            NoteCategory.Legal => "Legale",
            NoteCategory.Technical => "Tecnico",
            NoteCategory.Other => "Altro",
            _ => "Generale"
        };
    }

    /// <summary>
    /// Get Italian relative date string for display in note cards.
    /// </summary>
    public static string GetRelativeDate(DateTime utcDate)
    {
        var local = utcDate.ToLocalTime();
        var now = DateTime.Now;
        var diff = now - local;

        if (diff.TotalMinutes < 1) return "Adesso";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min fa";
        if (diff.TotalHours < 24 && local.Date == now.Date) return $"{(int)diff.TotalHours} ore fa";
        if (local.Date == now.Date.AddDays(-1)) return "Ieri";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}g fa";
        if (local.Year == now.Year) return local.ToString("d MMM", new CultureInfo("it-IT"));
        return local.ToString("d MMM yyyy", new CultureInfo("it-IT"));
    }
}
