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

                // Desktop services
                services.AddSingleton<INavigationStack, NavigationStack>();
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IDialogQueueService, DialogQueueService>();
                services.AddSingleton<IClipboardService, ClipboardService>();
                services.AddSingleton<IVaultStateService, VaultStateService>();
                services.AddSingleton<IDatabaseService, DatabaseService>();
                services.AddSingleton<IVaultRepository, SqliteVaultRepository>();
                services.AddSingleton<IBrowserIpcService, BrowserIpcService>();

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
            })
            .Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Registra passkey:// nel registro HKCU (idempotente, no admin)
        ProtocolActivationService.EnsureRegistered();

        // Registra Native Messaging Host per Chrome/Edge/Firefox (idempotente, no admin)
        NativeMessagingRegistrationService.EnsureRegistered();

        MainWindow = new MainWindow();
        MainWindow.Activate();

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
        catch
        {
            // IPC service failure should not prevent app from starting
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
        try
        {
            if (MainWindow is { } mw)
            {
                mw.Content = new Microsoft.UI.Xaml.Controls.TextBlock
                {
                    Text = $"UNHANDLED EXCEPTION:\n{e.Exception}\n\nMessage: {e.Message}",
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
