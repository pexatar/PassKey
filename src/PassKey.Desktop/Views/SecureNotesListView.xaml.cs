using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PassKey.Core.Constants;
using PassKey.Core.Models;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Secure notes master-detail view.
/// Left panel (320px): filter icon + MenuFlyout, search, mini-cards with colored border + preview + date.
/// Right panel: editor (SecureNoteDetailView).
/// </summary>
public sealed partial class SecureNotesListView : UserControl
{
    private SecureNotesListViewModel? _viewModel;

    public SecureNotesListView()
    {
        InitializeComponent();
    }

    public async void SetViewModel(SecureNotesListViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.SaveCompleted += ShowSavedToast;

        BuildCategoryFilter();

        // Wire EmptyState primary action button
        EmptyState.PrimaryActionCommand = new RelayCommand(() => _viewModel?.AddNewCommand.Execute(null));

        await vm.LoadEntriesCommand.ExecuteAsync(null);
        UpdateList();
        UpdateEmptyState();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SecureNotesListViewModel.IsEditorOpen):
                UpdateEditorPanel();
                break;
            case nameof(SecureNotesListViewModel.IsEmpty):
            case nameof(SecureNotesListViewModel.IsFilteredEmpty):
                UpdateEmptyState();
                break;
            case nameof(SecureNotesListViewModel.DetailViewModel):
                UpdateEditorContent();
                break;
            case nameof(SecureNotesListViewModel.SelectedEntry):
                NotesList.SelectedItem = _viewModel?.SelectedEntry;
                break;
            case nameof(SecureNotesListViewModel.FilterCategory):
                UpdateFilterBadge();
                var filterName = _viewModel?.FilterCategory.HasValue == true
                    ? SecureNotesListViewModel.GetCategoryName(_viewModel.FilterCategory!.Value)
                    : "Tutte le categorie";
                Announce($"Filtro: {filterName}");
                break;
        }
    }

    private void UpdateList()
    {
        NotesList.ItemsSource = _viewModel?.Entries;
    }

    private void UpdateEmptyState()
    {
        if (_viewModel is null) return;

        if (_viewModel.IsEmpty)
        {
            EmptyState.Visibility = Visibility.Visible;
            FilteredEmptyState.Visibility = Visibility.Collapsed;
            NotesList.Visibility = Visibility.Collapsed;
        }
        else if (_viewModel.IsFilteredEmpty)
        {
            EmptyState.Visibility = Visibility.Collapsed;
            FilteredEmptyState.Visibility = Visibility.Visible;
            NotesList.Visibility = Visibility.Collapsed;
            Announce("Nessun risultato trovato.");
        }
        else
        {
            EmptyState.Visibility = Visibility.Collapsed;
            FilteredEmptyState.Visibility = Visibility.Collapsed;
            NotesList.Visibility = Visibility.Visible;
        }
    }

    private void UpdateEditorPanel()
    {
        if (_viewModel is null) return;

        if (_viewModel.IsEditorOpen)
        {
            EditorPanel.Visibility = Visibility.Visible;
            EmptyEditor.Visibility = Visibility.Collapsed;
        }
        else
        {
            EditorPanel.Visibility = Visibility.Collapsed;
            EmptyEditor.Visibility = Visibility.Visible;
            NotesList.Focus(FocusState.Programmatic);
        }
    }

    private void UpdateEditorContent()
    {
        if (_viewModel?.DetailViewModel is not null)
        {
            var detailView = new SecureNoteDetailView();
            detailView.SetViewModel(_viewModel.DetailViewModel);
            EditorContent.Content = detailView;
        }
        else
        {
            EditorContent.Content = null;
        }
    }

    // --- Category filter: Icon Button + MenuFlyout + RadioMenuFlyoutItem ---

    private void BuildCategoryFilter()
    {
        CategoryFilterFlyout.Items.Clear();

        // "Tutte le categorie" (nessun filtro)
        var allItem = new RadioMenuFlyoutItem
        {
            Text = "Tutte le categorie",
            GroupName = "CategoryFilter",
            IsChecked = true
        };
        allItem.Click += (_, _) =>
        {
            _viewModel?.SetFilter(null);
        };
        CategoryFilterFlyout.Items.Add(allItem);

        CategoryFilterFlyout.Items.Add(new MenuFlyoutSeparator());

        // Una voce per ogni categoria con dot colorato
        foreach (NoteCategory cat in Enum.GetValues<NoteCategory>())
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = SecureNotesListViewModel.GetCategoryName(cat),
                GroupName = "CategoryFilter",
                Tag = cat,
                Icon = new FontIcon
                {
                    Glyph = "\u25CF",
                    FontFamily = new FontFamily("Segoe UI"),
                    Foreground = new SolidColorBrush(
                        ParseColor(SecureNotesListViewModel.GetCategoryColor(cat))),
                    FontSize = 14
                }
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                item, SecureNotesListViewModel.GetCategoryName(cat));

            var capturedCat = cat;
            item.Click += (_, _) =>
            {
                _viewModel?.SetFilter(capturedCat);
            };
            CategoryFilterFlyout.Items.Add(item);
        }
    }

    /// <summary>
    /// Show/hide the accent badge dot on the filter button when a category filter is active.
    /// </summary>
    private void UpdateFilterBadge()
    {
        FilterBadge.Visibility = _viewModel?.FilterCategory.HasValue == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // --- Event handlers ---

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

    private void NotesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SecureNoteEntry entry)
            _viewModel?.SelectNoteCommand.Execute(entry);
    }

    /// <summary>
    /// Populate dynamic fields in each note card via visual tree traversal.
    /// DataTemplate structure: StackPanel[TextBlock(section header), Grid[Border(4px), Grid[Grid(pin+title+date), TextBlock(preview), TextBlock(category)]]].
    /// </summary>
    private void NotesList_ContainerContentChanging(ListViewBase sender,
        ContainerContentChangingEventArgs args)
    {
        if (args.Item is not SecureNoteEntry entry) return;

        var rootStack = args.ItemContainer.ContentTemplateRoot as StackPanel;
        if (rootStack is null) return;

        // Reset container margin (recycled containers may have stale values)
        args.ItemContainer.Margin = new Thickness(0);

        // --- Section header ("Fissate" / "Note") ---
        if (rootStack.Children[0] is TextBlock sectionHeader)
        {
            var entries = _viewModel?.Entries;
            if (entries != null)
            {
                int idx = entries.IndexOf(entry);

                if (idx < 0)
                {
                    // Entry non più nella collection (lista in fase di aggiornamento)
                    sectionHeader.Visibility = Visibility.Collapsed;
                }
                else if (entry.IsPinned && (idx == 0 || !entries[idx - 1].IsPinned))
                {
                    // Primo pinnato → "Fissate"
                    sectionHeader.Text = "Fissate";
                    sectionHeader.Visibility = Visibility.Visible;
                }
                else if (!entry.IsPinned && idx > 0 && entries[idx - 1].IsPinned)
                {
                    // Primo non-pinnato dopo pinnati → "Note"
                    sectionHeader.Text = "Note";
                    sectionHeader.Visibility = Visibility.Visible;
                }
                else
                {
                    sectionHeader.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                sectionHeader.Visibility = Visibility.Collapsed;
            }
        }

        // --- Card content ---
        var rootGrid = rootStack.Children[1] as Grid;
        if (rootGrid is null) return;

        // Children[0]: Border bordo colorato categoria
        if (rootGrid.Children[0] is Border categoryBorder)
        {
            categoryBorder.Background = new SolidColorBrush(
                ParseColor(SecureNotesListViewModel.GetCategoryColor(entry.Category)));
        }

        // Children[1]: Grid contenuto card (3 righe)
        if (rootGrid.Children[1] is not Grid contentGrid) return;

        // Riga 0: Grid con pin + titolo + data
        if (contentGrid.Children[0] is Grid headerRow)
        {
            if (headerRow.Children[0] is FontIcon pinIcon)
                pinIcon.Visibility = entry.IsPinned ? Visibility.Visible : Visibility.Collapsed;

            if (headerRow.Children[2] is TextBlock dateTb)
                dateTb.Text = SecureNotesListViewModel.GetRelativeDate(entry.ModifiedAt);
        }

        // Riga 1: Preview contenuto
        if (contentGrid.Children[1] is TextBlock previewTb)
        {
            var preview = entry.Content.ReplaceLineEndings(" ");
            previewTb.Text = preview.Length > 80 ? preview[..80] + "..." : preview;
        }

        // Riga 2: Categoria
        if (contentGrid.Children[2] is TextBlock categoryTb)
            categoryTb.Text = SecureNotesListViewModel.GetCategoryName(entry.Category);

        // Accessibility: ItemStatus per note pinnate
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetItemStatus(
            args.ItemContainer, entry.IsPinned ? "Fissata" : "");
    }

    // --- Toast conferma salvataggio ---

    public void ShowSavedToast()
    {
        SavedTip.IsOpen = true;
        Announce("Nota salvata.");
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (s, _) =>
        {
            SavedTip.IsOpen = false;
            ((DispatcherTimer)s!).Stop();
        };
        timer.Start();
    }

    // --- Helpers ---

    private void Announce(string message)
    {
        A11yAnnouncer.Text = "";
        A11yAnnouncer.Text = message;
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        return ColorHelper.FromArgb(255,
            byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber));
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
                    _viewModel.SelectNoteCommand.Execute(_viewModel.SelectedEntry);
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
                if (_viewModel.IsEditorOpen)
                {
                    _viewModel.CloseEditor();
                    e.Handled = true;
                }
                break;
        }
    }
}
