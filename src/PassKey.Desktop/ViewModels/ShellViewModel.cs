using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Shell container ViewModel. Manages NavigationView sidebar selection
/// and hosts the current page ViewModel inside the authenticated shell.
/// </summary>
/// <remarks>
/// Dependencies injected via constructor: <see cref="IVaultStateService"/>, <see cref="INavigationStack"/>,
/// <see cref="DashboardViewModel"/>, <see cref="PasswordsListViewModel"/>,
/// <see cref="CreditCardsListViewModel"/>, <see cref="IdentitiesListViewModel"/>,
/// <see cref="SecureNotesListViewModel"/>, <see cref="GeneratorViewModel"/>,
/// <see cref="PasswordVerifierViewModel"/>, <see cref="SettingsViewModel"/>, <see cref="HelpViewModel"/>.
/// All child ViewModels are pre-created and cached so navigation is instantaneous.
/// </remarks>
public partial class ShellViewModel : ObservableObject
{
    private readonly IVaultStateService _vaultState;
    private readonly INavigationStack _navigation;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly PasswordsListViewModel _passwordsListViewModel;
    private readonly CreditCardsListViewModel _creditCardsListViewModel;
    private readonly IdentitiesListViewModel _identitiesListViewModel;
    private readonly SecureNotesListViewModel _secureNotesListViewModel;
    private readonly GeneratorViewModel _generatorViewModel;
    private readonly PasswordVerifierViewModel _verifierViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly HelpViewModel _helpViewModel;

    /// <summary>
    /// Gets or sets the ViewModel of the page currently displayed in the shell content area.
    /// </summary>
    [ObservableProperty]
    public partial ObservableObject? CurrentPage { get; set; }

    /// <summary>
    /// Gets or sets the zero-based index of the currently selected NavigationView sidebar item.
    /// </summary>
    [ObservableProperty]
    public partial int SelectedNavigationIndex { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="ShellViewModel"/> with all page ViewModels.
    /// </summary>
    /// <param name="vaultState">Vault state service used by the lock command.</param>
    /// <param name="navigation">Navigation stack (not actively used in shell; kept for DI consistency).</param>
    /// <param name="dashboardViewModel">Pre-created Dashboard ViewModel.</param>
    /// <param name="passwordsListViewModel">Pre-created Passwords list ViewModel.</param>
    /// <param name="creditCardsListViewModel">Pre-created Credit Cards list ViewModel.</param>
    /// <param name="identitiesListViewModel">Pre-created Identities list ViewModel.</param>
    /// <param name="secureNotesListViewModel">Pre-created Secure Notes list ViewModel.</param>
    /// <param name="generatorViewModel">Pre-created Generator ViewModel.</param>
    /// <param name="verifierViewModel">Pre-created Password Verifier ViewModel.</param>
    /// <param name="settingsViewModel">Pre-created Settings ViewModel.</param>
    /// <param name="helpViewModel">Pre-created Help ViewModel.</param>
    public ShellViewModel(
        IVaultStateService vaultState,
        INavigationStack navigation,
        DashboardViewModel dashboardViewModel,
        PasswordsListViewModel passwordsListViewModel,
        CreditCardsListViewModel creditCardsListViewModel,
        IdentitiesListViewModel identitiesListViewModel,
        SecureNotesListViewModel secureNotesListViewModel,
        GeneratorViewModel generatorViewModel,
        PasswordVerifierViewModel verifierViewModel,
        SettingsViewModel settingsViewModel,
        HelpViewModel helpViewModel)
    {
        _vaultState = vaultState;
        _navigation = navigation;
        _dashboardViewModel = dashboardViewModel;
        _passwordsListViewModel = passwordsListViewModel;
        _creditCardsListViewModel = creditCardsListViewModel;
        _identitiesListViewModel = identitiesListViewModel;
        _secureNotesListViewModel = secureNotesListViewModel;
        _generatorViewModel = generatorViewModel;
        _verifierViewModel = verifierViewModel;
        _settingsViewModel = settingsViewModel;
        _helpViewModel = helpViewModel;
    }

    /// <summary>
    /// Called after the ShellView is displayed. Navigates to the Dashboard (index 0).
    /// </summary>
    public void Initialize()
    {
        NavigateTo(0);
    }

    /// <summary>
    /// Navigates to a page by its sidebar index.
    /// Valid indices: 0=Dashboard, 1=Passwords, 2=Cards, 3=Identities, 4=Notes, 5=Generator, 6=Verifier.
    /// </summary>
    /// <param name="index">Zero-based sidebar navigation index.</param>
    public void NavigateTo(int index)
    {
        SelectedNavigationIndex = index;

        CurrentPage = index switch
        {
            0 => _dashboardViewModel,
            1 => _passwordsListViewModel,
            2 => _creditCardsListViewModel,
            3 => _identitiesListViewModel,
            4 => _secureNotesListViewModel,
            5 => _generatorViewModel,
            6 => _verifierViewModel,
            _ => CurrentPage
        };
    }

    /// <summary>
    /// Navigates to the Settings page (outside the indexed sidebar items).
    /// </summary>
    public void NavigateToSettings()
    {
        CurrentPage = _settingsViewModel;
    }

    /// <summary>
    /// Navigates to the Help page (outside the indexed sidebar items).
    /// </summary>
    public void NavigateToHelp()
    {
        CurrentPage = _helpViewModel;
    }

    /// <summary>
    /// Locks the vault by calling <see cref="IVaultStateService.Lock"/>.
    /// <see cref="MainViewModel"/> detects the lock event and navigates back to <see cref="LoginViewModel"/>.
    /// </summary>
    [RelayCommand]
    private void LockVault()
    {
        _vaultState.Lock();
        // MainViewModel detects lock event and navigates back to LoginView
    }
}
