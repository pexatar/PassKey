using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;

namespace PassKey.Desktop.Services;

/// <summary>
/// Default <see cref="IAutoLockService"/> implementation. Runs a single always-on
/// per-second timer that locks the vault once the machine has been idle longer than
/// <see cref="ISettingsService.AutoLockSeconds"/>, warning the user with a "stay active"
/// toast 60 s and 30 s beforehand.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why an always-on timer instead of arming on <c>VaultUnlocked</c>?</b> That event is
/// raised on whatever thread completed the unlock — for the login path a thread-pool thread,
/// since <c>UnlockAsync</c> runs inside <c>Task.Run</c>. Creating/starting a
/// <see cref="DispatcherQueueTimer"/> off the UI thread does not work. Creating the timer
/// once in <see cref="Initialize"/> (guaranteed to run on the UI thread) and simply checking
/// <see cref="IVaultStateService.IsUnlocked"/> on every tick removes that entire class of bug.
/// </para>
/// <para>
/// <b>Why poll <c>GetLastInputInfo</c>?</b> WinUI 3 exposes no global "last input" signal,
/// and tracking input only on PassKey's own windows would miss a user active elsewhere. The
/// Win32 API gives a true machine-wide idle measurement — the correct basis for a security
/// auto-lock.
/// </para>
/// </remarks>
public sealed class AutoLockService : IAutoLockService, IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint CbSize;
        public uint DwTime;
    }

    // Classic DllImport (not LibraryImport): the source-generated variant requires
    // <AllowUnsafeBlocks> and AOT, neither of which this project uses.
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);

    /// <summary>Below this idle time (seconds) the user is considered freshly active.</summary>
    private const int ActivityThresholdSeconds = 5;

    private readonly IVaultStateService _vaultState;
    private readonly ISettingsService _settings;
    private readonly IToastService _toast;
    private readonly ResourceLoader _resourceLoader = new();

    private DispatcherQueueTimer? _timer;
    private bool _warned60;
    private bool _warned30;

    public AutoLockService(IVaultStateService vaultState, ISettingsService settings, IToastService toast)
    {
        _vaultState = vaultState;
        _settings = settings;
        _toast = toast;
    }

    /// <inheritdoc/>
    public void Initialize()
    {
        // Called from MainWindow activation → guaranteed UI thread, so the timer is bound
        // to the correct DispatcherQueue. It then runs for the whole process lifetime;
        // OnTick is a no-op whenever the vault is locked.
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        if (dispatcher is null) return;

        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.IsRepeating = true;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        // Idle only matters while the vault is open; reset the one-shot warnings so a
        // fresh idle period after the next unlock starts clean.
        if (!_vaultState.IsUnlocked)
        {
            _warned60 = false;
            _warned30 = false;
            return;
        }

        var limit = _settings.AutoLockSeconds;
        if (limit <= 0) return; // 0 = auto-lock disabled

        var idle = GetIdleSeconds();

        // Recent activity re-arms the one-shot warnings for the next idle stretch.
        if (idle < ActivityThresholdSeconds)
        {
            _warned60 = false;
            _warned30 = false;
        }

        var remaining = limit - idle;

        if (remaining <= 0)
        {
            _warned60 = false;
            _warned30 = false;
            _vaultState.Lock();
            return;
        }

        // Warnings only make sense when the timeout is long enough to have a "before" window.
        if (remaining <= 30 && !_warned30 && limit > 30)
        {
            _warned30 = true;
            ShowCountdownToast(30);
        }
        else if (remaining <= 60 && !_warned60 && limit > 60)
        {
            _warned60 = true;
            ShowCountdownToast(60);
        }
    }

    private void ShowCountdownToast(int seconds)
    {
        var message = string.Format(_resourceLoader.GetString("AutoLockCountdown"), seconds);
        _toast.Show(
            ToastSeverity.Warning,
            message,
            title: null,
            actionLabel: _resourceLoader.GetString("AutoLockStayActive"),
            // The click itself is user input, which resets the system idle timer — so the
            // countdown restarts on its own. The callback only needs to exist to render the button.
            actionCallback: static () => { });
    }

    /// <summary>Returns whole seconds since the last system-wide user input.</summary>
    private static int GetIdleSeconds()
    {
        var info = new LastInputInfo { CbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return 0;

        // Unsigned subtraction handles the ~24.9-day GetTickCount wraparound correctly.
        var idleMs = unchecked((uint)Environment.TickCount - info.DwTime);
        return (int)(idleMs / 1000);
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer = null;
    }
}
