using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using PassKey.Core.Interfaces;
using PassKey.Core.Services;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }

    public static IHost Host { get; private set; } = null!;
    public static IServiceProvider Services => Host.Services;

    public App()
    {
        // Must be called before InitializeComponent() so MRT Core loads
        // x:Uid resources in the correct language (Microsoft.Windows.Globalization API).
        ApplySavedLanguage();
        InitializeComponent();
        UnhandledException += OnUnhandledException;

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // Core services
                services.AddSingleton<ICryptoService, CryptoService>();
                services.AddSingleton<IVaultService, VaultService>();
                services.AddSingleton<IPasswordGenerator, PasswordGenerator>();
                services.AddSingleton<IPasswordStrengthAnalyzer, PasswordStrengthAnalyzer>();
                services.AddSingleton<ITotpService, TotpService>();
                services.AddSingleton<IHibpService, HibpService>();
                services.AddSingleton<IWatchtowerScanService, WatchtowerScanService>();

                // Desktop services
                // Registered first: every other service may log during construction.
                services.AddSingleton<ILogService, LogService>();
                services.AddSingleton<INavigationStack, NavigationStack>();
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IDialogQueueService, DialogQueueService>();
                services.AddSingleton<IToastService, ToastService>();
                services.AddSingleton<IAutoLockService, AutoLockService>();
                services.AddSingleton<IClipboardService, ClipboardService>();
                services.AddSingleton<IVaultStateService, VaultStateService>();
                services.AddSingleton<IDatabaseService, DatabaseService>();
                services.AddSingleton<IVaultRepository, SqliteVaultRepository>();
                services.AddSingleton<IBrowserIpcService, BrowserIpcService>();
                services.AddSingleton<IUpdateService, UpdateService>();

                // Backup/Import services
                services.AddSingleton<IBackupService, BackupService>();
                services.AddSingleton<IMergeService, MergeService>();
                services.AddSingleton<ICsvImporter, CsvImporter>();
                services.AddSingleton<IBitwardenImporter, BitwardenImporter>();
                services.AddSingleton<IOnePuxImporter, OnePuxImporter>();
                services.AddSingleton<IBackupFileService, BackupFileService>();
                services.AddSingleton<IFilePickerService, FilePickerService>();
                services.AddSingleton<IImportOrchestrator, ImportOrchestrator>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<LoginViewModel>();
                services.AddTransient<SetupViewModel>();
                services.AddTransient<WelcomeViewModel>();
                services.AddTransient<ShellViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<PasswordsListViewModel>();
                services.AddTransient<PasswordDetailViewModel>();
                services.AddTransient<CreditCardsListViewModel>();
                services.AddTransient<CreditCardDetailViewModel>();
                services.AddTransient<IdentitiesListViewModel>();
                services.AddTransient<IdentityDetailViewModel>();
                services.AddTransient<SecureNotesListViewModel>();
                services.AddTransient<SecureNoteDetailViewModel>();
                services.AddTransient<GeneratorViewModel>();
                services.AddTransient<PasswordVerifierViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<HelpViewModel>();
                services.AddTransient<ActivityLogViewModel>();

                // Detail panel factories. A section list creates one detail ViewModel per
                // editing session and disposes it on close (R4), so it needs to ask for a new
                // instance rather than hold an injected one — a single shared instance is what
                // forced the old "assign null, then reassign" trick to provoke a notification.
                services.AddTransient<Func<SecureNoteDetailViewModel>>(
                    sp => sp.GetRequiredService<SecureNoteDetailViewModel>);
            })
            .Build();

        // Start diagnostics before anything else runs: the first session lines must be able to
        // record a failure that happens during start-up, not only after the window is up.
        var log = Services.GetRequiredService<ILogService>();
        log.Initialize(Services.GetRequiredService<ISettingsService>().VerboseLoggingEnabled);
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Registra passkey:// nel registro HKCU (idempotente, no admin)
        ProtocolActivationService.EnsureRegistered();

        // Registra Native Messaging Host per Chrome/Edge/Firefox (idempotente, no admin)
        NativeMessagingRegistrationService.EnsureRegistered();

        MainWindow = new MainWindow();
        MainWindow.Activate();

        var settings = Services.GetRequiredService<ISettingsService>();
        if (settings.StartMinimized)
            MainWindow.HideToTray();

        // Gestisce attivazione da URI passkey://unlock (es. click su link browser)
        var protocolAction = ProtocolActivationService.GetActivationAction();
        if (protocolAction == ProtocolAction.Unlock)
            MainWindow.Activate();

        // Start the Named Pipe IPC server for browser extension communication
        try
        {
            var ipcService = Services.GetRequiredService<IBrowserIpcService>();
            await ipcService.StartAsync();
        }
        catch (Exception ex)
        {
            // IPC service failure should not prevent app from starting; log for diagnostics.
            Services.GetRequiredService<ILogService>()
                    .Error(LogArea.Ipc, "Browser IPC service failed to start", ex);
        }

        // Fire-and-forget: silent update check (max once per 24h, 10s timeout)
        _ = CheckForUpdateSilentlyAsync();
    }

    private async Task CheckForUpdateSilentlyAsync()
    {
        try
        {
            var settings = Services.GetRequiredService<ISettingsService>();

            if (!settings.AutoUpdateCheckEnabled) return;

            // Throttle: check at most once every 24 hours
            if (settings.LastUpdateCheckUtc.HasValue &&
                (DateTime.UtcNow - settings.LastUpdateCheckUtc.Value).TotalHours < 24)
                return;

            using var cts  = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
            var updateSvc  = Services.GetRequiredService<IUpdateService>();
            var result     = await updateSvc.CheckForUpdateAsync(cts.Token);

            // Record timestamp regardless of outcome — avoids hammering the API when offline
            settings.LastUpdateCheckUtc = DateTime.UtcNow;
            settings.Save();

            if (result is null || !result.UpdateAvailable) return;

            // Skip if the user previously dismissed this exact version
            if (settings.SkippedUpdateVersion == $"v{result.NewVersion}") return;

            // Publish on the UI thread — UpdateDetected subscribers touch XAML controls
            MainWindow?.DispatcherQueue.TryEnqueue(() => updateSvc.Publish(result));
        }
        catch
        {
            // Never let an update check crash the app
        }
    }

    private static void ApplySavedLanguage()
    {
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PassKey", "settings.json");

        if (!File.Exists(settingsPath)) return;

        try
        {
            var json = File.ReadAllText(settingsPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Language", out var langElem))
            {
                var lang = langElem.GetString();
                if (!string.IsNullOrEmpty(lang) && lang != "auto")
                {
                    // Set thread culture (affects ResourceLoader.GetString in code-behind)
                    var culture = new System.Globalization.CultureInfo(lang);
                    System.Globalization.CultureInfo.CurrentCulture = culture;
                    System.Globalization.CultureInfo.CurrentUICulture = culture;

                    // Set MRT Core override (affects x:Uid resolution in XAML via .pri)
                    // NOTE: Must use Microsoft.Windows.Globalization (SDK 1.6+) — NOT
                    // Windows.Globalization which is UWP-only and broken for unpackaged apps.
                    Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = lang;
                }
                else
                {
                    // "auto" or empty → reset to system language
                    Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = string.Empty;
                }
            }
        }
        catch
        {
            // Corrupted settings — use system default
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Record it first: whatever happens to the UI afterwards, the failure must survive on disk.
        string? logPath = null;
        try
        {
            var log = Services.GetRequiredService<ILogService>();
            log.Error(LogArea.App, "Unhandled exception", e.Exception);
            logPath = log.CurrentLogFile;
            _ = log.FlushAsync();
        }
        catch
        {
            // Diagnostics must never make a bad situation worse.
        }

        // Fail secure (SYS-01): an unhandled exception means the process is in an unknown state.
        // Zero the decryption key immediately rather than leaving an unlocked vault sitting in
        // memory behind an error screen — the app is no longer trustworthy enough to hold it.
        try
        {
            var vaultState = Services.GetRequiredService<IVaultStateService>();
            if (vaultState.IsUnlocked)
            {
                vaultState.Lock();
                Services.GetRequiredService<ILogService>()
                        .Warn(LogArea.Vault, "Vault locked after unhandled exception (fail-secure)");
            }
        }
        catch
        {
            // Locking is best-effort: never mask the original failure.
        }

        try
        {
            if (MainWindow is { } mw)
            {
                // The error screen is the moment a non-technical user most needs to know where
                // the log is — and the only moment they will look for it.
                var where = logPath is null
                    ? string.Empty
                    : $"\n\nDettagli salvati in:\n{logPath}";

                mw.Content = new Microsoft.UI.Xaml.Controls.TextBlock
                {
                    Text = $"UNHANDLED EXCEPTION:\n{e.Exception}\n\nMessage: {e.Message}{where}",
                    IsTextSelectionEnabled = true,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20),
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red)
                };
            }
        }
        catch
        {
            // If we can't display the error, at least don't crash the handler
        }

        e.Handled = true;
    }
}
