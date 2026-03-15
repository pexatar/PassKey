using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Constants;
using PassKey.Core.Interfaces;
using PassKey.Core.Models;
using PassKey.Core.Services;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Credit cards list ViewModel: collection, sort, search, view toggle (card/list), detail panel.
/// </summary>
public partial class CreditCardsListViewModel : ObservableObject
{
    private readonly IVaultStateService _vaultState;
    private readonly IClipboardService _clipboard;
    private readonly IDialogQueueService _dialogQueue;
    private readonly IVaultRepository _repository;

    private List<CreditCardEntry> _allEntries = [];

    public ObservableCollection<CreditCardEntry> Entries { get; } = [];

    [ObservableProperty]
    public partial CreditCardEntry? SelectedEntry { get; set; }

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
        CreditCardDetailViewModel detailViewModel)
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
            var masked = CardTypeDetector.MaskCardNumber(entry.CardNumber, entry.CardType);
            _clipboard.Copy(entry.CardNumber, CopyType.Sensitive);
        }
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
                Title = "Elimina carta",
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
            vault?.CreditCards.Remove(SelectedEntry);
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
        }
    }

    private async void OnEntrySaved(bool isNew, Guid entryId)
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
        CloseDetail();
        SaveCompleted?.Invoke();
    }

    private async void OnEntryDeleted(Guid entryId)
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
    }
}
