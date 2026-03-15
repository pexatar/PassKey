using Microsoft.Win32;

namespace PassKey.Desktop.Services;

/// <summary>
/// Handles registration and parsing of the <c>passkey://</c> custom URL scheme.
/// Registration writes to HKCU (no administrator privileges required) and is idempotent.
/// Parsing reads command-line arguments supplied by the OS when the app is launched via the scheme.
/// </summary>
public static class ProtocolActivationService
{
    private const string Protocol = "passkey";

    /// <summary>
    /// Registers the <c>passkey://</c> URL scheme in the Windows registry under
    /// <c>HKCU\Software\Classes\passkey</c>. Safe to call on every app start;
    /// uses <see cref="Registry.CreateSubKey"/> which is an upsert operation.
    /// Registration failure is silently swallowed — the app functions normally without it.
    /// </summary>
    public static void EnsureRegistered()
    {
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath)) return;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Protocol}");
            key.SetValue("", "URL:PassKey Protocol");
            key.SetValue("URL Protocol", "");

            using var iconKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Protocol}\DefaultIcon");
            iconKey.SetValue("", $"\"{exePath}\",0");

            using var cmdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Protocol}\shell\open\command");
            cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
        }
        catch
        {
            // Registration is non-critical — the app works without the URL scheme
        }
    }

    /// <summary>
    /// Inspects the process command-line arguments for a <c>passkey://</c> URI.
    /// The OS passes the URI as the first argument when the app is launched via the scheme.
    /// </summary>
    /// <returns>
    /// A <see cref="ProtocolAction"/> if a recognized URI was found;
    /// null if the app was not launched via the protocol or the URI is unrecognized.
    /// </returns>
    public static ProtocolAction? GetActivationAction()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length < 2) return null;

        if (Uri.TryCreate(args[1], UriKind.Absolute, out var uri))
            return Parse(uri);

        return null;
    }

    /// <summary>
    /// Parses a <c>passkey://</c> URI and maps its host segment to a <see cref="ProtocolAction"/>.
    /// Returns null if the scheme is not <c>passkey</c> or the host is unrecognized.
    /// </summary>
    /// <param name="uri">The URI to parse.</param>
    /// <returns>The corresponding <see cref="ProtocolAction"/>, or null if unrecognized.</returns>
    public static ProtocolAction? Parse(Uri uri)
    {
        if (!uri.Scheme.Equals(Protocol, StringComparison.OrdinalIgnoreCase)) return null;
        return uri.Host.ToLowerInvariant() switch
        {
            "unlock" => ProtocolAction.Unlock,
            _        => null
        };
    }
}

/// <summary>
/// Actions that can be triggered via the <c>passkey://</c> URL scheme.
/// </summary>
public enum ProtocolAction
{
    /// <summary>
    /// Brings the PassKey window to the foreground and shows the unlock/login screen.
    /// Triggered by <c>passkey://unlock</c>.
    /// </summary>
    Unlock
}
