using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PassKey.Core.Models;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

public sealed partial class PasswordVerifierView : UserControl
{
    private PasswordVerifierViewModel? _viewModel;

    // Strength bar segments
    private Border[] _strengthSegments = [];

    public PasswordVerifierView()
    {
        InitializeComponent();
        _strengthSegments = [StrengthSeg0, StrengthSeg1, StrengthSeg2, StrengthSeg3, StrengthSeg4];
    }

    public void SetViewModel(PasswordVerifierViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.Initialize();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PasswordVerifierViewModel.AnalysisResult):
                UpdateAnalysisUI();
                break;
            case nameof(PasswordVerifierViewModel.HasInput):
                // ResultsPanel and RequirementsSection are always visible (neutral gray state).
                // Only SuggestionsPanel toggles based on input.
                if (!_viewModel!.HasInput)
                    SuggestionsPanel.Visibility = Visibility.Collapsed;
                break;
            case nameof(PasswordVerifierViewModel.VaultScore):
            case nameof(PasswordVerifierViewModel.VaultScoreLabel):
                UpdateVaultScoreUI();
                break;
            case nameof(PasswordVerifierViewModel.TotalPasswords):
                TotalCountText.Text = _viewModel!.TotalPasswords.ToString();
                break;
            case nameof(PasswordVerifierViewModel.WeakCount):
                WeakCountText.Text = _viewModel!.WeakCount.ToString();
                UpdateWeakList();
                break;
            case nameof(PasswordVerifierViewModel.DuplicateCount):
                DuplicateCountText.Text = _viewModel!.DuplicateCount.ToString();
                UpdateDuplicateList();
                break;
            case nameof(PasswordVerifierViewModel.IsAuditLoading):
                AuditLoadingRing.IsActive = _viewModel!.IsAuditLoading;
                break;
            case nameof(PasswordVerifierViewModel.HasAuditResults):
                var hasPasswords = _viewModel!.TotalPasswords > 0;
                AuditEmptyText.Visibility = !hasPasswords ? Visibility.Visible : Visibility.Collapsed;
                break;
        }
    }

    private void VerifyPasswordInput_PasswordChanged(object sender, string password)
    {
        _viewModel?.AnalyzePassword(password);
    }

    private void RefreshAuditButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.RunAuditCommand.Execute(null);
    }

    private void UpdateAnalysisUI()
    {
        var result = _viewModel?.AnalysisResult;
        if (result is null)
        {
            ScoreText.Text = "—";
            ScoreText.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            StrengthLabel.Text = "—";
            CrackTimeText.Text = "—";
            UpdateStrengthBar(0);
            ResetChecklist();
            return;
        }

        // Score number (colored)
        ScoreText.Text = result.Score.ToString();
        ScoreText.Foreground = GetStrengthBrush(result.Score);

        // Strength label + crack time
        StrengthLabel.Text = GetLocalizedLabel(result.Label);
        CrackTimeText.Text = result.EstimatedCrackTime;

        // 5-segment bar
        UpdateStrengthBar(result.Score);

        // Checklist
        SetCheckItem(CheckMinLengthIcon, result.HasMinLength);
        SetCheckItem(CheckRecLengthIcon, result.HasRecommendedLength);
        SetCheckItem(CheckUppercaseIcon, result.HasUppercase);
        SetCheckItem(CheckLowercaseIcon, result.HasLowercase);
        SetCheckItem(CheckDigitsIcon, result.HasDigits);
        SetCheckItem(CheckSymbolsIcon, result.HasSymbols);
        SetCheckItem(CheckNoPatternsIcon, result.HasNoCommonPatterns);

        // Suggestions
        UpdateSuggestions(result);
    }

    /// <summary>
    /// Updates the 5-segment strength bar based on the score.
    /// Same algorithm as GeneratorView.
    /// </summary>
    private void UpdateStrengthBar(int score)
    {
        var filledCount = score switch
        {
            0 => 0,
            < 20 => 1,
            < 40 => 2,
            < 60 => 3,
            < 80 => 4,
            _ => 5
        };

        var brush = GetStrengthBrush(score);
        var emptyBrush = (Brush)Application.Current.Resources["ControlStrongFillColorDisabledBrush"];

        for (int i = 0; i < _strengthSegments.Length; i++)
        {
            _strengthSegments[i].Background = i < filledCount ? brush : emptyBrush;
        }
    }

    /// <summary>
    /// Returns the appropriate strength brush for a given score.
    /// Same logic as GeneratorView.
    /// </summary>
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

    private static void SetCheckItem(FontIcon icon, bool satisfied)
    {
        icon.Glyph = satisfied ? "\uE73E" : "\uE711"; // Checkmark or Dismiss
        icon.Foreground = satisfied
            ? (Brush)Application.Current.Resources["CheckPassBrush"]
            : (Brush)Application.Current.Resources["CheckFailBrush"];

        // Dim unsatisfied rows, "illuminate" satisfied ones
        if (icon.Parent is StackPanel row)
            row.Opacity = satisfied ? 1.0 : 0.45;
    }

    private void ResetChecklist()
    {
        var icons = new[] { CheckMinLengthIcon, CheckRecLengthIcon, CheckUppercaseIcon,
                            CheckLowercaseIcon, CheckDigitsIcon, CheckSymbolsIcon, CheckNoPatternsIcon };
        foreach (var icon in icons)
        {
            icon.Glyph = "\uE711";
            icon.Foreground = (Brush)Application.Current.Resources["CheckFailBrush"];
        }
        SuggestionsPanel.Visibility = Visibility.Collapsed;
    }

    private void UpdateSuggestions(PasswordStrengthResult result)
    {
        SuggestionsList.Children.Clear();

        if (result.Suggestions.Count == 0)
        {
            SuggestionsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        SuggestionsPanel.Visibility = Visibility.Visible;
        foreach (var suggestion in result.Suggestions)
        {
            var text = GetLocalizedSuggestion(suggestion);
            SuggestionsList.Children.Add(new TextBlock
            {
                Text = $"\u2022 {text}",
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    private void UpdateVaultScoreUI()
    {
        if (_viewModel is null) return;
        VaultScoreRing.Value = _viewModel.VaultScore;
        VaultScoreText.Text = _viewModel.VaultScore.ToString();
        VaultScoreLabelText.Text = GetLocalizedLabel(_viewModel.VaultScoreLabel);
    }

    private void UpdateWeakList()
    {
        WeakPasswordsList.Children.Clear();
        if (_viewModel is null) return;

        foreach (var item in _viewModel.WeakPasswords)
        {
            var row = new Grid { Padding = new Thickness(8, 6, 8, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Strength dot (colored by score)
            var dot = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = GetStrengthBrush(item.Score),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(dot, 0);
            row.Children.Add(dot);

            // Info
            var info = new StackPanel { Spacing = 2 };
            info.Children.Add(new TextBlock
            {
                Text = item.Title,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
            });
            info.Children.Add(new TextBlock
            {
                Text = $"{item.Username} — Punteggio: {item.Score}",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            Grid.SetColumn(info, 1);
            row.Children.Add(info);

            WeakPasswordsList.Children.Add(row);
        }

        WeakExpanderHeaderText.Text = $"Password deboli ({_viewModel.WeakCount})";
    }

    private void UpdateDuplicateList()
    {
        DuplicateGroupsList.Children.Clear();
        if (_viewModel is null) return;

        foreach (var group in _viewModel.DuplicateGroups)
        {
            var groupPanel = new StackPanel { Spacing = 4 };
            groupPanel.Children.Add(new TextBlock
            {
                Text = $"Gruppo ({group.Count} password identiche)",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
            });

            foreach (var entry in group.Entries)
            {
                groupPanel.Children.Add(new TextBlock
                {
                    Text = $"  \u2022 {entry.Title} ({entry.Username})",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
            }

            DuplicateGroupsList.Children.Add(groupPanel);
        }

        DuplicateExpanderHeaderText.Text = $"Password riutilizzate ({_viewModel.DuplicateCount})";
    }

    private static string GetLocalizedLabel(string label) => label switch
    {
        "VeryWeak" => "Molto debole",
        "Weak" => "Debole",
        "Medium" => "Media",
        "Strong" => "Forte",
        "VeryStrong" => "Molto forte",
        _ => label
    };

    private static string GetLocalizedSuggestion(string key) => key switch
    {
        "UseAtLeast8Characters" => "Usa almeno 8 caratteri",
        "UseAtLeast12Characters" => "Usa almeno 12 caratteri per una protezione migliore",
        "AddUppercaseLetters" => "Aggiungi lettere maiuscole",
        "AddLowercaseLetters" => "Aggiungi lettere minuscole",
        "AddNumbers" => "Aggiungi numeri",
        "AddSpecialCharacters" => "Aggiungi simboli speciali (!@#$%)",
        "AvoidCommonPatterns" => "Evita pattern comuni (password, 123456, qwerty...)",
        _ => key
    };
}
