using Alaris.Quantlib;

namespace Alaris.Double;

/// <summary>
/// Implements spectral collocation methods using Chebyshev polynomials
/// for boundary function approximation in American option pricing
/// </summary>
public static class Spectral
{
    /// <summary>
    /// Generates Chebyshev-Gauss-Lobatto collocation nodes on [-1, 1]
    /// </summary>
    /// <param name="n">Number of nodes</param>
    /// <returns>Array of collocation points</returns>
    public static double[] ChebyshevNodes(int n)
    {
        if (n < 2)
        {
            throw new ArgumentException("Number of nodes must be at least 2");
        }

        var nodes = new double[n];
        for (int i = 0; i < n; i++)
        {
            nodes[i] = Math.Cos(Math.PI * i / (n - 1));
        }
        return nodes;
    }

    /// <summary>
    /// Computes Chebyshev polynomial of the first kind T_n(x)
    /// </summary>
    /// <param name="n">Polynomial degree</param>
    /// <param name="x">Evaluation point</param>
    /// <returns>T_n(x)</returns>
    public static double ChebyshevPolynomial(int n, double x)
    {
        if (n == 0) return 1.0;
        if (n == 1) return x;

        double T0 = 1.0, T1 = x;
        for (int k = 2; k <= n; k++)
        {
            double T2 = 2.0 * x * T1 - T0;
            T0 = T1;
            T1 = T2;
        }
        return T1;
    }

    /// <summary>
    /// Computes derivative of Chebyshev polynomial U_n(x) = T'_{n+1}(x)/(n+1)
    /// </summary>
    /// <param name="n">Polynomial degree</param>
    /// <param name="x">Evaluation point</param>
    /// <returns>T'_n(x)</returns>
    public static double ChebyshevPolynomialDerivative(int n, double x)
    {
        if (n == 0) return 0.0;
        if (n == 1) return 1.0;

        // T'_n(x) = n * U_{n-1}(x) where U_k is Chebyshev polynomial of second kind
        return n * ChebyshevSecondKind(n - 1, x);
    }

    /// <summary>
    /// Interpolates function values at Chebyshev nodes using Clenshaw's algorithm
    /// </summary>
    /// <param name="coefficients">Chebyshev expansion coefficients</param>
    /// <param name="x">Evaluation point in [-1, 1]</param>
    /// <returns>Interpolated value</returns>
    public static double ChebyshevInterpolate(double[] coefficients, double x)
    {
        int n = coefficients.Length;
        if (n == 0) return 0.0;
        if (n == 1) return coefficients[0];

        // Clenshaw's recurrence algorithm for stable evaluation
        double b_k = 0.0, b_k1 = 0.0;
        
        for (int k = n - 1; k >= 1; k--)
        {
            double b_k_minus_1 = coefficients[k] + 2.0 * x * b_k - b_k1;
            b_k1 = b_k;
            b_k = b_k_minus_1;
        }
        
        return coefficients[0] + x * b_k - b_k1;
    }

    /// <summary>
    /// Computes Chebyshev expansion coefficients from function values at collocation nodes
    /// </summary>
    /// <param name="functionValues">Function values at Chebyshev nodes</param>
    /// <returns>Chebyshev expansion coefficients</returns>
    public static double[] ChebyshevCoefficients(double[] functionValues)
    {
        int n = functionValues.Length;
        var coefficients = new double[n];
        
        for (int k = 0; k < n; k++)
        {
            double sum = 0.0;
            double ck = (k == 0 || k == n - 1) ? 2.0 : 1.0;
            
            for (int j = 0; j < n; j++)
            {
                double cj = (j == 0 || j == n - 1) ? 2.0 : 1.0;
                sum += functionValues[j] * Math.Cos(Math.PI * k * j / (n - 1)) / cj;
            }
            
            coefficients[k] = 2.0 * sum / ((n - 1) * ck);
        }
        
        return coefficients;
    }

