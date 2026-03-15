using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Welcome view shown immediately after first-run vault creation.
/// Offers four quick-start actions to the new user before entering the main shell.
/// </summary>
/// <remarks>
/// Dependency injected via constructor: <see cref="INavigationStack"/>.
/// All four commands currently navigate to <see cref="ShellViewModel"/>; deeper routing
/// (e.g., directly to the password-add form) is planned for a later phase.
/// </remarks>
public partial class WelcomeViewModel : ObservableObject
{
    private readonly INavigationStack _navigation;

    /// <summary>
    /// Initializes a new instance of <see cref="WelcomeViewModel"/>.
    /// </summary>
    /// <param name="navigation">Navigation stack used to replace the current page with the Shell.</param>
    public WelcomeViewModel(INavigationStack navigation)
    {
        _navigation = navigation;
    }

    /// <summary>
    /// Navigates to the Shell. Intended to open the password-add form directly (planned for a future phase).
    /// </summary>
    [RelayCommand]
    private Task AddPasswordAsync()
    {
        // Navigate to Shell (Dashboard for now; Phase 5 will add direct password page nav)
        _navigation.Replace<ShellViewModel>();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Navigates to the Shell. Intended to open the Settings import page directly (planned for a future phase).
    /// </summary>
    [RelayCommand]
    private Task ImportDataAsync()
    {
        // Phase 11: Navigate to Shell → Settings → Import
        _navigation.Replace<ShellViewModel>();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Navigates to the Shell. Intended to open the Settings page directly (planned for a future phase).
    /// </summary>
    [RelayCommand]
    private Task OpenSettingsAsync()
    {
        // Phase 11: Navigate to Shell → Settings
        _navigation.Replace<ShellViewModel>();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Navigates to the Shell and begins the normal authenticated session.
    /// </summary>
    [RelayCommand]
    private Task ContinueAsync()
    {
        _navigation.Replace<ShellViewModel>();
        return Task.CompletedTask;
    }
}
