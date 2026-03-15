using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Core.Services;
using PassKey.Desktop.Controls;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

public sealed partial class SettingsView : UserControl
{
    private SettingsViewModel? _viewModel;
    private bool _updatingFromVm;
    private readonly ResourceLoader _res = new();

    /// <summary>Raised when the user clicks "Guida e scorciatoie" to navigate to HelpView.</summary>
    public event Action? NavigateToHelpRequested;

    public SettingsView()
    {
        InitializeComponent();
    }

    public void SetViewModel(SettingsViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;

        _updatingFromVm = true;

        vm.Initialize();

        // Populate UI from ViewModel
        ThemeCombo.SelectedIndex = vm.SelectedThemeIndex;
        AutoLockCombo.SelectedIndex = vm.SelectedAutoLockIndex;
        LanguageCombo.SelectedIndex = vm.SelectedLanguageIndex;
        StartWithWindowsToggle.IsOn = vm.StartWithWindows;
        StartMinimizedCheck.IsChecked = vm.StartMinimized;
        StartMinimizedCheck.IsEnabled = vm.StartWithWindows;
        VersionText.Text = vm.AppVersion;

        _updatingFromVm = false;

        // Subscribe to VM changes
        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.ThemeChangeRequested += OnThemeChangeRequested;

        // Backup/Restore/Import event subscriptions
        vm.BackupPasswordRequested += OnBackupPasswordRequested;
        vm.RestoreWarningRequested += OnRestoreWarningRequested;
        vm.RestorePasswordRequested += OnRestorePasswordRequested;
        vm.ImportFormatRequested += OnImportFormatRequested;
        vm.ImportPasswordRequested += OnImportPasswordRequested;
        vm.MergeStrategyRequested += OnMergeStrategyRequested;
        vm.ImportCompleted += OnImportCompleted;
        vm.BackupCompleted += OnBackupCompleted;
        vm.RestoreCompleted += OnRestoreCompleted;
        vm.OperationError += OnOperationError;
    }

    private void OnThemeChangeRequested(ElementTheme theme)
    {
        App.MainWindow?.ApplyTheme(theme);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_updatingFromVm) return;

