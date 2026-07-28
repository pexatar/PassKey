using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace PassKey.Desktop.Services;

/// <summary>
/// Default <see cref="ILogService"/>: one file per session, written by a single background
/// worker draining a lock-free queue.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a queue instead of writing inline.</b> Log calls happen on the UI thread during
/// navigation, editing and saving. Touching the disk there would add latency to every
/// interaction and could block on a slow or contended volume. Producers only enqueue a
/// pre-formatted string; a single consumer owns the file, which also removes any need for
/// locking around the writer.
/// </para>
/// <para>
/// <b>Why it never throws.</b> Diagnostics must not be able to break the thing they diagnose.
/// Every I/O path is wrapped; on failure the entry is dropped silently.
/// </para>
/// </remarks>
public sealed class LogService : ILogService
{
    /// <summary>Sessions kept on disk; older files are purged at startup. Decided with the user.</summary>
    private const int MaxSessionsKept = 20;

    /// <summary>Size cap per file; beyond it the writer rolls over to a "_2", "_3"… suffix.</summary>
    private const long MaxFileBytes = 10 * 1024 * 1024;

    private const string FilePrefix = "passkey_";
    private const string FileExtension = ".log";

    private readonly ConcurrentQueue<string> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _shutdown = new();

    private Task? _worker;
    private string? _currentFile;
    private int _rollIndex = 1;
    private long _bytesWritten;
    private volatile bool _verbose;
    private volatile bool _initialised;

    /// <inheritdoc/>
    public bool IsVerboseEnabled => _verbose;

    /// <inheritdoc/>
    public string LogDirectory { get; private set; } = string.Empty;

    /// <inheritdoc/>
    public string? CurrentLogFile => _currentFile;

    /// <inheritdoc/>
    public bool IsUsingFallbackLocation { get; private set; }

    /// <inheritdoc/>
    public void Initialize(bool verboseEnabled)
    {
        if (_initialised) return;
        _initialised = true;
        _verbose = verboseEnabled;

        try
        {
            LogDirectory = ResolveLogDirectory(out var usedFallback);
            IsUsingFallbackLocation = usedFallback;

            _currentFile = Path.Combine(LogDirectory, BuildFileName(_rollIndex));
            _worker = Task.Run(WriteLoopAsync);

            WriteHeader();
            PurgeOldSessions();
        }
        catch
        {
            // Logging must never prevent the app from starting.
            _currentFile = null;
        }
    }

    /// <summary>
    /// Picks the log folder: next to the executable when writable, otherwise %LocalAppData%.
    /// </summary>
    /// <remarks>
    /// The folder beside the executable is preferred because it is where a user naturally looks
    /// and where the portable build always lives. When PassKey is installed under Program Files
    /// that location is read-only for a standard user — historically this made log files silently
    /// never appear. Writability is therefore proven with a real write, not inferred from ACLs,
    /// because inspecting permissions gives the wrong answer under UAC virtualisation.
    /// </remarks>
    private static string ResolveLogDirectory(out bool usedFallback)
    {
        var exeDir = AppContext.BaseDirectory;
        var preferred = Path.Combine(exeDir, "logs");

        if (TryPrepareDirectory(preferred))
        {
            usedFallback = false;
            return preferred;
        }

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PassKey",
            "logs");

