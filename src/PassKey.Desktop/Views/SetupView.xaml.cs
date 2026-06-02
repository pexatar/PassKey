using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
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
    private readonly ResourceLoader _resourceLoader = new();

    /// <summary>Localized default caption of the create button, captured after x:Uid is applied.</summary>
    private readonly string _createButtonDefaultText;

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
        // Capture the localized caption now (x:Uid has already been applied by
        // InitializeComponent) so the creating state can restore it later.
        _createButtonDefaultText = CreateButtonText.Text;
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
            < 25 => _resourceLoader.GetString("StrengthVeryWeak"),
            < 40 => _resourceLoader.GetString("StrengthWeak"),
            < 60 => _resourceLoader.GetString("StrengthMedium"),
            < 80 => _resourceLoader.GetString("StrengthStrong"),
            _ => _resourceLoader.GetString("StrengthVeryStrong")
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
            // Don't surface the raw stack trace to the user; show a generic localized message and log.
            System.Diagnostics.Debug.WriteLine($"[Setup] Create vault failed: {ex}");
            errorMsg = _resourceLoader.GetString("SetupCreateError");
        }
        finally
        {
            SetCreatingState(false);
        }

        // Show error AFTER SetCreatingState resets the button text
        if (errorMsg is not null)
        {
            CreateButtonText.Text = _resourceLoader.GetString("SetupErrorButton");
            CreateButton.IsEnabled = false;
            StrengthLabel.Text = errorMsg;
        }
    }

    private void SetCreatingState(bool creating)
    {
        CreateProgress.IsActive = creating;
        CreateProgress.Visibility = creating ? Visibility.Visible : Visibility.Collapsed;
        CreateButtonText.Text = creating
            ? _resourceLoader.GetString("SetupCreating")
            : _createButtonDefaultText;
        CreateButton.IsEnabled = !creating;
        PasswordInput.IsEnabled = !creating;
        ConfirmInput.IsEnabled = !creating;
    }
}
