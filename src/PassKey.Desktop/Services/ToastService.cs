using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace PassKey.Desktop.Services;

/// <summary>
/// Default <see cref="IToastService"/> implementation. Drives a single shared
/// <see cref="InfoBar"/> through a serial queue so toasts never overlap.
/// </summary>
/// <remarks>
/// The pump is fire-and-forget and guarded by <see cref="_isPumping"/>, mirroring
/// <see cref="DialogQueueService"/>. Every toast — auto-dismissed or manually closed —
/// completes via the <see cref="InfoBar.Closed"/> event, so the pump advances on a
/// single, uniform signal regardless of how the toast was dismissed.
/// </remarks>
public sealed class ToastService : IToastService
{
    private static readonly TimeSpan InfoDuration    = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SuccessDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan WarningDuration = TimeSpan.FromSeconds(5);

    /// <summary>Gap left between consecutive toasts so the close/open transition is visible.</summary>
    private static readonly TimeSpan InterToastGap = TimeSpan.FromMilliseconds(150);

    private readonly Queue<ToastItem> _queue = new();
    private InfoBar? _host;
    private DispatcherQueue? _dispatcher;
    private bool _isPumping;

    /// <inheritdoc/>
    public void Attach(InfoBar host)
    {
        _host = host;
        _dispatcher = host.DispatcherQueue;
    }

    /// <inheritdoc/>
    public void Show(
        ToastSeverity severity,
        string message,
        string? title = null,
        string? actionLabel = null,
        Action? actionCallback = null)
    {
        var dispatcher = _dispatcher;
        if (dispatcher is null) return; // Attach() not called yet — nothing to render into.

        // Marshal onto the UI thread: the queue and pump are single-threaded by design.
        dispatcher.TryEnqueue(() =>
        {
            _queue.Enqueue(new ToastItem(severity, message, title, actionLabel, actionCallback));
            _ = PumpAsync();
        });
    }

    /// <summary>
    /// Drains the queue sequentially. Only one pump runs at a time; concurrent
    /// calls return immediately via the <see cref="_isPumping"/> guard.
    /// </summary>
    private async Task PumpAsync()
    {
        if (_isPumping) return;
        _isPumping = true;

        try
        {
            while (_queue.TryDequeue(out var item))
            {
                await ShowOneAsync(item);
                await Task.Delay(InterToastGap);
            }
        }
        finally
        {
            _isPumping = false;
        }
    }

    /// <summary>
    /// Shows a single toast and completes when it is dismissed — either by the
    /// auto-dismiss timer (Info/Success/Warning) or by the user (always allowed,
    /// and the only way to dismiss an Error).
    /// </summary>
    private async Task ShowOneAsync(ToastItem item)
    {
        var host = _host;
        if (host is null) return;

        host.Title = item.Title ?? string.Empty;
        host.Message = item.Message;
        host.Severity = item.Severity switch
        {
            ToastSeverity.Success => InfoBarSeverity.Success,
            ToastSeverity.Warning => InfoBarSeverity.Warning,
            ToastSeverity.Error   => InfoBarSeverity.Error,
            _                     => InfoBarSeverity.Informational,
        };
        host.IsClosable = true; // user can always dismiss early

        // Optional inline action button (e.g. "Stay active" on the auto-lock warning).
        if (item.ActionLabel is not null && item.ActionCallback is not null)
        {
            var actionButton = new Button { Content = item.ActionLabel };
            actionButton.Click += (_, _) =>
            {
                item.ActionCallback();
                host.IsOpen = false;
            };
            host.ActionButton = actionButton;
        }
        else
        {
            host.ActionButton = null;
        }

        // A single TaskCompletionSource resolved by the Closed event unifies both
        // dismiss paths: the auto-dismiss timer sets IsOpen=false (which raises
        // Closed), and the user clicking the close button raises Closed directly.
        var dismissed = new TaskCompletionSource();
        Windows.Foundation.TypedEventHandler<InfoBar, InfoBarClosedEventArgs>? onClosed = null;
        onClosed = (sender, _) =>
        {
            sender.Closed -= onClosed;
            dismissed.TrySetResult();
        };
        host.Closed += onClosed;

        host.IsOpen = true;

        if (item.Severity != ToastSeverity.Error)
        {
            var duration = item.Severity switch
            {
                ToastSeverity.Success => SuccessDuration,
                ToastSeverity.Warning => WarningDuration,
                _                     => InfoDuration,
            };

            // Fire-and-forget timer: close the bar after the timeout. If the user
            // already closed it, setting IsOpen=false again is a harmless no-op.
            _ = Task.Delay(duration).ContinueWith(
                _ => _dispatcher?.TryEnqueue(() => { if (host.IsOpen) host.IsOpen = false; }),
                TaskScheduler.Default);
        }

        await dismissed.Task;
    }

    /// <summary>A queued toast: severity, text, and an optional inline action button.</summary>
    private readonly record struct ToastItem(
        ToastSeverity Severity,
        string Message,
        string? Title,
        string? ActionLabel = null,
        Action? ActionCallback = null);
}
