using CommunityToolkit.Mvvm.ComponentModel;
using PassKey.Core.Constants;
using PassKey.Core.Models;
using PassKey.Core.Services;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels.Base;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Credit card detail ViewModel for add/edit panel.
/// Fields follow physical card flow: Number → Cardholder → Expiry → CVV → PIN → Label → Category → Color → Notes.
/// Shared add/edit/save/delete plumbing is provided by <see cref="BaseDetailViewModel{TEntry}"/>.
/// </summary>
public partial class CreditCardDetailViewModel : BaseDetailViewModel<CreditCardEntry>
{
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

    // ── Inline validation (T5.6) ───────────────────────────────────────────────

    [ObservableProperty]
    public partial bool IsCardNumberEmpty { get; set; }

    [ObservableProperty]
    public partial bool IsCardholderNameEmpty { get; set; }

    [ObservableProperty]
    public partial bool IsCvvEmpty { get; set; }

    public CreditCardDetailViewModel(
        IVaultStateService vaultState,
        IDialogQueueService dialogQueue)
        : base(vaultState, dialogQueue)
    {
    }

    // ─── Template-method overrides ────────────────────────────────────────────

    protected override string GetDeleteDisplayName(CreditCardEntry entry)
        => entry.Label is { Length: > 0 } l ? l : _res.GetString("CardNoName");

    protected override IList<CreditCardEntry> GetVaultCollection(Vault vault) => vault.CreditCards;

    protected override void ResetFieldsForNew()
    {
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
        IsCardNumberEmpty = true;
        IsCardholderNameEmpty = true;
        IsCvvEmpty = true;
    }

    protected override void LoadFromEntry(CreditCardEntry entry)
    {
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
    }

    protected override CreditCardEntry CreateNewEntry() => new()
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

    protected override void ApplyToEntry(CreditCardEntry entry)
    {
        entry.CardNumber = CardNumber.Trim();
        entry.CardholderName = CardholderName.Trim();
        entry.ExpiryMonth = ExpiryMonth;
        entry.ExpiryYear = ExpiryYear;
        entry.Cvv = Cvv.Trim();
        entry.Pin = Pin.Trim();
        entry.Label = Label.Trim();
        entry.Category = Category;
        entry.AccentColor = AccentColor;
        entry.CardType = DetectedCardType;
        entry.Notes = Notes.Trim();
    }

    protected override void UpdateCanSave()
    {
        CanSave = !string.IsNullOrWhiteSpace(CardNumber) &&
                  !string.IsNullOrWhiteSpace(CardholderName) &&
                  ExpiryMonth >= 1 && ExpiryMonth <= 12 &&
                  ExpiryYear >= DateTime.Now.Year &&
                  !string.IsNullOrWhiteSpace(Cvv);
    }

    // ─── Property change handlers ─────────────────────────────────────────────

    partial void OnCardNumberChanged(string value)
    {
        IsCardNumberEmpty = string.IsNullOrWhiteSpace(value);
        DetectedCardType = CardTypeDetector.Detect(value);
        IsLuhnValid = CardTypeDetector.ValidateLuhn(value);
        UpdateCardNumberDisplay();
        UpdateCanSave();
    }

    partial void OnCardholderNameChanged(string value)
    {
        IsCardholderNameEmpty = string.IsNullOrWhiteSpace(value);
        UpdateCanSave();
    }

    partial void OnExpiryMonthChanged(int value) => UpdateCanSave();
    partial void OnExpiryYearChanged(int value) => UpdateCanSave();

    partial void OnCvvChanged(string value)
    {
        IsCvvEmpty = string.IsNullOrWhiteSpace(value);
        UpdateCanSave();
    }

    private void UpdateCardNumberDisplay()
    {
        FormattedCardNumber = CardTypeDetector.FormatCardNumber(CardNumber, DetectedCardType);
    }
}
