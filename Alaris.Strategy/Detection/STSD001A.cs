// =============================================================================
// STSD001A.cs - Neyman-Pearson Signal Detection Framework
// Component: STSD001A | Category: Detection | Variant: A (Primary)
// =============================================================================
// Reference: Neyman-Pearson Lemma (1933), Alaris Phase 3 Specification
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Detection;

/// <summary>
/// Implements the Neyman-Pearson optimal signal detection framework.
/// </summary>
/// <remarks>
/// <para>
/// This component provides statistically optimal thresholds for signal detection
/// based on the Neyman-Pearson lemma. Given calibrated distributions for profitable
/// and unprofitable trade outcomes, it computes the likelihood ratio test threshold
/// that minimises Type II errors (missed opportunities) subject to a constraint on
/// Type I errors (bad trades).
/// </para>
/// <para>
/// Mathematical foundation:
/// For testing H₀: signal unprofitable vs H₁: signal profitable,
/// the most powerful test at level α has rejection region:
/// C = {x : L(θ₁; x) / L(θ₀; x) > k_α}
/// where k_α is chosen such that P(X ∈ C | H₀) = α
/// </para>
/// </remarks>
public sealed class STSD001A
{
    private readonly ILogger<STSD001A>? _logger;

    // Distribution parameters under H₀ (unprofitable trades)
    private double _mu0;
    private double _sigma0;

    // Distribution parameters under H₁ (profitable trades)
    private double _mu1;
    private double _sigma1;

    // Prior probabilities
    private double _pi0;
    private double _pi1;

    // Calibration state
    private bool _isCalibrated;
    private int _calibrationSampleSize;

