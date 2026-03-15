using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Constants;
using PassKey.Core.Models;
using PassKey.Core.Services;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Credit card detail ViewModel for add/edit panel.
/// Fields follow physical card flow: Number → Cardholder → Expiry → CVV → PIN → Label → Category → Color → Notes.
/// </summary>
public partial class CreditCardDetailViewModel : ObservableObject
{
    private readonly IVaultStateService _vaultState;
    private readonly IDialogQueueService _dialogQueue;

    private CreditCardEntry? _editingEntry;
    private bool _isNew;

    public bool IsNew => _isNew;

    [ObservableProperty]
    public partial string PanelTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CardNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CardholderName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int ExpiryMonth { get; set; }

    [ObservableProperty]
    public partial int ExpiryYear { get; set; }

    [ObservableProperty]
    public partial string Cvv { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Pin { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Label { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CardCategory Category { get; set; } = CardCategory.Personal;

    [ObservableProperty]
    public partial CardColor AccentColor { get; set; } = CardColor.Default;

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CardType DetectedCardType { get; set; } = CardType.Unknown;

    [ObservableProperty]
    public partial bool IsLuhnValid { get; set; }

    [ObservableProperty]
    public partial string FormattedCardNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanSave { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    /// <summary>Callback when save completes: (isNew, entryId).</summary>
    public Action<bool, Guid>? Saved { get; set; }

    /// <summary>Callback when delete completes: (entryId).</summary>
    public Action<Guid>? Deleted { get; set; }

    /// <summary>Callback when cancel is clicked.</summary>
    public Action? Cancelled { get; set; }

    public CreditCardDetailViewModel(
        IVaultStateService vaultState,
        IDialogQueueService dialogQueue)
    {
        _vaultState = vaultState;
        _dialogQueue = dialogQueue;
    }

    public void StartNew()
    {
        _editingEntry = null;
        _isNew = true;
        PanelTitle = "Aggiungi carta";
        CardNumber = string.Empty;
        CardholderName = string.Empty;
        ExpiryMonth = DateTime.Now.Month;
        ExpiryYear = DateTime.Now.Year;
        Cvv = string.Empty;
        Pin = string.Empty;
        Label = string.Empty;
        Category = CardCategory.Personal;
        AccentColor = CardColor.Default;
        Notes = string.Empty;
        DetectedCardType = CardType.Unknown;
        IsLuhnValid = false;
        FormattedCardNumber = string.Empty;
        UpdateCanSave();
    }

    public void StartEdit(CreditCardEntry entry)
    {
        _editingEntry = entry;
        _isNew = false;
        PanelTitle = "Modifica carta";
        CardNumber = entry.CardNumber;
        CardholderName = entry.CardholderName;
        ExpiryMonth = entry.ExpiryMonth;
        ExpiryYear = entry.ExpiryYear;
        Cvv = entry.Cvv;
        Pin = entry.Pin;
        Label = entry.Label;
        Category = entry.Category;
        AccentColor = entry.AccentColor;
        Notes = entry.Notes;
        DetectedCardType = entry.CardType;
        UpdateCardNumberDisplay();
        UpdateCanSave();
    }

    partial void OnCardNumberChanged(string value)
    {
        DetectedCardType = CardTypeDetector.Detect(value);
        IsLuhnValid = CardTypeDetector.ValidateLuhn(value);
        UpdateCardNumberDisplay();
        UpdateCanSave();
    }

    partial void OnCardholderNameChanged(string value) => UpdateCanSave();
    partial void OnExpiryMonthChanged(int value) => UpdateCanSave();
    partial void OnExpiryYearChanged(int value) => UpdateCanSave();
    partial void OnCvvChanged(string value) => UpdateCanSave();

    private void UpdateCardNumberDisplay()
    {
        FormattedCardNumber = CardTypeDetector.FormatCardNumber(CardNumber, DetectedCardType);
    }

    private void UpdateCanSave()
    {
        CanSave = !string.IsNullOrWhiteSpace(CardNumber) &&
                  !string.IsNullOrWhiteSpace(CardholderName) &&
                  ExpiryMonth >= 1 && ExpiryMonth <= 12 &&
                  ExpiryYear >= DateTime.Now.Year &&
                  !string.IsNullOrWhiteSpace(Cvv);
    }

    [RelayCommand]
    private Task SaveAsync()
    {
        if (!CanSave) return Task.CompletedTask;

        var vault = _vaultState.CurrentVault;
        if (vault is null) return Task.CompletedTask;

        IsSaving = true;

        try
        {
            bool wasNew = _isNew;
            Guid entryId;

            if (_isNew)
            {
                var entry = new CreditCardEntry
                {
                    CardNumber = CardNumber.Trim(),
                    CardholderName = CardholderName.Trim(),
                    ExpiryMonth = ExpiryMonth,
                    ExpiryYear = ExpiryYear,
                    Cvv = Cvv.Trim(),
                    Pin = Pin.Trim(),
                    Label = Label.Trim(),
                    Category = Category,
                    AccentColor = AccentColor,
                    CardType = DetectedCardType,
                    Notes = Notes.Trim()
                };
                vault.CreditCards.Add(entry);
                entryId = entry.Id;
            }
            else if (_editingEntry is not null)
            {
                _editingEntry.CardNumber = CardNumber.Trim();
                _editingEntry.CardholderName = CardholderName.Trim();
                _editingEntry.ExpiryMonth = ExpiryMonth;
                _editingEntry.ExpiryYear = ExpiryYear;
                _editingEntry.Cvv = Cvv.Trim();
                _editingEntry.Pin = Pin.Trim();
                _editingEntry.Label = Label.Trim();
                _editingEntry.Category = Category;
                _editingEntry.AccentColor = AccentColor;
                _editingEntry.CardType = DetectedCardType;
                _editingEntry.Notes = Notes.Trim();
                _editingEntry.ModifiedAt = DateTime.UtcNow;
                entryId = _editingEntry.Id;
            }
            else
            {
                return Task.CompletedTask;
            }

            Saved?.Invoke(wasNew, entryId);
        }
        finally
        {
            IsSaving = false;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_editingEntry is null || _isNew) return;

        var result = await _dialogQueue.EnqueueAndWait(() =>
        {
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Elimina carta",
                Content = $"Eliminare \"{(_editingEntry.Label is { Length: > 0 } l ? l : "Carta senza nome")}\"?\nQuesta azione è irreversibile.",
                PrimaryButtonText = "Elimina",
                CloseButtonText = "Annulla",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close
            };
            return dialog.ShowAsync().AsTask();
        });

        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
        {
            var vault = _vaultState.CurrentVault;
            var entryId = _editingEntry.Id;
            vault?.CreditCards.Remove(_editingEntry);
            Deleted?.Invoke(entryId);
        }
    }
}
