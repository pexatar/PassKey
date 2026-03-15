using System.Text.Json;
using PassKey.BrowserHost;
using PassKey.BrowserHost.Models;

/// <summary>
/// PassKey Native Messaging Host entry point.
/// This process acts as a bridge between the browser extension and the PassKey Desktop app.
/// The browser launches this process on demand and communicates via stdin/stdout using the
/// Chrome/Firefox Native Messaging protocol.
///
/// <para>
/// <b>Wire protocol (stdin/stdout):</b>
/// Each message consists of a 4-byte little-endian length prefix followed by a UTF-8 JSON payload.
/// The loop reads messages from stdin, forwards them to PassKey Desktop via a Named Pipe
/// (<c>PassKey.IPC</c>), and writes the Desktop response back to stdout.
/// The loop terminates when stdin reaches EOF (browser closed the connection).
/// </para>
///
/// <para>
/// <b>Error handling:</b>
/// If the Desktop pipe is unavailable or a reconnection attempt fails, a structured error
/// response (JSON) is written to stdout so the extension can display an appropriate message.
/// The process exits with code 0 on clean shutdown and 1 on unexpected errors.
/// </para>
/// </summary>
public static class Program
{
    /// <summary>
    /// Application entry point. Reads Native Messaging messages from stdin,
    /// forwards them to the PassKey Desktop Named Pipe, and writes responses to stdout.
    /// </summary>
    /// <param name="args">Command-line arguments (not used).</param>
    /// <returns>Exit code: 0 for clean shutdown, 1 for unexpected errors.</returns>
    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();
        using var pipeClient = new PipeClient();

        try
        {
            // Main message loop: read from browser, forward to desktop, respond
            while (!cts.Token.IsCancellationRequested)
            {
                // Read message from browser extension via Native Messaging protocol
                // Format: [4-byte LE uint32 length][UTF-8 JSON payload]
                var message = await NativeMessagingProtocol.ReadMessageAsync(stdin, cts.Token);
                if (message is null)
                    break; // EOF — browser closed the connection

                // Parse the envelope to extract action and requestId for error responses
                var action = "unknown";
                var requestId = "";
                try
                {
                    var envelope = JsonSerializer.Deserialize(message, IpcJsonContext.Default.IpcRequest);
                    if (envelope is not null)
                    {
                        action = envelope.Action;
                        requestId = envelope.RequestId;
                    }
                }
                catch
                {
                    // If we can't parse the envelope, we still try to forward it
                }

                string responseJson;

                // Ensure pipe connection to Desktop
                if (!pipeClient.IsConnected)
                {
                    var connected = await pipeClient.ConnectAsync(ct: cts.Token);
                    if (!connected)
                    {
                        // Desktop not running — send error response
                        var errorResponse = IpcResponse.DesktopNotRunning(action, requestId);
                        responseJson = JsonSerializer.Serialize(errorResponse, IpcJsonContext.Default.IpcResponse);
                        await NativeMessagingProtocol.WriteMessageAsync(stdout, responseJson, cts.Token);
                        continue;
                    }
                }

                // Forward message to Desktop via Named Pipe
                var pipeResponse = await pipeClient.SendAsync(message, cts.Token);

                if (pipeResponse is null)
                {
                    // Pipe disconnected — try to reconnect once
                    var reconnected = await pipeClient.ConnectAsync(ct: cts.Token);
                    if (reconnected)
                    {
                        pipeResponse = await pipeClient.SendAsync(message, cts.Token);
                    }

                    if (pipeResponse is null)
                    {
                        var errorResponse = IpcResponse.CreateError(action, requestId, "desktop-connection-lost");
                        responseJson = JsonSerializer.Serialize(errorResponse, IpcJsonContext.Default.IpcResponse);
                        await NativeMessagingProtocol.WriteMessageAsync(stdout, responseJson, cts.Token);
                        continue;
                    }
                }

                // Forward Desktop response back to browser extension
                await NativeMessagingProtocol.WriteMessageAsync(stdout, pipeResponse, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception)
        {
            // Unexpected error — exit cleanly
            return 1;
        }

        return 0;
    }
}
