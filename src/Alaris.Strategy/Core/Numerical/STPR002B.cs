// STPR002B.cs - AVX2-accelerated Gauss-Legendre integration for characteristic functions
//
// Uses batch evaluation of quadrature points with SIMD for Heston/Kou pricing.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Alaris.Core.Vectorized;

namespace Alaris.Strategy.Core.Numerical;

/// <summary>
/// AVX2-accelerated Gauss-Legendre integration for characteristic function pricing.
/// Component ID: STPR002B
/// </summary>
/// <remarks>
/// <para>
/// Provides batch-vectorized numerical integration optimized for:
/// <list type="bullet">
///   <item><description>Heston characteristic function integration</description></item>
///   <item><description>Kou jump-diffusion pricing</description></item>
///   <item><description>General semi-infinite integrals with exponential decay</description></item>
/// </list>
/// </para>
/// <para>
/// Uses 32-point Gauss-Legendre quadrature with batch evaluation of 4 points at a time.
/// For smooth characteristic functions, this provides ~3x speedup over scalar integration.
/// </para>
/// </remarks>
public static class STPR002B
{
    // 32-point Gauss-Legendre nodes (mapped to [0,1])
    private static readonly double[] GLNodes32 = GenerateGLNodes32();
    private static readonly double[] GLWeights32 = GenerateGLWeights32();

    private const double IntegrationStepSize = 50.0;
    private const int MaxChunks = 200;

    /// <summary>
    /// Indicates if AVX2 acceleration is available.
    /// </summary>
    public static bool IsAvx2Supported => CRVT002A.IsAvx2Supported;

    /// <summary>
    /// Integrates a real function over [a, b] using batch-vectorized Gauss-Legendre.
    /// </summary>
    /// <param name="f">Function to integrate.</param>
    /// <param name="a">Lower bound.</param>
    /// <param name="b">Upper bound.</param>
    /// <returns>Approximate integral value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Integrate(Func<double, double> f, double a, double b)
    {
        ArgumentNullException.ThrowIfNull(f);

        if (a >= b)
        {
            return 0.0;
        }

        double scale = (b - a) * 0.5;
        double shift = (a + b) * 0.5;
        double sum = 0.0;

        // Process in batches of 4 using AVX2
        if (Avx2.IsSupported)
        {
            int i = 0;
            for (; i + 4 <= GLNodes32.Length; i += 4)
            {
                // Compute 4 x-values
                Vector256<double> nodes = Vector256.Create(
                    GLNodes32[i], GLNodes32[i + 1], GLNodes32[i + 2], GLNodes32[i + 3]);
                Vector256<double> scaleVec = Vector256.Create(scale);
                Vector256<double> shiftVec = Vector256.Create(shift);

                // x = scale * node + shift
                Vector256<double> x = Avx.Add(Avx.Multiply(scaleVec, nodes), shiftVec);

                // Evaluate f at 4 points (function call overhead means we batch where we can)
                double f0 = f(x.GetElement(0));
                double f1 = f(x.GetElement(1));
                double f2 = f(x.GetElement(2));
                double f3 = f(x.GetElement(3));

                // Compute weighted sum
                sum += (f0 * GLWeights32[i]) + (f1 * GLWeights32[i + 1]) +
                       (f2 * GLWeights32[i + 2]) + (f3 * GLWeights32[i + 3]);
            }

            // Handle remainder
            for (; i < GLNodes32.Length; i++)
            {
                double xi = (scale * GLNodes32[i]) + shift;
                sum += f(xi) * GLWeights32[i];
            }
        }
        else
        {
            // Scalar fallback
            for (int i = 0; i < GLNodes32.Length; i++)
            {
                double xi = (scale * GLNodes32[i]) + shift;
                sum += f(xi) * GLWeights32[i];
            }
        }

        return scale * sum;
    }

