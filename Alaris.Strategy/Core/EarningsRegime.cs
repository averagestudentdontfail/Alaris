namespace Alaris.Strategy.Core;

/// <summary>
/// Defines earnings-related regimes for IV modeling.
/// </summary>
public enum EarningsRegimeType
{
    /// <summary>
    /// No earnings event before option expiration.
    /// Use standard models (BSM, Heston, Kou).
    /// </summary>
    NoEarnings,

    /// <summary>
    /// Pre-earnings announcement regime.
    /// IV typically elevated due to anticipated jump.
    /// Use L&amp;S model or jump-augmented models.
    /// </summary>
    PreEarnings,

    /// <summary>
    /// Post-earnings announcement regime (0-5 days after).
    /// IV typically collapses as uncertainty resolves.
    /// Transitional period back to normal.
    /// </summary>
    PostEarningsTransition,

    /// <summary>
    /// Normal regime after earnings effects dissipate.
    /// Use standard continuous models.
    /// </summary>
    PostEarningsNormal
}

/// <summary>
/// Handles regime detection and switching around earnings announcements.
/// Determines appropriate model behavior based on proximity to earnings.
/// </summary>
public sealed class EarningsRegime
{
    /// <summary>
    /// Days after earnings for transition period.
    /// </summary>
    public const int TransitionDays = 5;

    /// <summary>
    /// Maximum days before earnings to consider pre-EA regime.
    /// </summary>
    public const int MaxPreEarningsDays = 60;

    /// <summary>
    /// Current regime type.
    /// </summary>
    public EarningsRegimeType RegimeType { get; }

    /// <summary>
    /// Time parameters for this regime.
    /// </summary>
    public TimeParameters TimeParams { get; }

    /// <summary>
    /// Days since earnings (negative if before, positive if after).
    /// </summary>
    public int? DaysSinceEarnings { get; }

    /// <summary>
    /// Blending weight for transition period [0, 1].
    /// 0 = full post-EA crush, 1 = normalized volatility.
    /// </summary>
    public double TransitionWeight { get; }

    /// <summary>
    /// Recommended model type for this regime.
    /// </summary>
    public RecommendedModel ModelRecommendation { get; }

    private EarningsRegime(
        EarningsRegimeType regimeType,
        TimeParameters timeParams,
        int? daysSinceEarnings,
        double transitionWeight,
        RecommendedModel modelRecommendation)
    {
        RegimeType = regimeType;
        TimeParams = timeParams;
        DaysSinceEarnings = daysSinceEarnings;
        TransitionWeight = transitionWeight;
        ModelRecommendation = modelRecommendation;
    }

    /// <summary>
    /// Detects the current earnings regime from time parameters.
    /// </summary>
    public static EarningsRegime Detect(TimeParameters timeParams)
    {
        ArgumentNullException.ThrowIfNull(timeParams);

        if (!timeParams.EarningsDate.HasValue || !timeParams.HasEarningsBeforeExpiry)
        {
            return new EarningsRegime(
                EarningsRegimeType.NoEarnings,
                timeParams,
                null,
                1.0,
                RecommendedModel.Heston);
        }

        if (timeParams.IsPreEarnings)
        {
            int daysToEarnings = timeParams.DaysToEarnings ?? 0;

            // Far from earnings - standard model
            if (daysToEarnings > MaxPreEarningsDays)
            {
                return new EarningsRegime(
                    EarningsRegimeType.NoEarnings,
                    timeParams,
                    -daysToEarnings,
                    1.0,
                    RecommendedModel.Heston);
            }

            // Pre-earnings regime
            return new EarningsRegime(
                EarningsRegimeType.PreEarnings,
                timeParams,
                -daysToEarnings,
                0.0,
                RecommendedModel.LeungSantoli);
        }

        // Post-earnings
        int daysSinceEa = timeParams.DaysToEarnings.HasValue
            ? -timeParams.DaysToEarnings.Value
            : 0;

        if (daysSinceEa <= TransitionDays)
        {
            // Transition period - blend IV crush with normalization
            double weight = (double)daysSinceEa / TransitionDays;

            return new EarningsRegime(
                EarningsRegimeType.PostEarningsTransition,
                timeParams,
                daysSinceEa,
                weight,
                RecommendedModel.PostEarningsBlend);
        }

        // Normal post-earnings
        return new EarningsRegime(
            EarningsRegimeType.PostEarningsNormal,
            timeParams,
            daysSinceEa,
            1.0,
            RecommendedModel.Heston);
    }

