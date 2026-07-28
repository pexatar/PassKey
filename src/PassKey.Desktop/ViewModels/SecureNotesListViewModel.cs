using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Windows.ApplicationModel.Resources;
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
public partial class SecureNotesListViewModel : ObservableObject, IDisposable
{
    private readonly IVaultStateService _vaultState;
    private readonly IDialogQueueService _dialogQueue;
    private readonly IVaultRepository _repository;
    private readonly IToastService _toast;
    private readonly ResourceLoader _resourceLoader = new();
    // Static loader for the static GetCategoryName/GetRelativeDate helpers.
    private static readonly ResourceLoader s_res = new();
    private bool _disposed;

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

    /// <summary>
    /// Incremented after a save or delete so the view can rebuild its rows.
    /// </summary>
    /// <remarks>
    /// An in-place edit changes the entry object the list already holds, which raises no
    /// collection change: the recycled row keeps showing the previous values. Bumping this
    /// counter gives the view an explicit, observable signal to regenerate the containers.
    /// Temporary bridge until rows bind to observable item objects.
    /// </remarks>
    [ObservableProperty]
    public partial int ListRevision { get; set; }

    [ObservableProperty]
    public partial SecureNoteDetailViewModel? DetailViewModel { get; set; }

    private readonly SecureNoteDetailViewModel _detailVm;

    public SecureNotesListViewModel(
        IVaultStateService vaultState,
        IDialogQueueService dialogQueue,
        IVaultRepository repository,
        IToastService toast,
        SecureNoteDetailViewModel detailViewModel)
    {
        _vaultState = vaultState;
        _dialogQueue = dialogQueue;
        _repository = repository;
        _toast = toast;
        _detailVm = detailViewModel;

        _vaultState.VaultLocked += OnVaultLocked;
    }

    private void OnVaultLocked()
    {
        _allEntries = [];
        Entries.Clear();
        IsEditorOpen = false;
        DetailViewModel = null;
        SelectedEntry = null;
        SearchQuery = string.Empty;
        FilterCategory = null;
        IsEmpty = false;
        IsFilteredEmpty = false;
    }

    /// <summary>Detaches the <see cref="IVaultStateService.VaultLocked"/> handler to prevent leaks.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _vaultState.VaultLocked -= OnVaultLocked;
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

        var confirmed = await _dialogQueue.ConfirmAsync(
            title: string.Format(_resourceLoader.GetString("DeleteConfirmTitle"), SelectedEntry.Title),
            content: string.Format(_resourceLoader.GetString("DeleteConfirmMessage"), SelectedEntry.Title),
            primaryButtonText: _resourceLoader.GetString("DeleteButton"),
            closeButtonText: _resourceLoader.GetString("CancelButton"));

        if (confirmed)
        {
            var vault = _vaultState.CurrentVault;
            var entryId = SelectedEntry.Id;
            vault?.SecureNotes.Remove(SelectedEntry);
            // AsyncRelayCommand rethrows on the UI thread by default: a persistence
            // failure here would terminate the process, so catch and surface it.
            try
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
                _toast.Show(ToastSeverity.Info, _resourceLoader.GetString("ToastDeleted"));
            }
            catch (Exception)
            {
                await LoadEntriesCommand.ExecuteAsync(null);
                CloseEditor();
                _toast.Show(ToastSeverity.Error, _resourceLoader.GetString("ToastSaveError"));
            }
        }
    }

    /// <summary>
    /// Called when pin is toggled instantly from the editor.
    /// Saves vault and refreshes list immediately (no Save button needed).
    /// </summary>
    private async void OnPinToggled()
    {
        // async void: an unhandled exception here would terminate the process, so
        // persistence failures (disk full, DB lock) must be caught and surfaced.
        try
        {
            await _vaultState.SaveVaultAsync();
            await LoadEntriesCommand.ExecuteAsync(null);
        }
        catch (Exception)
        {
            // The pin state is already in the in-memory vault and will persist with
            // the next successful save.
            _toast.Show(ToastSeverity.Error, _resourceLoader.GetString("ToastSaveError"));
        }
    }

    private async void OnEntrySaved(bool isNew, Guid entryId)
    {
        // async void: an unhandled exception here would terminate the process, so
        // persistence failures (disk full, DB lock) must be caught and surfaced.
        try
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
            ListRevision++;
            _toast.Show(ToastSeverity.Success, _resourceLoader.GetString("ToastSaved"));
        }
        catch (Exception)
        {
            // The change is already in the in-memory vault and will persist with the
            // next successful save; keep the editor open so the user can retry Save.
            _toast.Show(ToastSeverity.Error, _resourceLoader.GetString("ToastSaveError"));
        }
    }

    private async void OnEntryDeleted(Guid entryId)
    {
        // async void: an unhandled exception here would terminate the process, so
        // persistence failures (disk full, DB lock) must be caught and surfaced.
        try
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
            _toast.Show(ToastSeverity.Info, _resourceLoader.GetString("ToastDeleted"));
        }
        catch (Exception)
        {
            // The entry is already removed from the in-memory vault: refresh the list
            // so the UI stays consistent (removal persists with the next successful save).
            await LoadEntriesCommand.ExecuteAsync(null);
            CloseEditor();
            _toast.Show(ToastSeverity.Error, _resourceLoader.GetString("ToastSaveError"));
        }
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
            NoteCategory.General => s_res.GetString("NoteCategoryGeneral"),
            NoteCategory.Personal => s_res.GetString("NoteCategoryPersonal"),
            NoteCategory.Work => s_res.GetString("NoteCategoryWork"),
            NoteCategory.Financial => s_res.GetString("NoteCategoryFinancial"),
            NoteCategory.Medical => s_res.GetString("NoteCategoryMedical"),
            NoteCategory.Travel => s_res.GetString("NoteCategoryTravel"),
            NoteCategory.Education => s_res.GetString("NoteCategoryEducation"),
            NoteCategory.Legal => s_res.GetString("NoteCategoryLegal"),
            NoteCategory.Technical => s_res.GetString("NoteCategoryTechnical"),
            NoteCategory.Other => s_res.GetString("NoteCategoryOther"),
            _ => s_res.GetString("NoteCategoryGeneral")
        };
    }

    /// <summary>
    /// Get the localized relative date string for display in note cards.
    /// </summary>
    public static string GetRelativeDate(DateTime utcDate)
    {
        var local = utcDate.ToLocalTime();
        var now = DateTime.Now;
        var diff = now - local;
        var culture = new CultureInfo(s_res.GetString("NoteDateCulture"));

        if (diff.TotalMinutes < 1) return s_res.GetString("NoteTimeNow");
        if (diff.TotalMinutes < 60) return string.Format(s_res.GetString("NoteTimeMinutes"), (int)diff.TotalMinutes);
        if (diff.TotalHours < 24 && local.Date == now.Date) return string.Format(s_res.GetString("NoteTimeHours"), (int)diff.TotalHours);
        if (local.Date == now.Date.AddDays(-1)) return s_res.GetString("NoteTimeYesterday");
        if (diff.TotalDays < 7) return string.Format(s_res.GetString("NoteTimeDays"), (int)diff.TotalDays);
        if (local.Year == now.Year) return local.ToString("d MMM", culture);
        return local.ToString("d MMM yyyy", culture);
    }
}
