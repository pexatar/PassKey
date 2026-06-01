using System.IO;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;
using ZXing;
using ZXing.Common;

namespace PassKey.Desktop.Views;

/// <summary>
/// Password detail view (add/edit form) displayed in the 400px side panel.
/// </summary>
public sealed partial class PasswordDetailView : UserControl
{
    private PasswordDetailViewModel? _viewModel;
    private bool _updatingFromVm;
    private readonly ResourceLoader _resourceLoader = new();

    /// <summary>Per-second timer that refreshes the live TOTP code while the view is loaded.</summary>
    private DispatcherQueueTimer? _totpTimer;

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

        Unloaded += (_, _) => StopTotpTimer();
    }

    public void SetViewModel(PasswordDetailViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;

        // Populate UI from ViewModel
        _updatingFromVm = true;
        // PanelTitle on the VM is hardcoded Italian — bypass with a localised lookup.
        PanelTitleText.Text = vm.IsNew
            ? _resourceLoader.GetString("PwPanelTitleNew")
            : _resourceLoader.GetString("PwPanelTitleEdit");
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
            ? _resourceLoader.GetString("PwPanelSubtitleEdit")
            : _resourceLoader.GetString("PwPanelSubtitleNew");

        // Strength indicator
        UpdateStrengthLabel();

        // Icon preview
        _ = UpdateIconPreviewAsync();

        // TOTP section state + per-second refresh timer
        UpdateTotpUi();
        StartTotpTimer();

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
            case nameof(PasswordDetailViewModel.TotpSecret):
            case nameof(PasswordDetailViewModel.CurrentTotpCode):
            case nameof(PasswordDetailViewModel.TotpRemainingSeconds):
                UpdateTotpUi();
                break;
        }
    }

    // ─── TOTP UI helpers ─────────────────────────────────────────────────────

    private void StartTotpTimer()
    {
        StopTotpTimer();
        if (_viewModel is null) return;

        var dq = DispatcherQueue.GetForCurrentThread();
        if (dq is null) return;

        _totpTimer = dq.CreateTimer();
        _totpTimer.Interval = TimeSpan.FromSeconds(1);
        _totpTimer.IsRepeating = true;
        _totpTimer.Tick += (_, _) => _viewModel?.RefreshTotpDisplay();
        _totpTimer.Start();
    }

    private void StopTotpTimer()
    {
        _totpTimer?.Stop();
        _totpTimer = null;
    }

    private void UpdateTotpUi()
    {
        if (_viewModel is null) return;

        var hasTotp = _viewModel.HasTotp;
        TotpEmptyPanel.Visibility = hasTotp ? Visibility.Collapsed : Visibility.Visible;
        TotpFilledPanel.Visibility = hasTotp ? Visibility.Visible : Visibility.Collapsed;

        if (hasTotp)
        {
            TotpCodeText.Text = string.IsNullOrEmpty(_viewModel.CurrentTotpCode) ? "—" : _viewModel.CurrentTotpCode;
            TotpCountdownText.Text = _viewModel.TotpRemainingSeconds > 0
                ? _viewModel.TotpRemainingSeconds.ToString()
                : "—";

            // The ProgressRing visualises remaining/total. We invert the value so the ring
            // fills as the period elapses (full at 0s left, empty at period seconds left).
            TotpCountdownRing.Maximum = _viewModel.TotpPeriod;
            TotpCountdownRing.Value = _viewModel.TotpPeriod - _viewModel.TotpRemainingSeconds;
            TotpCountdownRing.IsActive = true;

            // Hide the secret readout unless the user explicitly asked to show it.
            // (Show button toggles it from below.)
        }
        else
        {
            TotpSecretReadout.Visibility = Visibility.Collapsed;
        }
    }

    private async void TotpScanQrButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;

        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        var otpauthUri = await DecodeQrFromFileAsync(file);
        if (string.IsNullOrEmpty(otpauthUri))
        {
            ToolTipService.SetToolTip(TotpScanQrButton, "Nessun codice QR riconoscibile nell'immagine.");
            return;
        }

        if (!_viewModel.ApplyOtpAuthUri(otpauthUri))
        {
            ToolTipService.SetToolTip(TotpScanQrButton, "QR riconosciuto ma non è un URI 'otpauth://' valido.");
            return;
        }

        // Re-sync TextBoxes if the URI carried Title/Username so the user sees them populated.
        SyncTextBoxesFromViewModel();
    }

    private void TotpPasteUriButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;

        var pkg = Clipboard.GetContent();
        if (!pkg.Contains(StandardDataFormats.Text))
        {
            ToolTipService.SetToolTip(TotpPasteUriButton, "Negli appunti non c'è testo.");
            return;
        }

        _ = ApplyClipboardUriAsync(pkg);
    }

    private async Task ApplyClipboardUriAsync(DataPackageView pkg)
    {
        try
        {
            var text = await pkg.GetTextAsync();
            if (_viewModel is null || string.IsNullOrWhiteSpace(text)) return;

            if (!_viewModel.ApplyOtpAuthUri(text.Trim()))
            {
                ToolTipService.SetToolTip(TotpPasteUriButton, "Il testo negli appunti non è un URI 'otpauth://' valido.");
                return;
            }
            SyncTextBoxesFromViewModel();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TOTP paste] {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async void TotpEnterSecretButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;

        var input = new TextBox
        {
            PlaceholderText = _resourceLoader.GetString("TotpSeedPlaceholder"),
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas, Courier New"),
            AcceptsReturn = false,
        };
        var dialog = new ContentDialog
        {
            Title = _resourceLoader.GetString("Totp2faDialogTitle"),
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = _resourceLoader.GetString("Totp2faDialogBody"),
                        TextWrapping = TextWrapping.Wrap,
                    },
                    input,
                },
            },
            PrimaryButtonText = _resourceLoader.GetString("SaveButton"),
            CloseButtonText = _resourceLoader.GetString("CancelButton"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var res = await dialog.ShowAsync();
        if (res != ContentDialogResult.Primary) return;
        if (!_viewModel.ApplyManualSeed(input.Text))
            ToolTipService.SetToolTip(TotpEnterSecretButton, "Chiave Base32 non valida (usa solo A-Z e 2-7).");
    }

    private void TotpCopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        var raw = _viewModel.GetCurrentTotpCodeRaw();
        if (string.IsNullOrEmpty(raw)) return;

        // Reuse the existing ClipboardService so the auto-clear / history-suppression
        // behaviour matches the rest of the app (sensitive copy, 30 s auto-clear).
        var clip = App.Services.GetService(typeof(IClipboardService)) as IClipboardService;
        if (clip is null)
        {
            // Fallback: plain clipboard set without auto-clear.
            var pkg = new DataPackage();
            pkg.SetText(raw);
            Clipboard.SetContent(pkg);
        }
        else
        {
            clip.Copy(raw, CopyType.Sensitive);
        }

        if (App.Services.GetService(typeof(IToastService)) is IToastService toast)
            toast.Show(ToastSeverity.Info, _resourceLoader.GetString("ToastCopied"));
    }

    private void TotpShowSecretButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        if (TotpSecretReadout.Visibility == Visibility.Visible)
        {
            TotpSecretReadout.Visibility = Visibility.Collapsed;
            TotpSecretReadout.Text = string.Empty;
        }
        else
        {
            TotpSecretReadout.Visibility = Visibility.Visible;
            TotpSecretReadout.Text = _viewModel.TotpSecret ?? string.Empty;
        }
    }

    private void TotpRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.RemoveTotp();
    }

    private void SyncTextBoxesFromViewModel()
    {
        if (_viewModel is null) return;
        _updatingFromVm = true;
        try
        {
            if (TitleBox.Text != _viewModel.Title) TitleBox.Text = _viewModel.Title;
            if (UsernameBox.Text != _viewModel.Username) UsernameBox.Text = _viewModel.Username;
        }
        finally
        {
            _updatingFromVm = false;
        }
    }

    /// <summary>
    /// Decodes a QR code from an image file using ZXing.Net and the Windows
    /// <see cref="BitmapDecoder"/> API to extract raw 32-bit pixels. Returns the
    /// decoded textual payload (typically an <c>otpauth://...</c> URI) or null on failure.
    /// </summary>
    private static async Task<string?> DecodeQrFromFileAsync(StorageFile file)
    {
        try
        {
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                new BitmapTransform(),
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage);
            var bytes = pixelData.DetachPixelData();

            var luminance = new RGBLuminanceSource(bytes,
                (int)decoder.PixelWidth,
                (int)decoder.PixelHeight,
                RGBLuminanceSource.BitmapFormat.BGRA32);

            var reader = new ZXing.QrCode.QRCodeReader();
            var binarizer = new HybridBinarizer(luminance);
            var bitmap = new BinaryBitmap(binarizer);

            var result = reader.decode(bitmap);
            return result?.Text;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QR decode] {ex.GetType().Name}: {ex.Message}");
            return null;
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
            "VeryWeak" => _resourceLoader.GetString("StrengthVeryWeak"),
            "Weak" => _resourceLoader.GetString("StrengthWeak"),
            "Medium" => _resourceLoader.GetString("StrengthMedium"),
            "Strong" => _resourceLoader.GetString("StrengthStrong"),
            "VeryStrong" => _resourceLoader.GetString("StrengthVeryStrong"),
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
