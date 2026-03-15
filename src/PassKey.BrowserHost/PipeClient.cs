using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;

namespace PassKey.BrowserHost;

/// <summary>
/// Named Pipe client that connects to the PassKey Desktop IPC server on
/// <c>\\.\pipe\PassKey.IPC</c>. Uses the same length-prefixed wire format as
/// the Native Messaging protocol: [4-byte little-endian length][UTF-8 JSON payload].
/// </summary>
internal sealed class PipeClient : IDisposable
{
    private const string PipeName = "PassKey.IPC";
    private const int DefaultTimeoutMs = 3000;
    private const int MaxMessageSize = 1024 * 1024; // 1 MB
    private const int LengthPrefixSize = 4;

    private NamedPipeClientStream? _pipe;

    /// <summary>
    /// Gets a value indicating whether the pipe is currently connected to the Desktop server.
    /// </summary>
    public bool IsConnected => _pipe?.IsConnected == true;

    /// <summary>
    /// Connects to the PassKey Desktop Named Pipe server.
    /// Disposes any existing connection before creating a new one.
    /// </summary>
    /// <param name="timeoutMs">
    /// Maximum time in milliseconds to wait for the server to accept the connection.
    /// Default is 3000 ms.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the connection was established; false if it timed out or the server is unavailable.</returns>
    public async Task<bool> ConnectAsync(int timeoutMs = DefaultTimeoutMs, CancellationToken ct = default)
    {
        Disconnect();

        _pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            await _pipe.ConnectAsync(timeoutMs, ct);
            return true;
        }
        catch (TimeoutException)
        {
            Disconnect();
            return false;
        }
        catch (IOException)
        {
            Disconnect();
            return false;
        }
    }

    /// <summary>
    /// Sends a JSON message to the Desktop server and waits for the response.
    /// The message is framed with a 4-byte little-endian length prefix before sending,
    /// and the response is read using the same framing.
    /// </summary>
    /// <param name="json">The JSON request string to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The JSON response string from the Desktop server, or null if the connection was lost
    /// or the pipe was not connected.
    /// </returns>
    public async Task<string?> SendAsync(string json, CancellationToken ct = default)
    {
        if (_pipe is null || !_pipe.IsConnected)
            return null;

        try
        {
            // Write: [4-byte LE length][UTF-8 JSON]
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var lengthBuffer = new byte[LengthPrefixSize];
            BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, jsonBytes.Length);

            await _pipe.WriteAsync(lengthBuffer, ct);
            await _pipe.WriteAsync(jsonBytes, ct);
            await _pipe.FlushAsync(ct);

            // Read response: [4-byte LE length][UTF-8 JSON]
            var responseLengthBuffer = new byte[LengthPrefixSize];
            var bytesRead = await ReadExactAsync(_pipe, responseLengthBuffer, ct);
            if (bytesRead < LengthPrefixSize)
                return null;

            var responseLength = BinaryPrimitives.ReadInt32LittleEndian(responseLengthBuffer);
            if (responseLength <= 0 || responseLength > MaxMessageSize)
                return null;

            var responseBuffer = new byte[responseLength];
            bytesRead = await ReadExactAsync(_pipe, responseBuffer, ct);
            if (bytesRead < responseLength)
                return null;

            return Encoding.UTF8.GetString(responseBuffer);
        }
        catch (IOException)
        {
            Disconnect();
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Closes and disposes the current pipe connection. Safe to call when not connected.
    /// </summary>
    public void Disconnect()
    {
        if (_pipe is not null)
        {
            try { _pipe.Dispose(); } catch { /* ignore */ }
            _pipe = null;
        }
    }

    /// <summary>
    /// Disconnects and releases the pipe resources.
    /// </summary>
    public void Dispose() => Disconnect();

    /// <summary>
    /// Reads exactly <c>buffer.Length</c> bytes from <paramref name="stream"/>,
    /// handling partial reads. Returns the total bytes read (may be less than requested on EOF).
    /// </summary>
    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            if (read == 0) return totalRead;
            totalRead += read;
        }
        return totalRead;
    }
}
