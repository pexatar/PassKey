using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PassKey.Desktop.Helpers;

/// <summary>
/// Shared UI helpers for list-view code-behind classes.
/// Centralises boilerplate that would otherwise be duplicated across
/// <c>PasswordsListView</c>, <c>CreditCardsListView</c>, <c>IdentitiesListView</c>
/// and <c>SecureNotesListView</c>.
/// </summary>
internal static class ListViewHelpers
{
    /// <summary>
    /// Opens <paramref name="tip"/> for 2 seconds, then closes it automatically
    /// via a <see cref="DispatcherTimer"/>.
    /// </summary>
    /// <param name="tip">The <see cref="TeachingTip"/> to show (e.g. SavedTip).</param>
    /// <param name="extraAction">
    /// Optional action invoked immediately after opening the tip.
    /// Used by <c>SecureNotesListView</c> to trigger the accessibility announcer.
    /// </param>
    internal static void ShowSavedToast(TeachingTip tip, Action? extraAction = null)
    {
        tip.IsOpen = true;
        extraAction?.Invoke();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (s, _) =>
        {
            tip.IsOpen = false;
            ((DispatcherTimer)s!).Stop();
        };
        timer.Start();
    }
}
