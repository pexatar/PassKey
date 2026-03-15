using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Identity detail view (add/edit form) in the 400px side panel.
/// 3 expandable sections: Personal Data (expanded), Address (collapsed), Documents (collapsed).
/// </summary>
public sealed partial class IdentityDetailView : UserControl
{
    private IdentityDetailViewModel? _viewModel;
    private bool _updatingFromVm;
    private readonly Microsoft.Windows.ApplicationModel.Resources.ResourceLoader _res = new();

    public IdentityDetailView()
    {
        InitializeComponent();
    }

    public void SetViewModel(IdentityDetailViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;

        // Populate UI from ViewModel
        _updatingFromVm = true;

        PanelTitleText.Text = vm.PanelTitle;
        LabelBox.Text = vm.Label;

        // Personal Data
        FirstNameBox.Text = vm.FirstName;
        LastNameBox.Text = vm.LastName;
        BirthDateBox.Text = vm.BirthDate;
        EmailBox.Text = vm.Email;
        PhoneBox.Text = FormatPhone(vm.Phone);

        // Address
        StreetBox.Text = vm.Street;
        CityBox.Text = vm.City;
        PostalCodeBox.Text = vm.PostalCode;
        ProvinceBox.Text = vm.Province;
        RegionBox.Text = vm.Region;
        CountryBox.Text = vm.Country;

        // Documents
        IdCardBox.Text = vm.IdCardNumber;
        HealthCardBox.Text = vm.HealthCardNumber;
        DrivingLicenseBox.Text = vm.DrivingLicenseNumber;
        PassportBox.Text = vm.PassportNumber;

        // Notes
        NotesBox.Text = vm.Notes;

        _updatingFromVm = false;

        // State
        SaveButton.IsEnabled = vm.CanSave;

        // Show delete button only in edit mode
        bool isEdit = !vm.IsNew;
        DeleteButton.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;

        // Subtitle
        PanelSubtitle.Text = isEdit
            ? _res.GetString("IdPanelSubtitleEdit")
            : _res.GetString("IdPanelSubtitleNew");

        // Expand sections that have data in edit mode
        if (!string.IsNullOrWhiteSpace(vm.Street) || !string.IsNullOrWhiteSpace(vm.City) ||
            !string.IsNullOrWhiteSpace(vm.PostalCode) || !string.IsNullOrWhiteSpace(vm.Province) ||
            !string.IsNullOrWhiteSpace(vm.Region) || !string.IsNullOrWhiteSpace(vm.Country))
            AddressExpander.IsExpanded = true;
        if (!string.IsNullOrWhiteSpace(vm.IdCardNumber) || !string.IsNullOrWhiteSpace(vm.HealthCardNumber) ||
            !string.IsNullOrWhiteSpace(vm.DrivingLicenseNumber) || !string.IsNullOrWhiteSpace(vm.PassportNumber))
            DocumentsExpander.IsExpanded = true;

        // Focus first field
        FirstNameBox.Focus(FocusState.Programmatic);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IdentityDetailViewModel.CanSave):
                SaveButton.IsEnabled = _viewModel?.CanSave ?? false;
                break;
            case nameof(IdentityDetailViewModel.IsSaving):
                UpdateSavingState(_viewModel?.IsSaving ?? false);
                break;
        }
    }

    // TextBox → ViewModel sync — Personal Data
    private void LabelBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.Label = LabelBox.Text;
    }

    private void FirstNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.FirstName = FirstNameBox.Text;
    }

    private void LastNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.LastName = LastNameBox.Text;
    }

    private void BirthDateBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingFromVm) return;

        var current = BirthDateBox.Text;
        var formatted = FormatBirthDate(current);

        if (formatted != current)
        {
            _updatingFromVm = true;
            int cursor = BirthDateBox.SelectionStart;
            BirthDateBox.Text = formatted;
            BirthDateBox.SelectionStart = Math.Min(cursor + (formatted.Length - current.Length), formatted.Length);
            _updatingFromVm = false;
        }

        if (_viewModel is not null) _viewModel.BirthDate = BirthDateBox.Text;
    }

    private void EmailBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.Email = EmailBox.Text;
    }

    private void PhoneBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingFromVm) return;

        var current = PhoneBox.Text;
        var formatted = FormatPhone(current);

        if (formatted != current)
        {
            _updatingFromVm = true;
            int cursor = PhoneBox.SelectionStart;
            PhoneBox.Text = formatted;
            PhoneBox.SelectionStart = Math.Min(cursor + (formatted.Length - current.Length), formatted.Length);
            _updatingFromVm = false;
        }

        if (_viewModel is not null) _viewModel.Phone = PhoneBox.Text;
    }

    // TextBox → ViewModel sync — Address
    private void StreetBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.Street = StreetBox.Text;
    }

    private void CityBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.City = CityBox.Text;
    }

    private void PostalCodeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.PostalCode = PostalCodeBox.Text;
    }

    private void ProvinceBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.Province = ProvinceBox.Text;
    }

    private void RegionBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.Region = RegionBox.Text;
    }

    private void CountryBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.Country = CountryBox.Text;
    }

    // TextBox → ViewModel sync — Documents
    private void IdCardBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.IdCardNumber = IdCardBox.Text;
    }

    private void HealthCardBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.HealthCardNumber = HealthCardBox.Text;
    }

    private void DrivingLicenseBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.DrivingLicenseNumber = DrivingLicenseBox.Text;
    }

    private void PassportBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.PassportNumber = PassportBox.Text;
    }

    // Notes
    private void NotesBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null) _viewModel.Notes = NotesBox.Text;
    }

    // Action buttons
    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            await _viewModel.SaveCommand.ExecuteAsync(null);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            await _viewModel.DeleteCommand.ExecuteAsync(null);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.Cancelled?.Invoke();
    }

    private void UpdateSavingState(bool saving)
    {
        SaveProgress.IsActive = saving;
        SaveProgress.Visibility = saving ? Visibility.Visible : Visibility.Collapsed;
        SaveButtonText.Text = saving ? _res.GetString("SaveInProgress") : _res.GetString("ButtonSave");
        SaveButton.IsEnabled = !saving;
    }

    // ── Formatters ────────────────────────────────────────────────────────────

    /// <summary>Auto-inserts "/" separators for GG/MM/AAAA date input.</summary>
    private static string FormatBirthDate(string input)
    {
        var digits = new string(input.Where(char.IsDigit).ToArray());
        return digits.Length switch
        {
            <= 2 => digits,
            <= 4 => digits[..2] + "/" + digits[2..],
            _    => digits[..2] + "/" + digits[2..4] + "/" + digits[4..Math.Min(8, digits.Length)]
        };
    }

    /// <summary>Formats a phone number with spaces (e.g. "+393518584980" → "+39 351 858 4980").</summary>
    private static string FormatPhone(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        bool hasPlus = input.TrimStart().StartsWith('+');
        var digits = new string(input.Where(char.IsDigit).ToArray());

        if (!hasPlus || digits.Length == 0) return input;

        // Country code: +1/+7 = 1 digit, others = 2 digits (simplified)
        int ccLen = (digits[0] == '1' || digits[0] == '7') ? 1 : 2;
        if (digits.Length <= ccLen) return "+" + digits;

        var cc = digits[..ccLen];
        var local = digits[ccLen..];

        // Italian (+39) with 10 local digits: 3+3+4 grouping
        if (cc == "39" && local.Length == 10)
            return $"+39 {local[..3]} {local[3..6]} {local[6..10]}";

        // Generic: groups of 3
        var parts = new List<string>();
        for (int i = 0; i < local.Length; i += 3)
            parts.Add(local[i..Math.Min(i + 3, local.Length)]);
        return "+" + cc + " " + string.Join(" ", parts);
    }
}