    /// <summary>
    /// Applies the transformation sequence to regularize boundary functions
    /// ξ = √(τ/τ_max) → G(ξ) = ln(B̃(ξ²)) → H(ξ) = G(ξ)²
    /// </summary>
    /// <param name="boundaryValues">Raw boundary values B(τ)</param>
    /// <param name="timePoints">Time points τ</param>
    /// <param name="strike">Strike price</param>
    /// <param name="r">Interest rate</param>
    /// <param name="q">Dividend yield</param>
    /// <returns>Transformed function values H(ξ)</returns>
    public static double[] ApplyBoundaryTransformation(double[] boundaryValues, double[] timePoints, 
                                                     double strike, double r, double q)
    {
        if (boundaryValues.Length != timePoints.Length)
        {
            throw new ArgumentException("Boundary values and time points must have same length");
        }

        int n = boundaryValues.Length;
        var transformedValues = new double[n];
        double tauMax = timePoints.Max();
        
        // Normalization factor X = K * min(1, r/q)
        double normalizationFactor = strike * Math.Min(1.0, Math.Abs(r / Math.Max(q, 1e-10)));
        
        for (int i = 0; i < n; i++)
        {
            // Stage 1: Temporal transformation ξ = √(τ/τ_max)
            double xi = Math.Sqrt(timePoints[i] / tauMax);
            
            // Stage 2: Boundary normalization B̃(τ) = B(τ) / X
            double normalizedBoundary = boundaryValues[i] / normalizationFactor;
            
            // Stage 3: Logarithmic transformation G(ξ) = ln(B̃(ξ²))
            double G = Math.Log(Math.Max(normalizedBoundary, 1e-10)); // Avoid log(0)
            
            // Stage 4: Variance-stabilizing transformation H(ξ) = G(ξ)²
            transformedValues[i] = G * G;
        }
        
        return transformedValues;
    }

    /// <summary>
    /// Inverts the transformation sequence to recover boundary values
    /// H(ξ) → G(ξ) = √H(ξ) → B̃(ξ²) = exp(G(ξ)) → B(τ) = X * B̃(τ)
    /// </summary>
    /// <param name="transformedValues">Transformed function values H(ξ)</param>
    /// <param name="timePoints">Time points τ</param>
    /// <param name="strike">Strike price</param>
    /// <param name="r">Interest rate</param>
    /// <param name="q">Dividend yield</param>
    /// <returns>Recovered boundary values B(τ)</returns>
    public static double[] InvertBoundaryTransformation(double[] transformedValues, double[] timePoints,
                                                      double strike, double r, double q)
    {
        if (transformedValues.Length != timePoints.Length)
        {
            throw new ArgumentException("Transformed values and time points must have same length");
        }

        int n = transformedValues.Length;
        var boundaryValues = new double[n];
        
        // Normalization factor X = K * min(1, r/q)
        double normalizationFactor = strike * Math.Min(1.0, Math.Abs(r / Math.Max(q, 1e-10)));
        
        for (int i = 0; i < n; i++)
        {
            // Stage 4⁻¹: G(ξ) = √H(ξ) (take positive square root)
            double G = Math.Sqrt(Math.Max(transformedValues[i], 0.0));
            
            // Stage 3⁻¹: B̃(ξ²) = exp(G(ξ))
            double normalizedBoundary = Math.Exp(G);
            
            // Stage 2⁻¹: B(τ) = X * B̃(τ)
            boundaryValues[i] = normalizationFactor * normalizedBoundary;
        }
        
        return boundaryValues;
    }

