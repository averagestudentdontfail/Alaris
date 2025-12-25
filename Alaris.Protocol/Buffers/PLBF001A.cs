// =============================================================================
// PLBF001A.cs - Buffer Pool Manager for Zero-Allocation Serialization
// Component: PLBF001A | Category: Buffers | Variant: A (Primary)
// =============================================================================
// References:
// - Alaris.Governance/Coding.md Rule 5 (Zero-Allocation Hot Paths)
// - ArrayPool<T> pattern for buffer reuse
// =============================================================================

using System.Buffers;

namespace Alaris.Protocol.Buffers;

/// <summary>
/// Thread-safe buffer pool manager for zero-allocation binary serialization.
/// Component ID: PLBF001A
/// </summary>
/// <remarks>
/// Rule 5 (Zero-Allocation Hot Paths): This class provides reusable byte buffers
/// to avoid heap allocations during message encoding/decoding. All hot path 
/// serialization operations should rent buffers from this pool.
/// 
/// Usage:
/// <code>
/// using var buffer = PLBF001A.RentBuffer(4096);
/// int bytesWritten = encoder.Encode(message, buffer.Span);
/// // buffer is automatically returned to pool on dispose
/// </code>
/// </remarks>
public static class PLBF001A
{
    /// <summary>
    /// Default buffer size for most market data messages (4KB).
    /// Sufficient for individual OptionContract, PriceBar, EarningsEvent.
    /// </summary>
    public const int DefaultBufferSize = 4096;

    /// <summary>
    /// Large buffer size for option chain snapshots (64KB).
    /// Accommodates chains with ~500+ contracts.
    /// </summary>
    public const int LargeBufferSize = 65536;

    /// <summary>
    /// Maximum buffer size to prevent excessive memory usage (1MB).
    /// </summary>
    public const int MaxBufferSize = 1024 * 1024;

    /// <summary>
    /// Rents a buffer from the shared ArrayPool.
    /// </summary>
    /// <param name="minimumLength">Minimum required buffer size.</param>
    /// <returns>A disposable buffer handle that returns the buffer to the pool on dispose.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If minimumLength exceeds MaxBufferSize.</exception>
    public static PooledBuffer RentBuffer(int minimumLength = DefaultBufferSize)
    {
        if (minimumLength <= 0 || minimumLength > MaxBufferSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumLength),
                $"Buffer size must be between 1 and {MaxBufferSize} bytes.");
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(minimumLength);
        return new PooledBuffer(buffer, minimumLength);
    }

    /// <summary>
    /// Rents a large buffer suitable for option chain snapshots.
    /// </summary>
    /// <returns>A disposable buffer handle.</returns>
    public static PooledBuffer RentLargeBuffer() => RentBuffer(LargeBufferSize);
}

/// <summary>
/// Disposable wrapper for a pooled byte array buffer.
/// </summary>
/// <remarks>
/// This struct ensures the buffer is returned to the pool when disposed,
/// preventing memory leaks and reducing GC pressure.
/// </remarks>
public readonly struct PooledBuffer : IDisposable
{
    private readonly byte[] _buffer;
    private readonly int _requestedLength;

    internal PooledBuffer(byte[] buffer, int requestedLength)
    {
        _buffer = buffer;
        _requestedLength = requestedLength;
    }

    /// <summary>
    /// Gets the underlying byte array. Length may be >= requested size.
    /// </summary>
    public byte[] Array => _buffer;

    /// <summary>
    /// Gets a span over the usable portion of the buffer.
    /// </summary>
    public Span<byte> Span => _buffer.AsSpan(0, _requestedLength);

    /// <summary>
    /// Gets a memory over the usable portion of the buffer.
    /// </summary>
    public Memory<byte> Memory => _buffer.AsMemory(0, _requestedLength);

    /// <summary>
    /// Gets the requested (usable) length of the buffer.
    /// </summary>
    public int Length => _requestedLength;

    /// <summary>
    /// Returns the buffer to the shared pool.
    /// </summary>
    public void Dispose()
    {
        if (_buffer != null)
        {
            ArrayPool<byte>.Shared.Return(_buffer, clearArray: false);
        }
    }
}
