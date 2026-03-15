using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Desktop.ViewModels;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace PassKey.Desktop.Views;

/// <summary>
/// Password detail view (add/edit form) displayed in the 400px side panel.
/// </summary>
public sealed partial class PasswordDetailView : UserControl
{
    private PasswordDetailViewModel? _viewModel;
    private bool _updatingFromVm;
    private readonly ResourceLoader _res = new();

    // 24 Segoe MDL2 Assets glyphs for the icon picker
    private static readonly string[] IconGlyphs =
    [
        "\uE715", "\uE8C3", "\uE753", "\uE8BD", "\uE774", "\uE72C",
        "\uE780", "\uE8D4", "\uE821", "\uE754", "\uE825", "\uE968",
        "\uE8F9", "\uE8EC", "\uE90F", "\uE909", "\uE10F", "\uE2B1",
        "\uE965", "\uE8A0", "\uE8B7", "\uE720", "\uE737", "\uE8F1"
    ];

    public PasswordDetailView()
    {
        InitializeComponent();
        PasswordInput.PasswordChanged += OnPasswordInputChanged;
        IconPickerGrid.ItemsSource = IconGlyphs;
    }

    public void SetViewModel(PasswordDetailViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;

        // Populate UI from ViewModel
        _updatingFromVm = true;
        PanelTitleText.Text = vm.PanelTitle;
        TitleBox.Text = vm.Title;
        UsernameBox.Text = vm.Username;
        UrlBox.Text = vm.Url;
        NotesBox.Text = vm.Notes;
        _updatingFromVm = false;

        // Set password in SecureInputBox
        if (!string.IsNullOrEmpty(vm.Password))
            PasswordInput.SetPassword(vm.Password);
        else
            PasswordInput.Clear();

        SaveButton.IsEnabled = vm.CanSave;

        // Show delete button only in edit mode
        bool isEdit = !vm.IsNew;
        DeleteButton.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;

        // Subtitle
        PanelSubtitle.Text = isEdit
            ? _res.GetString("PwPanelSubtitleEdit")
            : _res.GetString("PwPanelSubtitleNew");

        // Strength indicator
        UpdateStrengthLabel();

        // Icon preview
        _ = UpdateIconPreviewAsync();

        // Focus first field
        TitleBox.Focus(FocusState.Programmatic);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PasswordDetailViewModel.CanSave):
                SaveButton.IsEnabled = _viewModel?.CanSave ?? false;
                break;
            case nameof(PasswordDetailViewModel.IsSaving):
                UpdateSavingState(_viewModel?.IsSaving ?? false);
                break;
            case nameof(PasswordDetailViewModel.PasswordStrengthLabel):
                UpdateStrengthLabel();
                break;
            case nameof(PasswordDetailViewModel.Password):
                // Password changed programmatically (e.g., generated) — update SecureInputBox
                if (_viewModel is not null && !string.IsNullOrEmpty(_viewModel.Password))
                    PasswordInput.SetPassword(_viewModel.Password);
                else
                    PasswordInput.Clear();
                break;
            case nameof(PasswordDetailViewModel.FaviconBase64):
                _ = UpdateIconPreviewAsync();
                break;
            case nameof(PasswordDetailViewModel.Title):
                // Refresh letter avatar if no custom icon is set
                if (string.IsNullOrEmpty(_viewModel?.FaviconBase64))
                    _ = UpdateIconPreviewAsync();
                break;
        }
    }

    // --- Icon picker ---

    private async Task UpdateIconPreviewAsync()
    {
        if (_viewModel is null) return;

        var favicon = _viewModel.FaviconBase64;

        if (string.IsNullOrEmpty(favicon))
        {
            var first = string.IsNullOrEmpty(_viewModel.Title) ? "?" : _viewModel.Title[0].ToString().ToUpper();
            IconPreviewLetter.Text = first;
            IconPreviewLetter.Visibility = Visibility.Visible;
            IconPreviewGlyph.Visibility = Visibility.Collapsed;
            IconPreviewImage.Visibility = Visibility.Collapsed;
            BtnRemoveIcon.Visibility = Visibility.Collapsed;
        }
        else if (favicon.StartsWith("glyph:", StringComparison.Ordinal))
        {
            IconPreviewGlyph.Glyph = favicon["glyph:".Length..];
            IconPreviewLetter.Visibility = Visibility.Collapsed;
            IconPreviewGlyph.Visibility = Visibility.Visible;
            IconPreviewImage.Visibility = Visibility.Collapsed;
            BtnRemoveIcon.Visibility = Visibility.Visible;
        }
        else
        {
            try
            {
                var bytes = Convert.FromBase64String(favicon);
                using var stream = new InMemoryRandomAccessStream();
                using var writer = new DataWriter(stream);
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                writer.DetachStream();
                stream.Seek(0);
                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(stream);
                IconPreviewImage.Source = bmp;
                IconPreviewLetter.Visibility = Visibility.Collapsed;
                IconPreviewGlyph.Visibility = Visibility.Collapsed;
                IconPreviewImage.Visibility = Visibility.Visible;
                BtnRemoveIcon.Visibility = Visibility.Visible;
            }
            catch
            {
                var first = string.IsNullOrEmpty(_viewModel.Title) ? "?" : _viewModel.Title[0].ToString().ToUpper();
                IconPreviewLetter.Text = first;
                IconPreviewLetter.Visibility = Visibility.Visible;
                IconPreviewGlyph.Visibility = Visibility.Collapsed;
                IconPreviewImage.Visibility = Visibility.Collapsed;
                BtnRemoveIcon.Visibility = Visibility.Collapsed;
            }
        }
    }

    private async void UploadIcon_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;

        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".ico");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        var bytes = await Task.Run(() => File.ReadAllBytes(file.Path));
        if (bytes.Length > 65536)
        {
            // File troppo grande — mostra tooltip sul bottone
            ToolTipService.SetToolTip(BtnUploadIcon, "Immagine troppo grande (max 64 KB)");
            return;
        }

        _viewModel.FaviconBase64 = Convert.ToBase64String(bytes);
    }

    private void IconPickerGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is string glyph && _viewModel is not null)
        {
            _viewModel.FaviconBase64 = "glyph:" + glyph;
            GlyphFlyout.Hide();
        }
    }

    private void RemoveIcon_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.FaviconBase64 = null;
    }

    private void UpdateStrengthLabel()
    {
        if (_viewModel is null) return;

        var label = _viewModel.PasswordStrengthLabel;
        if (string.IsNullOrEmpty(label))
        {
            StrengthLabel.Visibility = Visibility.Collapsed;
            return;
        }

        StrengthLabel.Visibility = Visibility.Visible;
        StrengthLabel.Text = label switch
        {
            "VeryWeak" => _res.GetString("StrengthVeryWeak"),
            "Weak" => _res.GetString("StrengthWeak"),
            "Medium" => _res.GetString("StrengthMedium"),
            "Strong" => _res.GetString("StrengthStrong"),
            "VeryStrong" => _res.GetString("StrengthVeryStrong"),
            _ => label
        };
        StrengthLabel.Foreground = GetStrengthBrush(_viewModel.PasswordStrengthScore);
    }

    private Microsoft.UI.Xaml.Media.Brush GetStrengthBrush(int score) => score switch
    {
        < 20 => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["StrengthVeryWeakBrush"],
        < 40 => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["StrengthWeakBrush"],
        < 60 => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["StrengthMediumBrush"],
        < 80 => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["StrengthStrongBrush"],
        _ => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["StrengthVeryStrongBrush"]
    };

    // TextBox → ViewModel sync
    private void TitleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null)
            _viewModel.Title = TitleBox.Text;
    }

    private void UrlBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null)
            _viewModel.Url = UrlBox.Text;
    }

    private void UsernameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null)
            _viewModel.Username = UsernameBox.Text;
    }

    private void NotesBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null)
            _viewModel.Notes = NotesBox.Text;
    }

    private void OnPasswordInputChanged(object? sender, string password)
    {
        if (_viewModel is not null)
            _viewModel.Password = password;
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.GeneratePasswordCommand.Execute(null);

        // After generation, update SecureInputBox to show masked password
        if (_viewModel is not null && !string.IsNullOrEmpty(_viewModel.Password))
        {
            PasswordInput.SetPassword(_viewModel.Password);
        }
    }

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
        SaveButtonText.Text = saving ? "Salvataggio..." : "Salva";
        SaveButton.IsEnabled = !saving;
    }
}
