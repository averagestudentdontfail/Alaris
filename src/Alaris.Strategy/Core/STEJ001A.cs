// STEJ001A.cs - earnings jump risk calibrator

using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Core;

/// <summary>
/// Calibrates and evaluates earnings jump risk from historical announcement data.
/// </summary>

public sealed class STEJ001A
{
    private readonly ILogger<STEJ001A>? _logger;

    /// <summary>
    /// Minimum number of historical earnings events for calibration.
    /// </summary>
    private const int MinDataPoints = 8;

    /// <summary>
    /// Default Laplace scale parameter if insufficient data.
    /// </summary>
    private const double DefaultLaplaceScale = 0.40;

    // LoggerMessage delegates
    private static readonly Action<ILogger, int, double, double, Exception?> LogCalibration =
        LoggerMessage.Define<int, double, double>(
            LogLevel.Information,
            new EventId(1, nameof(LogCalibration)),
            "Jump risk calibration: N={DataPoints}, Lambda={Lambda:F3}, LaplaceB={LaplaceB:F4}");

    /// <summary>
    /// Initialises a new instance of the earnings jump risk calibrator.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public STEJ001A(ILogger<STEJ001A>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calibrates the jump distribution from historical earnings moves.
    /// </summary>
    /// <param name="historicalMoves">Standardised earnings moves (actual / implied).</param>
    /// <returns>Calibrated jump distribution parameters.</returns>
    /// <exception cref="ArgumentNullException">Thrown when historicalMoves is null.</exception>
    public STEJ002A CalibrateFromMoves(IReadOnlyList<double> historicalMoves)
    {
        ArgumentNullException.ThrowIfNull(historicalMoves);

        if (historicalMoves.Count < MinDataPoints)
        {
            return STEJ002A.DefaultParameters();
        }

        // Calculate statistics
        List<double> absMoves = new List<double>(historicalMoves.Count);
        double sumAbs = 0.0;
        double maxObserved = 0.0;
        for (int i = 0; i < historicalMoves.Count; i++)
        {
            double absMove = Math.Abs(historicalMoves[i]);
            absMoves.Add(absMove);
            sumAbs += absMove;
            if (absMove > maxObserved)
            {
                maxObserved = absMove;
            }
        }

        double mean = sumAbs / absMoves.Count;
        double sumSquared = 0.0;
        for (int i = 0; i < absMoves.Count; i++)
        {
            double diff = absMoves[i] - mean;
            sumSquared += diff * diff;
        }

        double variance = sumSquared / absMoves.Count;
        double stdDev = Math.Sqrt(variance);

        // Estimate mixture parameters using method of moments
        // Normal: Var = 1, Laplace: Var = 2b²
        // Mixture: Var = λ×1 + (1-λ)×2b²

        // Count outliers (beyond 2 sigma) to estimate Laplace weight
        double outlierThreshold = mean + (2 * stdDev);
        int outlierCount = 0;
        double outlierSum = 0.0;
        for (int i = 0; i < absMoves.Count; i++)
        {
            double absMove = absMoves[i];
            if (absMove > outlierThreshold)
            {
                outlierCount++;
                outlierSum += absMove;
            }
        }

        double empiricalOutlierRate = (double)outlierCount / absMoves.Count;

        // Normal distribution has ~2.3% beyond 2σ
        // Laplace has higher tail probability
        double normalOutlierRate = 0.023;
        double estimatedLaplaceWeight = Math.Max(0, (empiricalOutlierRate - normalOutlierRate) / (0.10 - normalOutlierRate));
        estimatedLaplaceWeight = Math.Clamp(estimatedLaplaceWeight, 0.05, 0.50);

        double mixtureWeight = 1.0 - estimatedLaplaceWeight;

        // Estimate Laplace scale from outliers
        double laplaceScale = outlierCount > 0
            ? (outlierSum / outlierCount) - outlierThreshold
            : DefaultLaplaceScale;
        laplaceScale = Math.Clamp(laplaceScale, 0.20, 1.0);

        SafeLog(() => LogCalibration(_logger!, historicalMoves.Count, mixtureWeight, laplaceScale, null));

        return new STEJ002A
        {
            MixtureWeight = mixtureWeight,
            LaplaceScale = laplaceScale,
            DataPointCount = historicalMoves.Count,
            MeanAbsoluteMove = mean,
            MaxObservedMove = maxObserved,
            CalibrationTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Calculates the jump risk score (probability of exceeding threshold).
    /// </summary>
    /// <param name="parameters">Calibrated distribution parameters.</param>
    /// <param name="impliedMove">Expected move from options (σ√T).</param>
    /// <param name="thresholdMultiple">Multiple of implied move to test (default 2.0).</param>
    /// <returns>Probability of move exceeding threshold.</returns>
    public double CalculateJumpRiskScore(
        STEJ002A parameters,
        double impliedMove,
        double thresholdMultiple = 2.0)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(impliedMove);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thresholdMultiple);

        // P(Z > k) = λ × Φ(-k) + (1-λ) × 0.5 × exp(-k/b)
        double k = thresholdMultiple;
        double normalTail = parameters.MixtureWeight * NormalCdfComplement(k);
        double laplaceTail = (1.0 - parameters.MixtureWeight) * 0.5 * Math.Exp(-k / parameters.LaplaceScale);

        return normalTail + laplaceTail;
    }

