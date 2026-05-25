using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Activity-log ("Cronologia") viewer: lists recent vault activity and offers CSV export.
/// </summary>
public sealed partial class ActivityLogView : UserControl
{
    private ActivityLogViewModel? _viewModel;
    private readonly ResourceLoader _resourceLoader = new();

    /// <summary>Raised when the user clicks the back button to return to the Settings page.</summary>
    public event Action? BackRequested;

    public ActivityLogView()
    {
        InitializeComponent();
        EmptyState.Title = _resourceLoader.GetString("ActivityEmptyTitle");
        EmptyState.Subtitle = _resourceLoader.GetString("ActivityEmptySubtitle");
    }

    public async void SetViewModel(ActivityLogViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;
        EntriesList.ItemsSource = vm.Entries;

        await vm.LoadAsync();
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        var empty = _viewModel?.IsEmpty ?? true;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        EntriesList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        ExportButton.IsEnabled = !empty;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
        => BackRequested?.Invoke();

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        ExportButton.IsEnabled = false;
        try
        {
            await _viewModel.ExportCsvCommand.ExecuteAsync(null);
        }
        finally
        {
            ExportButton.IsEnabled = _viewModel.Entries.Count > 0;
        }
    }
}
