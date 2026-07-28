using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Core.Constants;
using PassKey.Core.Interfaces;
using PassKey.Core.Models;
using PassKey.Core.Services;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Credit cards list ViewModel: collection, sort, search, view toggle (card/list), detail panel.
/// </summary>
public partial class CreditCardsListViewModel : ObservableObject, IDisposable
{
    private readonly IVaultStateService _vaultState;
    private readonly IClipboardService _clipboard;
    private readonly IDialogQueueService _dialogQueue;
    private readonly IVaultRepository _repository;
    private readonly IToastService _toast;
    private readonly ResourceLoader _resourceLoader = new();
    private bool _disposed;

    private List<CreditCardEntry> _allEntries = [];

    public ObservableCollection<CreditCardEntry> Entries { get; } = [];

    [ObservableProperty]
    public partial CreditCardEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial bool IsDetailOpen { get; set; }

    /// <summary>
    /// Incremented after a save or delete so the view can rebuild its rows/cards.
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
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SortField { get; set; } = "Label";

    [ObservableProperty]
    public partial bool SortAscending { get; set; } = true;

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>
    /// True = card view (default), False = list view.
    /// </summary>
    [ObservableProperty]
    public partial bool IsCardView { get; set; } = true;

    [ObservableProperty]
    public partial CreditCardDetailViewModel? DetailViewModel { get; set; }

    private readonly CreditCardDetailViewModel _detailVm;

    public CreditCardsListViewModel(
        IVaultStateService vaultState,
        IClipboardService clipboard,
        IDialogQueueService dialogQueue,
        IVaultRepository repository,
        IToastService toast,
        CreditCardDetailViewModel detailViewModel)
    {
        _vaultState = vaultState;
        _clipboard = clipboard;
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
        IsDetailOpen = false;
        DetailViewModel = null;
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
        _allEntries = vault?.CreditCards ?? [];
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
                e.CardholderName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                e.CardNumber.Length >= 4 && e.CardNumber[^4..].Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

        var sorted = (SortField, SortAscending) switch
        {
            ("Label", true) => filtered.OrderBy(e => e.Label, StringComparer.OrdinalIgnoreCase),
            ("Label", false) => filtered.OrderByDescending(e => e.Label, StringComparer.OrdinalIgnoreCase),
            ("Cardholder", true) => filtered.OrderBy(e => e.CardholderName, StringComparer.OrdinalIgnoreCase),
            ("Cardholder", false) => filtered.OrderByDescending(e => e.CardholderName, StringComparer.OrdinalIgnoreCase),
            ("Last4", true) => filtered.OrderBy(e => e.CardNumber.Length >= 4 ? e.CardNumber[^4..] : ""),
            ("Last4", false) => filtered.OrderByDescending(e => e.CardNumber.Length >= 4 ? e.CardNumber[^4..] : ""),
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
    private void ToggleView()
    {
        IsCardView = !IsCardView;
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
    private void EditEntry(CreditCardEntry? entry)
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
    private void CopyCardNumber(CreditCardEntry? entry)
    {
        if (entry is not null && !string.IsNullOrEmpty(entry.CardNumber))
        {
            _clipboard.Copy(entry.CardNumber, CopyType.Sensitive);
            _toast.Show(ToastSeverity.Info, _resourceLoader.GetString("ToastCopied"));
        }
    }

    public void CloseDetail()
    {
        IsDetailOpen = false;
        DetailViewModel = null;
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync(CreditCardEntry? entry)
    {
        // entry != null → quick-delete from a list row; entry == null → keyboard Delete on the selection.
        var target = entry ?? SelectedEntry;
        if (target is null) return;

        var confirmed = await _dialogQueue.ConfirmAsync(
            title: string.Format(_resourceLoader.GetString("DeleteConfirmTitle"), target.Label),
            content: string.Format(_resourceLoader.GetString("DeleteConfirmMessage"), target.Label),
            primaryButtonText: _resourceLoader.GetString("DeleteButton"),
            closeButtonText: _resourceLoader.GetString("CancelButton"));

        if (confirmed)
        {
            var vault = _vaultState.CurrentVault;
            var entryId = target.Id;
            vault?.CreditCards.Remove(target);
            // AsyncRelayCommand rethrows on the UI thread by default: a persistence
            // failure here would terminate the process, so catch and surface it.
            try
            {
                await _vaultState.SaveVaultAsync();
                await _repository.LogActivityAsync(new ActivityLogEntry
                {
                    EntityType = "CreditCardEntry",
                    EntityId = entryId,
                    Action = "Deleted",
                    Timestamp = DateTime.UtcNow
                });
                await LoadEntriesCommand.ExecuteAsync(null);
                CloseDetail();
                _toast.Show(ToastSeverity.Info, _resourceLoader.GetString("ToastDeleted"));
            }
            catch (Exception)
            {
                await LoadEntriesCommand.ExecuteAsync(null);
                CloseDetail();
                _toast.Show(ToastSeverity.Error, _resourceLoader.GetString("ToastSaveError"));
            }
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
                EntityType = "CreditCardEntry",
                EntityId = entryId,
                Action = isNew ? "Created" : "Modified",
                Timestamp = DateTime.UtcNow
            });
            await LoadEntriesCommand.ExecuteAsync(null);
            ListRevision++;
            CloseDetail();
            _toast.Show(ToastSeverity.Success, _resourceLoader.GetString("ToastSaved"));
        }
        catch (Exception)
        {
            // The change is already in the in-memory vault and will persist with the
            // next successful save; keep the detail open so the user can retry Save.
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
                EntityType = "CreditCardEntry",
                EntityId = entryId,
                Action = "Deleted",
                Timestamp = DateTime.UtcNow
            });
            await LoadEntriesCommand.ExecuteAsync(null);
            ListRevision++;
            CloseDetail();
            _toast.Show(ToastSeverity.Info, _resourceLoader.GetString("ToastDeleted"));
        }
        catch (Exception)
        {
            // The entry is already removed from the in-memory vault: refresh the list
            // so the UI stays consistent (removal persists with the next successful save).
            await LoadEntriesCommand.ExecuteAsync(null);
            ListRevision++;
            CloseDetail();
            _toast.Show(ToastSeverity.Error, _resourceLoader.GetString("ToastSaveError"));
        }
    }
}