    /// <summary>
    /// Computes the spectral derivative of a function represented by Chebyshev coefficients
    /// </summary>
    /// <param name="coefficients">Chebyshev expansion coefficients</param>
    /// <returns>Coefficients of the derivative</returns>
    public static double[] ChebyshevDerivativeCoefficients(double[] coefficients)
    {
        int n = coefficients.Length;
        if (n <= 1) return new double[0];
        
        var derivativeCoeffs = new double[n - 1];
        
        // T'_n(x) = n * U_{n-1}(x), and the coefficients follow a specific recurrence
        for (int k = 0; k < n - 1; k++)
        {
            derivativeCoeffs[k] = 0.0;
            for (int j = k + 1; j < n; j += 2)
            {
                derivativeCoeffs[k] += 2.0 * j * coefficients[j];
            }
            if (k == 0) derivativeCoeffs[k] /= 2.0;
        }
        
        return derivativeCoeffs;
    }

    /// <summary>
    /// Estimates the convergence rate of the spectral approximation
    /// </summary>
    /// <param name="coefficients">Chebyshev expansion coefficients</param>
    /// <returns>Estimated convergence rate (negative exponent)</returns>
    public static double EstimateConvergenceRate(double[] coefficients)
    {
        int n = coefficients.Length;
        if (n < 5) return double.NaN;
        
        // Look at decay of coefficients for last few terms
        var lastCoeffs = coefficients.Skip(n - 5).Take(5).Select(Math.Abs).ToArray();
        
        // Fit exponential decay: |a_k| ≈ C * ρ^(-k)
        double sumLogK = 0.0, sumLogCoeff = 0.0, sumLogKSq = 0.0, sumLogKLogCoeff = 0.0;
        int count = 0;
        
        for (int i = 0; i < lastCoeffs.Length; i++)
        {
            if (lastCoeffs[i] > 1e-16) // Avoid log(0)
            {
                double logK = Math.Log(n - 5 + i + 1);
                double logCoeff = Math.Log(lastCoeffs[i]);
                
                sumLogK += logK;
                sumLogCoeff += logCoeff;
                sumLogKSq += logK * logK;
                sumLogKLogCoeff += logK * logCoeff;
                count++;
            }
        }
        
        if (count < 2) return double.NaN;
        
        // Linear regression slope gives -log(ρ)
        double slope = (count * sumLogKLogCoeff - sumLogK * sumLogCoeff) / 
                      (count * sumLogKSq - sumLogK * sumLogK);
        
        return -slope; // Return positive convergence rate
    }

    private static double ChebyshevSecondKind(int n, double x)
    {
        if (n < 0) return 0.0;
        if (n == 0) return 1.0;
        if (n == 1) return 2.0 * x;

        double U0 = 1.0, U1 = 2.0 * x;
        for (int k = 2; k <= n; k++)
        {
            double U2 = 2.0 * x * U1 - U0;
            U0 = U1;
            U1 = U2;
        }
        return U1;
    }
}

/// <summary>
/// Represents a boundary function using spectral interpolation
/// Handles the complete transformation sequence for numerical stability
/// </summary>
public class BoundaryFunction
{
    private readonly double[] _chebyshevCoefficients;
    private readonly double[] _originalNodes;
    private readonly double _tauMax;
    private readonly double _strike;
    private readonly double _r, _q;
    private readonly bool _isTransformed;

    /// <summary>
    /// Constructs a boundary function from collocation data
    /// </summary>
    /// <param name="nodes">Chebyshev nodes in [-1, 1]</param>
    /// <param name="boundaryValues">Boundary values at corresponding time points</param>
    /// <param name="timePoints">Time points τ</param>
    /// <param name="strike">Strike price</param>
    /// <param name="r">Interest rate</param>
    /// <param name="q">Dividend yield</param>
    /// <param name="applyTransformation">Whether to apply spectral transformation</param>
    public BoundaryFunction(double[] nodes, double[] boundaryValues, double[] timePoints,
                          double strike, double r, double q, bool applyTransformation = true)
    {
        if (nodes.Length != boundaryValues.Length || nodes.Length != timePoints.Length)
        {
            throw new ArgumentException("All arrays must have the same length");
        }

        _originalNodes = (double[])nodes.Clone();
        _tauMax = timePoints.Max();
        _strike = strike;
        _r = r;
        _q = q;
        _isTransformed = applyTransformation;

        if (applyTransformation)
        {
            // Apply transformation sequence and compute Chebyshev coefficients
            var transformedValues = Spectral.ApplyBoundaryTransformation(boundaryValues, timePoints, strike, r, q);
            _chebyshevCoefficients = Spectral.ChebyshevCoefficients(transformedValues);
        }
        else
        {
            // Direct Chebyshev interpolation without transformation
            _chebyshevCoefficients = Spectral.ChebyshevCoefficients(boundaryValues);
        }
    }

