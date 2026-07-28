namespace PassKey.Desktop.Services;

/// <summary>Severity of a log entry. Ordered from least to most severe.</summary>
public enum LogLevel
{
    /// <summary>Detailed diagnostic trace. Written only when verbose logging is enabled.</summary>
    Debug,

    /// <summary>Key lifecycle event (unlock, save, import…). Always written.</summary>
    Info,

    /// <summary>Non-blocking anomaly. Always written.</summary>
    Warn,

    /// <summary>Error or unhandled exception. Always written.</summary>
    Error,
}

/// <summary>
/// Canonical area names used as the third column of every log line. Using constants
/// instead of free-form strings keeps the log greppable (e.g. search "Detail" to follow
/// a whole editing session) and prevents typos from fragmenting the output.
/// </summary>
public static class LogArea
{
    public const string App = "App";
    public const string Vault = "Vault";
    public const string Nav = "Nav";
    public const string List = "List";
    public const string Detail = "Detail";
    public const string Command = "Command";
    public const string Persist = "Persist";
    public const string Dialog = "Dialog";
    public const string Toast = "Toast";
    public const string Ipc = "Ipc";
    public const string Settings = "Settings";
    public const string Import = "Import";
    public const string Backup = "Backup";
}

/// <summary>
/// Writes a per-session diagnostic log to disk so problems can be diagnosed from a file
/// instead of by interrogating the user.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two levels of detail.</b> <see cref="LogLevel.Info"/>, <see cref="LogLevel.Warn"/> and
/// <see cref="LogLevel.Error"/> are <i>always</i> written (a handful of lines per session), so
/// the first error a user hits is never lost. <see cref="LogLevel.Debug"/> is written only when
/// <see cref="IsVerboseEnabled"/> is on — the switch lives in Settings and takes effect immediately.
/// </para>
/// <para>
/// <b>Location.</b> Preferred: a <c>logs</c> folder next to the executable, so a user can find and
/// attach it without knowing anything about Windows. When the app is installed under Program Files
/// that folder is not writable, so the service silently falls back to
/// <c>%LocalAppData%\PassKey\logs</c>. The resolved path is always exposed via
/// <see cref="LogDirectory"/> and surfaced in Settings — the user must never have to guess.
/// </para>
/// <para>
/// <b>Never throws, never blocks.</b> Entries are queued and written by a single background
/// worker; any I/O failure is swallowed. Logging must not be able to break or slow the app.
/// </para>
/// <para>
/// <b>🔒 Never log secrets.</b> Log files are plain text, meant to be attached to bug reports.
/// Master password, entry passwords, PAN, CVV, PIN, TOTP seeds, note contents, identity fields
/// and IPC key material must never reach this service. Log ids, types, outcomes and durations.
/// </para>
/// </remarks>
public interface ILogService
{
    /// <summary>Gets a value indicating whether <see cref="LogLevel.Debug"/> entries are written.</summary>
    bool IsVerboseEnabled { get; }

    /// <summary>Gets the folder holding the log files. Always valid after <see cref="Initialize"/>.</summary>
    string LogDirectory { get; }

    /// <summary>Gets the full path of the current session's log file, or <see langword="null"/> if unavailable.</summary>
    string? CurrentLogFile { get; }

    /// <summary>
    /// Gets a value indicating whether logs are being written to the fallback location because
    /// the folder next to the executable is not writable. Drives the explanatory note in Settings.
    /// </summary>
    bool IsUsingFallbackLocation { get; }

    /// <summary>
    /// Resolves the log folder, opens this session's file, writes the header and purges old
    /// sessions. Called once at startup, before any other service runs.
    /// </summary>
    /// <param name="verboseEnabled">Initial state of the verbose switch, from settings.</param>
    void Initialize(bool verboseEnabled);

    /// <summary>Turns verbose (Debug) logging on or off at runtime. Takes effect immediately.</summary>
    void SetVerbose(bool enabled);

    /// <summary>Writes a <see cref="LogLevel.Debug"/> entry (verbose only).</summary>
    void Debug(string area, string message, string? data = null);

    /// <summary>Writes a <see cref="LogLevel.Info"/> entry (always).</summary>
    void Info(string area, string message, string? data = null);

    /// <summary>Writes a <see cref="LogLevel.Warn"/> entry (always).</summary>
    void Warn(string area, string message, string? data = null);

    /// <summary>Writes a <see cref="LogLevel.Error"/> entry (always), including the exception detail.</summary>
    void Error(string area, string message, Exception? exception = null, string? data = null);

    /// <summary>Deletes every log file in <see cref="LogDirectory"/> except the current session's.</summary>
    /// <returns>The number of files deleted.</returns>
    int DeleteAllLogs();

    /// <summary>Waits for the queue to drain. Called on shutdown so the tail of the session is not lost.</summary>
    Task FlushAsync();
}
