using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PassKey.Core.Models;
using PassKey.Desktop.Services;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Desktop.Helpers;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

public sealed partial class PasswordVerifierView : UserControl
{
    private PasswordVerifierViewModel? _viewModel;
    // Static loader (used by the static GetLocalized* helpers and the header builders).
    private static readonly ResourceLoader s_res = new();

    // Strength bar segments
    private Border[] _strengthSegments = [];

    public PasswordVerifierView()
    {
        InitializeComponent();
        _strengthSegments = [StrengthSeg0, StrengthSeg1, StrengthSeg2, StrengthSeg3, StrengthSeg4];
    }

    /// <summary>
    /// Selects the second Pivot item ("Vault"). Called by the Shell when the user clicks
    /// the Dashboard health card so the audit is the first thing they see.
    /// </summary>
    public void SelectVaultTab() => TabPivot.SelectedIndex = 1;

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
            case nameof(PasswordVerifierViewModel.CompromisedCount):
                CompromisedCountText.Text = _viewModel!.CompromisedCount.ToString();
                CompromisedExpanderHeaderText.Text = string.Format(s_res.GetString("VerifierCompromisedFmt"), _viewModel.CompromisedCount);
                RebuildIssueList(CompromisedPasswordsList, _viewModel.CompromisedPasswords);
                break;
            case nameof(PasswordVerifierViewModel.WeakCount):
                WeakCountText.Text = _viewModel!.WeakCount.ToString();
                WeakExpanderHeaderText.Text = string.Format(s_res.GetString("VerifierWeakFmt"), _viewModel.WeakCount);
                RebuildIssueList(WeakPasswordsList, _viewModel.WeakPasswords);
                break;
            case nameof(PasswordVerifierViewModel.DuplicateCount):
                DuplicateCountText.Text = _viewModel!.DuplicateCount.ToString();
                DuplicateExpanderHeaderText.Text = string.Format(s_res.GetString("VerifierDuplicateFmt"), _viewModel.DuplicateCount);
                RebuildIssueList(DuplicateGroupsList, _viewModel.DuplicateEntries);
                break;
            case nameof(PasswordVerifierViewModel.IsAuditLoading):
                UpdateAuditLoadingUI();
                break;
            case nameof(PasswordVerifierViewModel.AuditProgress):
                UpdateAuditProgressUI();
                break;
            case nameof(PasswordVerifierViewModel.HasAuditResults):
                var hasPasswords = _viewModel!.TotalPasswords > 0;
                AuditEmptyText.Visibility = !hasPasswords ? Visibility.Visible : Visibility.Collapsed;
                break;
            case nameof(PasswordVerifierViewModel.HibpEnabled):
                HibpDisabledBanner.IsOpen = !_viewModel!.HibpEnabled;
                break;
        }
    }

    /// <summary>
    /// Rebuilds an expander's content stack from a flat list of <see cref="WatchtowerIssue"/>.
    /// Used for the Compromised / Weak / Duplicates lists inside the Vault tab.
    /// </summary>
    private void RebuildIssueList(StackPanel host, IEnumerable<WatchtowerIssue> items)
    {
        host.Children.Clear();
        foreach (var item in items)
        {
            host.Children.Add(BuildIssueRow(item));
        }
    }

    private static Grid BuildIssueRow(WatchtowerIssue item)
    {
        var row = new Grid { Padding = new Thickness(8, 6, 8, 6), ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Severity dot (red for breached, orange for weak, otherwise muted)
        var dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = SeverityBrush(item),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(dot, 0);
        row.Children.Add(dot);

        // Title + sub-info (username, breach count, "riutilizzata" flag)
        var info = new StackPanel { Spacing = 2 };
        info.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(item.Title) ? s_res.GetString("VerifierUntitled") : item.Title,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        });
        var details = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(item.Username)) details.Append(item.Username);
        if (item.BreachCount > 0)
        {
            if (details.Length > 0) details.Append(" — ");
            details.Append(string.Format(s_res.GetString("VerifierBreachLabel"), item.BreachCount));
        }
        if (item.IsDuplicate)
        {
            if (details.Length > 0) details.Append(" — ");
            details.Append(s_res.GetString("VerifierReused"));
        }
        if (details.Length > 0)
        {
            info.Children.Add(new TextBlock
            {
                Text = details.ToString(),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
        }
        Grid.SetColumn(info, 1);
        row.Children.Add(info);

        // Strength score (right side)
        var score = new TextBlock
        {
            Text = item.StrengthScore.ToString(),
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = GetStrengthBrush(item.StrengthScore),
        };
        Grid.SetColumn(score, 2);
        row.Children.Add(score);

        return row;
    }

    private static Brush SeverityBrush(WatchtowerIssue item)
    {
        if (item.BreachCount > 0)
            return (Brush)Application.Current.Resources["StatRemovedBrush"];
        if (item.StrengthScore < 40)
            return (Brush)Application.Current.Resources["StatModifiedBrush"];
        return (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
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
        CrackTimeText.Text = CrackTimeFormatter.Localize(result.EstimatedCrackTime);

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
        // While a scan is running the score ring is repurposed as a live progress ring
        // (see UpdateAuditProgressUI); don't overwrite it with the not-yet-final score.
        if (_viewModel.IsAuditLoading) return;
        VaultScoreRing.Value = _viewModel.VaultScore;
        VaultScoreText.Text = _viewModel.VaultScore.ToString();
        VaultScoreLabelText.Text = GetLocalizedLabel(_viewModel.VaultScoreLabel);
    }

    /// <summary>
    /// Toggles between the live-progress presentation (during a scan) and the final score
    /// (once it completes). The separate indeterminate spinner is no longer used: the score
    /// ring itself doubles as a determinate progress ring so the user sees the count climb.
    /// </summary>
    private void UpdateAuditLoadingUI()
    {
        if (_viewModel is null) return;

        if (_viewModel.IsAuditLoading)
            UpdateAuditProgressUI();
        else
            UpdateVaultScoreUI();
    }

    /// <summary>
    /// Drives the score ring as a determinate progress indicator during a scan: the arc
    /// grows with the percentage and the centre shows the live "X / N" count, so the user
    /// always sees tangible forward motion instead of a frozen "0".
    /// </summary>
    private void UpdateAuditProgressUI()
    {
        if (_viewModel is null || !_viewModel.IsAuditLoading) return;
        VaultScoreRing.Value = _viewModel.AuditProgress;          // 0..100
        VaultScoreText.Text = _viewModel.ScannedCount.ToString();
        VaultScoreLabelText.Text = $"/ {_viewModel.TotalToScan}";
    }


    private static string GetLocalizedLabel(string label) => label switch
    {
        "VeryWeak" => s_res.GetString("StrengthVeryWeak"),
        "Weak" => s_res.GetString("StrengthWeak"),
        "Medium" => s_res.GetString("StrengthMedium"),
        "Strong" => s_res.GetString("StrengthStrong"),
        "VeryStrong" => s_res.GetString("StrengthVeryStrong"),
        _ => label
    };

    private static string GetLocalizedSuggestion(string key) => key switch
    {
        "UseAtLeast8Characters" => s_res.GetString("SuggestUseAtLeast8"),
        "UseAtLeast12Characters" => s_res.GetString("SuggestUseAtLeast12"),
        "AddUppercaseLetters" => s_res.GetString("SuggestAddUppercase"),
        "AddLowercaseLetters" => s_res.GetString("SuggestAddLowercase"),
        "AddNumbers" => s_res.GetString("SuggestAddNumbers"),
        "AddSpecialCharacters" => s_res.GetString("SuggestAddSpecial"),
        "AvoidCommonPatterns" => s_res.GetString("SuggestAvoidCommon"),
        _ => key
    };
}
