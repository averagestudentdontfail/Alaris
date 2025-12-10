// =============================================================================
// STIV006A.cs - Volatility Surface Interpolator
// Component: STIV006A | Category: Core | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.2.3
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Core;

/// <summary>
/// Interpolates implied volatility across strikes using sticky-delta convention.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mathematical Foundation</b>
/// </para>
/// 
/// <para>
/// The implied volatility surface σ(K, T) exhibits "smile" or "skew" patterns
/// where IV varies with strike. For equity options, the typical pattern is:
/// <code>
/// σ(K) ≈ σ_ATM × (1 + ρ × ln(K/F) / (σ_ATM × √T))
/// </code>
/// where ρ &lt; 0 represents the negative correlation between spot and vol (leverage effect).
/// </para>
/// 
/// <para>
/// <b>Sticky-Delta Convention</b>
/// </para>
/// 
/// <para>
/// Under sticky-delta, the implied volatility for a given delta level remains
/// constant as spot moves. This means:
/// <code>
/// σ(Δ = 0.25, t) = σ(Δ = 0.25, t')   for all t, t'
/// </code>
/// The strike corresponding to that delta changes, but the vol doesn't.
/// </para>
/// 
/// <para>
/// <b>Skew Parameterisation</b>
/// </para>
/// 
/// <para>
/// We use a simplified SABR-lite parameterisation:
/// <code>
/// σ(K) = σ_ATM + β_skew × (Δ_ATM - Δ(K))
/// </code>
/// where β_skew is the 25-delta risk reversal slope (typically negative for equities).
/// </para>
/// 
/// <para>
/// Reference: Gatheral (2006) "The Volatility Surface: A Practitioner's Guide"
/// </para>
/// </remarks>
public sealed class STIV006A
{
    private readonly ILogger<STIV006A>? _logger;

    /// <summary>
    /// Default skew coefficient (25-delta risk reversal).
    /// Negative for equity (downside skew).
    /// </summary>
    private const double DefaultSkewCoefficient = -0.10;

    /// <summary>
    /// Minimum time to expiry for smile calculations.
    /// </summary>
    private const double MinTimeToExpiry = 1.0 / 252.0;

    // LoggerMessage delegates
    private static readonly Action<ILogger, double, double, double, Exception?> LogCalibration =
        LoggerMessage.Define<double, double, double>(
            LogLevel.Debug,
            new EventId(1, nameof(LogCalibration)),
            "Vol surface calibration: ATM_IV={AtmIV:F4}, Skew={Skew:F4}, TimeToExpiry={TimeToExpiry:F4}");

    /// <summary>
    /// Initialises a new instance of the volatility surface interpolator.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public STIV006A(ILogger<STIV006A>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calibrates the volatility surface from an option chain.
    /// </summary>
    /// <param name="chain">List of option quotes with strikes and IVs.</param>
    /// <param name="spotPrice">Current spot price.</param>
    /// <param name="timeToExpiry">Time to expiration in years.</param>
    /// <param name="riskFreeRate">Risk-free interest rate.</param>
    /// <returns>Calibrated volatility surface parameters.</returns>
    /// <exception cref="ArgumentNullException">Thrown when chain is null.</exception>
    /// <exception cref="ArgumentException">Thrown when chain has insufficient data.</exception>
    public STIV007A CalibrateFromChain(
        IReadOnlyList<STIV008A> chain,
        double spotPrice,
        double timeToExpiry,
        double riskFreeRate)
    {
        ArgumentNullException.ThrowIfNull(chain);

        if (chain.Count < 3)
        {
            throw new ArgumentException("At least 3 strikes required for calibration", nameof(chain));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spotPrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeToExpiry);

        // Find ATM strike (closest to spot)
        STIV008A atmQuote = chain
            .OrderBy(q => Math.Abs(q.Strike - spotPrice))
            .First();

        double atmIV = atmQuote.ImpliedVolatility;

        // Calculate skew from 25-delta options if available
        double skewCoefficient = CalculateSkewFromChain(chain, spotPrice, atmIV, timeToExpiry);

        SafeLog(() => LogCalibration(_logger!, atmIV, skewCoefficient, timeToExpiry, null));

        return new STIV007A
        {
            AtmImpliedVolatility = atmIV,
            SkewCoefficient = skewCoefficient,
            SpotPrice = spotPrice,
            TimeToExpiry = timeToExpiry,
            RiskFreeRate = riskFreeRate,
            CalibrationTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Interpolates implied volatility at an arbitrary strike.
    /// </summary>
    /// <param name="parameters">Calibrated surface parameters.</param>
    /// <param name="strike">Target strike price.</param>
    /// <returns>Interpolated implied volatility.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when strike is non-positive.</exception>
    public double InterpolateIV(STIV007A parameters, double strike)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strike);

        // Calculate moneyness (log-moneyness normalised by vol×sqrt(T))
        double logMoneyness = Math.Log(strike / parameters.SpotPrice);
        double normalisedMoneyness = logMoneyness /
            (parameters.AtmImpliedVolatility * Math.Sqrt(Math.Max(parameters.TimeToExpiry, MinTimeToExpiry)));

        // Apply skew adjustment
        // σ(K) = σ_ATM × (1 + ρ × normalised_moneyness)
        double skewAdjustment = parameters.SkewCoefficient * normalisedMoneyness;

        // Clamp adjustment to prevent extreme values
        skewAdjustment = Math.Clamp(skewAdjustment, -0.30, 0.30);

        double interpolatedIV = parameters.AtmImpliedVolatility * (1.0 + skewAdjustment);

        // Ensure IV remains positive and reasonable
        return Math.Clamp(interpolatedIV, 0.05, 3.0);
    }

