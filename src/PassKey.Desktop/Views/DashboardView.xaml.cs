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
    private readonly ResourceLoader _res = new();
    private DispatcherTimer? _searchTimer;

    public DashboardView()
    {
        InitializeComponent();
        EmptyState.Title = _res.GetString("EmptyDashboardTitle");
        EmptyState.Subtitle = _res.GetString("EmptyDashboardSubtitle");
        EmptyState.PrimaryActionText = _res.GetString("AddPassword");
        EmptyState.SecondaryActionText = _res.GetString("ImportData");

        VaultOverviewTitle.Text = _res.GetString("DashVaultOverview");
    }

    public async void SetViewModel(DashboardViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;

        // Pass localized resources to ViewModel
        vm.SetGreetingResources(
            _res.GetString("GreetingMorning"),
            _res.GetString("GreetingAfternoon"),
            _res.GetString("GreetingEvening"));

        vm.SetActionLabels(
            _res.GetString("ActionCreated"),
            _res.GetString("ActionModified"),
            _res.GetString("ActionDeleted"));

        vm.SetDeletedLabels(
            _res.GetString("DeletedPassword"),
            _res.GetString("DeletedCard"),
            _res.GetString("DeletedIdentity"),
            _res.GetString("DeletedNote"));

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
        PasswordsSubtitle.Text = _res.GetString("DashSubtitlePasswords");
        CardsSubtitle.Text = _res.GetString("DashSubtitleCards");
        IdentitiesSubtitle.Text = _res.GetString("DashSubtitleIdentities");
        NotesSubtitle.Text = _res.GetString("DashSubtitleNotes");

        // Per-card compact activity (3 indicators each)
        var noChangesLabel = _res.GetString("DashCardNoChanges");
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
            HealthTitle.Text = _res.GetString("DashHealthTitle");
            HealthSubtitle.Text = _viewModel.WeakPasswordCount > 0
                ? string.Format(_res.GetString("DashHealthWeak"), _viewModel.WeakPasswordCount)
                : _res.GetString("DashHealthGood");
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
                _res.GetString("DashExpiringCards"), _viewModel.ExpiringCardsCount);
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
            _viewModel?.NavigateToItem(item.EntityType, item.EntityId);
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
