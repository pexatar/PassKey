using Microsoft.UI.Xaml.Controls;

namespace PassKey.Desktop.Services;

/// <summary>
/// Severity of a transient toast notification. Maps onto <see cref="InfoBarSeverity"/>
/// and also drives the auto-dismiss behaviour (see <see cref="IToastService"/>).
/// </summary>
public enum ToastSeverity
{
    /// <summary>Neutral information. Auto-dismisses after 3 seconds.</summary>
    Info,

    /// <summary>Successful action confirmation. Auto-dismisses after 5 seconds.</summary>
    Success,

    /// <summary>Non-blocking warning. Auto-dismisses after 5 seconds.</summary>
    Warning,

    /// <summary>Error. Stays open until the user dismisses it manually.</summary>
    Error,
}

/// <summary>
/// Shows transient, non-blocking toast notifications in a single shared
/// <see cref="InfoBar"/> anchored to the bottom-right of the main window.
/// </summary>
/// <remarks>
/// <para>
/// Toasts are serialised through an internal queue and a fire-and-forget pump —
/// the same deadlock-free pattern used by <see cref="IDialogQueueService"/> — so
/// rapid-fire calls (e.g. copy username then copy password) never overlap.
/// </para>
/// <para>
/// <b>Auto-dismiss:</b> <see cref="ToastSeverity.Info"/> closes after 3s,
/// <see cref="ToastSeverity.Success"/> and <see cref="ToastSeverity.Warning"/> after 5s.
/// <see cref="ToastSeverity.Error"/> never auto-closes — the user must dismiss it,
/// so failures are not missed.
/// </para>
/// <para>
/// <b>Host attachment:</b> the <see cref="InfoBar"/> lives in the main window visual
/// tree; <see cref="Attach"/> is called once at window activation. Calls to
/// <see cref="Show"/> before attachment are silently dropped (no host to render into).
/// </para>
/// </remarks>
public interface IToastService
{
    /// <summary>
    /// Binds the service to the host <see cref="InfoBar"/>. Called once during
    /// main-window activation. Until this is called, <see cref="Show"/> is a no-op.
    /// </summary>
    /// <param name="host">The bottom-right <see cref="InfoBar"/> in the main window.</param>
    void Attach(InfoBar host);

    /// <summary>
    /// Queues a toast for display. Safe to call from any thread — the work is
    /// marshalled onto the UI thread. No-op if <see cref="Attach"/> has not run yet.
    /// </summary>
    /// <param name="severity">Severity, which also selects the auto-dismiss timeout.</param>
    /// <param name="message">Localised body text.</param>
    /// <param name="title">Optional localised title shown in bold above the message.</param>
    /// <param name="actionLabel">
    /// Optional localised label for an action button shown inside the toast
    /// (e.g. "Stay active"). When supplied, <paramref name="actionCallback"/> must also be set.
    /// </param>
    /// <param name="actionCallback">
    /// Invoked on the UI thread when the action button is clicked. The toast is dismissed
    /// immediately afterwards.
    /// </param>
    void Show(
        ToastSeverity severity,
        string message,
        string? title = null,
        string? actionLabel = null,
        Action? actionCallback = null);
}
