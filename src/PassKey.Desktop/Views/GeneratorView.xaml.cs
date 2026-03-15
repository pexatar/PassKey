using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
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

    public GeneratorView()
    {
        InitializeComponent();
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
                Announce("Nuova password generata");
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

        var letterBrush = (Brush)Application.Current.Resources["PasswordCharLetterBrush"];
        var digitBrush = (Brush)Application.Current.Resources["PasswordCharDigitBrush"];
        var symbolBrush = (Brush)Application.Current.Resources["PasswordCharSymbolBrush"];

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
        CrackTimeText.Text = GetCrackTimeLabel(result.EstimatedCrackTime);

        // Segmented bar
        UpdateStrengthBar(result.Score, brush);
    }

    private void UpdateStrengthBar(int score, Brush? activeBrush)
    {
        var segments = new[] { StrengthSeg0, StrengthSeg1, StrengthSeg2, StrengthSeg3, StrengthSeg4 };
        var inactiveBrush = (Brush)Application.Current.Resources["ControlStrongFillColorDisabledBrush"];

        int filledCount;
        if (score == 0) filledCount = 0;
        else if (score < 20) filledCount = 1;
        else if (score < 40) filledCount = 2;
        else if (score < 60) filledCount = 3;
        else if (score < 80) filledCount = 4;
        else filledCount = 5;

        for (int i = 0; i < segments.Length; i++)
        {
            segments[i].Background = i < filledCount ? (activeBrush ?? inactiveBrush) : inactiveBrush;
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
        var grid = new Grid
        {
            Padding = new Thickness(10, 8, 10, 8),
            ColumnSpacing = 8,
            CornerRadius = new CornerRadius(6),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]
        };
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
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 12
        };
        Grid.SetColumn(timeText, 2);
        grid.Children.Add(timeText);

        // Copy button
        var copyBtn = new Button
        {
            Padding = new Thickness(6, 4, 6, 4),
            Content = new FontIcon { Glyph = "\uE8C8", FontSize = 12 }
        };
        ToolTipService.SetToolTip(copyBtn, "Copia");
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
        Announce("Password copiata negli appunti");

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

    private static string GetCrackTimeLabel(string time) => time switch
    {
        "instant" => "Istantaneo",
        "seconds" => "Pochi secondi",
        "centuries" => "Secoli",
        "millennia" => "Millenni",
        _ => LocalizeCrackTimeString(time)
    };

    private static string LocalizeCrackTimeString(string time)
    {
        var parts = time.Split(' ', 2);
        if (parts.Length != 2) return time;

        var number = parts[0];
        var unit = parts[1].ToLowerInvariant();

        return unit switch
        {
            "minutes" or "minute" => $"{number} minuti",
            "hours" or "hour" => $"{number} ore",
            "days" or "day" => $"{number} giorni",
            "years" or "year" => $"{number} anni",
            _ => time
        };
    }

    private static Brush GetStrengthBrush(int score)
    {
        var key = score switch
        {
            < 20 => "StrengthVeryWeakBrush",
            < 40 => "StrengthWeakBrush",
            < 60 => "StrengthMediumBrush",
            < 80 => "StrengthStrongBrush",
            _ => "StrengthVeryStrongBrush"
        };
        return (Brush)Application.Current.Resources[key];
    }
}
