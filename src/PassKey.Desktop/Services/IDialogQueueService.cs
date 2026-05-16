using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PassKey.Desktop.Services;

/// <summary>
/// Serialises <see cref="ContentDialog"/> invocations to prevent WinUI 3 crashes
/// caused by concurrent dialog display attempts.
/// </summary>
/// <remarks>
/// WinUI 3 throws if a second <see cref="ContentDialog"/> is shown while another is open.
/// This service queues dialog factories and executes them one at a time.
/// A <see cref="System.Threading.SemaphoreSlim"/> is intentionally avoided here because
/// awaiting it on the UI thread causes a deadlock; use a <see cref="System.Collections.Generic.Queue{T}"/>
/// with a serial pump instead.
///
/// <para>
/// <b>XamlRoot:</b> WinUI 3 desktop apps require every <see cref="ContentDialog"/> to have
/// its <see cref="ContentDialog.XamlRoot"/> assigned before <c>ShowAsync</c> is called;
/// otherwise <see cref="System.ArgumentException"/> ("This element does not have a XamlRoot")
/// is raised. Code-behind handlers can use <c>this.XamlRoot</c>, but ViewModels lack a
/// natural reference. To remove that duplication, the service exposes
/// <see cref="RootForDialogs"/> — set once at startup from the main window — and
/// <see cref="ConfirmAsync"/> which applies it automatically.
/// </para>
/// </remarks>
public interface IDialogQueueService
{
    /// <summary>
    /// Gets or sets the just-in-time accessor used by <see cref="ConfirmAsync"/> to resolve
    /// the host <see cref="XamlRoot"/> at the moment the dialog is shown.
    /// </summary>
    /// <remarks>
    /// A direct <see cref="XamlRoot"/> value cannot be cached at startup because
    /// <see cref="Window.Content"/>.<see cref="UIElement.XamlRoot"/> can be <see langword="null"/>
    /// until the window is fully loaded. The accessor pattern instead reads the root lazily on
    /// the UI thread when each dialog is actually displayed, by which point the value is valid.
    /// Set this once during application startup, e.g.
    /// <c>XamlRootAccessor = () =&gt; mainWindow.Content?.XamlRoot;</c>.
    /// </remarks>
    Func<XamlRoot?>? XamlRootAccessor { get; set; }

    /// <summary>
    /// Enqueues a dialog factory for fire-and-forget execution.
    /// The result is discarded; use <see cref="EnqueueAndWait"/> when the result is needed.
    /// </summary>
    /// <param name="dialogFactory">A factory that creates and shows the <see cref="ContentDialog"/>.</param>
    void Enqueue(Func<Task<ContentDialogResult>> dialogFactory);

    /// <summary>
    /// Enqueues a dialog factory and returns a <see cref="Task"/> that completes
    /// with the dialog result once it has been shown and dismissed.
    /// </summary>
    /// <param name="dialogFactory">A factory that creates and shows the <see cref="ContentDialog"/>.</param>
    /// <returns>The <see cref="ContentDialogResult"/> selected by the user.</returns>
    Task<ContentDialogResult> EnqueueAndWait(Func<Task<ContentDialogResult>> dialogFactory);

    /// <summary>
    /// Shows a standard confirmation dialog with the supplied strings and waits for the user's choice.
    /// The dialog's <see cref="ContentDialog.XamlRoot"/> is set automatically from
    /// <see cref="RootForDialogs"/>, removing a common source of <see cref="System.ArgumentException"/>
    /// crashes when called from ViewModels.
    /// </summary>
    /// <param name="title">Localised dialog title.</param>
    /// <param name="content">Localised dialog body text.</param>
    /// <param name="primaryButtonText">Localised primary action label (returned as <see langword="true"/>).</param>
    /// <param name="closeButtonText">Localised cancel/close label (returned as <see langword="false"/>).</param>
    /// <param name="defaultButton">Which button is highlighted by default (defaults to the close/cancel button).</param>
    /// <returns><see langword="true"/> if the user selected the primary button; <see langword="false"/> for close.</returns>
    Task<bool> ConfirmAsync(
        string title,
        string content,
        string primaryButtonText,
        string closeButtonText,
        ContentDialogButton defaultButton = ContentDialogButton.Close);
}
