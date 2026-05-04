using System.Reflection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PassKey.Desktop;

public static class Program
{
    // Named event used for single-instance detection and window-restore signalling.
    // AutoReset: each Set() wakes exactly one WaitOne() call.
    private const string ShowEventName = "PassKey.Desktop.ShowWindow";

    // Path to the startup crash log — written only when Application.Start() throws.
    private static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PassKey", "startup-crash.log");

    [STAThread]
    public static void Main(string[] args)
    {
        // ── Single-instance ──────────────────────────────────────────────────
        // If another instance is already running, signal it to restore its window
        // and exit immediately. This prevents the "app doesn't appear to start"
        // symptom when PassKey is already running minimised in the system tray.

        EventWaitHandle? showEvent = null;
        bool isFirstInstance = true;

        try
        {
            showEvent = new EventWaitHandle(
                initialState: false,
                mode: EventResetMode.AutoReset,
                name: ShowEventName,
                createdNew: out isFirstInstance);
        }
        catch
        {
            // Named-event creation failed (unusual system configuration) —
            // treat as first instance so the app always starts.
        }

        if (!isFirstInstance)
        {
            try { showEvent?.Set(); } catch { }
            showEvent?.Dispose();
            return;
        }

        // Start a daemon thread that listens for subsequent launch attempts and
        // restores the main window via the UI dispatcher whenever signalled.
        if (showEvent != null)
        {
            new Thread(() => MonitorShowEvent(showEvent))
            {
                IsBackground = true,
                Name = "PassKey.SingleInstance.Monitor"
            }.Start();
        }

        // ── App startup ──────────────────────────────────────────────────────
        // Note: ApplySavedLanguage() is called inside App() constructor before
        // InitializeComponent(), following the official Microsoft docs pattern.
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            Application.Start(p =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }
        catch (Exception ex)
        {
            // Write a crash log so users can share it for support.
            // The log is only created on an actual startup failure.
            WriteCrashLog(ex);
            throw;
        }
    }

    /// <summary>
    /// Background loop: waits for the named event to be signalled (a subsequent
    /// launch attempt), then marshals a <see cref="MainWindow.RestoreWindow"/> call
    /// onto the UI thread so the window becomes visible again.
    /// </summary>
    private static void MonitorShowEvent(EventWaitHandle showEvent)
    {
        while (true)
        {
            try
            {
                showEvent.WaitOne();
                App.MainWindow?.DispatcherQueue.TryEnqueue(() => App.MainWindow?.RestoreWindow());
            }
            catch
            {
                // Silently ignore — this is a non-critical background thread.
            }
        }
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            var logDir = Path.GetDirectoryName(CrashLogPath)!;
            Directory.CreateDirectory(logDir);
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            File.AppendAllText(
                CrashLogPath,
                $"[{DateTime.Now:s}] PassKey v{version} startup crash:{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // If we can't write the log, there is nothing more we can do.
        }
    }
}
