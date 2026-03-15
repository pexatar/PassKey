using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Login view with master password input, ProgressRing during KDF, and inline error.
/// Code-behind handles UI events; ViewModel handles business logic.
/// </summary>
public sealed partial class LoginView : UserControl
{
    private LoginViewModel? _viewModel;

    public LoginView()
    {
        InitializeComponent();
        PasswordInput.PasswordChanged += OnPasswordChanged;

        // Display app version
        var version = typeof(App).Assembly.GetName().Version;
        if (version is not null)
            VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
    }

    /// <summary>
    /// Called by MainWindow when DataContext is set via ViewModel-First navigation.
    /// </summary>
    public void SetViewModel(LoginViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;
    }

    private void OnPasswordChanged(object? sender, string password)
    {
        // Enable/disable login button based on password presence
        LoginButton.IsEnabled = !string.IsNullOrEmpty(password);
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;

        var password = PasswordInput.Password;
        if (string.IsNullOrEmpty(password)) return;

        // Show loading state
        SetAuthenticatingState(true);

        try
        {
            await _viewModel.LoginCommand.ExecuteAsync(password);
        }
        finally
        {
            SetAuthenticatingState(false);
        }

        // Update error display based on ViewModel state
        UpdateErrorDisplay();
    }

    private void SetAuthenticatingState(bool authenticating)
    {
        LoginProgress.IsActive = authenticating;
        LoginProgress.Visibility = authenticating ? Visibility.Visible : Visibility.Collapsed;
        LoginButtonText.Text = authenticating ? "Sblocco in corso..." : "Accedi";
        LoginButton.IsEnabled = !authenticating;
        PasswordInput.IsEnabled = !authenticating;
    }

    private void UpdateErrorDisplay()
    {
        if (_viewModel is null) return;

        if (_viewModel.HasError)
        {
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorText.Text = _viewModel.ErrorMessage switch
            {
                "IncorrectPassword" => "Password non corretta. Riprova.",
                "UnlockFailed" => "Errore durante lo sblocco del vault.",
                _ => _viewModel.ErrorMessage
            };
        }
        else
        {
            ErrorPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void LoginForm_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && LoginButton.IsEnabled)
        {
            e.Handled = true;
            LoginButton_Click(LoginButton, e);
        }
    }

    private async void ForgotPasswordLink_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Password dimenticata",
            Content = "PassKey non memorizza la master password.\n\n" +
                      "Se l'hai dimenticata, i tuoi dati non sono recuperabili.\n\n" +
                      "Questo \u00e8 un principio di sicurezza chiamato \"zero-knowledge\": " +
                      "solo tu conosci la chiave per accedere ai tuoi dati.",
            CloseButtonText = "Ho capito",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }
}
