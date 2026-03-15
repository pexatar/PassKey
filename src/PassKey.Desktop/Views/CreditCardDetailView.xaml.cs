using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using PassKey.Core.Constants;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Credit card detail view (add/edit form) displayed in the 400px side panel.
/// Form order: Label → Category → Color → [separator] → Number → Cardholder → Expiry → CVV → PIN → Notes.
/// </summary>
public sealed partial class CreditCardDetailView : UserControl
{
    private CreditCardDetailViewModel? _viewModel;
    private bool _updatingFromVm;
    private readonly Microsoft.Windows.ApplicationModel.Resources.ResourceLoader _res = new();

    /// <summary>
    /// All 10 accent colors for the swatch selector.
    /// </summary>
    private static readonly (CardColor color, string hex)[] AccentColors =
    [
        (CardColor.Default, "#37474F"),
        (CardColor.Blue, "#1565C0"),
        (CardColor.Red, "#C62828"),
        (CardColor.Green, "#2E7D32"),
        (CardColor.Purple, "#6A1B9A"),
        (CardColor.Orange, "#E65100"),
        (CardColor.Teal, "#00838F"),
        (CardColor.Pink, "#AD1457"),
        (CardColor.Gold, "#F9A825"),
        (CardColor.Black, "#212121")
    ];

    public CreditCardDetailView()
    {
        InitializeComponent();
        InitializeMonthYearCombos();
        InitializeColorCombo();
    }

    public void SetViewModel(CreditCardDetailViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;

        // Populate UI from ViewModel
        _updatingFromVm = true;
        PanelTitleText.Text = vm.PanelTitle;
        CardNumberBox.Text = vm.CardNumber;
        CardholderBox.Text = vm.CardholderName;
        LabelBox.Text = vm.Label;
        NotesBox.Text = vm.Notes;

        // Set month/year combos
        if (vm.ExpiryMonth >= 1 && vm.ExpiryMonth <= 12)
            MonthCombo.SelectedIndex = vm.ExpiryMonth - 1;
        var yearIndex = vm.ExpiryYear - DateTime.Now.Year;
        if (yearIndex >= 0 && yearIndex < 11)
            YearCombo.SelectedIndex = yearIndex;

        // Set category
        CategoryCombo.SelectedIndex = (int)vm.Category;

        // Set accent color selection in ComboBox
        for (int i = 0; i < ColorCombo.Items.Count; i++)
        {
            if (ColorCombo.Items[i] is ComboBoxItem item && item.Tag is CardColor c && c == vm.AccentColor)
            {
                ColorCombo.SelectedIndex = i;
                break;
            }
        }

        // Set CVV/PIN in SecureInputBox
        if (!string.IsNullOrEmpty(vm.Cvv))
            CvvInput.SetPassword(vm.Cvv);
        else
            CvvInput.Clear();

        if (!string.IsNullOrEmpty(vm.Pin))
            PinInput.SetPassword(vm.Pin);
        else
            PinInput.Clear();

        _updatingFromVm = false;

        // Update card type display
        UpdateCardTypeIndicator();
        SaveButton.IsEnabled = vm.CanSave;

        // Show delete button only in edit mode
        bool isEdit = !vm.IsNew;
        DeleteButton.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;

        // Subtitle
        PanelSubtitle.Text = isEdit
            ? _res.GetString("CardPanelSubtitleEdit")
            : _res.GetString("CardPanelSubtitleNew");

        // Hook SecureInputBox events
        CvvInput.PasswordChanged += OnCvvChanged;
        PinInput.PasswordChanged += OnPinChanged;

        // Focus first field
        LabelBox.Focus(FocusState.Programmatic);
    }

    private void InitializeMonthYearCombos()
    {
        // Months 01-12
        for (var m = 1; m <= 12; m++)
            MonthCombo.Items.Add(m.ToString("D2"));

        // Years: current → current + 10
        var currentYear = DateTime.Now.Year;
        for (var y = currentYear; y <= currentYear + 10; y++)
            YearCombo.Items.Add(y.ToString());
    }

