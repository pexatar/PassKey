namespace PassKey.Desktop.Services;

/// <summary>
/// Named Pipe IPC server for browser extension communication.
/// Listens on \\.\pipe\PassKey.IPC with ACL restricted to current user.
/// </summary>
public interface IBrowserIpcService : IDisposable
{
    /// <summary>Whether the pipe server is currently running.</summary>
    bool IsRunning { get; }

    /// <summary>Starts the Named Pipe server in the background.</summary>
    Task StartAsync();

    /// <summary>Stops the Named Pipe server.</summary>
    Task StopAsync();
}
