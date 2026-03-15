using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Interfaces;
using PassKey.Core.Models;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Identities list ViewModel: collection, sort, search, CRUD, detail panel.
/// </summary>
public partial class IdentitiesListViewModel : ObservableObject
{
    private readonly IVaultStateService _vaultState;
    private readonly IClipboardService _clipboard;
    private readonly IDialogQueueService _dialogQueue;
    private readonly IVaultRepository _repository;

    private List<IdentityEntry> _allEntries = [];

    public ObservableCollection<IdentityEntry> Entries { get; } = [];

    [ObservableProperty]
    public partial IdentityEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial bool IsDetailOpen { get; set; }

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SortField { get; set; } = "Label";

    [ObservableProperty]
    public partial bool SortAscending { get; set; } = true;

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial IdentityDetailViewModel? DetailViewModel { get; set; }

    private readonly IdentityDetailViewModel _detailVm;

    public IdentitiesListViewModel(
        IVaultStateService vaultState,
        IClipboardService clipboard,
        IDialogQueueService dialogQueue,
        IVaultRepository repository,
        IdentityDetailViewModel detailViewModel)
    {
        _vaultState = vaultState;
        _clipboard = clipboard;
        _dialogQueue = dialogQueue;
        _repository = repository;
        _detailVm = detailViewModel;
    }

    [RelayCommand]
    public Task LoadEntriesAsync()
    {
        var vault = _vaultState.CurrentVault;
        _allEntries = vault?.Identities ?? [];
        ApplyFilterAndSort();
        return Task.CompletedTask;
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilterAndSort();
    }

    public void Sort(string field)
    {
        if (SortField == field)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortField = field;
            SortAscending = true;
        }
        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchQuery)
            ? _allEntries
            : _allEntries.Where(e =>
                e.Label.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                e.FirstName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                e.LastName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                e.Email.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

        var sorted = (SortField, SortAscending) switch
        {
            ("Label", true) => filtered.OrderBy(e => e.Label, StringComparer.OrdinalIgnoreCase),
            ("Label", false) => filtered.OrderByDescending(e => e.Label, StringComparer.OrdinalIgnoreCase),
            ("Name", true) => filtered.OrderBy(e => e.LastName, StringComparer.OrdinalIgnoreCase)
                                      .ThenBy(e => e.FirstName, StringComparer.OrdinalIgnoreCase),
            ("Name", false) => filtered.OrderByDescending(e => e.LastName, StringComparer.OrdinalIgnoreCase)
                                       .ThenByDescending(e => e.FirstName, StringComparer.OrdinalIgnoreCase),
            ("Email", true) => filtered.OrderBy(e => e.Email, StringComparer.OrdinalIgnoreCase),
            ("Email", false) => filtered.OrderByDescending(e => e.Email, StringComparer.OrdinalIgnoreCase),
            ("Date", true) => filtered.OrderBy(e => e.ModifiedAt),
            ("Date", false) => filtered.OrderByDescending(e => e.ModifiedAt),
            _ => filtered.OrderBy(e => e.Label, StringComparer.OrdinalIgnoreCase)
        };

        Entries.Clear();
        foreach (var entry in sorted)
            Entries.Add(entry);

        IsEmpty = Entries.Count == 0 && string.IsNullOrWhiteSpace(SearchQuery);
    }

    [RelayCommand]
    private void AddNew()
    {
        _detailVm.StartNew();
        _detailVm.Saved = OnEntrySaved;
        _detailVm.Deleted = OnEntryDeleted;
        _detailVm.Cancelled = CloseDetail;
        DetailViewModel = null;
        DetailViewModel = _detailVm;
        IsDetailOpen = true;
    }

    [RelayCommand]
    private void EditEntry(IdentityEntry? entry)
    {
        if (entry is null) return;
        SelectedEntry = entry;
        _detailVm.StartEdit(entry);
        _detailVm.Saved = OnEntrySaved;
        _detailVm.Deleted = OnEntryDeleted;
        _detailVm.Cancelled = CloseDetail;
        // Force PropertyChanged even when the same VM instance is reused
        // (CommunityToolkit skips notification if reference is equal)
        DetailViewModel = null;
        DetailViewModel = _detailVm;
        IsDetailOpen = true;
    }

    [RelayCommand]
    private void CopyEmail(IdentityEntry? entry)
    {
        if (entry is not null && !string.IsNullOrEmpty(entry.Email))
            _clipboard.Copy(entry.Email, CopyType.Standard);
    }

    [RelayCommand]
    private void CopyPhone(IdentityEntry? entry)
    {
        if (entry is not null && !string.IsNullOrEmpty(entry.Phone))
            _clipboard.Copy(entry.Phone, CopyType.Standard);
    }

    /// <summary>Raised after a successful save, for the View to show a toast.</summary>
    public event Action? SaveCompleted;

    public void CloseDetail()
    {
        IsDetailOpen = false;
        DetailViewModel = null;
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedEntry is null) return;

        var result = await _dialogQueue.EnqueueAndWait(() =>
        {
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Elimina identità",
                Content = $"Eliminare \"{SelectedEntry.Label}\"?\nQuesta azione è irreversibile.",
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
            vault?.Identities.Remove(SelectedEntry);
            await _vaultState.SaveVaultAsync();
            await _repository.LogActivityAsync(new ActivityLogEntry
            {
                EntityType = "IdentityEntry",
                EntityId = entryId,
                Action = "Deleted",
                Timestamp = DateTime.UtcNow
            });
            await LoadEntriesCommand.ExecuteAsync(null);
            CloseDetail();
        }
    }

    private async void OnEntrySaved(bool isNew, Guid entryId)
    {
        await _vaultState.SaveVaultAsync();
        await _repository.LogActivityAsync(new ActivityLogEntry
        {
            EntityType = "IdentityEntry",
            EntityId = entryId,
            Action = isNew ? "Created" : "Modified",
            Timestamp = DateTime.UtcNow
        });
        await LoadEntriesCommand.ExecuteAsync(null);
        CloseDetail();
        SaveCompleted?.Invoke();
    }

    private async void OnEntryDeleted(Guid entryId)
    {
        await _vaultState.SaveVaultAsync();
        await _repository.LogActivityAsync(new ActivityLogEntry
        {
            EntityType = "IdentityEntry",
            EntityId = entryId,
            Action = "Deleted",
            Timestamp = DateTime.UtcNow
        });
        await LoadEntriesCommand.ExecuteAsync(null);
        CloseDetail();
    }
}
