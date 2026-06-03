using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Desktop.Helpers;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Code-behind for <see cref="GeneratorView"/>.
/// Delegates all business logic to <see cref="GeneratorViewModel"/> via <see cref="SetViewModel"/>.
/// </summary>
/// <remarks>
/// Handles UI-specific interactions:
/// <list type="bullet">
///   <item>Syntax-coloured password display (letters / digits / symbols rendered with distinct brushes).</item>
///   <item>5-segment strength bar driven by <see cref="GeneratorViewModel.StrengthResult"/>.</item>
///   <item>History list rendered dynamically as <see cref="Grid"/> items with copy buttons.</item>
///   <item>Slider ↔ ViewModel length synchronisation guarded by <c>_updatingFromVm</c>.</item>
///   <item>ARIA live-region announcements via <c>A11yAnnouncer</c>.</item>
/// </list>
/// </remarks>
public sealed partial class GeneratorView : UserControl
{
    private GeneratorViewModel? _viewModel;
    private bool _updatingFromVm;
    // Shared loader (also used by the static crack-time helpers).
    private static readonly ResourceLoader s_res = new();

    public GeneratorView()
    {
        InitializeComponent();
        // Re-render colour-dependent UI when the app theme changes at runtime, so the
        // code-built colours (password syntax, strength bar, history) follow the new theme.
        ActualThemeChanged += OnActualThemeChanged;
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        if (_viewModel is null) return;
        UpdatePasswordDisplay(_viewModel.GeneratedPassword);
        UpdateStrengthUI();
        UpdateHistoryUI();
    }

    public void SetViewModel(GeneratorViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;

        // Set initial UI state from VM
        _updatingFromVm = true;
        LengthSlider.Value = vm.Length;
        LengthLabel.Text = vm.Length.ToString();
        UppercaseToggle.IsOn = vm.IncludeUppercase;
        LowercaseToggle.IsOn = vm.IncludeLowercase;
        DigitsToggle.IsOn = vm.IncludeDigits;
        SymbolsToggle.IsOn = vm.IncludeSymbols;
        ExcludeAmbiguousToggle.IsOn = vm.ExcludeAmbiguous;
        _updatingFromVm = false;

        // Generate initial password
        vm.Initialize();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GeneratorViewModel.GeneratedPassword):
                UpdatePasswordDisplay(_viewModel?.GeneratedPassword ?? string.Empty);
                Announce(s_res.GetString("GeneratorPwGenerated"));
                break;

            case nameof(GeneratorViewModel.StrengthResult):
                UpdateStrengthUI();
                break;

            case nameof(GeneratorViewModel.ShowCopiedFeedback):
                if (_viewModel?.ShowCopiedFeedback == true)
                    ShowCopyFeedback();
                break;

            case nameof(GeneratorViewModel.History):
                UpdateHistoryUI();
                break;

