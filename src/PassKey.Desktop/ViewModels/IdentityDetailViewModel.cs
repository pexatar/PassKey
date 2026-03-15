using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Models;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Identity detail ViewModel for add/edit panel.
/// Form organized in 3 expandable sections: Personal Data, Address, Documents + Notes.
/// </summary>
public partial class IdentityDetailViewModel : ObservableObject
{
    private readonly IVaultStateService _vaultState;
    private readonly IDialogQueueService _dialogQueue;

    private IdentityEntry? _editingEntry;
    private bool _isNew;

    public bool IsNew => _isNew;

    // Panel
    [ObservableProperty]
    public partial string PanelTitle { get; set; } = string.Empty;

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

    // State
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

    public IdentityDetailViewModel(
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
        PanelTitle = "Aggiungi identità";
        ClearAllFields();
        UpdateCanSave();
    }

    public void StartEdit(IdentityEntry entry)
    {
        _editingEntry = entry;
        _isNew = false;
        PanelTitle = "Modifica identità";

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
        UpdateCanSave();
    }

    private void ClearAllFields()
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
    }

    // Validation triggers
    partial void OnFirstNameChanged(string value) => UpdateCanSave();
    partial void OnLastNameChanged(string value) => UpdateCanSave();
    partial void OnEmailChanged(string value) => UpdateCanSave();

    private void UpdateCanSave()
    {
        // At minimum: first name OR last name required
        CanSave = !string.IsNullOrWhiteSpace(FirstName) || !string.IsNullOrWhiteSpace(LastName);
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
                var entry = new IdentityEntry
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
                vault.Identities.Add(entry);
                entryId = entry.Id;
            }
            else if (_editingEntry is not null)
            {
                _editingEntry.Label = Label.Trim();
                _editingEntry.FirstName = FirstName.Trim();
                _editingEntry.LastName = LastName.Trim();
                _editingEntry.BirthDate = BirthDate.Trim();
                _editingEntry.Email = Email.Trim();
                _editingEntry.Phone = Phone.Trim();
                _editingEntry.Street = Street.Trim();
                _editingEntry.City = City.Trim();
                _editingEntry.Province = Province.Trim();
                _editingEntry.PostalCode = PostalCode.Trim();
                _editingEntry.Region = Region.Trim();
                _editingEntry.Country = Country.Trim();
                _editingEntry.IdCardNumber = IdCardNumber.Trim();
                _editingEntry.HealthCardNumber = HealthCardNumber.Trim();
                _editingEntry.DrivingLicenseNumber = DrivingLicenseNumber.Trim();
                _editingEntry.PassportNumber = PassportNumber.Trim();
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

        var displayName = !string.IsNullOrWhiteSpace(_editingEntry.Label)
            ? _editingEntry.Label
            : $"{_editingEntry.FirstName} {_editingEntry.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName)) displayName = "Identità senza nome";

        var result = await _dialogQueue.EnqueueAndWait(() =>
        {
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Elimina identità",
                Content = $"Eliminare \"{displayName}\"?\nQuesta azione è irreversibile.",
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
            vault?.Identities.Remove(_editingEntry);
            Deleted?.Invoke(entryId);
        }
    }
}
