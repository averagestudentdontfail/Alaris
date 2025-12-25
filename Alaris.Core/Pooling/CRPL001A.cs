// =============================================================================
// CRPL001A.cs - Object Pool Manager
// Component: CR (Core) | Category: PL (Pooling) | Variant: A (Primary)
// =============================================================================
// ArrayPool-based pooling for hot path allocations.
// Provides rent/return semantics for Greeks buffers and pricing contexts.
// =============================================================================
// Governance: Rule 5 (Zero-Allocation Hot Paths), Rule 19 (Hot Path Standards)
// =============================================================================

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Alaris.Core.Pooling;

/// <summary>
/// Object pool manager for hot path allocations.
/// Uses ArrayPool for buffer management and thread-safe pooling.
/// </summary>
public static class CRPL001A
{
    /// <summary>
    /// Shared pool for double arrays (Greeks, prices, etc.).
    /// </summary>
    public static ArrayPool<double> DoublePool { get; } = ArrayPool<double>.Shared;

    /// <summary>
    /// Shared pool for int arrays (indices, counts, etc.).
    /// </summary>
    public static ArrayPool<int> IntPool { get; } = ArrayPool<int>.Shared;

    /// <summary>
    /// Shared pool for byte arrays (serialization buffers).
    /// </summary>
    public static ArrayPool<byte> BytePool { get; } = ArrayPool<byte>.Shared;

    /// <summary>
    /// Rents a double array of at least the specified length.
    /// </summary>
    /// <param name="minimumLength">Minimum array length required.</param>
    /// <returns>Pooled array (may be larger than requested).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double[] RentDoubles(int minimumLength)
    {
        return DoublePool.Rent(minimumLength);
    }

    /// <summary>
    /// Returns a double array to the pool.
    /// </summary>
    /// <param name="array">Array to return.</param>
    /// <param name="clearArray">Whether to clear the array before returning.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReturnDoubles(double[] array, bool clearArray = false)
    {
        if (array is not null)
        {
            DoublePool.Return(array, clearArray);
        }
    }

    /// <summary>
    /// Rents a byte array for serialization buffers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] RentBytes(int minimumLength)
    {
        return BytePool.Rent(minimumLength);
    }

    /// <summary>
    /// Returns a byte array to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReturnBytes(byte[] array, bool clearArray = false)
    {
        if (array is not null)
        {
            BytePool.Return(array, clearArray);
        }
    }

    /// <summary>
    /// Executes an action with a rented double buffer, automatically returning it.
    /// </summary>
    /// <typeparam name="TResult">Return type of the action.</typeparam>
    /// <param name="minimumLength">Minimum buffer length.</param>
    /// <param name="action">Action to execute with the buffer.</param>
    /// <returns>Result of the action.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult WithDoubleBuffer<TResult>(
        int minimumLength,
        Func<double[], TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        double[] buffer = RentDoubles(minimumLength);
        try
        {
            return action(buffer);
        }
        finally
        {
            ReturnDoubles(buffer);
        }
    }

    /// <summary>
    /// Executes an action with a rented byte buffer, automatically returning it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult WithByteBuffer<TResult>(
        int minimumLength,
        Func<byte[], TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        byte[] buffer = RentBytes(minimumLength);
        try
        {
            return action(buffer);
        }
        finally
        {
            ReturnBytes(buffer);
        }
    }

    /// <summary>
    /// Executes an action with a rented double buffer (void return).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WithDoubleBuffer(int minimumLength, Action<double[]> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        double[] buffer = RentDoubles(minimumLength);
        try
        {
            action(buffer);
        }
        finally
        {
            ReturnDoubles(buffer);
        }
    }
}