            case nameof(GeneratorViewModel.IncludeLowercase):
                _updatingFromVm = true;
                LowercaseToggle.IsOn = _viewModel?.IncludeLowercase ?? true;
                _updatingFromVm = false;
                break;
        }
    }

    // --- Password display with syntax coloring ---

    private void UpdatePasswordDisplay(string password)
    {
        PasswordParagraph.Inlines.Clear();

        if (string.IsNullOrEmpty(password))
            return;

        var letterBrush = ThemeBrush("PasswordCharLetterBrush");
        var digitBrush = ThemeBrush("PasswordCharDigitBrush");
        var symbolBrush = ThemeBrush("PasswordCharSymbolBrush");

        // Group consecutive characters of the same type into a single Run
        var currentType = ClassifyChar(password[0]);
        var segment = new System.Text.StringBuilder();
        segment.Append(password[0]);

        for (int i = 1; i < password.Length; i++)
        {
            var charType = ClassifyChar(password[i]);
            if (charType == currentType)
            {
                segment.Append(password[i]);
            }
            else
            {
                PasswordParagraph.Inlines.Add(CreateRun(segment.ToString(),
                    GetBrushForType(currentType, letterBrush, digitBrush, symbolBrush)));
                segment.Clear();
                segment.Append(password[i]);
                currentType = charType;
            }
        }

        // Flush last segment
        if (segment.Length > 0)
        {
            PasswordParagraph.Inlines.Add(CreateRun(segment.ToString(),
                GetBrushForType(currentType, letterBrush, digitBrush, symbolBrush)));
        }
    }

    private enum CharType { Letter, Digit, Symbol }

    private static CharType ClassifyChar(char c)
    {
        if (char.IsLetter(c)) return CharType.Letter;
        if (char.IsDigit(c)) return CharType.Digit;
        return CharType.Symbol;
    }

    private static Brush GetBrushForType(CharType type, Brush letter, Brush digit, Brush symbol) => type switch
    {
        CharType.Letter => letter,
        CharType.Digit => digit,
        CharType.Symbol => symbol,
        _ => letter
    };

    private static Run CreateRun(string text, Brush foreground) => new()
    {
        Text = text,
        Foreground = foreground
    };

    // --- Strength UI ---

    private void UpdateStrengthUI()
    {
        var result = _viewModel?.StrengthResult;
        if (result is null)
        {
            ScoreText.Text = "0";
            StrengthLabel.Text = "—";
            CrackTimeText.Text = "—";
            UpdateStrengthBar(0, null);
            return;
        }

        // Score number + strength label + color
        var brush = GetStrengthBrush(result.Score);
        ScoreText.Text = result.Score.ToString();
        ScoreText.Foreground = brush;
        StrengthLabel.Text = GetStrengthLabel(result.Label);
        StrengthLabel.Foreground = brush;

        // Crack time
        CrackTimeText.Text = CrackTimeFormatter.Localize(result.EstimatedCrackTime);

        // Segmented bar
        UpdateStrengthBar(result.Score, brush);
    }

    private void UpdateStrengthBar(int score, Brush? activeBrush)
    {
        var segments = new[] { StrengthSeg0, StrengthSeg1, StrengthSeg2, StrengthSeg3, StrengthSeg4 };

        int filledCount;
        if (score == 0) filledCount = 0;
        else if (score < 20) filledCount = 1;
        else if (score < 40) filledCount = 2;
        else if (score < 60) filledCount = 3;
        else if (score < 80) filledCount = 4;
        else filledCount = 5;

        for (int i = 0; i < segments.Length; i++)
        {
            if (i < filledCount && activeBrush is not null)
                segments[i].Background = activeBrush;
            else
                // Revert to the XAML-declared {ThemeResource ControlStrongFillColorDisabledBrush}
                // so the inactive colour stays theme-aware.
                segments[i].ClearValue(Border.BackgroundProperty);
        }
    }

    // --- History UI ---

    private void UpdateHistoryUI()
    {
        HistoryList.Children.Clear();

        var history = _viewModel?.History;
        if (history is null || history.Count == 0)
        {
            HistoryEmptyText.Visibility = Visibility.Visible;
            return;
        }

        HistoryEmptyText.Visibility = Visibility.Collapsed;

        foreach (var entry in history)
        {
            var item = CreateHistoryItem(entry);
            HistoryList.Children.Add(item);
        }
    }

    private Grid CreateHistoryItem(GeneratorViewModel.HistoryEntry entry)
    {
        // Style (theme-aware ThemeResource background) defined in GeneratorView.xaml.
        var grid = new Grid { Style = (Style)Resources["HistoryItemGridStyle"] };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // strength dot
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // password
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // timestamp
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // copy button

        // Strength dot
        var dot = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = GetStrengthBrush(entry.Score),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dot, 0);
        grid.Children.Add(dot);

        // Truncated password text
        var text = new TextBlock
        {
            Text = entry.DisplayPassword,
            VerticalAlignment = VerticalAlignment.Center,
            IsTextSelectionEnabled = true,
            FontFamily = new FontFamily("Cascadia Mono"),
            FontSize = 13
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        // Relative timestamp
        var timeText = new TextBlock
        {
            Text = GetRelativeTime(entry.GeneratedAt),
            Style = (Style)Resources["HistoryTimeTextStyle"]
        };
        Grid.SetColumn(timeText, 2);
        grid.Children.Add(timeText);

        // Copy button
        var copyBtn = new Button
        {
            Padding = new Thickness(6, 4, 6, 4),
            Content = new FontIcon { Glyph = "\uE8C8", FontSize = 12 }
        };
        ToolTipService.SetToolTip(copyBtn, s_res.GetString("ButtonCopy"));
        copyBtn.Click += (_, _) => _viewModel?.CopyHistoryEntryCommand.Execute(entry);
        Grid.SetColumn(copyBtn, 3);
        grid.Children.Add(copyBtn);

        return grid;
    }

    private string GetRelativeTime(DateTime dt)
    {
        var loader = new ResourceLoader();
        var diff = DateTime.Now - dt;
        if (diff.TotalSeconds < 60) return loader.GetString("RelativeTimeNow");
        if (diff.TotalMinutes < 60) return string.Format(loader.GetString("RelativeTimeMinutes"), (int)diff.TotalMinutes);
        return string.Format(loader.GetString("RelativeTimeHours"), (int)diff.TotalHours);
    }

    // --- Copy feedback ---

    private async void ShowCopyFeedback()
    {
        CopyIcon.Glyph = "\uE73E"; // Checkmark
        Announce(s_res.GetString("GeneratorPwCopied"));

        await Task.Delay(2000);

        CopyIcon.Glyph = "\uE8C8"; // Clipboard

        if (_viewModel is not null)
            _viewModel.ShowCopiedFeedback = false;
    }

    // --- Accessibility ---

    private void Announce(string message)
    {
        A11yAnnouncer.Text = "";
        A11yAnnouncer.Text = message;
    }

    // --- Event handlers ---

    private void LengthSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_updatingFromVm || _viewModel is null) return;

        var length = (int)e.NewValue;
        LengthLabel.Text = length.ToString();
        _viewModel.Length = length;
    }

    private void CharsetToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingFromVm || _viewModel is null) return;

        _viewModel.IncludeUppercase = UppercaseToggle.IsOn;
        _viewModel.IncludeLowercase = LowercaseToggle.IsOn;
        _viewModel.IncludeDigits = DigitsToggle.IsOn;
        _viewModel.IncludeSymbols = SymbolsToggle.IsOn;
        _viewModel.ExcludeAmbiguous = ExcludeAmbiguousToggle.IsOn;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.CopyPasswordCommand.Execute(null);
    }

    private void RegenerateButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.AddToHistory();
        _viewModel?.GenerateCommand.Execute(null);
    }

    // --- Helpers ---

    private string GetStrengthLabel(string label)
    {
        var loader = new ResourceLoader();
        return label switch
        {
            "VeryWeak"  => loader.GetString("StrengthVeryWeak"),
            "Weak"      => loader.GetString("StrengthWeak"),
            "Medium"    => loader.GetString("StrengthMedium"),
            "Strong"    => loader.GetString("StrengthStrong"),
            "VeryStrong"=> loader.GetString("StrengthVeryStrong"),
            _           => "—"
        };
    }

    private Brush GetStrengthBrush(int score)
    {
        var key = score switch
        {
            < 20 => "StrengthVeryWeakBrush",
            < 40 => "StrengthWeakBrush",
            < 60 => "StrengthMediumBrush",
            < 80 => "StrengthStrongBrush",
            _ => "StrengthVeryStrongBrush"
        };
        return ThemeBrush(key);
    }

    /// <summary>
    /// Resolves a brush that lives inside ThemeColors' ThemeDictionaries for the control's
    /// current ActualTheme. Needed because Application.Current.Resources[key] is NOT theme-aware
    /// for keys declared inside ThemeDictionaries (it returns the wrong theme's value).
    /// </summary>
    private Brush ThemeBrush(string key)
    {
        var dictKey = ActualTheme == ElementTheme.Dark ? "Default" : "Light";
        foreach (var md in Application.Current.Resources.MergedDictionaries)
        {
            if (md.ThemeDictionaries.TryGetValue(dictKey, out var obj) &&
                obj is ResourceDictionary td && td.TryGetValue(key, out var b) && b is Brush brush)
                return brush;
        }
        return (Brush)Application.Current.Resources[key];
    }
}
