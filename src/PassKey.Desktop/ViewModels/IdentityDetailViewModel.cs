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
    public partial string MiddleName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BirthDate { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Phone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Company { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Username { get; set; } = string.Empty;

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

    /// <summary>
    /// True when an email has been entered but its format is not plausible. Drives a
    /// non-blocking inline warning (FU1) — the email is optional, so this never affects
    /// <see cref="BaseDetailViewModel{T}.CanSave"/>; it only nudges the user to double-check.
    /// </summary>
    [ObservableProperty]
    public partial bool IsEmailFormatSuspect { get; set; }

    public IdentityDetailViewModel(
        IVaultStateService vaultState,
        IDialogQueueService dialogQueue)
        : base(vaultState, dialogQueue)
    {
    }

    // ─── Template-method overrides ────────────────────────────────────────────

    protected override string GetDeleteDisplayName(IdentityEntry entry)
    {
        var displayName = !string.IsNullOrWhiteSpace(entry.Label)
            ? entry.Label
            : $"{entry.FirstName} {entry.LastName}".Trim();
        return string.IsNullOrWhiteSpace(displayName) ? _res.GetString("IdentityNoName") : displayName;
    }

    protected override IList<IdentityEntry> GetVaultCollection(Vault vault) => vault.Identities;

    protected override void ResetFieldsForNew()
    {
        Label = string.Empty;
        FirstName = string.Empty;
        MiddleName = string.Empty;
        LastName = string.Empty;
        BirthDate = string.Empty;
        Email = string.Empty;
        Phone = string.Empty;
        Company = string.Empty;
        Username = string.Empty;
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
        IsEmailFormatSuspect = false;
    }

    protected override void LoadFromEntry(IdentityEntry entry)
    {
        Label = entry.Label;
        FirstName = entry.FirstName;
        MiddleName = entry.MiddleName;
        LastName = entry.LastName;
        BirthDate = entry.BirthDate;
        Email = entry.Email;
        Phone = entry.Phone;
        Company = entry.Company;
        Username = entry.Username;

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
        MiddleName = MiddleName.Trim(),
        LastName = LastName.Trim(),
        BirthDate = BirthDate.Trim(),
        Email = Email.Trim(),
        Phone = Phone.Trim(),
        Company = Company.Trim(),
        Username = Username.Trim(),
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
        entry.MiddleName = MiddleName.Trim();
        entry.LastName = LastName.Trim();
        entry.BirthDate = BirthDate.Trim();
        entry.Email = Email.Trim();
        entry.Phone = Phone.Trim();
        entry.Company = Company.Trim();
        entry.Username = Username.Trim();
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

    partial void OnEmailChanged(string value)
    {
        // Non-blocking format check (FU1): warn only when something is typed and it
        // doesn't look like an email. Does NOT gate saving — email is optional.
        IsEmailFormatSuspect = !string.IsNullOrWhiteSpace(value) && !IsPlausibleEmail(value);
        UpdateCanSave();
    }

    private void UpdateValidationState()
    {
        IsFirstAndLastNameEmpty = string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName);
    }

    /// <summary>
    /// Lenient plausibility check for an email address — deliberately NOT a strict
    /// RFC 5322 validator. Requires exactly one '@' (neither first nor last char) and a
    /// domain part containing a dot that is neither the first nor the last character.
    /// Rejects the common typos "user", "user.com", "user@host", "user@host." while
    /// accepting ordinary addresses like "mario@esempio.it".
    /// </summary>
    private static bool IsPlausibleEmail(string email)
    {
        email = email.Trim();

        int at = email.IndexOf('@');
        if (at <= 0) return false;                       // no '@', or '@' is the first char
        if (at != email.LastIndexOf('@')) return false;  // more than one '@'
        if (at == email.Length - 1) return false;        // nothing after '@'

        var domain = email[(at + 1)..];
        int dot = domain.IndexOf('.');
        return dot > 0 && dot < domain.Length - 1;        // dot present, not first/last
    }
}