    /// <summary>
    /// Evaluates jump risk for a position and recommends adjustments.
    /// </summary>
    /// <param name="parameters">Calibrated distribution parameters.</param>
    /// <param name="impliedMove">Expected move from options.</param>
    /// <param name="currentAllocation">Current portfolio allocation.</param>
    /// <returns>Jump risk evaluation result.</returns>
    public STEJ003A EvaluateJumpRisk(
        STEJ002A parameters,
        double impliedMove,
        double currentAllocation)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(impliedMove);

        // Calculate tail risk probabilities
        double prob2x = CalculateJumpRiskScore(parameters, impliedMove, 2.0);
        double prob3x = CalculateJumpRiskScore(parameters, impliedMove, 3.0);

        // Determine risk level based on probability and max observed
        STEJ004A riskLevel;
        double allocationAdjustment;

        if (prob2x > 0.10 || parameters.MaxObservedMove > 3.0)
        {
            riskLevel = STEJ004A.High;
            allocationAdjustment = 0.50; // Cut to 50%
        }
        else if (prob2x > 0.05 || parameters.MaxObservedMove > 2.5)
        {
            riskLevel = STEJ004A.Elevated;
            allocationAdjustment = 0.75; // Cut to 75%
        }
        else if (prob2x > 0.03 || parameters.MaxObservedMove > 2.0)
        {
            riskLevel = STEJ004A.Moderate;
            allocationAdjustment = 0.90; // Minor reduction
        }
        else
        {
            riskLevel = STEJ004A.Low;
            allocationAdjustment = 1.0; // No adjustment
        }

        double adjustedAllocation = currentAllocation * allocationAdjustment;

