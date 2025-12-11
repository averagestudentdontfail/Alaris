// =============================================================================
// STHD009A.cs - Pin Risk Monitor
// Component: STHD009A | Category: Hedge | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.7
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

using Alaris.Strategy.Calendar;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Hedge;

/// <summary>
/// Monitors pin risk for options near expiry when underlying is close to strike.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mathematical Foundation</b>
/// </para>
/// 
/// <para>
/// At expiry, the option payoff is discontinuous at S = K. Pre-expiry gamma:
/// <code>
/// Γ ∝ 1/√τ × e^(-d₁²/2)
/// </code>
/// </para>
/// 
/// <para>
/// <b>Pin Risk</b>
/// </para>
/// 
/// <para>
/// Pin risk arises when the underlying "pins" near strike, causing:
/// - High gamma → large delta swings from small price moves
/// - Uncertain assignment (for short options)
/// - Hedge rebalancing becomes costly/infeasible
/// </para>
/// 
/// <para>
/// <b>Pin Risk Score</b>
/// </para>
/// 
/// <para>
/// <code>
/// PRS = (Γ × S²) / notional × 𝟙{|S - K| / K &lt; ε_pin}
/// </code>
/// where ε_pin = 1% defines the pin zone.
/// </para>
/// </remarks>
public sealed class STHD009A
{
    private readonly ILogger<STHD009A>? _logger;

    /// <summary>
    /// Pin zone threshold (fraction of strike).
    /// Position is "pinned" if |S - K| / K &lt; this value.
    /// </summary>
    public const double PinZoneThreshold = 0.01; // 1%

    /// <summary>
    /// Days to expiry below which pin risk becomes significant.
    /// </summary>
    public const int MinDaysForPinRisk = 3;

    // LoggerMessage delegates
    private static readonly Action<ILogger, double, int, string, Exception?> LogPinRiskDetected =
        LoggerMessage.Define<double, int, string>(
            LogLevel.Warning,
            new EventId(1, nameof(LogPinRiskDetected)),
            "Pin risk detected: moneyness={Moneyness:P2}, DTE={DaysToExpiry}, action={Action}");

