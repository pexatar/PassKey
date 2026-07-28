using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Desktop.Controls;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Login view with master password input, ProgressRing during KDF, and inline error.
/// Code-behind handles UI events; ViewModel handles business logic.
/// </summary>
public sealed partial class LoginView : UserControl
{
    private LoginViewModel? _viewModel;
    private readonly ResourceLoader _resourceLoader = new();
    private IDialogQueueService _dialogQueue = null!;

    /// <summary>Localized default caption of the login button, captured after x:Uid is applied.</summary>
    private readonly string _loginButtonDefaultText;

    public LoginView()
    {
        InitializeComponent();
        // Capture the localized caption now (x:Uid has already been applied by
        // InitializeComponent) so the authenticating state can restore it later.
        _loginButtonDefaultText = LoginButtonText.Text;
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
        _dialogQueue = App.Services.GetRequiredService<IDialogQueueService>();
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
        LoginButtonText.Text = authenticating
            ? _resourceLoader.GetString("LoginAuthenticating")
            : _loginButtonDefaultText;
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
                "IncorrectPassword" => _resourceLoader.GetString("LoginIncorrectPassword"),
                "UnlockFailed" => _resourceLoader.GetString("LoginUnlockFailed"),
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
        if (_viewModel is null) return;

        // Step 1 \u2014 explain zero-knowledge and offer the two recovery paths.
        var choice = new ContentDialog
        {
            Title = _resourceLoader.GetString("ForgotPwTitle"),
            Content = _resourceLoader.GetString("ForgotPwMessage"),
            PrimaryButtonText = _resourceLoader.GetString("ForgotPwRestore"),
            SecondaryButtonText = _resourceLoader.GetString("ForgotPwNewVault"),
            CloseButtonText = _resourceLoader.GetString("RestoreWarningCancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        // Route through the dialog queue: WinUI allows a single ContentDialog at a time,
        // and this flow chains a second dialog straight after this one. Showing them
        // directly would throw "Only a single ContentDialog can be open at any time"
        // (also if a queued dialog — e.g. the update prompt — is already open).
        try
        {
            var result = await _dialogQueue.EnqueueAndWait(() => choice.ShowAsync().AsTask());

            if (result == ContentDialogResult.Primary)
                await RestoreFromBackupAsync();
            else if (result == ContentDialogResult.Secondary)
                await CreateNewVaultAsync();
        }
        catch (Exception ex)
        {
            // async void handler: an unhandled exception here would terminate the process.
            System.Diagnostics.Debug.WriteLine($"[LoginView] Forgot-password flow failed: {ex}");
            App.Services.GetService<IToastService>()?.Show(
                ToastSeverity.Error, _resourceLoader.GetString("OperationGenericError"));
        }
    }

    /// <summary>
    /// "Restore backup" recovery path: pick a .pkbak, ask for its password, and adopt it
    /// as the new vault (the backup password becomes the master password).
    /// </summary>
    private async Task RestoreFromBackupAsync()
    {
        if (_viewModel is null) return;

        var path = await App.Services.GetRequiredService<IFilePickerService>()
                            .PickOpenFileAsync(".pkbak", "PassKey Backup");
        if (path is null) return; // user cancelled the file picker

        // Ask for the backup's password.
        var input = new SecureInputBox
        {
            PlaceholderText = _resourceLoader.GetString("BackupPwPlaceholder"),
            MaxLength = 128,
            Width = 320
        };
        var pwDialog = new ContentDialog
        {
            Title = _resourceLoader.GetString("BackupPwDialogTitle"),
            Content = input,
            PrimaryButtonText = _resourceLoader.GetString("ForgotPwRestore"),
            CloseButtonText = _resourceLoader.GetString("RestoreWarningCancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await _dialogQueue.EnqueueAndWait(() => pwDialog.ShowAsync().AsTask()) != ContentDialogResult.Primary) return;

        var password = input.Password;
        if (string.IsNullOrEmpty(password)) return;

        var chars = password.ToCharArray();
        try
        {
            var ok = await _viewModel.RestoreFromBackupAsync(path, chars);
            if (!ok)
            {
                // Wrong password or corrupt file \u2014 report via toast, stay on the login screen.
                App.Services.GetService<IToastService>()?.Show(
                    ToastSeverity.Error, _resourceLoader.GetString("RestoreErrorWrongPw"));
            }
        }
        finally
        {
            Array.Clear(chars);
        }
    }

    /// <summary>
    /// "Create new vault" recovery path: a second explicit confirmation (the existing vault
    /// becomes permanently inaccessible) before routing to first-run setup.
    /// </summary>
    private async Task CreateNewVaultAsync()
    {
        if (_viewModel is null) return;

        var confirm = new ContentDialog
        {
            Title = _resourceLoader.GetString("NewVaultWarnTitle"),
            Content = _resourceLoader.GetString("NewVaultWarnMessage"),
            PrimaryButtonText = _resourceLoader.GetString("NewVaultWarnConfirm"),
            CloseButtonText = _resourceLoader.GetString("RestoreWarningCancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await _dialogQueue.EnqueueAndWait(() => confirm.ShowAsync().AsTask()) == ContentDialogResult.Primary)
            _viewModel.StartNewVault();
    }
}
