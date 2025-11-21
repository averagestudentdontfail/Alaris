using MathNet.Numerics;
using MathNet.Numerics.LinearRegression;

namespace Alaris.Strategy.Core;

/// <summary>
/// Analyzes the term structure of implied volatility.
/// Used to identify inverted term structures that signal trading opportunities.
/// </summary>
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

        // Sort by days to expiry
        List<TermStructurePoint> sortedPoints = points.OrderBy(p => p.DaysToExpiry).ToList();

        // Extract arrays for regression
        double[] dte = sortedPoints.Select(p => (double)p.DaysToExpiry).ToArray();
        double[] iv = sortedPoints.Select(p => p.ImpliedVolatility).ToArray();

        // Perform linear regression: IV = intercept + slope * DTE
        (double intercept, double slope) = SimpleRegression.Fit(dte, iv);

        TermStructureAnalysis analysis = new TermStructureAnalysis
        {
            Intercept = intercept,
            Slope = slope,
            RSquared = CalculateRSquared(dte, iv, intercept, slope)
        };

        // Add sorted points to the collection
        foreach (TermStructurePoint point in sortedPoints)
        {
            analysis.Points.Add(point);
        }

        return analysis;
    }

    /// <summary>
    /// Calculates R-squared for the linear fit.
    /// </summary>
    private static double CalculateRSquared(double[] x, double[] y, double intercept, double slope)
    {
        double yMean = y.Average();
        double ssTotal = y.Sum(yi => Math.Pow(yi - yMean, 2));

        double ssResidual = 0.0;
        for (int i = 0; i < x.Length; i++)
        {
            double predicted = intercept + (slope * x[i]);
            ssResidual += Math.Pow(y[i] - predicted, 2);
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
    /// Gets or sets the number of days until option expiration.
    /// </summary>
    public int DaysToExpiry { get; set; }

    /// <summary>
    /// Gets or sets the implied volatility (annual, as a decimal).
    /// </summary>
    public double ImpliedVolatility { get; set; }

    /// <summary>
    /// Gets or sets the strike price of the option.
    /// </summary>
    public double Strike { get; set; }

    /// <summary>
    /// Gets or sets optional metadata about the option contract.
    /// </summary>
    public string? Metadata { get; set; }
}

/// <summary>
/// Results of term structure analysis including slope and intercept.
/// </summary>
public sealed class TermStructureAnalysis
{
    /// <summary>
    /// Gets or sets the intercept of the linear regression (IV at DTE=0).
    /// </summary>
    public double Intercept { get; set; }

    /// <summary>
    /// Gets or sets the slope of the term structure.
    /// Negative slope indicates inverted term structure (backwardation).
    /// </summary>
    public double Slope { get; set; }

    /// <summary>
    /// Gets or sets the R-squared value of the linear fit.
    /// </summary>
    public double RSquared { get; set; }

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