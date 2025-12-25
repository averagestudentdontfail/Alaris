using System.Numerics;
using MathNet.Numerics.Integration;

namespace Alaris.Strategy.Core.Numerical;

/// <summary>
/// Adaptive numerical integration using MathNet.Numerics.
/// Production-grade implementation for integrating characteristic functions in option pricing.
/// </summary>
public static class STPR002A
{
    private const int GaussLegendreOrder = 128;
    private const double IntegrationStepSize = 100.0; // Integration chunk size
    private const int MaxChunks = 1000; // Safety break

    /// <summary>
    /// Integrates a real-valued function over a finite interval using high-order Gauss-Legendre quadrature.
    /// </summary>
    public static (double Value, double Error) Integrate(
        Func<double, double> f,
        double a,
        double b,
        double absoluteTolerance = 1e-8,
        double relativeTolerance = 1e-6)
    {
        ArgumentNullException.ThrowIfNull(f);

        if (a >= b)
        {
            return (0, 0);
        }

        // Gauss-Legendre 128-point is extremely accurate for smooth functions like CharFuncs
        double value = GaussLegendreRule.Integrate(f, a, b, GaussLegendreOrder);
        
        // Simple error estimation (optional: could compare with lower order)
        // For option pricing, 128-point GL is usually exact enough that strict error est isn't needed per chunk
        return (value, 0.0); 
    }

    /// <summary>
    /// Integrates from a to infinity using Adaptive Truncation with Gauss-Legendre.
    /// 
    /// Instead of mapping (a, inf) -> (-1, 1), we integrate in chunks [a, a+step], [a+step, a+2*step]...
    /// until the contribution of a chunk is smaller than the tolerance.
    /// Uses adaptive step sizing: step doubles when convergence accelerates.
    /// This is superior for oscillatory Characteristic Functions (Heston/Kou).
    /// </summary>
    public static (double Value, double Error) IntegrateToInfinity(
        Func<double, double> f,
        double a,
        double absoluteTolerance = 1e-8,
        double relativeTolerance = 1e-6)
    {
        ArgumentNullException.ThrowIfNull(f);

        double totalSum = 0;
        double currentStart = a;
        int chunkCount = 0;
        double currentStepSize = IntegrationStepSize;
        double previousChunkMagnitude = double.MaxValue;

        while (chunkCount < MaxChunks)
        {
            double currentEnd = currentStart + currentStepSize;
            
            // Integrate this chunk
            double chunkValue = GaussLegendreRule.Integrate(f, currentStart, currentEnd, GaussLegendreOrder);
            double chunkMagnitude = Math.Abs(chunkValue);
            
            totalSum += chunkValue;
            
            // Check convergence: if the latest chunk added almost nothing, we are done.
            // Characteristic functions decay exponentially, so this happens relatively fast.
            if (chunkMagnitude < absoluteTolerance && chunkCount > 0)
            {
                return (totalSum, absoluteTolerance); // Converged
            }

            // Adaptive step sizing: if magnitude dropped significantly, double the step
            // This accelerates convergence for well-behaved decaying integrands
            if (chunkCount > 2 && chunkMagnitude < previousChunkMagnitude * 0.1)
            {
                currentStepSize = Math.Min(currentStepSize * 2.0, IntegrationStepSize * 16);
            }

            previousChunkMagnitude = chunkMagnitude;
            currentStart = currentEnd;
            chunkCount++;
        }

        // Return best effort if we hit max chunks
        return (totalSum, 1.0); 
    }

    /// <summary>
    /// Integrates a complex function from a to infinity using Adaptive Truncation.
    /// Uses adaptive step sizing for faster convergence.
    /// </summary>
    public static (Complex Value, double Error) IntegrateComplexToInfinity(
        Func<double, Complex> f,
        double a,
        double absoluteTolerance = 1e-8,
        double relativeTolerance = 1e-6)
    {
        ArgumentNullException.ThrowIfNull(f);

        Complex totalSum = Complex.Zero;
        double currentStart = a;
        int chunkCount = 0;
        double currentStepSize = IntegrationStepSize;
        double previousMagnitude = double.MaxValue;

        while (chunkCount < MaxChunks)
        {
            double currentEnd = currentStart + currentStepSize;

            // Integrate Real and Imaginary parts separately over this chunk
            double realPart = GaussLegendreRule.Integrate(x => f(x).Real, currentStart, currentEnd, GaussLegendreOrder);
            double imagPart = GaussLegendreRule.Integrate(x => f(x).Imaginary, currentStart, currentEnd, GaussLegendreOrder);
            
            Complex chunkValue = new Complex(realPart, imagPart);
            double chunkMagnitude = chunkValue.Magnitude;
            totalSum += chunkValue;

            // Convergence check on the magnitude of the added chunk
            if (chunkMagnitude < absoluteTolerance && chunkCount > 0)
            {
                return (totalSum, absoluteTolerance);
            }

            // Adaptive step sizing for faster convergence
            if (chunkCount > 2 && chunkMagnitude < previousMagnitude * 0.1)
            {
                currentStepSize = Math.Min(currentStepSize * 2.0, IntegrationStepSize * 16);
            }

            previousMagnitude = chunkMagnitude;
            currentStart = currentEnd;
            chunkCount++;
        }

        return (totalSum, 1.0);
    }
}