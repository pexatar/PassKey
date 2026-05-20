using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Shell container with NavigationView sidebar.
/// Hosts page content via ContentPresenter.
/// </summary>
public sealed partial class ShellView : UserControl
{
    private ShellViewModel? _viewModel;

    /// <summary>
    /// Exposes the current ViewModel for {x:Bind} expressions in ShellView.xaml.
    /// Must be a public property (x:Bind does not work with private fields).
    /// </summary>
    public ShellViewModel? ViewModel => _viewModel;

    public ShellView()
    {
        InitializeComponent();
    }

    public void SetViewModel(ShellViewModel vm)
    {
        // Unsubscribe and dispose the previous VM if any
        if (_viewModel is IDisposable old) old.Dispose();
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = vm;
        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;

        if (NavView.MenuItems.Count > 0)
            NavView.SelectedItem = NavView.MenuItems[0];

        vm.Initialize();

        // Notify x:Bind that ViewModel has changed
        Bindings.Update();
    }

    private void UpdateInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
        => _viewModel?.OnUpdateInfoBarClosed();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.CurrentPage))
            UpdatePageContent();
    }

    private void UpdatePageContent()
    {
        UIElement? view = _viewModel?.CurrentPage switch
        {
            DashboardViewModel vm => SetVm(new DashboardView(), vm),
            PasswordsListViewModel vm => SetVm(new PasswordsListView(), vm),
            CreditCardsListViewModel vm => SetVm(new CreditCardsListView(), vm),
            IdentitiesListViewModel vm => SetVm(new IdentitiesListView(), vm),
            SecureNotesListViewModel vm => SetVm(new SecureNotesListView(), vm),
            GeneratorViewModel vm => SetVm(new GeneratorView(), vm),
            PasswordVerifierViewModel vm => SetVm(new PasswordVerifierView(), vm),
            SettingsViewModel vm => SetVm(new SettingsView(), vm),
            HelpViewModel vm => SetVm(new HelpView(), vm),
            _ => null
        };

        ShellContent.Content = view;
    }

    private DashboardView SetVm(DashboardView v, DashboardViewModel vm)
    {
        // Unsubscribe first to avoid duplicate handlers when Dashboard is revisited
        vm.NavigateToItemRequested -= OnDashboardNavigateToItem;
        vm.NavigateToItemRequested += OnDashboardNavigateToItem;
        vm.NavigateToVerifierRequested -= OnDashboardNavigateToVerifier;
        vm.NavigateToVerifierRequested += OnDashboardNavigateToVerifier;
        v.SetViewModel(vm);
        return v;
    }

    private void OnDashboardNavigateToVerifier()
    {
        // Index 6 is the "Verifica" entry per ShellViewModel.NavigateTo. NavigateTo triggers
        // UpdatePageContent which constructs a fresh PasswordVerifierView; once that view is
        // hosted we explicitly select the second Pivot item ("Vault") because clicking the
        // health card on the Dashboard is a "show me the audit" gesture, not a "verify a
        // single password" one.
        _viewModel?.NavigateTo(6);
        NavView.SelectedItem = NavItemVerifier;

        if (ShellContent.Content is PasswordVerifierView verifier)
            verifier.SelectVaultTab();
    }
    private static PasswordsListView SetVm(PasswordsListView v, PasswordsListViewModel vm) { v.SetViewModel(vm); return v; }
    private static CreditCardsListView SetVm(CreditCardsListView v, CreditCardsListViewModel vm) { v.SetViewModel(vm); return v; }
    private static IdentitiesListView SetVm(IdentitiesListView v, IdentitiesListViewModel vm) { v.SetViewModel(vm); return v; }
    private static SecureNotesListView SetVm(SecureNotesListView v, SecureNotesListViewModel vm) { v.SetViewModel(vm); return v; }
    private static GeneratorView SetVm(GeneratorView v, GeneratorViewModel vm) { v.SetViewModel(vm); return v; }
    private PasswordVerifierView SetVm(PasswordVerifierView v, PasswordVerifierViewModel vm)
    {
        // Sync the compromise badge on the Verifica nav entry whenever the scan reports
        // breached passwords. Single source of truth lives in the verifier VM since the
        // standalone Watchtower entry has been folded into this page in PassKey 2.0.
        vm.PropertyChanged -= OnVerifierVmPropertyChanged;
        vm.PropertyChanged += OnVerifierVmPropertyChanged;
        UpdateVerifierBadge(vm.CompromisedCount);
        v.SetViewModel(vm);
        return v;
    }
    private static HelpView SetVm(HelpView v, HelpViewModel vm) { v.SetViewModel(vm); return v; }

    private void OnVerifierVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is PasswordVerifierViewModel vm && e.PropertyName == nameof(PasswordVerifierViewModel.CompromisedCount))
            UpdateVerifierBadge(vm.CompromisedCount);
    }

    private void UpdateVerifierBadge(int compromised)
    {
        if (FindName("VerifierBadge") is Microsoft.UI.Xaml.Controls.InfoBadge badge)
            badge.Visibility = compromised > 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    private SettingsView SetVm(SettingsView v, SettingsViewModel vm)
    {
        v.NavigateToHelpRequested += OnSettingsNavigateToHelp;
        v.SetViewModel(vm);
        return v;
    }

    private void OnSettingsNavigateToHelp()
    {
        _viewModel?.NavigateToHelp();
        NavView.SelectedItem = NavItemHelp;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            _viewModel?.NavigateToSettings();
            return;
        }
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();

            if (tag == "lock")
            {
                _viewModel?.LockVaultCommand.Execute(null);
                return;
            }

            if (tag == "help")
            {
                _viewModel?.NavigateToHelp();
                return;
            }

            if (int.TryParse(tag, out var index))
            {
                _viewModel?.NavigateTo(index);
            }
        }
    }

    private void LockVault_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _viewModel?.LockVaultCommand.Execute(null);
        args.Handled = true;
    }

    private void FocusSearch_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ShellContent.Content is DashboardView dashView)
        {
            dashView.FocusSearchBox();
        }
        args.Handled = true;
    }

    private void OnDashboardNavigateToItem(string entityType, Guid entityId)
    {
        var index = entityType switch
        {
            "PasswordEntry" => 1,
            "CreditCardEntry" => 2,
            "IdentityEntry" => 3,
            "SecureNoteEntry" => 4,
            "Import" => -1, // Special: go to Settings
            _ => 0
        };

        if (index == -1)
        {
            _viewModel?.NavigateToSettings();
            NavView.SelectedItem = NavView.SettingsItem;
            return;
        }

        if (index >= 0 && index < NavView.MenuItems.Count)
        {
            NavView.SelectedItem = NavView.MenuItems[index];
            _viewModel?.NavigateTo(index);
        }
    }

    private void NewItem_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        // Phases 5-10: Create new item based on current page
        args.Handled = true;
    }

    private void NavShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var keyNumber = (int)(sender.Key - Windows.System.VirtualKey.Number1); // 0-based

        // Ctrl+8 → Settings
        if (keyNumber == 7)
        {
            NavView.SelectedItem = NavView.SettingsItem;
            // SelectionChanged fires → _viewModel?.NavigateToSettings()
            args.Handled = true;
            return;
        }

        // Ctrl+1–7: skip NavigationViewItemSeparator, navigate via Tag
        var navItems = NavView.MenuItems.OfType<NavigationViewItem>().ToList();
        if (keyNumber >= 0 && keyNumber < navItems.Count)
        {
            var item = navItems[keyNumber];
            NavView.SelectedItem = item;
            if (int.TryParse(item.Tag?.ToString(), out var navIndex))
                _viewModel?.NavigateTo(navIndex);
        }
        args.Handled = true;
    }

    private void Help_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _viewModel?.NavigateToHelp();
        NavView.SelectedItem = NavItemHelp;
        args.Handled = true;
    }
}