    /// <summary>
    /// Initialises a new instance of the pin risk monitor.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public STHD009A(ILogger<STHD009A>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculates how far the underlying is from the strike (moneyness deviation).
    /// </summary>
    /// <param name="spotPrice">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <returns>Absolute deviation as fraction of strike.</returns>
    public static double CalculateMoneynessDeviation(double spotPrice, double strike)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spotPrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strike);

        return Math.Abs(spotPrice - strike) / strike;
    }

    /// <summary>
    /// Determines if the position is in the pin zone.
    /// </summary>
    /// <param name="spotPrice">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <returns>True if within pin zone threshold.</returns>
    public static bool IsInPinZone(double spotPrice, double strike)
    {
        return CalculateMoneynessDeviation(spotPrice, strike) < PinZoneThreshold;
    }

    /// <summary>
    /// Evaluates pin risk for a position.
    /// </summary>
    /// <param name="spotPrice">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="daysToExpiry">Days until expiration.</param>
    /// <param name="gamma">Position gamma (optional, estimated if not provided).</param>
    /// <param name="contracts">Number of contracts.</param>
    /// <returns>Pin risk evaluation result.</returns>
    public STHD010A Evaluate(
        double spotPrice,
        double strike,
        int daysToExpiry,
        double? gamma,
        int contracts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spotPrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strike);
        ArgumentOutOfRangeException.ThrowIfNegative(daysToExpiry);
        ArgumentOutOfRangeException.ThrowIfNegative(contracts);

        double moneynessDeviation = CalculateMoneynessDeviation(spotPrice, strike);
        bool inPinZone = moneynessDeviation < PinZoneThreshold;
        bool nearExpiry = daysToExpiry <= MinDaysForPinRisk;

        // Estimate gamma if not provided (ATM near expiry approximation)
        double effectiveGamma = gamma ?? EstimateAtmGamma(spotPrice, daysToExpiry);

        // Calculate pin risk score
        double pinRiskScore = 0.0;
        if (inPinZone && nearExpiry)
        {
            // Higher score for closer to ATM and closer to expiry
            double proximityFactor = 1.0 - (moneynessDeviation / PinZoneThreshold);
            double timeFactor = 1.0 - ((double)daysToExpiry / MinDaysForPinRisk);
            pinRiskScore = proximityFactor * timeFactor;
        }

        // Determine risk level
        STHD011A riskLevel;
        STHD012A recommendedAction;

        if (!inPinZone)
        {
            riskLevel = STHD011A.None;
            recommendedAction = STHD012A.Hold;
        }
        else if (!nearExpiry)
        {
            riskLevel = STHD011A.Low;
            recommendedAction = STHD012A.Hold;
        }
        else if (daysToExpiry <= 1 && pinRiskScore > 0.7)
        {
            riskLevel = STHD011A.Critical;
            recommendedAction = STHD012A.CloseEarly;
        }
        else if (daysToExpiry <= 2 && pinRiskScore > 0.5)
        {
            riskLevel = STHD011A.High;
            recommendedAction = STHD012A.RollOut;
        }
        else if (pinRiskScore > 0.3)
        {
            riskLevel = STHD011A.Elevated;
            recommendedAction = STHD012A.ReduceSize;
        }
        else
        {
            riskLevel = STHD011A.Moderate;
            recommendedAction = STHD012A.Hold;
        }

        if (riskLevel >= STHD011A.Elevated)
        {
            SafeLog(() => LogPinRiskDetected(_logger!, moneynessDeviation, daysToExpiry,
                recommendedAction.ToString(), null));
        }

        return new STHD010A
        {
            IsInPinZone = inPinZone,
            MoneynessDeviation = moneynessDeviation,
            DaysToExpiry = daysToExpiry,
            EstimatedGamma = effectiveGamma,
            PinRiskScore = pinRiskScore,
            RiskLevel = riskLevel,
            RecommendedAction = recommendedAction,
            Rationale = GenerateRationale(riskLevel, moneynessDeviation, daysToExpiry)
        };
    }

    /// <summary>
    /// Estimates ATM gamma near expiry (simplified approximation).
    /// </summary>
    private static double EstimateAtmGamma(double spotPrice, int daysToExpiry)
    {
        if (daysToExpiry <= 0)
        {
            return 0.0;
        }

        const double TypicalVol = 0.30;
        double timeToExpiry = TradingCalendarDefaults.DteToYears(daysToExpiry);
        double sqrtT = Math.Sqrt(timeToExpiry);

        // ATM gamma ≈ φ(0) / (S × σ × √T) ≈ 0.4 / (S × σ × √T)
        return 0.4 / (spotPrice * TypicalVol * sqrtT);
    }

    /// <summary>
    /// Generates human-readable rationale.
    /// </summary>
    private static string GenerateRationale(STHD011A riskLevel, double deviation, int dte)
    {
        return riskLevel switch
        {
            STHD011A.Critical => $"Critical pin risk: {deviation:P1} from strike, {dte} DTE - close immediately",
            STHD011A.High => $"High pin risk: {deviation:P1} from strike, {dte} DTE - roll to later expiry",
            STHD011A.Elevated => $"Elevated pin risk: {deviation:P1} from strike, {dte} DTE - consider reducing",
            STHD011A.Moderate => $"Moderate pin risk: {deviation:P1} from strike, {dte} DTE - monitor closely",
            STHD011A.Low => $"Low pin risk: near strike but {dte} DTE provides buffer",
            _ => "No pin risk: position not near strike"
        };
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

// =============================================================================
// Supporting Types
// =============================================================================

/// <summary>
/// Pin risk evaluation result.
/// </summary>
public sealed record STHD010A
{
    /// <summary>Whether position is in the pin zone.</summary>
    public required bool IsInPinZone { get; init; }

    /// <summary>Deviation from strike as fraction.</summary>
    public required double MoneynessDeviation { get; init; }

    /// <summary>Days until expiration.</summary>
    public required int DaysToExpiry { get; init; }

    /// <summary>Estimated or provided gamma.</summary>
    public required double EstimatedGamma { get; init; }

    /// <summary>Pin risk score (0-1).</summary>
    public required double PinRiskScore { get; init; }

    /// <summary>Risk level classification.</summary>
    public required STHD011A RiskLevel { get; init; }

    /// <summary>Recommended action.</summary>
    public required STHD012A RecommendedAction { get; init; }

    /// <summary>Human-readable rationale.</summary>
    public required string Rationale { get; init; }
}

/// <summary>
/// Pin risk levels.
/// </summary>
public enum STHD011A
{
    /// <summary>Not in pin zone.</summary>
    None = 0,

    /// <summary>In pin zone but far from expiry.</summary>
    Low = 1,

    /// <summary>Approaching pin risk danger zone.</summary>
    Moderate = 2,

    /// <summary>Significant pin risk.</summary>
    Elevated = 3,

    /// <summary>High pin risk - action recommended.</summary>
    High = 4,

    /// <summary>Critical pin risk - immediate action required.</summary>
    Critical = 5
}

/// <summary>
/// Recommended actions for pin risk.
/// </summary>
public enum STHD012A
{
    /// <summary>Maintain current position.</summary>
    Hold = 0,

    /// <summary>Reduce position size.</summary>
    ReduceSize = 1,

    /// <summary>Roll to later expiry.</summary>
    RollOut = 2,

    /// <summary>Close position before expiry.</summary>
    CloseEarly = 3
}