    /// <summary>
    /// Computes regime-adjusted implied volatility.
    /// </summary>
    /// <param name="baseIV">Base IV from continuous model (Heston/BSM).</param>
    /// <param name="earningsIV">IV with earnings component (L&amp;S).</param>
    /// <returns>Regime-adjusted IV.</returns>
    public double ComputeAdjustedIV(double baseIV, double earningsIV)
    {
        return RegimeType switch
        {
            EarningsRegimeType.NoEarnings => baseIV,
            EarningsRegimeType.PreEarnings => earningsIV,
            EarningsRegimeType.PostEarningsTransition => BlendIV(baseIV, earningsIV),
            EarningsRegimeType.PostEarningsNormal => baseIV,
            _ => baseIV
        };
    }

    /// <summary>
    /// Blends IV during transition period.
    /// Immediately after EA: IV drops sharply (crush).
    /// Over transition period: IV normalizes to base level.
    /// </summary>
    private double BlendIV(double baseIV, double earningsIV)
    {
        // Post-EA IV should be lower than pre-EA
        // Use base IV as the post-EA level
        // TransitionWeight interpolates from crushed level to normal

        // Estimate crushed IV (remove earnings component)
        double crushFactor = 1.0 - TransitionWeight;
        double crushAmount = (earningsIV - baseIV) * crushFactor;

        return baseIV + crushAmount * (1 - TransitionWeight);
    }
}

/// <summary>
/// Recommended model for a given regime.
/// </summary>
public enum RecommendedModel
{
    /// <summary>
    /// Black-Scholes-Merton (simple, no stochastic vol or jumps).
    /// </summary>
    BlackScholes,

    /// <summary>
    /// Heston stochastic volatility model.
    /// Best for: normal regimes, volatility clustering.
    /// </summary>
    Heston,

    /// <summary>
    /// Kou double-exponential jump-diffusion.
    /// Best for: capturing skew, leptokurtic returns.
    /// </summary>
    Kou,

    /// <summary>
    /// Leung &amp; Santoli pre-earnings model.
    /// Best for: pre-EA regime with deterministic jump timing.
    /// </summary>
    LeungSantoli,

    /// <summary>
    /// Blended model for post-earnings transition.
    /// </summary>
    PostEarningsBlend,

    /// <summary>
    /// Combined Heston + scheduled jump for pre-EA.
    /// </summary>
    HestonWithEarningsJump
}

/// <summary>
/// Extension to LeungSantoliModel for regime-aware computation.
/// </summary>
public static class LeungSantoliExtensions
{
    /// <summary>
    /// Computes pre-earnings IV with regime validation.
    /// </summary>
    public static double ComputePreEarningsIV(
        this LeungSantoliModel _,
        double baseVolatility,
        double earningsJumpVolatility,
        EarningsRegime regime)
    {
        ArgumentNullException.ThrowIfNull(regime);

        if (regime.RegimeType != EarningsRegimeType.PreEarnings)
        {
            throw new InvalidOperationException(
                $"Cannot compute pre-earnings IV in {regime.RegimeType} regime.");
        }

        return LeungSantoliModel.ComputeTheoreticalIV(
            baseVolatility,
            earningsJumpVolatility,
            regime.TimeParams.TimeToExpiry);
    }

    /// <summary>
    /// Computes post-earnings IV accounting for crush and normalization.
    /// </summary>
    public static double ComputePostEarningsIV(
        double baseVolatility,
        double preCrushIV,
        EarningsRegime regime)
    {
        ArgumentNullException.ThrowIfNull(regime);

        return regime.RegimeType switch
        {
            EarningsRegimeType.PostEarningsTransition =>
                regime.ComputeAdjustedIV(baseVolatility, preCrushIV),
            EarningsRegimeType.PostEarningsNormal =>
                baseVolatility,
            _ => preCrushIV
        };
    }
}

/// <summary>
/// Configuration for regime-based model behavior.
/// </summary>
public sealed class RegimeModelConfig
{
    /// <summary>
    /// Whether to automatically switch models based on regime.
    /// </summary>
    public bool AutoSwitch { get; init; } = true;

    /// <summary>
    /// Minimum confidence level to use regime-specific model.
    /// </summary>
    public double MinConfidence { get; init; } = 0.7;

    /// <summary>
    /// Whether to include earnings jump in continuous models.
    /// </summary>
    public bool AugmentWithEarningsJump { get; init; } = true;

    /// <summary>
    /// Transition smoothing factor [0, 1].
    /// Higher = smoother transition from pre to post EA.
    /// </summary>
    public double TransitionSmoothing { get; init; } = 0.5;

    /// <summary>
    /// Default configuration.
    /// </summary>
    public static RegimeModelConfig Default { get; } = new();
}
