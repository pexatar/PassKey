using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Login view. Handles vault unlock by verifying the master password
/// against the stored KDF parameters via <see cref="IVaultStateService.UnlockAsync"/>.
/// On success, navigates to <see cref="ShellViewModel"/>; on failure, exposes an error message.
/// </summary>
/// <remarks>
/// Dependencies injected via constructor: <see cref="IVaultStateService"/>, <see cref="INavigationStack"/>.
/// The password is handled as a <c>char[]</c> and cleared with <c>Array.Clear</c> after use
/// to minimize its lifetime in managed memory.
/// </remarks>
public partial class LoginViewModel : ObservableObject
{
    private readonly IVaultStateService _vaultState;
    private readonly INavigationStack _navigation;

    /// <summary>
    /// Gets or sets a value indicating whether a vault unlock operation is in progress.
    /// Used by the View to show a <c>ProgressRing</c> and disable the login button.
    /// </summary>
    [ObservableProperty]
    public partial bool IsAuthenticating { get; set; }

    /// <summary>
    /// Gets or sets the error message to display when authentication fails.
    /// Contains a localization key string (e.g., "IncorrectPassword" or "UnlockFailed").
    /// </summary>
    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether an authentication error occurred on the last attempt.
    /// Controls visibility of the inline error panel in the View.
    /// </summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="LoginViewModel"/>.
    /// </summary>
    /// <param name="vaultState">Vault state service used to attempt vault unlock.</param>
    /// <param name="navigation">Navigation stack for replacing the current page with the Shell on success.</param>
    public LoginViewModel(IVaultStateService vaultState, INavigationStack navigation)
    {
        _vaultState = vaultState;
        _navigation = navigation;
    }

    /// <summary>
    /// Attempts to unlock the vault with the provided master password.
    /// Navigates to <see cref="ShellViewModel"/> on success, or sets <see cref="HasError"/> on failure.
    /// </summary>
    [RelayCommand]
    private async Task LoginAsync(string password)
    {
        if (string.IsNullOrEmpty(password)) return;

        IsAuthenticating = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var chars = password.ToCharArray();
            var success = await Task.Run(async () => await _vaultState.UnlockAsync(chars));
            Array.Clear(chars);

            if (success)
            {
                _navigation.Replace<ShellViewModel>();
            }
            else
            {
                HasError = true;
                ErrorMessage = "IncorrectPassword"; // Localization key
            }
        }
        catch
        {
            HasError = true;
            ErrorMessage = "UnlockFailed"; // Localization key
        }
        finally
        {
            IsAuthenticating = false;
        }
    }
}