    /// <summary>
    /// Populate the ColorCombo with 10 accent colors, each showing a colored circle + name.
    /// </summary>
    private void InitializeColorCombo()
    {
        foreach (var (color, hex) in AccentColors)
        {
            var circle = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(ParseColor(hex))
            };

            var item = new ComboBoxItem
            {
                Tag = color,
                Content = circle,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            ToolTipService.SetToolTip(item, color.ToString());
            AutomationProperties.SetName(item, color.ToString());
            ColorCombo.Items.Add(item);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CreditCardDetailViewModel.CanSave):
                SaveButton.IsEnabled = _viewModel?.CanSave ?? false;
                break;
            case nameof(CreditCardDetailViewModel.IsSaving):
                UpdateSavingState(_viewModel?.IsSaving ?? false);
                break;
            case nameof(CreditCardDetailViewModel.DetectedCardType):
            case nameof(CreditCardDetailViewModel.IsLuhnValid):
                UpdateCardTypeIndicator();
                break;
        }
    }

    // TextBox → ViewModel sync
    private void CardNumberBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null)
        {
            // Strip formatting, keep only digits
            var rawDigits = new string(CardNumberBox.Text.Where(char.IsDigit).ToArray());
            _viewModel.CardNumber = rawDigits;

            // Display formatted number
            if (!string.IsNullOrEmpty(_viewModel.FormattedCardNumber))
            {
                _updatingFromVm = true;
                var cursorPos = CardNumberBox.SelectionStart;
                CardNumberBox.Text = _viewModel.FormattedCardNumber;
                CardNumberBox.SelectionStart = Math.Min(cursorPos, CardNumberBox.Text.Length);
                _updatingFromVm = false;
            }
        }
    }

    private void CardholderBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null)
            _viewModel.CardholderName = CardholderBox.Text;
    }

    private void LabelBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null)
            _viewModel.Label = LabelBox.Text;
    }

    private void NotesBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null)
            _viewModel.Notes = NotesBox.Text;
    }

    private void OnCvvChanged(object? sender, string password)
    {
        if (_viewModel is not null)
            _viewModel.Cvv = password;
    }

    private void OnPinChanged(object? sender, string password)
    {
        if (_viewModel is not null)
            _viewModel.Pin = password;
    }

    // ComboBox handlers
    private void MonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null && MonthCombo.SelectedIndex >= 0)
            _viewModel.ExpiryMonth = MonthCombo.SelectedIndex + 1;
    }

    private void YearCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null && YearCombo.SelectedIndex >= 0)
            _viewModel.ExpiryYear = DateTime.Now.Year + YearCombo.SelectedIndex;
    }

    private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null && CategoryCombo.SelectedIndex >= 0)
            _viewModel.Category = (CardCategory)CategoryCombo.SelectedIndex;
    }

    // Color ComboBox handler
    private void ColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null
            && ColorCombo.SelectedItem is ComboBoxItem item
            && item.Tag is CardColor c)
        {
            _viewModel.AccentColor = c;
        }
    }

    private void UpdateCardTypeIndicator()
    {
        if (_viewModel is null) return;

        var cardType = _viewModel.DetectedCardType;
        var isValid = _viewModel.IsLuhnValid;

        // Card type label
        CardTypeLabel.Text = cardType switch
        {
            CardType.Visa => "Visa",
            CardType.MasterCard => "MasterCard",
            CardType.Amex => "American Express",
            CardType.Discover => "Discover",
            CardType.JCB => "JCB",
            CardType.Maestro => "Maestro",
            CardType.DinersClub => "Diners Club",
            _ => _res.GetString("CardTypeUnknown")
        };

        // Luhn validation
        if (!string.IsNullOrWhiteSpace(_viewModel.CardNumber) && _viewModel.CardNumber.Length >= 8)
        {
            LuhnIcon.Visibility = Visibility.Visible;
            LuhnLabel.Visibility = Visibility.Visible;

            if (isValid)
            {
                LuhnIcon.Glyph = "\uE73E"; // checkmark
                LuhnIcon.Foreground = (Brush)Application.Current.Resources["LuhnValidBrush"];
                LuhnLabel.Text = _res.GetString("LuhnValid");
                LuhnLabel.Foreground = (Brush)Application.Current.Resources["LuhnValidBrush"];
            }
            else
            {
                LuhnIcon.Glyph = "\uE711"; // dismiss
                LuhnIcon.Foreground = (Brush)Application.Current.Resources["LuhnInvalidBrush"];
                LuhnLabel.Text = _res.GetString("LuhnInvalid");
                LuhnLabel.Foreground = (Brush)Application.Current.Resources["LuhnInvalidBrush"];
            }
        }
        else
        {
            LuhnIcon.Visibility = Visibility.Collapsed;
            LuhnLabel.Visibility = Visibility.Collapsed;
        }
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
        SaveButtonText.Text = saving ? _res.GetString("SaveInProgress") : _res.GetString("ButtonSaveLabel/Text");
        SaveButton.IsEnabled = !saving;
    }

    /// <summary>
    /// Parse hex color string to Windows.UI.Color.
    /// </summary>
    private static Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        return ColorHelper.FromArgb(255,
            byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber));
    }
}
