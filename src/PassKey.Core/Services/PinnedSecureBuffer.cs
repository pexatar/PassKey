using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PassKey.Core.Services;

/// <summary>
/// A pinned byte buffer that prevents GC relocation and guarantees
/// deterministic zeroing on disposal. Used to hold key material such as the
/// Data Encryption Key (DEK) while the vault is unlocked.
/// </summary>
/// <remarks>
/// <para>
/// The .NET GC may relocate managed byte arrays during compacting collections,
/// potentially leaving copies of sensitive data scattered in memory.
/// <see cref="GCHandle.Alloc(object, GCHandleType)"/> with <see cref="GCHandleType.Pinned"/>
/// instructs the GC to keep the array at a fixed address for its lifetime.
/// </para>
/// <para>
/// On <see cref="Dispose"/>, <see cref="CryptographicOperations.ZeroMemory"/> is called
/// before the GC handle is released. This ensures the key bytes are overwritten with zeros
/// even if the JIT would otherwise optimise away a plain <c>Array.Clear</c>.
/// The handle is then freed, allowing the GC to reclaim the pinned array.
/// </para>
/// <para>
/// Callers must always dispose this object (preferably via <c>using</c>) immediately
/// after the key is no longer needed. Failing to dispose leaves DEK bytes in memory
/// for an indeterminate period.
/// </para>
/// </remarks>
public sealed class PinnedSecureBuffer : IDisposable
{
    private readonly byte[] _buffer;
    private readonly GCHandle _handle;
    private bool _disposed;

    /// <summary>
    /// Allocates and pins a zeroed buffer of the specified size.
    /// </summary>
    /// <param name="size">Number of bytes to allocate. Must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="size"/> is zero or negative.</exception>
    public PinnedSecureBuffer(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        _buffer = new byte[size];
        _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
    }

    /// <summary>Gets the number of bytes in the buffer.</summary>
    public int Length => _buffer.Length;

    /// <summary>
    /// Gets a writable <see cref="Span{T}"/> over the pinned buffer.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if this instance has been disposed.</exception>
    public Span<byte> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffer.AsSpan();
        }
    }

    /// <summary>
    /// Gets a read-only <see cref="ReadOnlySpan{T}"/> over the pinned buffer.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if this instance has been disposed.</exception>
    public ReadOnlySpan<byte> ReadOnlySpan
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffer.AsSpan();
        }
    }

    /// <summary>
    /// Copies data from <paramref name="source"/> into the pinned buffer.
    /// </summary>
    /// <param name="source">The byte span to copy from. Must not exceed <see cref="Length"/>.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="source"/> is larger than the buffer.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this instance has been disposed.</exception>
    public void Write(ReadOnlySpan<byte> source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (source.Length > _buffer.Length)
            throw new ArgumentException("Source exceeds buffer size.");
        source.CopyTo(_buffer);
    }

    /// <summary>
    /// Zeroes all bytes in the buffer using <see cref="CryptographicOperations.ZeroMemory"/>,
    /// then frees the GC pin and marks this instance as disposed.
    /// This method is safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CryptographicOperations.ZeroMemory(_buffer);

        if (_handle.IsAllocated)
            _handle.Free();
    }
}
