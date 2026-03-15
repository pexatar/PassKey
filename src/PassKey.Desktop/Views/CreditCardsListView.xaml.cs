using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PassKey.Core.Constants;
using PassKey.Core.Models;
using PassKey.Core.Services;
using PassKey.Desktop.Controls;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Credit cards list view with header, search, card/list toggle, and 400px detail panel.
/// </summary>
public sealed partial class CreditCardsListView : UserControl
{
    private CreditCardsListViewModel? _viewModel;

    public CreditCardsListView()
    {
        InitializeComponent();
    }

    public async void SetViewModel(CreditCardsListViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.SaveCompleted += ShowSavedToast;

        await vm.LoadEntriesCommand.ExecuteAsync(null);
        UpdateCardRepeater();
        UpdateListView();
        UpdateEmptyState();
        UpdateViewToggle();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CreditCardsListViewModel.IsDetailOpen):
                UpdateDetailPanel();
                break;
            case nameof(CreditCardsListViewModel.IsEmpty):
                UpdateEmptyState();
                break;
            case nameof(CreditCardsListViewModel.DetailViewModel):
                UpdateDetailContent();
                break;
            case nameof(CreditCardsListViewModel.IsCardView):
                UpdateViewToggle();
                break;
        }
    }

    private void UpdateCardRepeater()
    {
        if (_viewModel is null) return;
        CardRepeater.ItemsSource = _viewModel.Entries;
    }

    private void UpdateListView()
    {
        if (_viewModel is null) return;
        ListViewControl.ItemsSource = _viewModel.Entries;
    }

    private void UpdateEmptyState()
    {
        if (_viewModel is null) return;
        var hasEntries = _viewModel.Entries.Count > 0;
        EmptyState.Visibility = _viewModel.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        CardViewScroller.Visibility = !_viewModel.IsEmpty && _viewModel.IsCardView ? Visibility.Visible : Visibility.Collapsed;
        ListViewControl.Visibility = !_viewModel.IsEmpty && !_viewModel.IsCardView ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateViewToggle()
    {
        if (_viewModel is null) return;

        var isCard = _viewModel.IsCardView;

        // Icona mostra la vista di DESTINAZIONE (dove si andrà cliccando)
        ViewToggleIcon.Glyph = isCard ? "\uE8FD" : "\uF0E2";

        // Show/hide appropriate views
        CardViewScroller.Visibility = isCard && !_viewModel.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        ListViewControl.Visibility = !isCard && !_viewModel.IsEmpty ? Visibility.Visible : Visibility.Collapsed;

        // Sort bar only in list view
        SortBar.Visibility = !isCard ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateDetailPanel()
    {
        if (_viewModel is null) return;

        if (_viewModel.IsDetailOpen)
        {
            DetailColumn.Width = new GridLength(400);
            DetailPanel.Visibility = Visibility.Visible;
        }
        else
        {
            DetailColumn.Width = new GridLength(0);
            DetailPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateDetailContent()
    {
        if (_viewModel?.DetailViewModel is not null)
        {
            var detailView = new CreditCardDetailView();
            detailView.SetViewModel(_viewModel.DetailViewModel);
            DetailContent.Content = detailView;
        }
        else
        {
            DetailContent.Content = null;
        }
    }

    /// <summary>
    /// Populate CreditCardControl for each card in ItemsRepeater.
    /// With DataTemplate, args.Element may be CreditCardControl or ContentPresenter wrapping it.
    /// </summary>
    private void CardRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        CreditCardControl? cardControl = args.Element as CreditCardControl
            ?? (args.Element as ContentPresenter)?.Content as CreditCardControl;

        if (cardControl is not null && _viewModel?.Entries.Count > args.Index)
        {
            var entry = _viewModel.Entries[args.Index];
            cardControl.CardNumber = entry.CardNumber;
            cardControl.CardholderName = entry.CardholderName;
            cardControl.ExpiryMonth = entry.ExpiryMonth;
            cardControl.ExpiryYear = entry.ExpiryYear;
            cardControl.CardType = entry.CardType;
            cardControl.AccentColor = entry.AccentColor;
            cardControl.Label = entry.Label;
            cardControl.Category = entry.Category;

            // Prevent duplicate handlers on element recycling
            cardControl.Tapped -= CardControl_Tapped;
            cardControl.Tapped += CardControl_Tapped;
        }
    }

    private void CardControl_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is CreditCardControl cardControl)
        {
            // Find the entry by matching card number + cardholder
            var entry = _viewModel?.Entries.FirstOrDefault(
                en => en.CardNumber == cardControl.CardNumber &&
                      en.CardholderName == cardControl.CardholderName);
            if (entry is not null)
                _viewModel?.EditEntryCommand.Execute(entry);
        }
    }

    // Event handlers
    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_viewModel is not null && args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            _viewModel.SearchQuery = sender.Text;
            UpdateEmptyState();
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.AddNewCommand.Execute(null);
    }

    private void ViewToggle_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ToggleViewCommand.Execute(null);
    }

    private void ListViewControl_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CreditCardEntry entry)
            _viewModel?.EditEntryCommand.Execute(entry);
    }

    private void CopyCardNumber_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CreditCardEntry entry)
            _viewModel?.CopyCardNumberCommand.Execute(entry);
    }

    private void EditEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CreditCardEntry entry)
            _viewModel?.EditEntryCommand.Execute(entry);
    }

    // --- Toast ---

    public void ShowSavedToast()
    {
        SavedTip.IsOpen = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (s, _) =>
        {
            SavedTip.IsOpen = false;
            ((DispatcherTimer)s!).Stop();
        };
        timer.Start();
    }

    // Hover effects — show/hide action buttons
    private void Row_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            foreach (var child in grid.Children.OfType<Button>())
                child.Opacity = 1;
        }
    }

    private void Row_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            foreach (var child in grid.Children.OfType<Button>())
                child.Opacity = 0;
        }
    }

    // Sort handlers
    private void SortLabel_Click(object sender, RoutedEventArgs e) => _viewModel?.Sort("Label");
    private void SortLast4_Click(object sender, RoutedEventArgs e) => _viewModel?.Sort("Last4");
    private void SortCardholder_Click(object sender, RoutedEventArgs e) => _viewModel?.Sort("Cardholder");
    private void SortDate_Click(object sender, RoutedEventArgs e) => _viewModel?.Sort("Date");

    private void OnViewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (_viewModel is null) return;
        if (FocusManager.GetFocusedElement(XamlRoot) is Microsoft.UI.Xaml.Controls.TextBox
            or Microsoft.UI.Xaml.Controls.AutoSuggestBox) return;

        switch (e.Key)
        {
            case Windows.System.VirtualKey.F2:
                if (_viewModel.SelectedEntry is not null)
                {
                    _viewModel.EditEntryCommand.Execute(_viewModel.SelectedEntry);
                    e.Handled = true;
                }
                break;
            case Windows.System.VirtualKey.Delete:
                if (_viewModel.SelectedEntry is not null)
                {
                    _ = _viewModel.DeleteSelectedCommand.ExecuteAsync(null);
                    e.Handled = true;
                }
                break;
            case Windows.System.VirtualKey.Escape:
                if (_viewModel.IsDetailOpen)
                {
                    _viewModel.CloseDetail();
                    e.Handled = true;
                }
                break;
        }
    }
}
