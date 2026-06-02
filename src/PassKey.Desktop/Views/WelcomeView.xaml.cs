using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Desktop.Controls;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Welcome view shown after first-run vault creation.
/// Offers quick actions: add password, import, restore backup, settings, or continue.
/// </summary>
public sealed partial class WelcomeView : UserControl
{
    private WelcomeViewModel? _viewModel;
    private readonly ResourceLoader _resourceLoader = new();
    private IDialogQueueService _dialogQueue = null!;

    public WelcomeView()
    {
        InitializeComponent();
    }

    public void SetViewModel(WelcomeViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;
        _dialogQueue = App.Services.GetRequiredService<IDialogQueueService>();

        // Restore-flow dialogs handled here in the code-behind.
        vm.RestoreWarningRequested += OnRestoreWarningRequested;
        vm.RestorePasswordRequested += OnRestorePasswordRequested;
        vm.RestoreCompleted += OnRestoreCompleted;
        vm.OperationError += OnOperationError;
    }

    private void AddPasswordButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.AddPasswordCommand.Execute(null);

    private void ImportButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.ImportDataCommand.Execute(null);

    private void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.RestoreBackupCommand.Execute(null);

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.OpenSettingsCommand.Execute(null);

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.ContinueCommand.Execute(null);

    // ─── Restore dialogs (strings reused from the Settings restore flow) ──────

    private async Task<bool> OnRestoreWarningRequested()
    {
        var dialog = new ContentDialog
        {
            Title = _resourceLoader.GetString("RestoreWarningTitle"),
            Content = _resourceLoader.GetString("RestoreWarningMessage"),
            PrimaryButtonText = _resourceLoader.GetString("RestoreWarningConfirm"),
            CloseButtonText = _resourceLoader.GetString("RestoreWarningCancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        var result = await _dialogQueue.EnqueueAndWait(() => dialog.ShowAsync().AsTask());
        return result == ContentDialogResult.Primary;
    }

    private async Task<(string password, bool confirmed)> OnRestorePasswordRequested()
    {
        var pwBox = new SecureInputBox
        {
            PlaceholderText = _resourceLoader.GetString("RestorePwPlaceholder"),
            ShowRevealButton = Visibility.Visible,
            Width = 320
        };
        var dialog = new ContentDialog
        {
            Title = _resourceLoader.GetString("RestorePwDialogTitle"),
            Content = pwBox,
            PrimaryButtonText = _resourceLoader.GetString("ButtonOk"),
            CloseButtonText = _resourceLoader.GetString("RestoreWarningCancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        var result = await _dialogQueue.EnqueueAndWait(() => dialog.ShowAsync().AsTask());
        return result == ContentDialogResult.Primary
            ? (pwBox.Password, true)
            : (string.Empty, false);
    }

    private Task OnRestoreCompleted()
        => ShowInfoDialogAsync(
            _resourceLoader.GetString("RestoreSuccessTitle"),
            _resourceLoader.GetString("RestoreSuccessMessage"));

    private Task OnOperationError(string errorCode)
    {
        var message = errorCode switch
        {
            "WRONG_PASSWORD" => _resourceLoader.GetString("RestoreErrorWrongPw"),
            "INVALID_FILE" => _resourceLoader.GetString("RestoreErrorInvalid"),
            "OPERATION_FAILED" => _resourceLoader.GetString("OperationGenericError"),
            _ => errorCode
        };
        return ShowInfoDialogAsync(_resourceLoader.GetString("ImportErrorTitle"), message);
    }

    private Task ShowInfoDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = _resourceLoader.GetString("ButtonOk"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        return _dialogQueue.EnqueueAndWait(() => dialog.ShowAsync().AsTask());
    }
}
