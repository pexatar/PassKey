using CommunityToolkit.Mvvm.ComponentModel;
using PassKey.Core.Models;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels.Base;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Identity detail ViewModel for add/edit panel.
/// Form organized in 3 expandable sections: Personal Data, Address, Documents + Notes.
/// Shared add/edit/save/delete plumbing is provided by <see cref="BaseDetailViewModel{TEntry}"/>.
/// </summary>
public partial class IdentityDetailViewModel : BaseDetailViewModel<IdentityEntry>
{
    // Personal Data
    [ObservableProperty]
    public partial string Label { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FirstName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BirthDate { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Phone { get; set; } = string.Empty;

    // Address
    [ObservableProperty]
    public partial string Street { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string City { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Province { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PostalCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Region { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Country { get; set; } = string.Empty;

    // Documents
    [ObservableProperty]
    public partial string IdCardNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HealthCardNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DrivingLicenseNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PassportNumber { get; set; } = string.Empty;

    // Notes
    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    // ── Inline validation (T5.6) ───────────────────────────────────────────────

    [ObservableProperty]
    public partial bool IsFirstAndLastNameEmpty { get; set; }

    public IdentityDetailViewModel(
        IVaultStateService vaultState,
        IDialogQueueService dialogQueue)
        : base(vaultState, dialogQueue)
    {
    }

    // ─── Template-method overrides ────────────────────────────────────────────

    protected override string GetPanelTitleForNew() => "Aggiungi identità";
    protected override string GetPanelTitleForEdit() => "Modifica identità";
    protected override string GetDeleteDialogTitle() => "Elimina identità";

    protected override string GetDeleteDisplayName(IdentityEntry entry)
    {
        var displayName = !string.IsNullOrWhiteSpace(entry.Label)
            ? entry.Label
            : $"{entry.FirstName} {entry.LastName}".Trim();
        return string.IsNullOrWhiteSpace(displayName) ? "Identità senza nome" : displayName;
    }

    protected override IList<IdentityEntry> GetVaultCollection(Vault vault) => vault.Identities;

    protected override void ResetFieldsForNew()
    {
        Label = string.Empty;
        FirstName = string.Empty;
        LastName = string.Empty;
        BirthDate = string.Empty;
        Email = string.Empty;
        Phone = string.Empty;
        Street = string.Empty;
        City = string.Empty;
        Province = string.Empty;
        PostalCode = string.Empty;
        Region = string.Empty;
        Country = string.Empty;
        IdCardNumber = string.Empty;
        HealthCardNumber = string.Empty;
        DrivingLicenseNumber = string.Empty;
        PassportNumber = string.Empty;
        Notes = string.Empty;
        IsFirstAndLastNameEmpty = true;
    }

    protected override void LoadFromEntry(IdentityEntry entry)
    {
        Label = entry.Label;
        FirstName = entry.FirstName;
        LastName = entry.LastName;
        BirthDate = entry.BirthDate;
        Email = entry.Email;
        Phone = entry.Phone;

        Street = entry.Street;
        City = entry.City;
        Province = entry.Province;
        PostalCode = entry.PostalCode;
        Region = entry.Region;
        Country = entry.Country;

        IdCardNumber = entry.IdCardNumber;
        HealthCardNumber = entry.HealthCardNumber;
        DrivingLicenseNumber = entry.DrivingLicenseNumber;
        PassportNumber = entry.PassportNumber;

        Notes = entry.Notes;
    }

    protected override IdentityEntry CreateNewEntry() => new()
    {
        Label = Label.Trim(),
        FirstName = FirstName.Trim(),
        LastName = LastName.Trim(),
        BirthDate = BirthDate.Trim(),
        Email = Email.Trim(),
        Phone = Phone.Trim(),
        Street = Street.Trim(),
        City = City.Trim(),
        Province = Province.Trim(),
        PostalCode = PostalCode.Trim(),
        Region = Region.Trim(),
        Country = Country.Trim(),
        IdCardNumber = IdCardNumber.Trim(),
        HealthCardNumber = HealthCardNumber.Trim(),
        DrivingLicenseNumber = DrivingLicenseNumber.Trim(),
        PassportNumber = PassportNumber.Trim(),
        Notes = Notes.Trim()
    };

    protected override void ApplyToEntry(IdentityEntry entry)
    {
        entry.Label = Label.Trim();
        entry.FirstName = FirstName.Trim();
        entry.LastName = LastName.Trim();
        entry.BirthDate = BirthDate.Trim();
        entry.Email = Email.Trim();
        entry.Phone = Phone.Trim();
        entry.Street = Street.Trim();
        entry.City = City.Trim();
        entry.Province = Province.Trim();
        entry.PostalCode = PostalCode.Trim();
        entry.Region = Region.Trim();
        entry.Country = Country.Trim();
        entry.IdCardNumber = IdCardNumber.Trim();
        entry.HealthCardNumber = HealthCardNumber.Trim();
        entry.DrivingLicenseNumber = DrivingLicenseNumber.Trim();
        entry.PassportNumber = PassportNumber.Trim();
        entry.Notes = Notes.Trim();
    }

    protected override void UpdateCanSave()
    {
        // At minimum: first name OR last name required.
        CanSave = !string.IsNullOrWhiteSpace(FirstName) || !string.IsNullOrWhiteSpace(LastName);
    }

    // ─── Property change handlers ─────────────────────────────────────────────

    partial void OnFirstNameChanged(string value)
    {
        UpdateValidationState();
        UpdateCanSave();
    }

    partial void OnLastNameChanged(string value)
    {
        UpdateValidationState();
        UpdateCanSave();
    }

    partial void OnEmailChanged(string value) => UpdateCanSave();

    private void UpdateValidationState()
    {
        IsFirstAndLastNameEmpty = string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName);
    }
}
