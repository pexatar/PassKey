using CommunityToolkit.Mvvm.ComponentModel;
using PassKey.Core.Interfaces;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Root application ViewModel. Initializes the database on startup and routes
/// to <see cref="LoginViewModel"/> if a vault already exists, or to
/// <see cref="SetupViewModel"/> for first-run vault creation.
/// </summary>
/// <remarks>
/// Dependencies injected via constructor: <see cref="IVaultRepository"/>,
/// <see cref="INavigationStack"/>, <see cref="IDatabaseService"/>, <see cref="IVaultStateService"/>.
/// Subscribes to <see cref="IVaultStateService.VaultLocked"/> to redirect to the login screen
/// whenever the vault is locked programmatically or after auto-lock timeout.
/// </remarks>
public partial class MainViewModel : ObservableObject
{
    private readonly IVaultRepository _repository;
    private readonly INavigationStack _navigation;
    private readonly IDatabaseService _database;
    private readonly IVaultStateService _vaultState;

    /// <summary>
    /// Gets or sets the currently active page ViewModel displayed in the main content area.
    /// Updated automatically when <see cref="INavigationStack.CurrentChanged"/> fires.
    /// </summary>
    [ObservableProperty]
    public partial ObservableObject? CurrentPage { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="MainViewModel"/> with all required services.
    /// </summary>
    /// <param name="repository">Vault repository used to check whether a vault already exists.</param>
    /// <param name="navigation">Navigation stack for routing between top-level pages.</param>
    /// <param name="database">Database service for SQLite initialization.</param>
    /// <param name="vaultState">Vault state service; vault-lock events trigger navigation to login.</param>
    public MainViewModel(
        IVaultRepository repository,
        INavigationStack navigation,
        IDatabaseService database,
        IVaultStateService vaultState)
    {
        _repository = repository;
        _navigation = navigation;
        _database = database;
        _vaultState = vaultState;

        _navigation.CurrentChanged += vm => CurrentPage = vm;
        _vaultState.VaultLocked += OnVaultLocked;
    }

    private void OnVaultLocked()
    {
        _navigation.Clear();
        _navigation.NavigateTo<LoginViewModel>();
    }

    /// <summary>
    /// Asynchronously initializes the SQLite database and navigates to the appropriate
    /// starting page: <see cref="LoginViewModel"/> if a vault exists, otherwise
    /// <see cref="SetupViewModel"/> for first-time setup.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var exists = await _repository.VaultExistsAsync();
        if (exists)
        {
            _navigation.NavigateTo<LoginViewModel>();
        }
        else
        {
            _navigation.NavigateTo<SetupViewModel>();
        }
    }
}
