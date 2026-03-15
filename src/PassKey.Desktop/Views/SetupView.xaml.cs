using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Setup view for first-run vault creation.
/// Code-behind handles UI updates (strength bar, checklist, progress);
/// ViewModel handles business logic (password analysis, vault creation).
/// </summary>
public sealed partial class SetupView : UserControl
{
    private SetupViewModel? _viewModel;

    private static Brush GetStrengthBrush(int score)
    {
        var key = score switch
        {
            < 25 => "StrengthVeryWeakBrush",
            < 40 => "StrengthWeakBrush",
            < 60 => "StrengthMediumBrush",
            < 80 => "StrengthStrongBrush",
            _ => "StrengthVeryStrongBrush"
        };
        return (Brush)Application.Current.Resources[key];
    }

    private const string CheckGlyph = "\uE73E";   // checkmark
    private const string DismissGlyph = "\uE711";  // dismiss/X

    public SetupView()
    {
        InitializeComponent();
        PasswordInput.PasswordChanged += OnPasswordChanged;
        ConfirmInput.PasswordChanged += OnConfirmChanged;
    }

    /// <summary>
    /// Called by MainWindow when navigating to this view (ViewModel-First pattern).
    /// </summary>
    public void SetViewModel(SetupViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;
    }

    private void OnPasswordChanged(object? sender, string password)
    {
        if (_viewModel is null) return;

        _viewModel.AnalyzePassword(password);

        var confirm = ConfirmInput.Password;
        _viewModel.CheckPasswordsMatch(password, confirm);

        UpdateStrengthUI();
        UpdateChecklist();
        UpdateCreateButton();
    }

    private void OnConfirmChanged(object? sender, string confirm)
    {
        if (_viewModel is null) return;

        var password = PasswordInput.Password;
        _viewModel.CheckPasswordsMatch(password, confirm);

        UpdateChecklist();
        UpdateCreateButton();
    }

    private void UpdateStrengthUI()
    {
        var result = _viewModel?.StrengthResult;
        if (result is null)
        {
            StrengthBar.Value = 0;
            StrengthLabel.Text = string.Empty;
            return;
        }

        StrengthBar.Value = result.Score;

        // Label and color based on score (theme-adaptive)
        StrengthLabel.Text = result.Score switch
        {
            < 25 => "Molto debole",
            < 40 => "Debole",
            < 60 => "Discreta",
            < 80 => "Forte",
            _ => "Molto forte"
        };
        StrengthBar.Foreground = GetStrengthBrush(result.Score);
    }

    private void UpdateChecklist()
    {
        var result = _viewModel?.StrengthResult;

        SetCheckItem(CheckLength, result?.HasRecommendedLength ?? false);
        SetCheckItem(CheckUpper, result?.HasUppercase ?? false);
        SetCheckItem(CheckDigit, result?.HasDigits ?? false);
        SetCheckItem(CheckSymbol, result?.HasSymbols ?? false);
        SetCheckItem(CheckMatch, _viewModel?.PasswordsMatch ?? false);
    }

    private static void SetCheckItem(FontIcon icon, bool passed)
    {
        icon.Glyph = passed ? CheckGlyph : DismissGlyph;
        icon.Foreground = passed
            ? (Brush)Application.Current.Resources["CheckPassBrush"]
            : (Brush)Application.Current.Resources["CheckFailBrush"];
    }

    private void UpdateCreateButton()
    {
        CreateButton.IsEnabled = _viewModel?.CanCreate ?? false;
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;

        var password = PasswordInput.Password;
        if (string.IsNullOrEmpty(password)) return;

        SetCreatingState(true);
        string? errorMsg = null;

        try
        {
            await _viewModel.CreateVaultCommand.ExecuteAsync(password);
        }
        catch (Exception ex)
        {
            errorMsg = ex.ToString();
        }
        finally
        {
            SetCreatingState(false);
        }

        // Show error AFTER SetCreatingState resets the button text
        if (errorMsg is not null)
        {
            CreateButtonText.Text = "ERRORE";
            CreateButton.IsEnabled = false;
            StrengthLabel.Text = errorMsg;
        }
    }

    private void SetCreatingState(bool creating)
    {
        CreateProgress.IsActive = creating;
        CreateProgress.Visibility = creating ? Visibility.Visible : Visibility.Collapsed;
        CreateButtonText.Text = creating ? "Creazione in corso..." : "Crea Vault";
        CreateButton.IsEnabled = !creating;
        PasswordInput.IsEnabled = !creating;
        ConfirmInput.IsEnabled = !creating;
    }
}
