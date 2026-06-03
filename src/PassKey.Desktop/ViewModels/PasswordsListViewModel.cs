using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Core.Interfaces;
using PassKey.Core.Models;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Passwords list ViewModel: collection, sort, search, selected entry, detail panel state.
/// </summary>
public partial class PasswordsListViewModel : ObservableObject, IDisposable
{
    private readonly IVaultStateService _vaultState;
    private readonly IClipboardService _clipboard;
    private readonly IDialogQueueService _dialogQueue;
    private readonly IVaultRepository _repository;
    private readonly IToastService _toast;
    private readonly ResourceLoader _resourceLoader = new();
    private bool _disposed;

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
        IToastService toast,
        PasswordDetailViewModel detailViewModel)
    {
        _vaultState = vaultState;
        _clipboard = clipboard;
        _dialogQueue = dialogQueue;
        _repository = repository;
        _toast = toast;
        _detailVm = detailViewModel;

        // Hygiene: clear in-memory entries when the vault is locked so we never
        // retain decrypted data after lock. Named handler so it can be unsubscribed.
        _vaultState.VaultLocked += OnVaultLocked;
    }

    private void OnVaultLocked()
    {
        _allEntries = [];
        Entries.Clear();
        CloseDetail();
        SelectedEntry = null;
        SearchQuery = string.Empty;
        IsEmpty = false;
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
        {
            _clipboard.Copy(entry.Username, CopyType.Standard);
            _toast.Show(ToastSeverity.Info, _resourceLoader.GetString("ToastUsernameCopied"));
        }
    }

    [RelayCommand]
    private void CopyPassword(PasswordEntry? entry)
    {
        if (entry is not null && !string.IsNullOrEmpty(entry.Password))
        {
            _clipboard.Copy(entry.Password, CopyType.Sensitive);
            _toast.Show(ToastSeverity.Info, _resourceLoader.GetString("ToastPasswordCopied"));
        }
    }

    public void CloseDetail()
    {
        IsDetailOpen = false;
        DetailViewModel = null;
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync(PasswordEntry? entry)
    {
        // entry != null → quick-delete from a list row; entry == null → keyboard Delete on the selection.
        var target = entry ?? SelectedEntry;
        if (target is null) return;

        var confirmed = await _dialogQueue.ConfirmAsync(
            title: string.Format(_resourceLoader.GetString("DeleteConfirmTitle"), target.Title),
            content: string.Format(_resourceLoader.GetString("DeleteConfirmMessage"), target.Title),
            primaryButtonText: _resourceLoader.GetString("DeleteButton"),
            closeButtonText: _resourceLoader.GetString("CancelButton"));

        if (confirmed)
        {
            var vault = _vaultState.CurrentVault;
            var entryId = target.Id;
            vault?.Passwords.Remove(target);
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
            _toast.Show(ToastSeverity.Success, _resourceLoader.GetString("ToastDeleted"));
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
        _toast.Show(ToastSeverity.Success, _resourceLoader.GetString("ToastSaved"));
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
        _toast.Show(ToastSeverity.Success, _resourceLoader.GetString("ToastDeleted"));
    }
}