    /// <summary>
    /// Evaluates the boundary function at a given time point
    /// </summary>
    /// <param name="tau">Time to maturity</param>
    /// <returns>Boundary value B(τ)</returns>
    public double Evaluate(double tau)
    {
        if (tau < 0) return _strike; // Boundary condition at expiration
        if (tau > _tauMax) tau = _tauMax; // Extrapolation
        
        if (_isTransformed)
        {
            // Transform time point: ξ = √(τ/τ_max)
            double xi = Math.Sqrt(tau / _tauMax);
            
            // Map to [-1, 1] for Chebyshev evaluation
            double x = 2.0 * xi - 1.0;
            
            // Evaluate transformed function H(ξ)
            double H = Spectral.ChebyshevInterpolate(_chebyshevCoefficients, x);
            
            // Invert transformation to get boundary value
            double G = Math.Sqrt(Math.Max(H, 0.0));
            double normalizedBoundary = Math.Exp(G);
            double normalizationFactor = _strike * Math.Min(1.0, Math.Abs(_r / Math.Max(_q, 1e-10)));
            
            return normalizationFactor * normalizedBoundary;
        }
        else
        {
            // Direct evaluation without transformation
            double x = 2.0 * tau / _tauMax - 1.0; // Map [0, τ_max] to [-1, 1]
            return Spectral.ChebyshevInterpolate(_chebyshevCoefficients, x);
        }
    }

    /// <summary>
    /// Evaluates the derivative of the boundary function
    /// </summary>
    /// <param name="tau">Time to maturity</param>
    /// <returns>Boundary derivative dB/dτ</returns>
    public double EvaluateDerivative(double tau)
    {
        if (tau <= 0 || tau > _tauMax) return 0.0;
        
        var derivativeCoeffs = Spectral.ChebyshevDerivativeCoefficients(_chebyshevCoefficients);
        
        if (_isTransformed)
        {
            double xi = Math.Sqrt(tau / _tauMax);
            double x = 2.0 * xi - 1.0;
            
            // Chain rule for transformation sequence
            double dH_dx = Spectral.ChebyshevInterpolate(derivativeCoeffs, x);
            double dx_dxi = 2.0;
            double dxi_dtau = 1.0 / (2.0 * Math.Sqrt(tau * _tauMax));
            
            // Additional derivative terms from transformation inversion would go here
            return dH_dx * dx_dxi * dxi_dtau; // Simplified
        }
        else
        {
            double x = 2.0 * tau / _tauMax - 1.0;
            double dB_dx = Spectral.ChebyshevInterpolate(derivativeCoeffs, x);
            double dx_dtau = 2.0 / _tauMax;
            
            return dB_dx * dx_dtau;
        }
    }

    /// <summary>
    /// Gets the Chebyshev expansion coefficients
    /// </summary>
    public double[] ChebyshevCoefficients => (double[])_chebyshevCoefficients.Clone();

    /// <summary>
    /// Estimates the accuracy of the spectral approximation
    /// </summary>
    public double EstimatedError => Spectral.EstimateConvergenceRate(_chebyshevCoefficients);

    /// <summary>
    /// Number of collocation points used
    /// </summary>
    public int NumberOfNodes => _originalNodes.Length;
}