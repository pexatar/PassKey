using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Interfaces;
using PassKey.Core.Models;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Passwords list ViewModel: collection, sort, search, selected entry, detail panel state.
/// </summary>
public partial class PasswordsListViewModel : ObservableObject
{
    private readonly IVaultStateService _vaultState;
    private readonly IClipboardService _clipboard;
    private readonly IDialogQueueService _dialogQueue;
    private readonly IVaultRepository _repository;

    private List<PasswordEntry> _allEntries = [];

    public ObservableCollection<PasswordEntry> Entries { get; } = [];

    [ObservableProperty]
    public partial PasswordEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial bool IsDetailOpen { get; set; }

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SortField { get; set; } = "Title";

    [ObservableProperty]
    public partial bool SortAscending { get; set; } = true;

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    // Detail panel ViewModel
    [ObservableProperty]
    public partial PasswordDetailViewModel? DetailViewModel { get; set; }

    // Injected by ShellViewModel (avoids Service Locator)
    private readonly PasswordDetailViewModel _detailVm;

    public PasswordsListViewModel(
        IVaultStateService vaultState,
        IClipboardService clipboard,
        IDialogQueueService dialogQueue,
        IVaultRepository repository,
        PasswordDetailViewModel detailViewModel)
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
        _allEntries = vault?.Passwords ?? [];
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
                e.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                e.Username.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                e.Url.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

        var sorted = (SortField, SortAscending) switch
        {
            ("Title", true) => filtered.OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase),
            ("Title", false) => filtered.OrderByDescending(e => e.Title, StringComparer.OrdinalIgnoreCase),
            ("Username", true) => filtered.OrderBy(e => e.Username, StringComparer.OrdinalIgnoreCase),
            ("Username", false) => filtered.OrderByDescending(e => e.Username, StringComparer.OrdinalIgnoreCase),
            ("Url", true) => filtered.OrderBy(e => e.Url, StringComparer.OrdinalIgnoreCase),
            ("Url", false) => filtered.OrderByDescending(e => e.Url, StringComparer.OrdinalIgnoreCase),
            ("Date", true) => filtered.OrderBy(e => e.ModifiedAt),
            ("Date", false) => filtered.OrderByDescending(e => e.ModifiedAt),
            _ => filtered.OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
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
        DetailViewModel = _detailVm;
        IsDetailOpen = true;
    }

    [RelayCommand]
    private void EditEntry(PasswordEntry? entry)
    {
        if (entry is null) return;
        SelectedEntry = entry;
        _detailVm.StartEdit(entry);
        _detailVm.Saved = OnEntrySaved;
        _detailVm.Deleted = OnEntryDeleted;
        _detailVm.Cancelled = CloseDetail;
        DetailViewModel = _detailVm;
        IsDetailOpen = true;
    }

    [RelayCommand]
    private void CopyUsername(PasswordEntry? entry)
    {
        if (entry is not null && !string.IsNullOrEmpty(entry.Username))
            _clipboard.Copy(entry.Username, CopyType.Standard);
    }

    [RelayCommand]
    private void CopyPassword(PasswordEntry? entry)
    {
        if (entry is not null && !string.IsNullOrEmpty(entry.Password))
            _clipboard.Copy(entry.Password, CopyType.Sensitive);
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
                Title = "Elimina password",
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
            vault?.Passwords.Remove(SelectedEntry);
            await _vaultState.SaveVaultAsync();
            await _repository.LogActivityAsync(new ActivityLogEntry
            {
                EntityType = "PasswordEntry",
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
            EntityType = "PasswordEntry",
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
            EntityType = "PasswordEntry",
            EntityId = entryId,
            Action = "Deleted",
            Timestamp = DateTime.UtcNow
        });
        await LoadEntriesCommand.ExecuteAsync(null);
        CloseDetail();
    }
}
