using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Desktop.Controls;
using PassKey.Desktop.Helpers;
using PassKey.Desktop.ViewModels;
using PassKey.Desktop.ViewModels.Items;

namespace PassKey.Desktop.Views;

/// <summary>
/// Secure notes master-detail view. Left panel: category filter and note cards.
/// Right panel: the search and add toolbar over the editor.
/// </summary>
/// <remarks>
/// <para>
/// The cards are rendered by the template from the row objects, so this file no longer walks
/// the visual tree to paint recycled containers, and there is nothing to force a rebuild
/// after a save: the rows announce their own changes.
/// </para>
/// <para>
/// What remains is what a binding cannot express: the category flyout (built once, because
/// <see cref="MenuFlyout"/> takes no items source), the keyboard shortcuts, and the
/// screen-reader announcements. The two subscriptions behind them are registered through
/// <see cref="BoundViewBase.TrackPropertyChanged"/>, which owns their removal — the leak
/// that used to keep every visited copy of this view alive.
/// </para>
/// </remarks>
public sealed partial class SecureNotesListView : BoundViewBase
{
    private readonly ResourceLoader _resourceLoader = new();
    private bool _initialized;

    public SecureNotesListView()
    {
        InitializeComponent();
        _initialized = true;

        // Localized tooltip + accessible name for the category filter button.
        var filterTip = _resourceLoader.GetString("NoteFilterTooltip");
        ToolTipService.SetToolTip(FilterButton, filterTip);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(FilterButton, filterTip);

        BuildCategoryFilter();
    }

    /// <summary>The section's ViewModel, typed for the compiled bindings.</summary>
    public SecureNotesListViewModel? ViewModel { get; private set; }

    /// <inheritdoc/>
    protected override void RefreshBindings()
    {
        if (_initialized) Bindings.Update();
    }

    /// <inheritdoc/>
    protected override void StopBindingTracking()
    {
        if (_initialized) Bindings.StopTracking();
    }

    /// <summary>Binds this view to a section ViewModel. Called by the shell on navigation.</summary>
    public async void SetViewModel(SecureNotesListViewModel vm)
    {
        ReleaseTrackedSubscriptions();

        ViewModel = vm;
        DataContext = vm;
        RefreshBindings();

        TrackPropertyChanged(vm, OnViewModelPropertyChanged);

        // Applied from code so the strings follow the app's language rather than the OS locale.
        EmptyState.Title = _resourceLoader.GetString("EmptyNotesTitle");
        EmptyState.Subtitle = _resourceLoader.GetString("EmptyNotesSubtitle");
        FilteredEmptyState.Title = _resourceLoader.GetString("EmptyFilteredTitle");
        FilteredEmptyState.Subtitle = _resourceLoader.GetString("EmptyFilteredSubtitle");

        // async void: a load failure must not terminate the process. The list is in-memory so
        // this is defensive; the UI falls back to the empty state.
        try
        {
            await vm.LoadEntriesCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SecureNotesListView] Load failed: {ex}");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SecureNotesListViewModel.IsDetailOpen):
                // Closing the editor hands the keyboard back to the list it came from.
                if (ViewModel?.IsDetailOpen == false)
                    NotesList.Focus(FocusState.Programmatic);
                break;

            case nameof(SecureNotesListViewModel.FilterCategory):
                var filterName = ViewModel?.FilterCategory is { } category
                    ? NoteCategoryInfo.GetName(category)
                    : _resourceLoader.GetString("NoteFilterAllCategories");
                Announce(string.Format(_resourceLoader.GetString("NoteFilterAnnounce"), filterName));
                break;

            case nameof(SecureNotesListViewModel.IsFilteredEmpty):
                if (ViewModel?.IsFilteredEmpty == true)
                    Announce(_resourceLoader.GetString("NoteNoResults"));
                break;
        }
    }

    // --- Category filter: Icon Button + MenuFlyout + RadioMenuFlyoutItem ---

    /// <summary>
    /// Builds the category menu once. <see cref="MenuFlyout"/> exposes no items source, so
    /// this is the only way to fill it; it is static content, not per-row rendering.
    /// </summary>
    private void BuildCategoryFilter()
    {
        CategoryFilterFlyout.Items.Clear();

        // "All categories" entry (no filter). A neutral grey dot in the icon column keeps it
        // aligned with the coloured category dots below (avoids a big dot-to-text gap).
        var allItem = new RadioMenuFlyoutItem
        {
            Text = _resourceLoader.GetString("NoteFilterAllCategories"),
            GroupName = "CategoryFilter",
            IsChecked = true,
            Icon = new FontIcon
            {
                Glyph = "●",
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
                FontSize = 14
            }
        };
        allItem.Click += (_, _) => ViewModel?.SetFilter(null);
        CategoryFilterFlyout.Items.Add(allItem);

        CategoryFilterFlyout.Items.Add(new MenuFlyoutSeparator());

        foreach (var option in NoteCategoryOption.All)
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = option.Name,
                GroupName = "CategoryFilter",
                Tag = option.Category,
                Icon = new FontIcon
                {
                    Glyph = "●",
                    FontFamily = new FontFamily("Segoe UI"),
                    Foreground = new SolidColorBrush(
                        ColorHex.Parse(NoteCategoryInfo.GetColor(option.Category))),
                    FontSize = 14
                }
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, option.Name);

            var selected = option.Category;
            item.Click += (_, _) => ViewModel?.SetFilter(selected);
            CategoryFilterFlyout.Items.Add(item);
        }
    }

    // --- Event handlers ---

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (ViewModel is not null && args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ViewModel.SearchQuery = sender.Text;
    }

    /// <summary>
    /// Opens the "new note" editor. Public so the Ctrl+N accelerator handled by
    /// <see cref="ShellView"/> can route the shortcut to whichever list page is shown.
    /// </summary>
    public void InvokeAddNew() => ViewModel?.AddNewCommand.Execute(null);

    private void NotesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SecureNoteItemViewModel item)
            ViewModel?.SelectItemCommand.Execute(item);
    }

    private void OnViewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel is null) return;
        if (FocusManager.GetFocusedElement(XamlRoot) is TextBox or AutoSuggestBox) return;

        switch (e.Key)
        {
            case Windows.System.VirtualKey.F2:
                if (ViewModel.SelectedItem is { } toEdit)
                {
                    ViewModel.SelectItemCommand.Execute(toEdit);
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.Delete:
                if (ViewModel.DeleteSelectedCommand.CanExecute(null))
                {
                    _ = ViewModel.DeleteSelectedCommand.ExecuteAsync(null);
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.Escape:
                if (ViewModel.IsDetailOpen)
                {
                    ViewModel.CloseDetail();
                    e.Handled = true;
                }
                break;
        }
    }

    private void Announce(string message)
    {
        A11yAnnouncer.Text = "";
        A11yAnnouncer.Text = message;
    }
}
