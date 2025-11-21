using System.Buffers;
using MathNet.Numerics;
using MathNet.Numerics.LinearRegression;

namespace Alaris.Strategy.Core;

/// <summary>
/// Analyzes the term structure of implied volatility.
/// Used to identify inverted term structures that signal trading opportunities.
/// </summary>
/// <remarks>
/// Optimized for Rule 5 (Zero-Allocation Hot Paths) using ArrayPool.
/// </remarks>
public sealed class TermStructure
{
    /// <summary>
    /// Analyzes a set of term structure points and calculates slope/intercept.
    /// </summary>
    public TermStructureAnalysis Analyze(IReadOnlyList<TermStructurePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 2)
        {
            throw new ArgumentException("Need at least 2 points for term structure analysis", nameof(points));
        }

        int n = points.Count;

        // Rule 5: Use ArrayPool for temporary arrays
        double[] dte = ArrayPool<double>.Shared.Rent(n);
        double[] iv = ArrayPool<double>.Shared.Rent(n);
        int[] indices = ArrayPool<int>.Shared.Rent(n);

        try
        {
            // Initialize indices for sorting
            for (int i = 0; i < n; i++)
            {
                indices[i] = i;
            }

            // Sort indices by DaysToExpiry (avoids creating sorted list)
            Array.Sort(indices, 0, n, Comparer<int>.Create((a, b) =>
                points[a].DaysToExpiry.CompareTo(points[b].DaysToExpiry)));

            // Extract sorted values into arrays
            for (int i = 0; i < n; i++)
            {
                int idx = indices[i];
                dte[i] = points[idx].DaysToExpiry;
                iv[i] = points[idx].ImpliedVolatility;
            }

            // Perform linear regression: IV = intercept + slope * DTE
            // Note: SimpleRegression.Fit only reads the arrays
            (double intercept, double slope) = SimpleRegression.Fit(
                dte.AsSpan(0, n).ToArray(),
                iv.AsSpan(0, n).ToArray());

            double rSquared = CalculateRSquaredOptimized(dte.AsSpan(0, n), iv.AsSpan(0, n), intercept, slope);

            TermStructureAnalysis analysis = new TermStructureAnalysis
            {
                Intercept = intercept,
                Slope = slope,
                RSquared = rSquared
            };

            // Add sorted points to the collection
            for (int i = 0; i < n; i++)
            {
                analysis.Points.Add(points[indices[i]]);
            }

            return analysis;
        }
        finally
        {
            ArrayPool<double>.Shared.Return(dte);
            ArrayPool<double>.Shared.Return(iv);
            ArrayPool<int>.Shared.Return(indices);
        }
    }

    /// <summary>
    /// Calculates R-squared for the linear fit using Span (zero allocation).
    /// </summary>
    private static double CalculateRSquaredOptimized(ReadOnlySpan<double> x, ReadOnlySpan<double> y, double intercept, double slope)
    {
        // Calculate mean
        double ySum = 0;
        for (int i = 0; i < y.Length; i++)
        {
            ySum += y[i];
        }
        double yMean = ySum / y.Length;

        // Calculate SS Total and SS Residual in single pass
        double ssTotal = 0;
        double ssResidual = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double yDiff = y[i] - yMean;
            ssTotal += yDiff * yDiff;

            double predicted = intercept + (slope * x[i]);
            double residual = y[i] - predicted;
            ssResidual += residual * residual;
        }

        return 1.0 - (ssResidual / ssTotal);
    }
}

/// <summary>
/// Represents a single point in the implied volatility term structure.
/// </summary>
public sealed class TermStructurePoint
{
    /// <summary>
    /// Gets the number of days until option expiration.
    /// </summary>
    public int DaysToExpiry { get; init; }

    /// <summary>
    /// Gets the implied volatility (annual, as a decimal).
    /// </summary>
    public double ImpliedVolatility { get; init; }

    /// <summary>
    /// Gets the strike price of the option.
    /// </summary>
    public double Strike { get; init; }

    /// <summary>
    /// Gets optional metadata about the option contract.
    /// </summary>
    public string? Metadata { get; init; }
}

/// <summary>
/// Results of term structure analysis including slope and intercept.
/// </summary>
public sealed class TermStructureAnalysis
{
    /// <summary>
    /// Gets the intercept of the linear regression (IV at DTE=0).
    /// </summary>
    public double Intercept { get; init; }

    /// <summary>
    /// Gets the slope of the term structure.
    /// Negative slope indicates inverted term structure (backwardation).
    /// </summary>
    public double Slope { get; init; }

    /// <summary>
    /// Gets the R-squared value of the linear fit.
    /// </summary>
    public double RSquared { get; init; }

    /// <summary>
    /// Gets the original data points used in the analysis.
    /// </summary>
    public IList<TermStructurePoint> Points { get; } = new List<TermStructurePoint>();

    /// <summary>
    /// Gets the implied volatility at a specific number of days to expiry.
    /// </summary>
    public double GetIVAt(int daysToExpiry)
    {
        return Intercept + (Slope * daysToExpiry);
    }

    /// <summary>
    /// Determines if the term structure is inverted (negative slope).
    /// </summary>
    public bool IsInverted => Slope < 0;

    /// <summary>
    /// Determines if the slope meets the trading criterion (slope &lt;= -0.00406).
    /// </summary>
    public bool MeetsTradingCriterion => Slope <= -0.00406;
}

/// <summary>
/// Helper class for term structure analysis.
/// </summary>
public sealed class TermStructureAnalyzer
{
    private readonly TermStructure _termStructure = new();

    /// <summary>
    /// Analyzes term structure points.
    /// </summary>
    public TermStructureAnalysis Analyze(IReadOnlyList<TermStructurePoint> points)
    {
        return _termStructure.Analyze(points);
    }
}