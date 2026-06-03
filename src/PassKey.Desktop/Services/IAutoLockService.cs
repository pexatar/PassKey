namespace PassKey.Desktop.Services;

/// <summary>
/// Locks the vault automatically after a configurable period of system inactivity,
/// warning the user with a countdown toast shortly before the lock fires.
/// </summary>
/// <remarks>
/// The inactivity period is read live from <see cref="ISettingsService.AutoLockSeconds"/>
/// (a value of <c>0</c> disables auto-lock). Inactivity is measured system-wide via the
/// Win32 <c>GetLastInputInfo</c> API, so the vault stays unlocked while the user is active
/// in any application and locks once the machine has been genuinely idle.
/// </remarks>
public interface IAutoLockService
{
    /// <summary>
    /// Starts monitoring. Must be called once at application startup on the UI thread.
    /// The service then arms itself whenever the vault is unlocked and disarms on lock.
    /// </summary>
    void Initialize();
}
