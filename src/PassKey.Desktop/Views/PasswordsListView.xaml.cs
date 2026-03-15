using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using PassKey.Core.Models;
using PassKey.Desktop.ViewModels;
using Windows.Storage.Streams;

namespace PassKey.Desktop.Views;

/// <summary>
/// Passwords list view with header, search, column-header sort, hover actions,
/// animated detail panel, and save toast.
/// </summary>
public sealed partial class PasswordsListView : UserControl
{
    private PasswordsListViewModel? _viewModel;

    public PasswordsListView()
    {
        InitializeComponent();
    }

    public async void SetViewModel(PasswordsListViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.SaveCompleted += ShowSavedToast;

        await vm.LoadEntriesCommand.ExecuteAsync(null);
        UpdateList();
        UpdateEmptyState();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PasswordsListViewModel.IsDetailOpen):
                UpdateDetailPanel();
                break;
            case nameof(PasswordsListViewModel.IsEmpty):
                UpdateEmptyState();
                break;
            case nameof(PasswordsListViewModel.DetailViewModel):
                UpdateDetailContent();
                break;
        }
    }

    private void UpdateList()
    {
        PasswordList.ItemsSource = _viewModel?.Entries;
    }

    private void UpdateEmptyState()
    {
        if (_viewModel is null) return;
        EmptyState.Visibility = _viewModel.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        PasswordList.Visibility = _viewModel.IsEmpty ? Visibility.Collapsed : Visibility.Visible;
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
            var detailView = new PasswordDetailView();
            detailView.SetViewModel(_viewModel.DetailViewModel);
            DetailContent.Content = detailView;
        }
        else
        {
            DetailContent.Content = null;
        }
    }

    // --- Toast (Step F) ---

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

    // --- Search ---

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_viewModel is not null && args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            _viewModel.SearchQuery = sender.Text;
            UpdateEmptyState();
        }
    }

    // --- Add ---

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.AddNewCommand.Execute(null);
    }

    // --- Item click ---

    private void PasswordList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PasswordEntry entry)
            _viewModel?.EditEntryCommand.Execute(entry);
    }

    // --- Quick actions ---

    private void CopyUsername_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PasswordEntry entry)
            _viewModel?.CopyUsernameCommand.Execute(entry);
    }

    private void CopyPassword_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PasswordEntry entry)
            _viewModel?.CopyPasswordCommand.Execute(entry);
    }

    private void EditEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PasswordEntry entry)
            _viewModel?.EditEntryCommand.Execute(entry);
    }

    // --- Column header sort (Step A) ---

    private void ColumnHeader_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is TextBlock tb && tb.Tag is string field)
            _viewModel?.Sort(field);
    }

    // --- Avatar (ContainerContentChanging) ---

    private async void PwList_ContainerContentChanging(ListViewBase sender,
        ContainerContentChangingEventArgs args)
    {
        if (args.Item is not PasswordEntry entry) return;

        var rootGrid = args.ItemContainer.ContentTemplateRoot as Grid;
        if (rootGrid is null) return;

        // Col 0 = avatar Grid; inside it: [0] TextBlock, [1] FontIcon, [2] Image
        if (rootGrid.Children[0] is not Grid avatarGrid) return;
        if (avatarGrid.Children.Count < 3) return;

        var letterTb  = avatarGrid.Children[0] as TextBlock;
        var glyphIcon = avatarGrid.Children[1] as FontIcon;
        var imgElem   = avatarGrid.Children[2] as Image;
        if (letterTb is null || glyphIcon is null || imgElem is null) return;

        var favicon = entry.FaviconBase64;

        if (string.IsNullOrEmpty(favicon))
        {
            letterTb.Text = string.IsNullOrEmpty(entry.Title) ? "?" : entry.Title[0].ToString().ToUpper();
            letterTb.Visibility = Visibility.Visible;
            glyphIcon.Visibility = Visibility.Collapsed;
            imgElem.Visibility = Visibility.Collapsed;
        }
        else if (favicon.StartsWith("glyph:", StringComparison.Ordinal))
        {
            glyphIcon.Glyph = favicon["glyph:".Length..];
            letterTb.Visibility = Visibility.Collapsed;
            glyphIcon.Visibility = Visibility.Visible;
            imgElem.Visibility = Visibility.Collapsed;
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
                imgElem.Source = bmp;
                letterTb.Visibility = Visibility.Collapsed;
                glyphIcon.Visibility = Visibility.Collapsed;
                imgElem.Visibility = Visibility.Visible;
            }
            catch
            {
                letterTb.Text = string.IsNullOrEmpty(entry.Title) ? "?" : entry.Title[0].ToString().ToUpper();
                letterTb.Visibility = Visibility.Visible;
                glyphIcon.Visibility = Visibility.Collapsed;
                imgElem.Visibility = Visibility.Collapsed;
            }
        }
    }

    // --- Row hover actions (Step B) ---

    private void Row_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid grid) return;

        // Column 4 contains a Grid wrapper with DateText + HoverActions
        if (grid.Children.Count > 4 && grid.Children[4] is Grid wrapper)
        {
            foreach (var child in wrapper.Children)
            {
                if (child is TextBlock) child.Opacity = 0;
                else if (child is StackPanel) child.Opacity = 1;
            }
        }
    }

    private void Row_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid grid) return;

        if (grid.Children.Count > 4 && grid.Children[4] is Grid wrapper)
        {
            foreach (var child in wrapper.Children)
            {
                if (child is TextBlock) child.Opacity = 1;
                else if (child is StackPanel) child.Opacity = 0;
            }
        }
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
