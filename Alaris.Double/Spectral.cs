namespace Alaris.Double;

/// <summary>
/// Simplified spectral methods - keeps essential Chebyshev functionality
/// Removes complex transformations that can be handled by QuantLib
/// </summary>
public static class SimplifiedSpectralMethods
{
    /// <summary>
    /// Generate Chebyshev collocation nodes - essential for spectral methods
    /// This is specialized for option pricing and cannot be easily replaced by QuantLib
    /// </summary>
    public static double[] CreateChebyshevNodes(int n)
    {
        var nodes = new double[n];
        for (int i = 0; i < n; i++)
        {
            nodes[i] = -Math.Cos((2 * i + 1) * Math.PI / (2 * n));
        }
        return nodes;
    }

    /// <summary>
    /// Evaluate Chebyshev polynomial using Clenshaw recurrence
    /// Optimized for performance - removes unnecessary complexity
    /// </summary>
    public static double EvaluateChebyshev(double[] coefficients, double x)
    {
        if (coefficients.Length == 0) return 0.0;
        if (coefficients.Length == 1) return coefficients[0];

        double b_k = 0.0, b_k_plus_1 = 0.0;
        
        for (int k = coefficients.Length - 1; k >= 1; k--)
        {
            double b_k_minus_1 = coefficients[k] + 2 * x * b_k - b_k_plus_1;
            b_k_plus_1 = b_k;
            b_k = b_k_minus_1;
        }
        
        return coefficients[0] + x * b_k - b_k_plus_1;
    }

    /// <summary>
    /// Fit Chebyshev interpolant to function values at collocation points
    /// Simplified version focusing on boundary functions
    /// </summary>
    public static double[] FitChebyshevInterpolant(double[] functionValues)
    {
        int n = functionValues.Length;
        var coefficients = new double[n];
        
        for (int k = 0; k < n; k++)
        {
            double sum = 0.0;
            for (int j = 0; j < n; j++)
            {
                sum += functionValues[j] * Math.Cos(k * (2 * j + 1) * Math.PI / (2 * n));
            }
            coefficients[k] = (k == 0 ? 1.0 : 2.0) * sum / n;
        }
        
        return coefficients;
    }

    /// <summary>
    /// Transform time domain for boundary problems - essential for convergence
    /// Maps [0, T] to [-1, 1] with concentration near expiry
    /// </summary>
    public static double TransformTimeToChebyshev(double tau, double maxTau)
    {
        if (maxTau <= 0) return 0.0;
        
        // Square root transformation concentrates points near expiry
        double sqrt_ratio = Math.Sqrt(tau / maxTau);
        return 2 * sqrt_ratio - 1; // Map to [-1, 1]
    }

    /// <summary>
    /// Inverse time transformation
    /// </summary>
    public static double TransformChebyshevToTime(double xi, double maxTau)
    {
        double sqrt_ratio = (xi + 1) / 2;
        return maxTau * sqrt_ratio * sqrt_ratio;
    }

    /// <summary>
    /// Simplified boundary function representation
    /// Removes complex transformation sequences in favor of direct approach
    /// </summary>
    public class SimpleBoundaryFunction
    {
        private readonly double[] _coefficients;
        private readonly double _maxTau;

        public SimpleBoundaryFunction(double[] nodeValues, double[] timePoints, double maxTau)
        {
            _maxTau = maxTau;
            
            // Transform time points to Chebyshev domain
            var transformedPoints = timePoints.Select(t => TransformTimeToChebyshev(t, maxTau)).ToArray();
            
            // Fit Chebyshev interpolant directly to node values
            _coefficients = FitChebyshevInterpolant(nodeValues);
        }

        /// <summary>
        /// Evaluate boundary at given time
        /// </summary>
        public double Evaluate(double tau)
        {
            if (tau <= 0) return _coefficients.Length > 0 ? _coefficients[0] : 0.0;
            if (tau >= _maxTau) return EvaluateChebyshev(_coefficients, 1.0);
            
            double xi = TransformTimeToChebyshev(tau, _maxTau);
            return EvaluateChebyshev(_coefficients, xi);
        }

        /// <summary>
        /// Get coefficients for analysis
        /// </summary>
        public double[] GetCoefficients() => (double[])_coefficients.Clone();
    }

    /// <summary>
    /// Estimate convergence rate from coefficient decay
    /// Simplified version for practical use
    /// </summary>
    public static double EstimateConvergenceRate(double[] coefficients)
    {
        if (coefficients.Length < 4) return double.NaN;
        
        // Look at last few coefficients
        var lastCoeffs = coefficients.Skip(coefficients.Length - 3).Select(Math.Abs).ToArray();
        
        // Simple exponential decay estimate
        if (lastCoeffs[0] > 1e-15 && lastCoeffs[2] > 1e-15)
        {
            return Math.Log(lastCoeffs[0] / lastCoeffs[2]) / 2.0;
        }
        
        return double.NaN;
    }

    /// <summary>
    /// Adaptive node selection based on required accuracy
    /// </summary>
    public static int SelectOptimalNodes(double tolerance, double timeToMaturity)
    {
        // Simple heuristic based on tolerance and time
        if (tolerance >= 1e-6) return Constants.Fast.SpectralNodes;
        if (tolerance >= 1e-10) return Constants.Standard.SpectralNodes;
        return Constants.HighPrecision.SpectralNodes;
    }
}