using System.Buffers.Binary;
using System.Text;

namespace PassKey.BrowserHost;

/// <summary>
/// Chrome/Firefox Native Messaging Protocol implementation.
/// Implements the wire format specified at
/// <see href="https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging"/>.
///
/// <para>
/// <b>Wire format:</b> Each message consists of a 4-byte little-endian unsigned integer
/// giving the byte length of the JSON payload, followed immediately by the UTF-8 encoded JSON.
/// Maximum message size is 1 MB (enforced to prevent DoS via oversized messages).
/// </para>
/// </summary>
internal static class NativeMessagingProtocol
{
    private const int MaxMessageSize = 1024 * 1024; // 1 MB
    private const int LengthPrefixSize = 4;

    /// <summary>
    /// Reads a single Native Messaging message from the input stream.
    /// </summary>
    /// <param name="input">The stream to read from (typically stdin).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The UTF-8 decoded JSON payload string, or null if the stream is at EOF
    /// (browser closed the connection).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the length prefix is incomplete, the message size exceeds 1 MB,
    /// or the payload is shorter than the declared length.
    /// </exception>
    public static async Task<string?> ReadMessageAsync(Stream input, CancellationToken ct = default)
    {
        // Read 4-byte length prefix (little-endian)
        var lengthBuffer = new byte[LengthPrefixSize];
        var bytesRead = await ReadExactAsync(input, lengthBuffer, ct);
        if (bytesRead == 0)
            return null; // EOF — browser closed the connection

        if (bytesRead < LengthPrefixSize)
            throw new InvalidOperationException("Incomplete length prefix received.");

        var messageLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);

        // DoS protection: reject oversized messages
        if (messageLength <= 0 || messageLength > MaxMessageSize)
            throw new InvalidOperationException($"Message size {messageLength} exceeds maximum {MaxMessageSize} bytes.");

        // Read the JSON payload
        var messageBuffer = new byte[messageLength];
        bytesRead = await ReadExactAsync(input, messageBuffer, ct);
        if (bytesRead < messageLength)
            throw new InvalidOperationException($"Incomplete message: expected {messageLength} bytes, got {bytesRead}.");

        return Encoding.UTF8.GetString(messageBuffer);
    }

    /// <summary>
    /// Writes a single Native Messaging message to the output stream and flushes.
    /// Encodes <paramref name="json"/> as UTF-8, prepends the 4-byte little-endian length,
    /// and writes both to <paramref name="output"/>.
    /// </summary>
    /// <param name="output">The stream to write to (typically stdout).</param>
    /// <param name="json">The JSON string to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the serialized JSON exceeds 1 MB.
    /// </exception>
    public static async Task WriteMessageAsync(Stream output, string json, CancellationToken ct = default)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        if (jsonBytes.Length > MaxMessageSize)
            throw new InvalidOperationException($"Response size {jsonBytes.Length} exceeds maximum {MaxMessageSize} bytes.");

        // Write 4-byte length prefix (little-endian)
        var lengthBuffer = new byte[LengthPrefixSize];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, jsonBytes.Length);

        await output.WriteAsync(lengthBuffer, ct);
        await output.WriteAsync(jsonBytes, ct);
        await output.FlushAsync(ct);
    }

    /// <summary>
    /// Reads exactly the requested number of bytes from <paramref name="stream"/>,
    /// handling partial reads that may occur on pipes and network streams.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer to fill.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The total number of bytes read. Returns 0 on immediate EOF;
    /// returns less than <c>buffer.Length</c> if EOF is reached before the buffer is full.
    /// </returns>
    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            if (read == 0)
                return totalRead; // EOF
            totalRead += read;
        }
        return totalRead;
    }
}