    /// <summary>
    /// Calculates the IV change from spot movement (sticky-delta).
    /// </summary>
    /// <remarks>
    /// Under sticky-delta, when spot moves, the IV at the original strike changes:
    /// <code>
    /// Δσ = -β_skew × (ΔS/S) / (σ × √T)
    /// </code>
    /// </remarks>
    /// <param name="parameters">Calibrated surface parameters.</param>
    /// <param name="originalStrike">Original strike price.</param>
    /// <param name="originalSpot">Spot price at position entry.</param>
    /// <param name="currentSpot">Current spot price.</param>
    /// <returns>Estimated IV change at the original strike.</returns>
    public double CalculateStickyDeltaIVChange(
        STIV007A parameters,
        double originalStrike,
        double originalSpot,
        double currentSpot)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(originalStrike);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(originalSpot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(currentSpot);

        double spotReturn = (currentSpot - originalSpot) / originalSpot;
        double sqrtT = Math.Sqrt(Math.Max(parameters.TimeToExpiry, MinTimeToExpiry));

        // Under sticky-delta: IV at original strike changes as spot moves
        // dσ/dS = -ρ / (S × σ × √T) approximately
        double ivChange = -parameters.SkewCoefficient * spotReturn /
            (parameters.AtmImpliedVolatility * sqrtT);

        return ivChange;
    }

    /// <summary>
    /// Evaluates skew exposure risk for a calendar spread position.
    /// </summary>
    /// <param name="parameters">Calibrated surface parameters.</param>
    /// <param name="currentMoneyness">Current moneyness (S/K).</param>
    /// <param name="entryMoneyness">Entry moneyness (S/K at entry).</param>
    /// <returns>Skew exposure result.</returns>
    public STIV009A EvaluateSkewExposure(
        STIV007A parameters,
        double currentMoneyness,
        double entryMoneyness)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        double moneynessShift = currentMoneyness - entryMoneyness;
        double logMoneynessShift = Math.Log(currentMoneyness / entryMoneyness);

        // Calculate skew P&L impact
        // Approximate: ΔPnL_skew ≈ Vega × Δσ(from skew)
        double skewIVImpact = parameters.SkewCoefficient * logMoneynessShift /
            (parameters.AtmImpliedVolatility * Math.Sqrt(Math.Max(parameters.TimeToExpiry, MinTimeToExpiry)));

        STIV010A riskLevel = Math.Abs(moneynessShift) switch
        {
            < 0.02 => STIV010A.Low,
            < 0.05 => STIV010A.Moderate,
            < 0.10 => STIV010A.Elevated,
            _ => STIV010A.High
        };