        _updatingFromVm = true;
        switch (e.PropertyName)
        {
            case nameof(SettingsViewModel.StartWithWindows):
                StartWithWindowsToggle.IsOn = _viewModel!.StartWithWindows;
                StartMinimizedCheck.IsEnabled = _viewModel.StartWithWindows;
                if (!_viewModel.StartWithWindows)
                    StartMinimizedCheck.IsChecked = false;
                break;
            case nameof(SettingsViewModel.StartMinimized):
                StartMinimizedCheck.IsChecked = _viewModel!.StartMinimized;
                break;
        }
        _updatingFromVm = false;
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_updatingFromVm || _viewModel is null) return;
        _viewModel.SelectedThemeIndex = ThemeCombo.SelectedIndex;
    }

    private void AutoLockCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_updatingFromVm || _viewModel is null) return;
        _viewModel.SelectedAutoLockIndex = AutoLockCombo.SelectedIndex;
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_updatingFromVm || _viewModel is null) return;
        _viewModel.SelectedLanguageIndex = LanguageCombo.SelectedIndex;
        LanguageRestartInfoBar.IsOpen = true;
    }

    private void RestartNowButton_Click(object sender, RoutedEventArgs e)
    {
        var exePath = Environment.ProcessPath;
        if (exePath is not null)
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(exePath) { UseShellExecute = true });
        Application.Current.Exit();
    }

    private void StartWithWindowsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingFromVm || _viewModel is null) return;
        _viewModel.StartWithWindows = StartWithWindowsToggle.IsOn;
        StartMinimizedCheck.IsEnabled = StartWithWindowsToggle.IsOn;
        if (!StartWithWindowsToggle.IsOn)
            StartMinimizedCheck.IsChecked = false;
    }

    private void StartMinimizedCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingFromVm || _viewModel is null) return;
        _viewModel.StartMinimized = StartMinimizedCheck.IsChecked == true;
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToHelpRequested?.Invoke();
    }

    private async void ChangePwButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;

        var currentPwBox = new SecureInputBox
        {
            PlaceholderText = _res.GetString("ChangePwCurrentPlaceholder"),
            ShowRevealButton = Visibility.Visible,
            Width = 320
        };

        var newPwBox = new SecureInputBox
        {
            PlaceholderText = _res.GetString("ChangePwNewPlaceholder"),
            ShowRevealButton = Visibility.Visible,
            Width = 320
        };

        var confirmPwBox = new SecureInputBox
        {
            PlaceholderText = _res.GetString("ChangePwConfirmPlaceholder"),
            ShowRevealButton = Visibility.Visible,
            Width = 320
        };

        var errorText = new TextBlock
        {
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(currentPwBox);
        panel.Children.Add(newPwBox);
        panel.Children.Add(confirmPwBox);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = _res.GetString("ChangePwDialogTitle"),
            Content = panel,
            PrimaryButtonText = _res.GetString("ChangePwDialogPrimary"),
            CloseButtonText = _res.GetString("ChangePwDialogClose"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        // Validate inline: prevent dialog from closing if validation fails
        dialog.PrimaryButtonClick += (_, args) =>
        {
            var currentPw = currentPwBox.Password;
            var newPw = newPwBox.Password;
            var confirmPw = confirmPwBox.Password;

            string? error = null;

            if (string.IsNullOrEmpty(currentPw))
                error = _res.GetString("ChangePwErrEmpty");
            else if (newPw.Length < 8)
                error = _res.GetString("ChangePwErrShort");
            else if (newPw != confirmPw)
                error = _res.GetString("ChangePwErrMismatch");
            else if (newPw == currentPw)
                error = _res.GetString("ChangePwErrSame");

            if (error is not null)
            {
                errorText.Text = error;
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        // Perform change (validation already passed)
        ChangePwButton.IsEnabled = false;
        try
        {
            var success = await _viewModel.PerformChangeMasterPasswordAsync(
                currentPwBox.Password, newPwBox.Password);

            if (success)
            {
                await ShowInfoDialogAsync(
                    _res.GetString("ChangePwSuccessTitle"),
                    _res.GetString("ChangePwSuccessMessage"));
            }
            else
            {
                await ShowInfoDialogAsync(
                    _res.GetString("ChangePwErrorTitle"),
                    _res.GetString("ChangePwErrorMessage"));
            }
        }
        finally
        {
            ChangePwButton.IsEnabled = true;
        }
    }

    private async Task ShowInfoDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }

    // ═══ BACKUP / RESTORE / IMPORT ═══

    private async void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        BackupButton.IsEnabled = false;
        try { await _viewModel.BackupVaultCommand.ExecuteAsync(null); }
        finally { BackupButton.IsEnabled = true; }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        RestoreButton.IsEnabled = false;
        try { await _viewModel.RestoreVaultCommand.ExecuteAsync(null); }
        finally { RestoreButton.IsEnabled = true; }
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        ImportButton.IsEnabled = false;
        try { await _viewModel.ImportDataCommand.ExecuteAsync(null); }
        finally { ImportButton.IsEnabled = true; }
    }

    private async Task<(string password, bool confirmed)> OnBackupPasswordRequested()
    {
        var pwBox = new SecureInputBox
        {
            PlaceholderText = _res.GetString("BackupPwPlaceholder"),
            ShowRevealButton = Visibility.Visible,
            Width = 320
        };
        var confirmBox = new SecureInputBox
        {
            PlaceholderText = _res.GetString("BackupPwConfirmPlaceholder"),
            ShowRevealButton = Visibility.Visible,
            Width = 320
        };
        var errorText = new TextBlock
        {
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var descText = new TextBlock
        {
            Text = _res.GetString("BackupPwDialogDesc"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(descText);
        panel.Children.Add(pwBox);
        panel.Children.Add(confirmBox);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = _res.GetString("BackupPwDialogTitle"),
            Content = panel,
            PrimaryButtonText = "OK",
            CloseButtonText = _res.GetString("RestoreWarningCancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            string? error = null;
            if (string.IsNullOrEmpty(pwBox.Password))
                error = _res.GetString("BackupPwErrEmpty");
            else if (pwBox.Password.Length < 8)
                error = _res.GetString("BackupPwErrShort");
            else if (pwBox.Password != confirmBox.Password)
                error = _res.GetString("BackupPwErrMismatch");

            if (error is not null)
            {
                errorText.Text = error;
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary
            ? (pwBox.Password, true)
            : (string.Empty, false);
    }

    private async Task<bool> OnRestoreWarningRequested()
    {
        var dialog = new ContentDialog
        {
            Title = _res.GetString("RestoreWarningTitle"),
            Content = _res.GetString("RestoreWarningMessage"),
            PrimaryButtonText = _res.GetString("RestoreWarningConfirm"),
            CloseButtonText = _res.GetString("RestoreWarningCancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async Task<(string password, bool confirmed)> OnRestorePasswordRequested()
    {
        var pwBox = new SecureInputBox
        {
            PlaceholderText = _res.GetString("RestorePwPlaceholder"),
            ShowRevealButton = Visibility.Visible,
            Width = 320
        };

        var dialog = new ContentDialog
        {
            Title = _res.GetString("RestorePwDialogTitle"),
            Content = pwBox,
            PrimaryButtonText = "OK",
            CloseButtonText = _res.GetString("RestoreWarningCancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary
            ? (pwBox.Password, true)
            : (string.Empty, false);
    }

    private async Task<(ImportFormat format, bool confirmed)> OnImportFormatRequested()
    {
        var combo = new ComboBox { Width = 320 };
        combo.Items.Add(new ComboBoxItem { Content = _res.GetString("ImportFormatCsv"), Tag = ImportFormat.Csv });
        combo.Items.Add(new ComboBoxItem { Content = _res.GetString("ImportFormatKdbx"), Tag = ImportFormat.Kdbx });
        combo.Items.Add(new ComboBoxItem { Content = _res.GetString("ImportFormatOnePux"), Tag = ImportFormat.OnePux });
        combo.Items.Add(new ComboBoxItem { Content = _res.GetString("ImportFormatBitwarden"), Tag = ImportFormat.Bitwarden });
        combo.SelectedIndex = 0;

        var dialog = new ContentDialog
        {
            Title = _res.GetString("ImportFormatDialogTitle"),
            Content = combo,
            PrimaryButtonText = "OK",
            CloseButtonText = _res.GetString("RestoreWarningCancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return (ImportFormat.Csv, false);

        var selectedItem = combo.SelectedItem as ComboBoxItem;
        var format = selectedItem?.Tag is ImportFormat f ? f : ImportFormat.Csv;
        return (format, true);
    }

    private async Task<(string password, bool confirmed)> OnImportPasswordRequested(ImportFormat format)
    {
        var pwBox = new SecureInputBox
        {
            PlaceholderText = _res.GetString("ImportKdbxPwPlaceholder"),
            ShowRevealButton = Visibility.Visible,
            Width = 320
        };

        var dialog = new ContentDialog
        {
            Title = _res.GetString("ImportKdbxPwTitle"),
            Content = pwBox,
            PrimaryButtonText = "OK",
            CloseButtonText = _res.GetString("RestoreWarningCancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary
            ? (pwBox.Password, true)
            : (string.Empty, false);
    }

    private async Task<(ImportMergeStrategy strategy, bool confirmed)> OnMergeStrategyRequested(
        int pwCount, int cardCount, int idCount, int noteCount)
    {
        var summaryText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        };
        var lines = new System.Text.StringBuilder();
        if (pwCount > 0) lines.AppendLine(string.Format(_res.GetString("ImportSummaryPasswords"), pwCount));
        if (cardCount > 0) lines.AppendLine(string.Format(_res.GetString("ImportSummaryCards"), cardCount));
        if (idCount > 0) lines.AppendLine(string.Format(_res.GetString("ImportSummaryIdentities"), idCount));
        if (noteCount > 0) lines.AppendLine(string.Format(_res.GetString("ImportSummaryNotes"), noteCount));
        summaryText.Text = lines.ToString().TrimEnd();

        var skipRadio = new RadioButton { Content = _res.GetString("ImportMergeSkip"), IsChecked = true };
        var overwriteRadio = new RadioButton { Content = _res.GetString("ImportMergeOverwrite") };
        var keepBothRadio = new RadioButton { Content = _res.GetString("ImportMergeKeepBoth") };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(summaryText);
        panel.Children.Add(skipRadio);
        panel.Children.Add(overwriteRadio);
        panel.Children.Add(keepBothRadio);

        var dialog = new ContentDialog
        {
            Title = _res.GetString("ImportMergeTitle"),
            Content = panel,
            PrimaryButtonText = "OK",
            CloseButtonText = _res.GetString("RestoreWarningCancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return (ImportMergeStrategy.SkipDuplicates, false);

        ImportMergeStrategy strategy;
        if (overwriteRadio.IsChecked == true) strategy = ImportMergeStrategy.Overwrite;
        else if (keepBothRadio.IsChecked == true) strategy = ImportMergeStrategy.KeepBoth;
        else strategy = ImportMergeStrategy.SkipDuplicates;

        return (strategy, true);
    }

    private async Task OnImportCompleted(ImportResult result)
    {
        var lines = new System.Text.StringBuilder();
        if (result.PasswordsImported > 0) lines.AppendLine(string.Format(_res.GetString("ImportSummaryPasswords"), result.PasswordsImported));
        if (result.CardsImported > 0) lines.AppendLine(string.Format(_res.GetString("ImportSummaryCards"), result.CardsImported));
        if (result.IdentitiesImported > 0) lines.AppendLine(string.Format(_res.GetString("ImportSummaryIdentities"), result.IdentitiesImported));
        if (result.NotesImported > 0) lines.AppendLine(string.Format(_res.GetString("ImportSummaryNotes"), result.NotesImported));
        if (result.Skipped > 0) lines.AppendLine(string.Format(_res.GetString("ImportSummarySkipped"), result.Skipped));

        await ShowInfoDialogAsync(_res.GetString("ImportSummaryTitle"), lines.ToString().TrimEnd());
    }

    private async Task OnBackupCompleted(string path)
    {
        await ShowInfoDialogAsync(
            _res.GetString("BackupSuccessTitle"),
            _res.GetString("BackupSuccessMessage"));
    }

    private async Task OnRestoreCompleted()
    {
        await ShowInfoDialogAsync(
            _res.GetString("RestoreSuccessTitle"),
            _res.GetString("RestoreSuccessMessage"));
    }

    private async Task OnOperationError(string errorCode)
    {
        var message = errorCode switch
        {
            "WRONG_PASSWORD" => _res.GetString("RestoreErrorWrongPw"),
            "INVALID_FILE" => _res.GetString("RestoreErrorInvalid"),
            _ => errorCode
        };
        await ShowInfoDialogAsync(_res.GetString("ImportErrorTitle"), message);
    }
}
