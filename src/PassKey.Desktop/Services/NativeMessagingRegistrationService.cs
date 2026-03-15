using Microsoft.Win32;

namespace PassKey.Desktop.Services;

/// <summary>
/// Registers the PassKey Native Messaging Host manifest for Chrome, Edge, and Firefox.
/// Called idempotently at every app start; requires no administrator privileges (HKCU only).
/// Writes two JSON manifest files to <c>%LOCALAPPDATA%\PassKey\native-messaging\</c>
/// and creates the corresponding registry keys under
/// <c>HKCU\SOFTWARE\Google\Chrome\NativeMessagingHosts\</c>,
/// <c>HKCU\SOFTWARE\Microsoft\Edge\NativeMessagingHosts\</c>, and
/// <c>HKCU\SOFTWARE\Mozilla\NativeMessagingHosts\</c>.
/// Silently skips registration if <c>PassKey.BrowserHost.exe</c> is not found
/// in the same directory as the Desktop executable.
/// </summary>
public static class NativeMessagingRegistrationService
{
    private const string HostName = "com.passkey.host";
    private const string BrowserHostExeName = "PassKey.BrowserHost.exe";

    // Chrome extension ID — must match the 'key' field in extensions/chrome/manifest.json.
    // See Item 2 for how to generate/update this value.
    internal const string ChromeExtensionId = "jmddfinmjgpgmfkiblhnjccagheadpop";

    // Firefox extension ID — defined in browser_specific_settings.gecko.id in Firefox manifest.
    private const string FirefoxExtensionId = "passkey@passkey.local";

    /// <summary>
    /// Ensures that Chrome, Edge, and Firefox Native Messaging Host manifests are written to disk
    /// and the corresponding registry keys point to those manifests.
    /// Any exception during registration is silently caught to prevent app startup failure.
    /// </summary>
    public static void EnsureRegistered()
    {
        try
        {
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (exeDir is null) return;

            var browserHostPath = Path.Combine(exeDir, BrowserHostExeName);
            if (!File.Exists(browserHostPath)) return; // BrowserHost not installed — skip

            var manifestsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PassKey", "native-messaging");
            Directory.CreateDirectory(manifestsDir);

            // Chrome / Edge share the same manifest format
            var chromeManifestPath = Path.Combine(manifestsDir, "com.passkey.host.json");
            File.WriteAllText(chromeManifestPath, BuildChromeManifest(browserHostPath));

            // Firefox uses allowed_extensions instead of allowed_origins
            var firefoxManifestPath = Path.Combine(manifestsDir, "com.passkey.host.firefox.json");
            File.WriteAllText(firefoxManifestPath, BuildFirefoxManifest(browserHostPath));

            RegisterKey(@"SOFTWARE\Google\Chrome\NativeMessagingHosts\" + HostName, chromeManifestPath);
            RegisterKey(@"SOFTWARE\Microsoft\Edge\NativeMessagingHosts\" + HostName, chromeManifestPath);
            RegisterKey(@"SOFTWARE\Mozilla\NativeMessagingHosts\" + HostName, firefoxManifestPath);
        }
        catch
        {
            // Registration failure must never crash the app
        }
    }

    /// <summary>
    /// Builds the Chrome/Edge Native Messaging Host manifest JSON.
    /// Uses <c>allowed_origins</c> restricted to the PassKey Chrome extension ID.
    /// </summary>
    /// <param name="browserHostPath">Absolute path to <c>PassKey.BrowserHost.exe</c>.</param>
    /// <returns>A JSON string conforming to the Chrome Native Messaging manifest format.</returns>
    private static string BuildChromeManifest(string browserHostPath) =>
        $$"""
        {
          "name": "{{HostName}}",
          "description": "PassKey Password Manager - Native Messaging Host",
          "path": "{{EscapeJsonPath(browserHostPath)}}",
          "type": "stdio",
          "allowed_origins": [
            "chrome-extension://{{ChromeExtensionId}}/"
          ]
        }
        """;

    /// <summary>
    /// Builds the Firefox Native Messaging Host manifest JSON.
    /// Uses <c>allowed_extensions</c> restricted to the PassKey Firefox extension ID.
    /// </summary>
    /// <param name="browserHostPath">Absolute path to <c>PassKey.BrowserHost.exe</c>.</param>
    /// <returns>A JSON string conforming to the Firefox Native Messaging manifest format.</returns>
    private static string BuildFirefoxManifest(string browserHostPath) =>
        $$"""
        {
          "name": "{{HostName}}",
          "description": "PassKey Password Manager - Native Messaging Host",
          "path": "{{EscapeJsonPath(browserHostPath)}}",
          "type": "stdio",
          "allowed_extensions": [
            "{{FirefoxExtensionId}}"
          ]
        }
        """;

    // JSON requires backslashes doubled: C:\foo\bar → C:\\foo\\bar
    private static string EscapeJsonPath(string path) => path.Replace(@"\", @"\\");

    /// <summary>
    /// Creates or updates a HKCU registry key whose default value points to the manifest path.
    /// </summary>
    /// <param name="subKey">Registry sub-key path relative to HKCU.</param>
    /// <param name="manifestPath">Full path to the manifest JSON file.</param>
    private static void RegisterKey(string subKey, string manifestPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKey);
        key?.SetValue(null, manifestPath); // (Default) = path to manifest JSON
    }
}
