using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Dashboard view: greeting, search, 4 stat cards with per-card activity,
/// health badge, expiring cards alert, recent items with hover actions, empty state.
/// </summary>
public sealed partial class DashboardView : UserControl
{
    private DashboardViewModel? _viewModel;
    private readonly ResourceLoader _resourceLoader = new();
    private DispatcherTimer? _searchTimer;

    public DashboardView()
    {
        InitializeComponent();
        EmptyState.Title = _resourceLoader.GetString("EmptyDashboardTitle");
        EmptyState.Subtitle = _resourceLoader.GetString("EmptyDashboardSubtitle");
        EmptyState.PrimaryActionText = _resourceLoader.GetString("AddPassword");
        EmptyState.SecondaryActionText = _resourceLoader.GetString("ImportData");

        VaultOverviewTitle.Text = _resourceLoader.GetString("DashVaultOverview");
    }

    public async void SetViewModel(DashboardViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;

        // Pass localized resources to ViewModel
        vm.SetGreetingResources(
            _resourceLoader.GetString("GreetingMorning"),
            _resourceLoader.GetString("GreetingAfternoon"),
            _resourceLoader.GetString("GreetingEvening"));

        vm.SetActionLabels(
            _resourceLoader.GetString("ActionCreated"),
            _resourceLoader.GetString("ActionModified"),
            _resourceLoader.GetString("ActionDeleted"));

        vm.SetDeletedLabels(
            _resourceLoader.GetString("DeletedPassword"),
            _resourceLoader.GetString("DeletedCard"),
            _resourceLoader.GetString("DeletedIdentity"),
            _resourceLoader.GetString("DeletedNote"));

        // Wire empty state buttons via PrimaryActionCommand/SecondaryActionCommand
        EmptyState.PrimaryActionCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(
            () => _viewModel?.NavigateToItem("PasswordEntry", Guid.Empty));
        EmptyState.SecondaryActionCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(
            () => _viewModel?.NavigateToItem("Import", Guid.Empty));

        // Show loading state
        LoadingProgress.IsActive = true;
        LoadingProgress.Visibility = Visibility.Visible;
        StatCardsGrid.Visibility = Visibility.Collapsed;

        await vm.LoadDashboardCommand.ExecuteAsync(null);

        // Hide loading state
        LoadingProgress.IsActive = false;
        LoadingProgress.Visibility = Visibility.Collapsed;

        UpdateUI();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DashboardViewModel.GreetingMessage):
            case nameof(DashboardViewModel.TotalPasswords):
            case nameof(DashboardViewModel.TotalCards):
            case nameof(DashboardViewModel.TotalIdentities):
            case nameof(DashboardViewModel.TotalNotes):
            case nameof(DashboardViewModel.IsVaultEmpty):
            case nameof(DashboardViewModel.VaultHealthScore):
            case nameof(DashboardViewModel.WeakPasswordCount):
            case nameof(DashboardViewModel.CompromisedPasswordCount):
            case nameof(DashboardViewModel.DuplicatePasswordCount):
            case nameof(DashboardViewModel.HasExpiringCards):
                UpdateUI();
                break;
        }
    }

    private void UpdateUI()
    {
        if (_viewModel is null) return;

        GreetingText.Text = _viewModel.GreetingMessage;

        // Stat card counts
        PasswordsCount.Text = _viewModel.TotalPasswords.ToString();
        CardsCount.Text = _viewModel.TotalCards.ToString();
        IdentitiesCount.Text = _viewModel.TotalIdentities.ToString();
        NotesCount.Text = _viewModel.TotalNotes.ToString();

        // Stat card subtitles
        PasswordsSubtitle.Text = _resourceLoader.GetString("DashSubtitlePasswords");
        CardsSubtitle.Text = _resourceLoader.GetString("DashSubtitleCards");
        IdentitiesSubtitle.Text = _resourceLoader.GetString("DashSubtitleIdentities");
        NotesSubtitle.Text = _resourceLoader.GetString("DashSubtitleNotes");

        // Per-card compact activity (3 indicators each)
        var noChangesLabel = _resourceLoader.GetString("DashCardNoChanges");
        UpdateCardActivity(_viewModel.PasswordsAdded, _viewModel.PasswordsRemoved, _viewModel.PasswordsModified,
            PwActivityRow, PwNoChanges, PwAdded, PwRemoved, PwModified, noChangesLabel);
        UpdateCardActivity(_viewModel.CardsAdded, _viewModel.CardsRemoved, _viewModel.CardsModified,
            CcActivityRow, CcNoChanges, CcAdded, CcRemoved, CcModified, noChangesLabel);
        UpdateCardActivity(_viewModel.IdentitiesAdded, _viewModel.IdentitiesRemoved, _viewModel.IdentitiesModified,
            IdActivityRow, IdNoChanges, IdAdded, IdRemoved, IdModified, noChangesLabel);
        UpdateCardActivity(_viewModel.NotesAdded, _viewModel.NotesRemoved, _viewModel.NotesModified,
            SnActivityRow, SnNoChanges, SnAdded, SnRemoved, SnModified, noChangesLabel);

        // Password Health Badge
        if (_viewModel.TotalPasswords > 0)
        {
            HealthBadge.Visibility = Visibility.Visible;
            HealthRing.Value = _viewModel.VaultHealthScore;
            HealthScoreText.Text = $"{_viewModel.VaultHealthScore}%";
            HealthRing.Foreground = GetHealthBrush(_viewModel.VaultHealthScore);
            HealthTitle.Text = _resourceLoader.GetString("DashHealthTitle");
            // Subtitle prefers the worst-news headline: compromised > weak > all good.
            HealthSubtitle.Text =
                _viewModel.CompromisedPasswordCount > 0
                    ? string.Format(_resourceLoader.GetString("DashHealthCompromisedSummary"), _viewModel.CompromisedPasswordCount)
                : _viewModel.WeakPasswordCount > 0
                    ? string.Format(_resourceLoader.GetString("DashHealthWeak"), _viewModel.WeakPasswordCount)
                : _resourceLoader.GetString("DashHealthGood");

            // 3 mini counters — leading-zero formatting ("01", "07") keeps the right column
            // visually aligned and matches the compact stacked layout from the user mockup.
            HealthCompromisedText.Text = _viewModel.CompromisedPasswordCount.ToString("D2");
            HealthWeakText.Text = _viewModel.WeakPasswordCount.ToString("D2");
            HealthDuplicatesText.Text = _viewModel.DuplicatePasswordCount.ToString("D2");
        }
        else
        {
            HealthBadge.Visibility = Visibility.Collapsed;
        }

        // Expiring Cards Alert
        if (_viewModel.HasExpiringCards)
        {
            ExpiringCardsAlert.Visibility = Visibility.Visible;
            ExpiringCardsAlert.IsOpen = true;
            ExpiringCardsAlert.Message = string.Format(
                _resourceLoader.GetString("DashExpiringCards"), _viewModel.ExpiringCardsCount);
        }
        else
        {
            ExpiringCardsAlert.IsOpen = false;
            ExpiringCardsAlert.Visibility = Visibility.Collapsed;
        }

        RecentList.ItemsSource = _viewModel.RecentItems;

        // Show/hide empty state vs content
        var isEmpty = _viewModel.IsVaultEmpty;
        EmptyState.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        StatCardsGrid.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
        VaultOverviewTitle.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
        HealthBadge.Visibility = isEmpty ? Visibility.Collapsed : HealthBadge.Visibility;
        ExpiringCardsAlert.Visibility = isEmpty ? Visibility.Collapsed : ExpiringCardsAlert.Visibility;
        RecentList.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void UpdateCardActivity(int added, int removed, int modified,
        StackPanel activityRow, TextBlock noChangesText,
        TextBlock addedTB, TextBlock removedTB, TextBlock modifiedTB,
        string noChangesLabel)
    {
        if (added == 0 && removed == 0 && modified == 0)
        {
            activityRow.Visibility = Visibility.Collapsed;
            noChangesText.Text = noChangesLabel;
            noChangesText.Visibility = Visibility.Visible;
        }
        else
        {
            activityRow.Visibility = Visibility.Visible;
            noChangesText.Visibility = Visibility.Collapsed;
            addedTB.Text = added.ToString();
            removedTB.Text = removed.ToString();
            modifiedTB.Text = modified.ToString();
        }
    }

    private Microsoft.UI.Xaml.Media.Brush GetHealthBrush(int score)
    {
        var key = score switch
        {
            < 20 => "StrengthVeryWeakBrush",
            < 40 => "StrengthWeakBrush",
            < 60 => "StrengthMediumBrush",
            < 80 => "StrengthStrongBrush",
            _ => "StrengthVeryStrongBrush"
        };

        if (Resources.TryGetValue(key, out var brush) && brush is Microsoft.UI.Xaml.Media.Brush b)
            return b;

        // Fallback: try from app resources
        if (Application.Current.Resources.TryGetValue(key, out var appBrush) && appBrush is Microsoft.UI.Xaml.Media.Brush ab)
            return ab;

        return (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
    }

    // --- Stat Card Click (Step D) ---

    private void StatCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid && int.TryParse(grid.Tag?.ToString(), out var index))
        {
            var entityType = index switch
            {
                0 => "PasswordEntry",
                1 => "CreditCardEntry",
                2 => "IdentityEntry",
                3 => "SecureNoteEntry",
                _ => ""
            };
            if (!string.IsNullOrEmpty(entityType))
                _viewModel?.NavigateToItem(entityType, Guid.Empty);
        }
    }

    private void StatCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
            ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
    }

    private void StatCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
    }

    // --- Health badge (clickable card → Verifica/Vault) ---

    private void HealthBadge_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _viewModel?.RequestNavigationToVerifier();
    }

    private void HealthBadge_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
    }

    private void HealthBadge_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
    }

    // --- Recent Items Hover Actions (Step C) ---

    private void RecentRow_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid && grid.Children.Count > 4 && grid.Children[4] is StackPanel actions)
            actions.Opacity = 1;
    }

    private void RecentRow_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid && grid.Children.Count > 4 && grid.Children[4] is StackPanel actions)
            actions.Opacity = 0;
    }

    private void CopyAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RecentActivityItem item } && !string.IsNullOrEmpty(item.CopyValue))
            _viewModel?.CopyToClipboard(item.CopyValue);
    }

    private void OpenUrlAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RecentActivityItem item } && item.HasUrl)
        {
            try
            {
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri(item.Url!));
            }
            catch { /* invalid URL */ }
        }
    }

    private void EditAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RecentActivityItem item })
            _viewModel?.NavigateToItem(item.EntityType, item.EntityId);
    }

    // --- Search ---

    private void DashSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

        _searchTimer?.Stop();

        if (_searchTimer is null)
        {
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchTimer.Tick += SearchTimer_Tick;
        }

        _searchTimer.Start();
    }

    private void SearchTimer_Tick(object? sender, object e)
    {
        _searchTimer?.Stop();
        if (_viewModel is null) return;

        _viewModel.ExecuteSearch(DashSearchBox.Text);
        DashSearchBox.ItemsSource = _viewModel.SearchResults;
    }

    private void DashSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchResultItem item)
        {
            sender.Text = item.Title;
        }
    }

    private void DashSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SearchResultItem item)
        {
            // Defer the navigation: navigating synchronously inside this event tears down
            // the Dashboard (and this AutoSuggestBox) before the control closes its
            // suggestion popup. The orphaned popup then overlays the destination page and
            // silently swallows all pointer input. Enqueueing lets the AutoSuggestBox
            // finish closing its popup before the view is replaced.
            DispatcherQueue.TryEnqueue(() => _viewModel?.NavigateToItem(item.EntityType, item.EntityId));
        }
    }

    /// <summary>
    /// Focus the search box (called by ShellView on Ctrl+F).
    /// </summary>
    public void FocusSearchBox()
    {
        DashSearchBox.Focus(FocusState.Programmatic);
    }

    // --- Recent items click ---

    private void RecentList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecentActivityItem item)
        {
            _viewModel?.NavigateToItem(item.EntityType, item.EntityId);
        }
    }
}
