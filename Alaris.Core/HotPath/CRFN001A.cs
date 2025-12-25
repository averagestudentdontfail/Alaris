// CRFN001A.cs - Zero-allocation finder utilities for hot paths

using System.Runtime.CompilerServices;

namespace Alaris.Core.HotPath;

/// <summary>
/// Zero-allocation finder utilities replacing LINQ on hot paths.
/// </summary>
public static class CRFN001A
{
    /// <summary>
    /// Finds the first item with minimum value of a selector, with optional filter.
    /// Zero allocations - no closures, no enumerators.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="items">Span of items to search.</param>
    /// <param name="selector">Selector function for comparison value.</param>
    /// <param name="filter">Optional filter predicate.</param>
    /// <returns>Item with minimum value, or default if none match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? FindMinBy<T>(
        ReadOnlySpan<T> items,
        Func<T, double> selector,
        Func<T, bool>? filter = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(selector);

        T? result = null;
        double minValue = double.MaxValue;

        for (int i = 0; i < items.Length; i++)
        {
            T item = items[i];
            if (filter is not null && !filter(item))
            {
                continue;
            }

            double value = selector(item);
            if (value < minValue)
            {
                minValue = value;
                result = item;
            }
        }

        return result;
    }

    /// <summary>
    /// Finds the first item with minimum value of a selector (array overload).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? FindMinBy<T>(
        T[] items,
        Func<T, double> selector,
        Func<T, bool>? filter = null) where T : class
    {
        return FindMinBy<T>(items.AsSpan(), selector, filter);
    }

    /// <summary>
    /// Finds the first item with minimum value (IReadOnlyList overload).
    /// Note: Uses indexed access for performance, no enumerator allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? FindMinBy<T>(
        IReadOnlyList<T> items,
        Func<T, double> selector,
        Func<T, bool>? filter = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(selector);

        T? result = null;
        double minValue = double.MaxValue;
        int count = items.Count;

        for (int i = 0; i < count; i++)
        {
            T item = items[i];
            if (filter is not null && !filter(item))
            {
                continue;
            }

            double value = selector(item);
            if (value < minValue)
            {
                minValue = value;
                result = item;
            }
        }

        return result;
    }

    /// <summary>
    /// Finds the first item with maximum value of a selector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? FindMaxBy<T>(
        IReadOnlyList<T> items,
        Func<T, double> selector,
        Func<T, bool>? filter = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(selector);

        T? result = null;
        double maxValue = double.MinValue;
        int count = items.Count;

        for (int i = 0; i < count; i++)
        {
            T item = items[i];
            if (filter is not null && !filter(item))
            {
                continue;
            }

            double value = selector(item);
            if (value > maxValue)
            {
                maxValue = value;
                result = item;
            }
        }

        return result;
    }

    /// <summary>
    /// Counts items matching a predicate without allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountWhere<T>(IReadOnlyList<T> items, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(predicate);

        int count = 0;
        int length = items.Count;

        for (int i = 0; i < length; i++)
        {
            if (predicate(items[i]))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Checks if any item matches a predicate without allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AnyWhere<T>(IReadOnlyList<T> items, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(predicate);

        int length = items.Count;

        for (int i = 0; i < length; i++)
        {
            if (predicate(items[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds first item matching a predicate without allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? FirstWhere<T>(IReadOnlyList<T> items, Func<T, bool> predicate) where T : class
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(predicate);

        int length = items.Count;

        for (int i = 0; i < length; i++)
        {
            T item = items[i];
            if (predicate(item))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the first item with minimum value (IList overload).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? FindMinBy<T>(
        IList<T> items,
        Func<T, double> selector,
        Func<T, bool>? filter = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(selector);

        T? result = null;
        double minValue = double.MaxValue;
        int count = items.Count;

        for (int i = 0; i < count; i++)
        {
            T item = items[i];
            if (filter is not null && !filter(item))
            {
                continue;
            }

            double value = selector(item);
            if (value < minValue)
            {
                minValue = value;
                result = item;
            }
        }

        return result;
    }

    /// <summary>
    /// Finds first item matching a predicate (IList overload).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? FirstWhere<T>(IList<T> items, Func<T, bool> predicate) where T : class
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(predicate);

        int length = items.Count;

        for (int i = 0; i < length; i++)
        {
            T item = items[i];
            if (predicate(item))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Computes sum of selector values for matching items.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SumWhere<T>(
        IReadOnlyList<T> items,
        Func<T, double> selector,
        Func<T, bool>? filter = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(selector);

        double sum = 0;
        int length = items.Count;

        for (int i = 0; i < length; i++)
        {
            T item = items[i];
            if (filter is null || filter(item))
            {
                sum += selector(item);
            }
        }

        return sum;
    }

    /// <summary>
    /// Computes average of selector values for matching items.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double AverageWhere<T>(
        IReadOnlyList<T> items,
        Func<T, double> selector,
        Func<T, bool>? filter = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(selector);

        double sum = 0;
        int count = 0;
        int length = items.Count;

        for (int i = 0; i < length; i++)
        {
            T item = items[i];
            if (filter is null || filter(item))
            {
                sum += selector(item);
                count++;
            }
        }

        return count > 0 ? sum / count : 0;
    }
}
