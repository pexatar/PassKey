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
/// </remarks>
public interface IDialogQueueService
{
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
}
