using Microsoft.UI.Xaml.Controls;

namespace PassKey.Desktop.Services;

/// <summary>
/// Serial dialog pump that ensures <see cref="ContentDialog"/> instances are shown one at a time
/// without overlapping. Uses a <see cref="Queue{T}"/> of <see cref="Func{Task}"/> delegates
/// drained by a single pump loop.
///
/// <para>
/// <b>Why not SemaphoreSlim?</b>
/// WinUI 3 <see cref="ContentDialog.ShowAsync"/> must run on the UI thread. Awaiting a
/// <c>SemaphoreSlim</c> before calling <c>ShowAsync</c> can deadlock because the release
/// from a previously shown dialog may arrive before the UI thread has re-entered the
/// <c>await</c> continuation. The queue approach avoids this by never blocking the UI thread:
/// <c>PumpQueueAsync</c> is fire-and-forget, and only one pump runs at a time via the
/// <c>_isPumping</c> guard.
/// </para>
/// </summary>
public sealed class DialogQueueService : IDialogQueueService
{
    private readonly Queue<Func<Task>> _queue = new();
    private bool _isPumping;

    /// <summary>
    /// Enqueues a dialog factory for fire-and-forget display. The result is discarded.
    /// Use <see cref="EnqueueAndWait"/> if you need the <see cref="ContentDialogResult"/>.
    /// </summary>
    /// <param name="dialogFactory">
    /// A factory that creates and shows a <see cref="ContentDialog"/>, returning its result.
    /// Called on the UI thread by the pump loop.
    /// </param>
    public void Enqueue(Func<Task<ContentDialogResult>> dialogFactory)
    {
        _queue.Enqueue(async () => await dialogFactory());
        _ = PumpQueueAsync();
    }

    /// <summary>
    /// Enqueues a dialog factory and returns a <see cref="Task{ContentDialogResult}"/> that
    /// completes when the dialog is dismissed. The task resolves in order relative to other
    /// queued dialogs.
    /// </summary>
    /// <param name="dialogFactory">
    /// A factory that creates and shows a <see cref="ContentDialog"/>, returning its result.
    /// Called on the UI thread by the pump loop.
    /// </param>
    /// <returns>
    /// A task that produces the <see cref="ContentDialogResult"/> when the dialog is closed,
    /// or propagates any exception thrown by the factory.
    /// </returns>
    public Task<ContentDialogResult> EnqueueAndWait(Func<Task<ContentDialogResult>> dialogFactory)
    {
        var tcs = new TaskCompletionSource<ContentDialogResult>();

        _queue.Enqueue(async () =>
        {
            try
            {
                var result = await dialogFactory();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        _ = PumpQueueAsync();
        return tcs.Task;
    }

    /// <summary>
    /// Drains the queue sequentially. Only one pump runs at a time; concurrent calls return
    /// immediately if a pump is already active. Each queued dialog is awaited fully before
    /// the next one starts, preventing UI overlap.
    /// </summary>
    private async Task PumpQueueAsync()
    {
        if (_isPumping) return;
        _isPumping = true;

        try
        {
            while (_queue.TryDequeue(out var task))
            {
                await task();
            }
        }
        finally
        {
            _isPumping = false;
        }
    }
}
