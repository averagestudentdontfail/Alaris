// STPR002C.cs - Unified numerical integration facade with automatic hardware dispatch

using System.Numerics;
using System.Runtime.CompilerServices;
using Alaris.Core.Vectorized;

namespace Alaris.Strategy.Core.Numerical;

/// <summary>
/// Unified numerical integration facade that automatically selects optimal implementation.
/// Component ID: STPR002C
/// </summary>
/// <remarks>
/// <para>
/// Dispatches to either:
/// <list type="bullet">
///   <item><description>STPR002B - AVX2-accelerated 32-point Gauss-Legendre (if AVX2 available)</description></item>
///   <item><description>STPR002A - MathNet.Numerics 128-point Gauss-Legendre (fallback)</description></item>
/// </list>
/// </para>
/// <para>
/// Use this facade in hot paths for automatic hardware optimization.
/// </para>
/// </remarks>
public static class STPR002C
{
    /// <summary>
    /// Indicates if AVX2-accelerated integration is available.
    /// </summary>
    public static bool IsAvx2Supported => CRVT002A.IsAvx2Supported;

    /// <summary>
    /// Integrates a real function over [a, b] using optimal implementation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (double Value, double Error) Integrate(
        Func<double, double> f,
        double a,
        double b,
        double absoluteTolerance = 1e-8,
        double relativeTolerance = 1e-6)
    {
        ArgumentNullException.ThrowIfNull(f);

        if (IsAvx2Supported)
        {
            // Use AVX2-accelerated version (faster for repeated calls)
            double value = STPR002B.Integrate(f, a, b);
            return (value, 0.0);
        }

        // Fallback to MathNet.Numerics 128-point GL (higher accuracy)
        return STPR002A.Integrate(f, a, b, absoluteTolerance, relativeTolerance);
    }

    /// <summary>
    /// Integrates from a to infinity using optimal implementation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (double Value, double Error) IntegrateToInfinity(
        Func<double, double> f,
        double a,
        double absoluteTolerance = 1e-8,
        double relativeTolerance = 1e-6)
    {
        ArgumentNullException.ThrowIfNull(f);

        if (IsAvx2Supported)
        {
            return STPR002B.IntegrateToInfinity(f, a, absoluteTolerance);
        }

        return STPR002A.IntegrateToInfinity(f, a, absoluteTolerance, relativeTolerance);
    }

    /// <summary>
    /// Integrates a complex function from a to infinity using optimal implementation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (Complex Value, double Error) IntegrateComplexToInfinity(
        Func<double, Complex> f,
        double a,
        double absoluteTolerance = 1e-8,
        double relativeTolerance = 1e-6)
    {
        ArgumentNullException.ThrowIfNull(f);

        if (IsAvx2Supported)
        {
            return STPR002B.IntegrateComplexToInfinity(f, a, absoluteTolerance);
        }

        return STPR002A.IntegrateComplexToInfinity(f, a, absoluteTolerance, relativeTolerance);
    }
}
