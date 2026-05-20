using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Shell container ViewModel. Manages NavigationView sidebar selection,
/// hosts the current page ViewModel inside the authenticated shell,
/// and coordinates the update-available InfoBar.
/// </summary>
public partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly IVaultStateService _vaultState;
    private readonly INavigationStack _navigation;
    private readonly IUpdateService _updateService;
    private readonly ISettingsService _settings;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly PasswordsListViewModel _passwordsListViewModel;
    private readonly CreditCardsListViewModel _creditCardsListViewModel;
    private readonly IdentitiesListViewModel _identitiesListViewModel;
    private readonly SecureNotesListViewModel _secureNotesListViewModel;
    private readonly GeneratorViewModel _generatorViewModel;
    private readonly PasswordVerifierViewModel _verifierViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly HelpViewModel _helpViewModel;

    private UpdateCheckResult? _currentUpdate;

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>Gets or sets the ViewModel of the page currently displayed in the shell content area.</summary>
    [ObservableProperty]
    public partial ObservableObject? CurrentPage { get; set; }

    /// <summary>Gets or sets the zero-based index of the currently selected NavigationView sidebar item.</summary>
    [ObservableProperty]
    public partial int SelectedNavigationIndex { get; set; }

    // ── Update InfoBar ────────────────────────────────────────────────────────

    /// <summary>Controls whether the update-available InfoBar is shown.</summary>
    [ObservableProperty]
    public partial bool IsUpdateInfoBarOpen { get; set; }

    /// <summary>Dynamic title for the InfoBar, e.g. "PassKey 1.0.5 disponibile".</summary>
    [ObservableProperty]
    public partial string UpdateInfoBarTitle { get; set; } = string.Empty;

    /// <summary>True while the installer is being downloaded.</summary>
    [ObservableProperty]
    public partial bool IsDownloading { get; set; }

    /// <summary>Download progress 0–100 for the ProgressBar.</summary>
    [ObservableProperty]
    public partial double DownloadProgress { get; set; }

    /// <summary>Computed inverse of <see cref="IsDownloading"/> — drives Button visibility.</summary>
    public bool IsNotDownloading => !IsDownloading;

    partial void OnIsDownloadingChanged(bool value) =>
        OnPropertyChanged(nameof(IsNotDownloading));

    // ── Constructor ───────────────────────────────────────────────────────────

    public ShellViewModel(
        IVaultStateService vaultState,
        INavigationStack navigation,
        IUpdateService updateService,
        ISettingsService settings,
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
        _vaultState               = vaultState;
        _navigation               = navigation;
        _updateService            = updateService;
        _settings                 = settings;
        _dashboardViewModel       = dashboardViewModel;
        _passwordsListViewModel   = passwordsListViewModel;
        _creditCardsListViewModel = creditCardsListViewModel;
        _identitiesListViewModel  = identitiesListViewModel;
        _secureNotesListViewModel = secureNotesListViewModel;
        _generatorViewModel       = generatorViewModel;
        _verifierViewModel        = verifierViewModel;
        _settingsViewModel        = settingsViewModel;
        _helpViewModel            = helpViewModel;

        // Handle race: background check may have completed before this VM was constructed
        if (_updateService.PendingUpdate is { UpdateAvailable: true } pending)
            ShowUpdateInfoBar(pending);

        // Subscribe for future notifications (e.g. manual check from SettingsView)
        _updateService.UpdateDetected += OnUpdateDetected;
    }

    /// <summary>
    /// Called after ShellView is displayed. Navigates to the Dashboard (index 0).
    /// </summary>
    public void Initialize()
    {
        NavigateTo(0);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _updateService.UpdateDetected -= OnUpdateDetected;
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigates to a page by its sidebar index.
    /// Valid: 0=Dashboard, 1=Passwords, 2=Cards, 3=Identities, 4=Notes, 5=Generator,
    /// 6=Verifier (manual password check + vault audit + HIBP).
    /// </summary>
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

    /// <summary>Navigates to the Settings page (outside indexed sidebar items).</summary>
    public void NavigateToSettings()
    {
        CurrentPage = _settingsViewModel;
    }

    /// <summary>Navigates to the Help page (outside indexed sidebar items).</summary>
    public void NavigateToHelp()
    {
        CurrentPage = _helpViewModel;
    }

    // ── Vault lock ────────────────────────────────────────────────────────────

    /// <summary>Locks the vault. MainViewModel detects the event and navigates to LoginViewModel.</summary>
    [RelayCommand]
    private void LockVault()
    {
        _vaultState.Lock();
    }

    // ── Update InfoBar logic ──────────────────────────────────────────────────

    private void OnUpdateDetected(UpdateCheckResult result)
    {
        if (result.UpdateAvailable)
            ShowUpdateInfoBar(result);
    }

    private void ShowUpdateInfoBar(UpdateCheckResult result)
    {
        _currentUpdate     = result;
        UpdateInfoBarTitle = $"PassKey {result.NewVersion} disponibile";
        IsUpdateInfoBarOpen = true;
        IsDownloading      = false;
        DownloadProgress   = 0;
    }

    /// <summary>
    /// Called when the user closes the InfoBar without installing.
    /// Persists the skipped version so the InfoBar is not shown again for this release.
    /// </summary>
    public void OnUpdateInfoBarClosed()
    {
        if (_currentUpdate is null) return;
        _settings.SkippedUpdateVersion = $"v{_currentUpdate.NewVersion}";
        _settings.Save();
        IsUpdateInfoBarOpen = false;
    }

    /// <summary>Downloads the installer and launches it, then exits the app.</summary>
    [RelayCommand]
    private async Task DownloadAndInstallAsync()
    {
        if (_currentUpdate?.InstallerDownloadUrl is not { Length: > 0 } url) return;

        IsDownloading = true;
        var progress  = new Progress<double>(p => DownloadProgress = p * 100.0);
        var path      = await _updateService.DownloadInstallerAsync(url, progress);
        IsDownloading = false;

        if (path is null)
        {
            // Download failed — reset progress silently; InfoBar stays open
            DownloadProgress = 0;
            return;
        }

        _updateService.LaunchInstallerAndExit(path);
    }

    /// <summary>Opens the GitHub release page in the default browser.</summary>
    [RelayCommand]
    private async Task OpenReleasePageAsync()
    {
        if (_currentUpdate?.ReleasePageUrl is { } url &&
            Uri.TryCreate(url, UriKind.Absolute, out var uri))
            await Windows.System.Launcher.LaunchUriAsync(uri);
    }
}