        usedFallback = true;
        TryPrepareDirectory(fallback);
        return fallback;
    }

    /// <summary>Creates the folder and proves it is writable by writing and deleting a probe file.</summary>
    private static bool TryPrepareDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, ".writetest");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildFileName(int rollIndex)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var pid = Environment.ProcessId;
        var suffix = rollIndex > 1 ? $"_{rollIndex}" : string.Empty;
        return $"{FilePrefix}{stamp}_{pid}{suffix}{FileExtension}";
    }

    private void WriteHeader()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        var location = IsUsingFallbackLocation
            ? "fallback (%LocalAppData%) — folder next to the executable is not writable"
            : "preferred (next to the executable)";

        Enqueue(LogLevel.Info, LogArea.App, "=== PassKey session start ===",
            $"version={version} os={Environment.OSVersion.VersionString} arch={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} culture={CultureInfo.CurrentUICulture.Name} verbose={_verbose}");
        Enqueue(LogLevel.Info, LogArea.App, "Log location resolved", $"path=\"{LogDirectory}\" mode={location}");
    }

    /// <summary>Keeps the newest <see cref="MaxSessionsKept"/> sessions and deletes the rest.</summary>
    private void PurgeOldSessions()
    {
        try
        {
            var files = new DirectoryInfo(LogDirectory)
                .GetFiles($"{FilePrefix}*{FileExtension}")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(MaxSessionsKept)
                .ToList();

            foreach (var file in files)
            {
                try { file.Delete(); } catch { /* a locked file is not worth failing over */ }
            }

            if (files.Count > 0)
                Enqueue(LogLevel.Debug, LogArea.App, "Old log sessions purged", $"deleted={files.Count} kept={MaxSessionsKept}");
        }
        catch
        {
            // Purging is housekeeping: never let it surface.
        }
    }

    /// <inheritdoc/>
    public void SetVerbose(bool enabled)
    {
        if (_verbose == enabled) return;
        _verbose = enabled;
        // Logged at Info so the transition is visible even in a non-verbose file.
        Enqueue(LogLevel.Info, LogArea.Settings, "Verbose logging changed", $"enabled={enabled}");
    }

    /// <inheritdoc/>
    public void Debug(string area, string message, string? data = null)
    {
        if (!_verbose) return;
        Enqueue(LogLevel.Debug, area, message, data);
    }

    /// <inheritdoc/>
    public void Info(string area, string message, string? data = null)
        => Enqueue(LogLevel.Info, area, message, data);

    /// <inheritdoc/>
    public void Warn(string area, string message, string? data = null)
        => Enqueue(LogLevel.Warn, area, message, data);

    /// <inheritdoc/>
    public void Error(string area, string message, Exception? exception = null, string? data = null)
    {
        var detail = exception is null
            ? data
            : $"{(data is null ? string.Empty : data + " ")}exception={exception.GetType().Name} message=\"{Sanitise(exception.Message)}\"";

        Enqueue(LogLevel.Error, area, message, detail);

        if (exception is not null)
            Enqueue(LogLevel.Error, area, "Stack trace", exception.StackTrace?.Replace(Environment.NewLine, " ⏎ "));
    }

    /// <summary>Formats one line and hands it to the writer. Cheap enough for the UI thread.</summary>
    private void Enqueue(LogLevel level, string area, string message, string? data)
    {
        if (!_initialised) return;

        try
        {
            var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
            var line = new StringBuilder(160)
                .Append(timestamp).Append(" | ")
                .Append(level.ToString().ToUpperInvariant().PadRight(5)).Append(" | ")
                .Append(area.PadRight(8)).Append(" | ")
                .Append(Sanitise(message));

            if (!string.IsNullOrEmpty(data))
                line.Append(" | ").Append(Sanitise(data));

            _queue.Enqueue(line.ToString());
            _signal.Release();
        }
        catch
        {
            // A log line is never worth an exception.
        }
    }

    /// <summary>Strips newlines so one entry is always exactly one line (keeps the file greppable).</summary>
    private static string Sanitise(string? value)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\r", string.Empty).Replace("\n", " ⏎ ");

    /// <summary>
    /// Single consumer: owns the file handle, so no locking is needed around the writer.
    /// Drains everything currently queued per wake-up to avoid one syscall per line.
    /// </summary>
    private async Task WriteLoopAsync()
    {
        var buffer = new StringBuilder(4096);

        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(_shutdown.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            buffer.Clear();
            while (_queue.TryDequeue(out var line))
                buffer.AppendLine(line);

            if (buffer.Length > 0)
                await AppendAsync(buffer.ToString()).ConfigureAwait(false);
        }

        // Final drain so the tail of the session survives shutdown.
        buffer.Clear();
        while (_queue.TryDequeue(out var line))
            buffer.AppendLine(line);
        if (buffer.Length > 0)
            await AppendAsync(buffer.ToString()).ConfigureAwait(false);
    }

    private async Task AppendAsync(string text)
    {
        if (_currentFile is null) return;

        try
        {
            RollOverIfNeeded(text.Length);
            await File.AppendAllTextAsync(_currentFile, text, Encoding.UTF8).ConfigureAwait(false);
            _bytesWritten += text.Length;
        }
        catch
        {
            // Disk full, file locked, folder removed mid-session: drop the entry.
        }
    }

    /// <summary>Starts a new numbered file once the current one exceeds the size cap.</summary>
    private void RollOverIfNeeded(int incomingLength)
    {
        if (_bytesWritten + incomingLength <= MaxFileBytes) return;

        _rollIndex++;
        _currentFile = Path.Combine(LogDirectory, BuildFileName(_rollIndex));
        _bytesWritten = 0;
    }

    /// <inheritdoc/>
    public int DeleteAllLogs()
    {
        var deleted = 0;
        try
        {
            foreach (var file in Directory.GetFiles(LogDirectory, $"{FilePrefix}*{FileExtension}"))
            {
                if (string.Equals(file, _currentFile, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(file); deleted++; } catch { /* in use: skip */ }
            }

            Info(LogArea.Settings, "Log files deleted by user", $"deleted={deleted}");
        }
        catch
        {
            // Nothing useful to do.
        }

        return deleted;
    }

    /// <inheritdoc/>
    public async Task FlushAsync()
    {
        if (_worker is null) return;

        _signal.Release();
        // Give the worker a moment to drain; never block shutdown indefinitely.
        await Task.WhenAny(_worker, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
    }
}
