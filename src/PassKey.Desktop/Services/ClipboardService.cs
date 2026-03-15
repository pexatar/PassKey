using System.Security.Cryptography;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;

namespace PassKey.Desktop.Services;

/// <summary>
/// Clipboard service that copies text to the Windows clipboard with auto-clear after 30 seconds,
/// SHA-256 hash verification before clearing (to avoid clearing content placed by another app),
/// and Windows clipboard history suppression via <see cref="ClipboardContentOptions.IsAllowedInHistory"/>.
/// Sensitive content (passwords, CVVs) triggers the auto-clear timer; non-sensitive content
/// (usernames, URLs) does not.
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    private byte[]? _copiedHash;
    private DispatcherQueueTimer? _clearTimer;
    private readonly DispatcherQueue _dispatcherQueue;

    /// <summary>Raised when content is successfully placed on the clipboard.</summary>
    public event Action<CopyType>? ContentCopied;

    /// <summary>Raised when the auto-clear timer fires and the clipboard is cleared.</summary>
    public event Action? ContentCleared;

    /// <summary>
    /// Initializes a new instance of <see cref="ClipboardService"/>.
    /// Captures the UI thread <see cref="DispatcherQueue"/> for timer operations.
    /// </summary>
    public ClipboardService()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    /// <summary>
    /// Copies <paramref name="content"/> to the Windows clipboard.
    /// Sets <see cref="ClipboardContentOptions.IsAllowedInHistory"/> and
    /// <see cref="ClipboardContentOptions.IsRoamable"/> to false to suppress
    /// the clipboard history (Win+V) and cross-device roaming for sensitive data.
    /// Falls back to <see cref="Clipboard.SetContent"/> if <see cref="Clipboard.SetContentWithOptions"/>
    /// fails (e.g., in virtualized or sandboxed environments).
    /// For <see cref="CopyType.Sensitive"/>, starts a 30-second auto-clear timer.
    /// </summary>
    /// <param name="content">The plaintext string to copy.</param>
    /// <param name="type">
    /// Determines whether auto-clear is active.
    /// Use <see cref="CopyType.Sensitive"/> for passwords and CVVs,
    /// <see cref="CopyType.Standard"/> for usernames and URLs.
    /// </param>
    public void Copy(string content, CopyType type)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(content);

        // Suppress clipboard history (Win+V) e roaming cloud — API ufficiale
        var clipboardOptions = new ClipboardContentOptions
        {
            IsAllowedInHistory = false,
            IsRoamable         = false
        };
        var setSuccess = Clipboard.SetContentWithOptions(dataPackage, clipboardOptions);
        if (!setSuccess)
        {
            // Fallback silenzioso (es. ambiente virtualizzato/sandbox)
            Clipboard.SetContent(dataPackage);
        }

        // Flush() rende i dati persistenti dopo chiusura app.
        // Può fallire con COMException 0x800401D0 in app self-contained.
        // Non critico: SetContent() è sufficiente per l'uso normale.
        try { Clipboard.Flush(); }
        catch (System.Runtime.InteropServices.COMException) { /* clipboard still works without Flush */ }

        // Store hash of copied content for verification before clearing
        _copiedHash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));

        ContentCopied?.Invoke(type);

        // Auto-clear timer (30s for sensitive, no timer for standard)
        _clearTimer?.Stop();
        if (type == CopyType.Sensitive)
        {
            _clearTimer = _dispatcherQueue.CreateTimer();
            _clearTimer.Interval = TimeSpan.FromSeconds(30);
            _clearTimer.IsRepeating = false;
            _clearTimer.Tick += (_, _) => ClearIfOurs();
            _clearTimer.Start();
        }
    }

    /// <summary>
    /// Clears the clipboard only if its current content matches the SHA-256 hash of what
    /// this service last copied. Prevents clearing content placed by another application
    /// during the 30-second window.
    /// </summary>
    private void ClearIfOurs()
    {
        if (_copiedHash is null) return;

        try
        {
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.Text))
            {
                // GetTextAsync is async but we're on UI thread via DispatcherQueue
                var textTask = content.GetTextAsync().AsTask();
                textTask.ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        var currentHash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(t.Result));
                        if (_copiedHash.AsSpan().SequenceEqual(currentHash))
                        {
                            _dispatcherQueue.TryEnqueue(() =>
                            {
                                Clipboard.Clear();
                                _copiedHash = null;
                                ContentCleared?.Invoke();
                            });
                        }
                    }
                });
            }
        }
        catch
        {
            // Clipboard access can fail if another app has it locked
        }
    }
}