    // LoggerMessage delegates
    private static readonly Action<ILogger, int, int, Exception?> LogCalibrationComplete =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogCalibrationComplete)),
            "Signal detection calibration complete: {ProfitableCount} profitable, {UnprofitableCount} unprofitable trades");

    private static readonly Action<ILogger, double, double, double, double, Exception?> LogDistributionParameters =
        LoggerMessage.Define<double, double, double, double>(
            LogLevel.Debug,
            new EventId(2, nameof(LogDistributionParameters)),
            "Distribution parameters: H₀(μ={Mu0:F4}, σ={Sigma0:F4}), H₁(μ={Mu1:F4}, σ={Sigma1:F4})");

    private static readonly Action<ILogger, double, double, Exception?> LogThresholdComputed =
        LoggerMessage.Define<double, double>(
            LogLevel.Information,
            new EventId(3, nameof(LogThresholdComputed)),
            "Optimal threshold computed: {Threshold:F4} (cost ratio={CostRatio:F2})");

    /// <summary>Mean IV/RV ratio for unprofitable trades.</summary>
    public const double DefaultMu0 = 1.10;

    /// <summary>Std dev of IV/RV ratio for unprofitable trades.</summary>
    public const double DefaultSigma0 = 0.15;

    /// <summary>Mean IV/RV ratio for profitable trades.</summary>
    public const double DefaultMu1 = 1.35;

    /// <summary>Std dev of IV/RV ratio for profitable trades.</summary>
    public const double DefaultSigma1 = 0.20;

    /// <summary>Prior probability of unprofitable trade.</summary>
    public const double DefaultPi0 = 0.55;

    /// <summary>Prior probability of profitable trade.</summary>
    public const double DefaultPi1 = 0.45;

    /// <summary>Default cost ratio (Type I / Type II error costs).</summary>
    public const double DefaultCostRatio = 2.0;

    /// <summary>
    /// Initialises a new instance of the signal detector with default parameters.
    /// </summary>
    public STSD001A(ILogger<STSD001A>? logger = null)
    {
        _logger = logger;
        ResetToDefaults();
    }

    /// <summary>
    /// Gets whether the detector has been calibrated from historical data.
    /// </summary>
    public bool IsCalibrated => _isCalibrated;

    /// <summary>
    /// Gets the calibration sample size.
    /// </summary>
    public int CalibrationSampleSize => _calibrationSampleSize;

    /// <summary>
    /// Resets distribution parameters to empirical defaults.
    /// </summary>
    public void ResetToDefaults()
    {
        _mu0 = DefaultMu0;
        _sigma0 = DefaultSigma0;
        _mu1 = DefaultMu1;
        _sigma1 = DefaultSigma1;
        _pi0 = DefaultPi0;
        _pi1 = DefaultPi1;
        _isCalibrated = false;
        _calibrationSampleSize = 0;
    }

    /// <summary>
    /// Calibrates the detector from historical trade outcomes.
    /// </summary>
    /// <param name="outcomes">Historical trade outcomes with IV/RV ratio and profitability.</param>
    /// <exception cref="ArgumentException">Thrown when insufficient data for calibration.</exception>
    public void Calibrate(IReadOnlyList<TradeOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        var profitable = outcomes.Where(o => o.IsProfitable).Select(o => o.IVRVRatio).ToList();
        var unprofitable = outcomes.Where(o => !o.IsProfitable).Select(o => o.IVRVRatio).ToList();

        if (profitable.Count < 10 || unprofitable.Count < 10)
        {
            throw new ArgumentException(
                $"Insufficient calibration data: need at least 10 each of profitable ({profitable.Count}) " +
                $"and unprofitable ({unprofitable.Count}) outcomes");
        }

        // Compute distribution parameters
        _mu0 = unprofitable.Average();
        _sigma0 = ComputeStandardDeviation(unprofitable, _mu0);

        _mu1 = profitable.Average();
        _sigma1 = ComputeStandardDeviation(profitable, _mu1);

        // Compute prior probabilities
        int total = outcomes.Count;
        _pi0 = (double)unprofitable.Count / total;
        _pi1 = (double)profitable.Count / total;

        _isCalibrated = true;
        _calibrationSampleSize = total;

        SafeLog(() => LogCalibrationComplete(_logger!, profitable.Count, unprofitable.Count, null));
        SafeLog(() => LogDistributionParameters(_logger!, _mu0, _sigma0, _mu1, _sigma1, null));
    }

    /// <summary>
    /// Computes the optimal threshold for the given cost ratio.
    /// </summary>
    /// <param name="costRatio">Ratio of Type I error cost to Type II error cost (c_I / c_II).</param>
    /// <returns>The optimal IV/RV threshold.</returns>
    /// <remarks>
    /// For equal variances, threshold = (μ₀ + μ₁)/2 + σ²/(μ₁ - μ₀) × ln(ρ)
    /// For unequal variances, uses full quadratic solution.
    /// </remarks>
    public double ComputeOptimalThreshold(double costRatio = 2.0)
    {
        if (costRatio <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costRatio), "Cost ratio must be positive");
        }

        double threshold;

        // Check for approximately equal variances
        double varianceRatio = _sigma1 / _sigma0;
        if (Math.Abs(varianceRatio - 1.0) < 0.1)
        {
            // Equal variance approximation: simpler formula
            double avgSigma = (_sigma0 + _sigma1) / 2;
            double avgSigmaSq = avgSigma * avgSigma;
            threshold = ((_mu0 + _mu1) / 2) + (avgSigmaSq / (_mu1 - _mu0) * Math.Log(costRatio * _pi0 / _pi1));
        }
        else
        {
            // Full quadratic solution for unequal variances
            threshold = ComputeThresholdUnequelVariances(costRatio);
        }

        SafeLog(() => LogThresholdComputed(_logger!, threshold, costRatio, null));

        return threshold;
    }

    /// <summary>
    /// Evaluates a signal observation and returns detection result.
    /// </summary>
    /// <param name="observedValue">The observed IV/RV ratio.</param>
    /// <param name="threshold">Optional explicit threshold; uses cost ratio 2.0 if not specified.</param>
    /// <returns>Signal detection result with likelihood ratio and posterior probability.</returns>
    public SignalDetectionResult Evaluate(double observedValue, double? threshold = null)
    {
        double actualThreshold = threshold ?? ComputeOptimalThreshold(DefaultCostRatio);

        // Compute likelihood ratio
        double likelihoodRatio = ComputeLikelihoodRatio(observedValue);

        // Compute posterior probability using Bayes' theorem
        // P(H₁|x) = p(x|H₁)π₁ / [p(x|H₀)π₀ + p(x|H₁)π₁]
        double posterior = ComputePosteriorProbability(observedValue);

        bool passes = observedValue >= actualThreshold;

        return new SignalDetectionResult(
            PassesThreshold: passes,
            LikelihoodRatio: likelihoodRatio,
            PosteriorProbability: posterior,
            Threshold: actualThreshold,
            ObservedValue: observedValue);
    }

    /// <summary>
    /// Computes the likelihood ratio L(H₁)/L(H₀) for an observation.
    /// </summary>
    public double ComputeLikelihoodRatio(double x)
    {
        double logL0 = LogNormalPdf(x, _mu0, _sigma0);
        double logL1 = LogNormalPdf(x, _mu1, _sigma1);
        return Math.Exp(logL1 - logL0);
    }

    /// <summary>
    /// Computes the posterior probability P(H₁|x) using Bayes' theorem.
    /// </summary>
    public double ComputePosteriorProbability(double x)
    {
        double logP0 = LogNormalPdf(x, _mu0, _sigma0) + Math.Log(_pi0);
        double logP1 = LogNormalPdf(x, _mu1, _sigma1) + Math.Log(_pi1);

        // Log-sum-exp trick for numerical stability
        double maxLog = Math.Max(logP0, logP1);
        double logNorm = maxLog + Math.Log(Math.Exp(logP0 - maxLog) + Math.Exp(logP1 - maxLog));

        return Math.Exp(logP1 - logNorm);
    }

    /// <summary>
    /// Computes the Type I error rate (false positive rate) for a given threshold.
    /// </summary>
    public double ComputeTypeIError(double threshold)
    {
        // P(X > threshold | H₀) = 1 - Φ((threshold - μ₀)/σ₀)
        double z = (threshold - _mu0) / _sigma0;
        return 1.0 - NormalCdf(z);
    }

    /// <summary>
    /// Computes the Type II error rate (false negative rate) for a given threshold.
    /// </summary>
    public double ComputeTypeIIError(double threshold)
    {
        // P(X ≤ threshold | H₁) = Φ((threshold - μ₁)/σ₁)
        double z = (threshold - _mu1) / _sigma1;
        return NormalCdf(z);
    }

    /// <summary>
    /// Computes the power (1 - β) of the test for a given threshold.
    /// </summary>
    public double ComputePower(double threshold)
    {
        return 1.0 - ComputeTypeIIError(threshold);
    }

    /// <summary>
    /// Gets the current distribution parameters.
    /// </summary>
    public DistributionParameters GetParameters()
    {
        return new DistributionParameters(
            Mu0: _mu0,
            Sigma0: _sigma0,
            Mu1: _mu1,
            Sigma1: _sigma1,
            Pi0: _pi0,
            Pi1: _pi1);
    }

    #region Private Methods

    private double ComputeThresholdUnequelVariances(double costRatio)
    {
        // Full quadratic solution when σ₀ ≠ σ₁
        // L(x) = (σ₀/σ₁) × exp[-½((x-μ₁)²/σ₁² - (x-μ₀)²/σ₀²)] > k
        // Taking log and rearranging gives a quadratic equation

        double sigma0Sq = _sigma0 * _sigma0;
        double sigma1Sq = _sigma1 * _sigma1;

        double a = (1.0 / sigma0Sq) - (1.0 / sigma1Sq);
        double b = 2.0 * ((_mu1 / sigma1Sq) - (_mu0 / sigma0Sq));
        double c = (_mu0 * _mu0 / sigma0Sq) - (_mu1 * _mu1 / sigma1Sq)
                   - (2.0 * Math.Log(_sigma1 / _sigma0 * costRatio * _pi0 / _pi1));

        // Solve ax² + bx + c = 0
        double discriminant = (b * b) - (4.0 * a * c);

        if (discriminant < 0)
        {
            // No real solution - use midpoint
            return (_mu0 + _mu1) / 2;
        }

        double sqrtD = Math.Sqrt(discriminant);
        double x1 = (-b + sqrtD) / (2.0 * a);
        double x2 = (-b - sqrtD) / (2.0 * a);

        // Choose the solution that falls between the means
        if (x1 >= _mu0 && x1 <= _mu1)
        {
            return x1;
        }

        if (x2 >= _mu0 && x2 <= _mu1)
        {
            return x2;
        }

        // If neither solution is between means, choose the one closer to the midpoint
        double midpoint = (_mu0 + _mu1) / 2;
        return Math.Abs(x1 - midpoint) < Math.Abs(x2 - midpoint) ? x1 : x2;
    }

    private static double ComputeStandardDeviation(List<double> values, double mean)
    {
        double sumSq = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSq / (values.Count - 1));
    }

    private static double LogNormalPdf(double x, double mu, double sigma)
    {
        double z = (x - mu) / sigma;
        return (-0.5 * z * z) - Math.Log(sigma) - (0.5 * Math.Log(2 * Math.PI));
    }

    private static double NormalCdf(double z)
    {
        // Abramowitz and Stegun approximation
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        int sign = z < 0 ? -1 : 1;
        z = Math.Abs(z) / Math.Sqrt(2);

        double t = 1.0 / (1.0 + (p * z));
        double polynomial = (((((((a5 * t) + a4) * t) + a3) * t) + a2) * t) + a1;
        double y = 1.0 - (polynomial * t * Math.Exp(-(z * z)));

        return 0.5 * (1.0 + (sign * y));
    }

    private void SafeLog(Action logAction)
    {
        if (_logger == null)
        {
            return;
        }

#pragma warning disable CA1031
        try
        {
            logAction();
        }
        catch
        {
            /* Fault isolation per Rule 15 */
        }
#pragma warning restore CA1031
    }

    #endregion
}

/// <summary>
/// Represents the result of signal detection evaluation.
/// </summary>
/// <param name="PassesThreshold">Whether the observation exceeds the optimal threshold.</param>
/// <param name="LikelihoodRatio">The likelihood ratio L(H₁)/L(H₀).</param>
/// <param name="PosteriorProbability">Posterior probability P(H₁|observation).</param>
/// <param name="Threshold">The threshold used for evaluation.</param>
/// <param name="ObservedValue">The observed IV/RV ratio.</param>
public readonly record struct SignalDetectionResult(
    bool PassesThreshold,
    double LikelihoodRatio,
    double PosteriorProbability,
    double Threshold,
    double ObservedValue);

/// <summary>
/// Represents the calibrated distribution parameters.
/// </summary>
public readonly record struct DistributionParameters(
    double Mu0,
    double Sigma0,
    double Mu1,
    double Sigma1,
    double Pi0,
    double Pi1);

/// <summary>
/// Represents a historical trade outcome for calibration.
/// </summary>
public readonly record struct TradeOutcome(
    string Symbol,
    DateTime TradeDate,
    double IVRVRatio,
    double TermStructureSlope,
    double ProfitLoss,
    bool IsProfitable);
