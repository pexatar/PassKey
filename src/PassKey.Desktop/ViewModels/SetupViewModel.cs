using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Models;
using PassKey.Core.Services;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Setup view. Handles first-run vault creation by collecting and
/// validating the master password before calling <see cref="IVaultStateService.InitializeAsync"/>.
/// Navigates to <see cref="WelcomeViewModel"/> after successful vault creation.
/// </summary>
/// <remarks>
/// Dependencies injected via constructor: <see cref="IVaultStateService"/>,
/// <see cref="IPasswordStrengthAnalyzer"/>, <see cref="INavigationStack"/>.
/// The <see cref="CanCreate"/> gate requires a password score of at least 60 and both
/// password fields to match.
/// </remarks>
public partial class SetupViewModel : ObservableObject
{
    private readonly IVaultStateService _vaultState;
    private readonly IPasswordStrengthAnalyzer _strengthAnalyzer;
    private readonly INavigationStack _navigation;

    /// <summary>
    /// Gets or sets the most recent password strength analysis result.
    /// Null when the password field is empty.
    /// </summary>
    [ObservableProperty]
    public partial PasswordStrengthResult? StrengthResult { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the password and confirm-password fields contain identical non-empty values.
    /// </summary>
    [ObservableProperty]
    public partial bool PasswordsMatch { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether all prerequisites for vault creation are satisfied
    /// (passwords match and strength score is at least 60).
    /// </summary>
    [ObservableProperty]
    public partial bool CanCreate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether vault creation (KDF derivation) is in progress.
    /// Used by the View to show a <c>ProgressRing</c> and disable the create button.
    /// </summary>
    [ObservableProperty]
    public partial bool IsCreating { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="SetupViewModel"/>.
    /// </summary>
    /// <param name="vaultState">Vault state service used to initialize the new vault.</param>
    /// <param name="strengthAnalyzer">Password strength analyzer for real-time feedback.</param>
    /// <param name="navigation">Navigation stack for transitioning to the Welcome page after setup.</param>
    public SetupViewModel(
        IVaultStateService vaultState,
        IPasswordStrengthAnalyzer strengthAnalyzer,
        INavigationStack navigation)
    {
        _vaultState = vaultState;
        _strengthAnalyzer = strengthAnalyzer;
        _navigation = navigation;
    }

    /// <summary>
    /// Analyzes the given password and updates <see cref="StrengthResult"/> and <see cref="CanCreate"/>.
    /// Called by the View whenever the primary password field changes.
    /// </summary>
    /// <param name="password">The plain-text password to analyze.</param>
    public void AnalyzePassword(string password)
    {
        StrengthResult = _strengthAnalyzer.Analyze(password.AsSpan());
        UpdateCanCreate();
    }

    /// <summary>
    /// Compares the two password fields and updates <see cref="PasswordsMatch"/> and <see cref="CanCreate"/>.
    /// Called by the View whenever either password field changes.
    /// </summary>
    /// <param name="password">Content of the primary password field.</param>
    /// <param name="confirm">Content of the confirm-password field.</param>
    public void CheckPasswordsMatch(string password, string confirm)
    {
        PasswordsMatch = !string.IsNullOrEmpty(password) &&
                         password == confirm;
        UpdateCanCreate();
    }

    private void UpdateCanCreate()
    {
        CanCreate = PasswordsMatch &&
                    StrengthResult is not null &&
                    StrengthResult.Score >= 60; // "Strong" threshold
    }

    /// <summary>
    /// Creates the vault with the provided master password.
    /// Runs KDF derivation on a background thread, then navigates to <see cref="WelcomeViewModel"/>.
    /// </summary>
    [RelayCommand]
    private async Task CreateVaultAsync(string password)
    {
        if (!CanCreate || string.IsNullOrEmpty(password)) return;

        IsCreating = true;
        try
        {
            var chars = password.ToCharArray();
            await Task.Run(async () => await _vaultState.InitializeAsync(chars));
            Array.Clear(chars);

            _navigation.Replace<WelcomeViewModel>();
        }
        finally
        {
            IsCreating = false;
        }
    }
}