        return new STEJ003A
        {
            RiskLevel = riskLevel,
            Prob2xImpliedMove = prob2x,
            Prob3xImpliedMove = prob3x,
            MaxHistoricalMove = parameters.MaxObservedMove,
            AllocationAdjustmentFactor = allocationAdjustment,
            RecommendedAllocation = adjustedAllocation,
            Rationale = riskLevel switch
            {
                STEJ004A.High => $"High jump risk: {prob2x:P1} chance of 2x implied move, max observed {parameters.MaxObservedMove:F2}x",
                STEJ004A.Elevated => $"Elevated jump risk: {prob2x:P1} chance of 2x implied move",
                STEJ004A.Moderate => $"Moderate jump risk: {prob2x:P1} chance of 2x implied move",
                _ => "Jump risk within normal bounds"
            }
        };
    }

    /// <summary>
    /// Complement of standard normal CDF: P(Z > z).
    /// </summary>
    private static double NormalCdfComplement(double z)
    {
        // Approximation using error function
        // Φ(z) ≈ 0.5 × (1 + erf(z/√2))
        // P(Z > z) = 1 - Φ(z) = 0.5 × erfc(z/√2)
        return 0.5 * Erfc(z / Math.Sqrt(2));
    }

    /// <summary>
    /// Complementary error function approximation.
    /// </summary>
    private static double Erfc(double x)
    {
        // Abramowitz and Stegun approximation (7.1.26)
        double t = 1.0 / (1.0 + (0.5 * Math.Abs(x)));
        double exponent = (-(x * x)) - 1.26551223 +
            (t * (1.00002368 +
            (t * (0.37409196 +
            (t * (0.09678418 +
            (t * (-0.18628806 +
            (t * (0.27886807 +
            (t * (-1.13520398 +
            (t * (1.48851587 +
            (t * (-0.82215223 +
            (t * 0.17087277)))))))))))))))));
        double tau = t * Math.Exp(exponent);

        return x >= 0 ? tau : 2.0 - tau;
    }

    /// <summary>
    /// Safely executes logging operation with fault isolation.
    /// </summary>
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
        catch (Exception)
        {
            // Swallow logging exceptions
        }
#pragma warning restore CA1031
    }
}

// Supporting Types

/// <summary>
/// Calibrated earnings jump distribution parameters.
/// </summary>
public sealed record STEJ002A
{
    /// <summary>Mixture weight for Normal component (λ).</summary>
    public required double MixtureWeight { get; init; }

    /// <summary>Scale parameter for Laplace component (b).</summary>
    public required double LaplaceScale { get; init; }

    /// <summary>Number of data points used in calibration.</summary>
    public required int DataPointCount { get; init; }

    /// <summary>Mean absolute standardised move.</summary>
    public required double MeanAbsoluteMove { get; init; }

    /// <summary>Maximum observed standardised move.</summary>
    public required double MaxObservedMove { get; init; }

    /// <summary>Time of calibration.</summary>
    public required DateTime CalibrationTime { get; init; }

    /// <summary>Creates default parameters when insufficient data.</summary>
    public static STEJ002A DefaultParameters() => new STEJ002A
    {
        MixtureWeight = 0.70,
        LaplaceScale = 0.40,
        DataPointCount = 0,
        MeanAbsoluteMove = 1.0,
        MaxObservedMove = 2.0,
        CalibrationTime = DateTime.UtcNow
    };
}

/// <summary>
/// Jump risk evaluation result.
/// </summary>
public sealed record STEJ003A
{
    /// <summary>Risk level classification.</summary>
    public required STEJ004A RiskLevel { get; init; }

    /// <summary>Probability of move exceeding 2x implied.</summary>
    public required double Prob2xImpliedMove { get; init; }

    /// <summary>Probability of move exceeding 3x implied.</summary>
    public required double Prob3xImpliedMove { get; init; }

    /// <summary>Maximum historical standardised move.</summary>
    public required double MaxHistoricalMove { get; init; }

    /// <summary>Recommended allocation adjustment factor (0-1).</summary>
    public required double AllocationAdjustmentFactor { get; init; }

    /// <summary>Recommended allocation after adjustment.</summary>
    public required double RecommendedAllocation { get; init; }

    /// <summary>Human-readable rationale.</summary>
    public required string Rationale { get; init; }
}

/// <summary>
/// Jump risk levels based on tail probability.
/// </summary>
public enum STEJ004A
{
    /// <summary>Low jump risk - normal distribution.</summary>
    Low = 0,

    /// <summary>Moderate jump risk - some fat tails.</summary>
    Moderate = 1,

    /// <summary>Elevated jump risk - significant fat tails.</summary>
    Elevated = 2,

    /// <summary>High jump risk - extreme tails observed.</summary>
    High = 3
}