        return new STIV009A
        {
            MoneynessShift = moneynessShift,
            EstimatedIVImpact = skewIVImpact,
            RiskLevel = riskLevel,
            ShouldRecenter = riskLevel >= STIV010A.Elevated,
            Rationale = riskLevel >= STIV010A.Elevated
                ? $"Position has drifted {Math.Abs(moneynessShift):P1} from ATM - consider recentering"
                : "Skew exposure within acceptable bounds"
        };
    }

    /// <summary>
    /// Calculates skew coefficient from option chain (25-delta risk reversal).
    /// </summary>
    private static double CalculateSkewFromChain(
        IReadOnlyList<STIV008A> chain,
        double spotPrice,
        double atmIV,
        double timeToExpiry)
    {
        // Find puts around 25-delta (≈ 0.85-0.90 moneyness) and calls around 25-delta (≈ 1.05-1.10 moneyness)
        List<STIV008A> lowStrikes = chain
            .Where(q => q.Strike < spotPrice * 0.95)
            .OrderByDescending(q => q.Strike)
            .Take(3)
            .ToList();

        List<STIV008A> highStrikes = chain
            .Where(q => q.Strike > spotPrice * 1.05)
            .OrderBy(q => q.Strike)
            .Take(3)
            .ToList();

        if (lowStrikes.Count == 0 || highStrikes.Count == 0)
        {
            return DefaultSkewCoefficient;
        }

        double avgLowIV = lowStrikes.Average(q => q.ImpliedVolatility);
        double avgHighIV = highStrikes.Average(q => q.ImpliedVolatility);

        // Risk reversal = Vol(OTM puts) - Vol(OTM calls)
        double riskReversal = avgLowIV - avgHighIV;

        // Convert to skew coefficient (normalise by ATM IV)
        double skew = riskReversal / (2.0 * atmIV);

        // Clamp to reasonable range
        return Math.Clamp(skew, -0.50, 0.10);
    }

    /// <summary>
    /// Safely executes logging operation with fault isolation (Rule 15).
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
/// Calibrated volatility surface parameters.
/// </summary>
public sealed record STIV007A
{
    /// <summary>At-the-money implied volatility.</summary>
    public required double AtmImpliedVolatility { get; init; }

    /// <summary>Skew coefficient (negative for equity downside skew).</summary>
    public required double SkewCoefficient { get; init; }

    /// <summary>Spot price at calibration.</summary>
    public required double SpotPrice { get; init; }

    /// <summary>Time to expiry in years.</summary>
    public required double TimeToExpiry { get; init; }

    /// <summary>Risk-free rate used.</summary>
    public required double RiskFreeRate { get; init; }

    /// <summary>Time of calibration.</summary>
    public required DateTime CalibrationTime { get; init; }
}

/// <summary>
/// Represents a single option quote for calibration.
/// </summary>
public sealed record STIV008A
{
    /// <summary>Strike price.</summary>
    public required double Strike { get; init; }

    /// <summary>Implied volatility (as decimal, e.g. 0.30 for 30%).</summary>
    public required double ImpliedVolatility { get; init; }

    /// <summary>True for call, false for put.</summary>
    public required bool IsCall { get; init; }

    /// <summary>Bid price.</summary>
    public double Bid { get; init; }

    /// <summary>Ask price.</summary>
    public double Ask { get; init; }
}

/// <summary>
/// Result of skew exposure evaluation.
/// </summary>
public sealed record STIV009A
{
    /// <summary>Moneyness shift from entry (current - entry).</summary>
    public required double MoneynessShift { get; init; }

    /// <summary>Estimated IV impact from skew.</summary>
    public required double EstimatedIVImpact { get; init; }

    /// <summary>Skew risk level.</summary>
    public required STIV010A RiskLevel { get; init; }

    /// <summary>Whether position should be recentered.</summary>
    public required bool ShouldRecenter { get; init; }

    /// <summary>Human-readable rationale.</summary>
    public required string Rationale { get; init; }
}

/// <summary>
/// Skew risk levels based on moneyness drift.
/// </summary>
public enum STIV010A
{
    /// <summary>Moneyness within 2% of entry.</summary>
    Low = 0,

    /// <summary>Moneyness 2-5% from entry.</summary>
    Moderate = 1,

    /// <summary>Moneyness 5-10% from entry.</summary>
    Elevated = 2,

    /// <summary>Moneyness beyond 10% from entry.</summary>
    High = 3
}
