using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PassKey.Core.Models;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Identities list view with header, search, sort bar, card grid, and 400px detail panel.
/// </summary>
public sealed partial class IdentitiesListView : UserControl
{
    private IdentitiesListViewModel? _viewModel;

    public IdentitiesListView()
    {
        InitializeComponent();
    }

    public async void SetViewModel(IdentitiesListViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.SaveCompleted += ShowSavedToast;

        // Wire EmptyState primary action to AddNew
        EmptyState.PrimaryActionCommand = new RelayCommand(() => _viewModel?.AddNewCommand.Execute(null));

        await vm.LoadEntriesCommand.ExecuteAsync(null);
        UpdateList();
        UpdateEmptyState();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IdentitiesListViewModel.IsDetailOpen):
                UpdateDetailPanel();
                break;
            case nameof(IdentitiesListViewModel.IsEmpty):
                UpdateEmptyState();
                break;
            case nameof(IdentitiesListViewModel.DetailViewModel):
                UpdateDetailContent();
                break;
        }
    }

    private void UpdateList()
    {
        IdentityList.ItemsSource = _viewModel?.Entries;
    }

    private void UpdateEmptyState()
    {
        if (_viewModel is null) return;
        EmptyState.Visibility = _viewModel.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        IdentityList.Visibility = _viewModel.IsEmpty ? Visibility.Collapsed : Visibility.Visible;
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
            var detailView = new IdentityDetailView();
            detailView.SetViewModel(_viewModel.DetailViewModel);
            DetailContent.Content = detailView;
        }
        else
        {
            DetailContent.Content = null;
        }
    }

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

    private void IdentityList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is IdentityEntry entry)
            _viewModel?.EditEntryCommand.Execute(entry);
    }

    // Card content — populate fields that require logic (avatar initial, full name, phone format)
    private void IdentityList_ContainerContentChanging(ListViewBase sender,
        ContainerContentChangingEventArgs args)
    {
        if (args.Item is not IdentityEntry entry) return;

        // Template visual tree:
        // Border (card)
        //   └── Grid (rootGrid, 4 rows)
        //         Row 0: StackPanel [Grid (avatar circle + TextBlock) | TextBlock (label)]
        //         Row 1: TextBlock (full name)
        //         Row 2: TextBlock (email) — bound via {Binding Email}
        //         Row 3: Grid [TextBlock (phone) | TextBlock (city)]

        var border = args.ItemContainer.ContentTemplateRoot as Border;
        if (border?.Child is not Grid rootGrid) return;

        // Row 0 — Avatar initial
        if (rootGrid.Children[0] is StackPanel headerRow &&
            headerRow.Children[0] is Grid avatarGrid &&
            avatarGrid.Children.OfType<TextBlock>().FirstOrDefault() is TextBlock avatarText)
        {
            avatarText.Text = !string.IsNullOrEmpty(entry.FirstName)
                ? entry.FirstName[..1].ToUpper()
                : !string.IsNullOrEmpty(entry.LastName)
                    ? entry.LastName[..1].ToUpper()
                    : "?";
        }

        // Row 1 — Full name (FirstName + LastName)
        if (rootGrid.Children[1] is TextBlock nameTb)
        {
            var fn = entry.FirstName?.Trim();
            var ln = entry.LastName?.Trim();
            nameTb.Text = string.IsNullOrEmpty(fn) ? (ln ?? "")
                        : string.IsNullOrEmpty(ln) ? fn
                        : $"{fn} {ln}";
        }

        // Row 3 — Phone (formatted)
        if (rootGrid.Children[3] is Grid bottomRow &&
            bottomRow.Children[0] is TextBlock phoneTb)
        {
            phoneTb.Text = FormatPhone(entry.Phone ?? "");
        }
    }

    // Card hover effects — accent border on hover
    private void Card_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border card &&
            Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out var brush))
            card.BorderBrush = (Brush)brush;
    }

    private void Card_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border card &&
            Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out var brush))
            card.BorderBrush = (Brush)brush;
    }

    // Save confirmation toast
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

    // ── Formatters ────────────────────────────────────────────────────────────

    /// <summary>Formats a phone number with spaces (e.g. "+393518584980" → "+39 351 858 4980").</summary>
    private static string FormatPhone(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        bool hasPlus = input.TrimStart().StartsWith('+');
        var digits = new string(input.Where(char.IsDigit).ToArray());

        if (!hasPlus || digits.Length == 0) return input;

        int ccLen = (digits[0] == '1' || digits[0] == '7') ? 1 : 2;
        if (digits.Length <= ccLen) return "+" + digits;

        var cc = digits[..ccLen];
        var local = digits[ccLen..];

        // Italian (+39) with 10 local digits: 3+3+4 grouping
        if (cc == "39" && local.Length == 10)
            return $"+39 {local[..3]} {local[3..6]} {local[6..10]}";

        // Generic: groups of 3
        var parts = new List<string>();
        for (int i = 0; i < local.Length; i += 3)
            parts.Add(local[i..Math.Min(i + 3, local.Length)]);
        return "+" + cc + " " + string.Join(" ", parts);
    }

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