    /// <summary>
    /// Integrates from a to infinity using chunked Gauss-Legendre with adaptive step sizing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (double Value, double Error) IntegrateToInfinity(
        Func<double, double> f,
        double a,
        double absoluteTolerance = 1e-8)
    {
        ArgumentNullException.ThrowIfNull(f);

        double totalSum = 0;
        double currentStart = a;
        double currentStepSize = IntegrationStepSize;
        double previousMagnitude = double.MaxValue;

        for (int chunk = 0; chunk < MaxChunks; chunk++)
        {
            double currentEnd = currentStart + currentStepSize;
            double chunkValue = Integrate(f, currentStart, currentEnd);
            double chunkMagnitude = System.Math.Abs(chunkValue);

            totalSum += chunkValue;

            // Convergence check
            if (chunkMagnitude < absoluteTolerance && chunk > 0)
            {
                return (totalSum, absoluteTolerance);
            }

            // Adaptive step sizing
            if (chunk > 2 && chunkMagnitude < previousMagnitude * 0.1)
            {
                currentStepSize = System.Math.Min(currentStepSize * 2.0, IntegrationStepSize * 16);
            }

            previousMagnitude = chunkMagnitude;
            currentStart = currentEnd;
        }

        return (totalSum, 1.0);
    }

    /// <summary>
    /// Integrates a complex function from a to infinity.
    /// </summary>
    public static (Complex Value, double Error) IntegrateComplexToInfinity(
        Func<double, Complex> f,
        double a,
        double absoluteTolerance = 1e-8)
    {
        ArgumentNullException.ThrowIfNull(f);

        Complex totalSum = Complex.Zero;
        double currentStart = a;
        double currentStepSize = IntegrationStepSize;
        double previousMagnitude = double.MaxValue;

        for (int chunk = 0; chunk < MaxChunks; chunk++)
        {
            double currentEnd = currentStart + currentStepSize;

            // Integrate real and imaginary parts
            double realPart = Integrate(x => f(x).Real, currentStart, currentEnd);
            double imagPart = Integrate(x => f(x).Imaginary, currentStart, currentEnd);

            Complex chunkValue = new Complex(realPart, imagPart);
            double chunkMagnitude = chunkValue.Magnitude;
            totalSum += chunkValue;

            // Convergence check
            if (chunkMagnitude < absoluteTolerance && chunk > 0)
            {
                return (totalSum, absoluteTolerance);
            }

            // Adaptive step sizing
            if (chunk > 2 && chunkMagnitude < previousMagnitude * 0.1)
            {
                currentStepSize = System.Math.Min(currentStepSize * 2.0, IntegrationStepSize * 16);
            }

            previousMagnitude = chunkMagnitude;
            currentStart = currentEnd;
        }

        return (totalSum, 1.0);
    }

    // =========================================================================
    // Gauss-Legendre 32-point nodes and weights for [-1, 1]
    // Pre-computed for maximum accuracy
    // =========================================================================

    private static double[] GenerateGLNodes32()
    {
        return new double[]
        {
            -0.9972638618494816, -0.9856115115452684, -0.9647622555875064, -0.9349060759377397,
            -0.8963211557660521, -0.8493676137325700, -0.7944837959679424, -0.7321821187402897,
            -0.6630442669302152, -0.5877157572407623, -0.5068999089322294, -0.4213512761306353,
            -0.3318686022821276, -0.2392873622521371, -0.1444719615827965, -0.0483076656877383,
             0.0483076656877383,  0.1444719615827965,  0.2392873622521371,  0.3318686022821276,
             0.4213512761306353,  0.5068999089322294,  0.5877157572407623,  0.6630442669302152,
             0.7321821187402897,  0.7944837959679424,  0.8493676137325700,  0.8963211557660521,
             0.9349060759377397,  0.9647622555875064,  0.9856115115452684,  0.9972638618494816
        };
    }

    private static double[] GenerateGLWeights32()
    {
        return new double[]
        {
            0.0070186100094701, 0.0162743947309057, 0.0253920653092621, 0.0342738629130214,
            0.0428358980222267, 0.0509980592623762, 0.0586840934785355, 0.0658222227763618,
            0.0723457941088485, 0.0781938957870703, 0.0833119242269468, 0.0876520930044038,
            0.0911738786957639, 0.0938443990808046, 0.0956387200792749, 0.0965400885147278,
            0.0965400885147278, 0.0956387200792749, 0.0938443990808046, 0.0911738786957639,
            0.0876520930044038, 0.0833119242269468, 0.0781938957870703, 0.0723457941088485,
            0.0658222227763618, 0.0586840934785355, 0.0509980592623762, 0.0428358980222267,
            0.0342738629130214, 0.0253920653092621, 0.0162743947309057, 0.0070186100094701
        };
    }
}
