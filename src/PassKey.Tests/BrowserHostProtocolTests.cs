using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using PassKey.BrowserHost;
using PassKey.BrowserHost.Models;

namespace PassKey.Tests;

/// <summary>
/// Tests for the Native Messaging protocol wire format and IPC models.
/// Tests are pure .NET, no WinUI 3 or IPC server dependency required.
/// </summary>
public class BrowserHostProtocolTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Test 1 — Round-trip: write a message, read it back from the same stream
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NativeMessaging_WriteRead_RoundTrip()
    {
        const string payload = """{"action":"get-credentials","requestId":"abc123"}""";

        using var stream = new MemoryStream();
        await NativeMessagingProtocol.WriteMessageAsync(stream, payload);

        stream.Position = 0;
        var result = await NativeMessagingProtocol.ReadMessageAsync(stream);

        Assert.Equal(payload, result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 2 — Zero-length / negative length prefix → exception
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NativeMessaging_ZeroLengthPrefix_ThrowsInvalidOperation()
    {
        using var stream = new MemoryStream();

        // Write a 4-byte LE length of 0
        var buf = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, 0);
        await stream.WriteAsync(buf);

        stream.Position = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NativeMessagingProtocol.ReadMessageAsync(stream));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 3 — Message > 1 MB length prefix → exception (DoS guard)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NativeMessaging_ExceedsMaxSize_ThrowsInvalidOperation()
    {
        using var stream = new MemoryStream();

        // Write a 4-byte LE length of 1 MB + 1
        var buf = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, 1024 * 1024 + 1);
        await stream.WriteAsync(buf);

        stream.Position = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NativeMessagingProtocol.ReadMessageAsync(stream));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 4 — Wire format: verifies 4-byte LE length prefix + UTF-8 payload
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NativeMessaging_BinaryFormat_LengthPrefixCorrect()
    {
        const string payload = """{"action":"ping"}""";
        var expectedBytes = Encoding.UTF8.GetBytes(payload);

        using var stream = new MemoryStream();
        await NativeMessagingProtocol.WriteMessageAsync(stream, payload);

        var written = stream.ToArray();

        // First 4 bytes: LE int32 = payload length
        Assert.True(written.Length >= 4);
        var length = BinaryPrimitives.ReadInt32LittleEndian(written.AsSpan(0, 4));
        Assert.Equal(expectedBytes.Length, length);

        // Remaining bytes: UTF-8 encoded payload
        Assert.Equal(expectedBytes, written[4..]);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 5 — IpcRequest / IpcResponse JSON round-trip via source-gen context
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("get-credentials", "req-001", true)]
    [InlineData("unlock-vault",    "req-002", false)]
    [InlineData("get-all-credentials", "req-003", true)]
    [InlineData("show-window",     "req-004", true)]
    [InlineData("unknown",         "req-005", false)]
    public void IpcModels_JsonRoundTrip_AllActionTypes(
        string action, string requestId, bool success)
    {
        // IpcRequest round-trip
        var request = new IpcRequest
        {
            Version   = 1,
            Action    = action,
            RequestId = requestId,
            ClientId  = "chrome-ext"
        };
        var requestJson = JsonSerializer.Serialize(request, IpcJsonContext.Default.IpcRequest);
        var requestRestored = JsonSerializer.Deserialize(requestJson, IpcJsonContext.Default.IpcRequest);

        Assert.NotNull(requestRestored);
        Assert.Equal(request.Action, requestRestored!.Action);
        Assert.Equal(request.RequestId, requestRestored.RequestId);
        Assert.Equal(request.ClientId, requestRestored.ClientId);
        Assert.Equal(request.Version, requestRestored.Version);

        // IpcResponse round-trip
        var response = new IpcResponse
        {
            Version   = 1,
            Action    = action,
            RequestId = requestId,
            Success   = success,
            Error     = success ? null : "desktop-not-running"
        };
        var responseJson = JsonSerializer.Serialize(response, IpcJsonContext.Default.IpcResponse);
        var responseRestored = JsonSerializer.Deserialize(responseJson, IpcJsonContext.Default.IpcResponse);

        Assert.NotNull(responseRestored);
        Assert.Equal(response.Action, responseRestored!.Action);
        Assert.Equal(response.RequestId, responseRestored.RequestId);
        Assert.Equal(response.Success, responseRestored.Success);
        Assert.Equal(response.Error, responseRestored.Error);
    }
}
