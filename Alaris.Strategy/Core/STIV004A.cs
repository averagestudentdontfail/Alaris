namespace Alaris.Strategy.Core;

/// <summary>
/// Implements the Leung &amp; Santoli (2014) pre-earnings announcement implied volatility model.
///
/// The model accounts for scheduled earnings jumps in option pricing by incorporating
/// a deterministic-time random jump in the stock price dynamics. The pre-EA implied
/// volatility follows:
///     I(t; K, T) = sqrt(sigma^2 + sigma_e^2 / (T - t))
///
/// Where:
///     sigma   = base (diffusion) volatility
///     sigma_e = earnings jump volatility (calibrated from historical EA moves)
///     T       = option expiration time
///     t       = current time (must be before EA date T_e)
///
/// Reference: "Accounting for Earnings Announcements in the Pricing of Equity Options"
/// Tim Leung &amp; Marco Santoli (2014), Journal of Derivatives
/// </summary>
public sealed class STIV004A
{
    /// <summary>
    /// Minimum time to expiry to avoid division by zero (1 hour in years).
    /// </summary>
    private const double MinTimeToExpiry = 1.0 / (252.0 * 6.5); // ~1 hour in trading years

    /// <summary>
    /// Computes the theoretical pre-earnings announcement implied volatility.
    /// </summary>
    /// <param name="baseVolatility">The base (diffusion) volatility sigma.</param>
    /// <param name="earningsJumpVolatility">The earnings jump volatility sigma_e.</param>
    /// <param name="timeToExpiry">Time to option expiration in years (T - t).</param>
    /// <returns>The theoretical implied volatility I(t; K, T).</returns>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    public static double ComputeTheoreticalIV(
        double baseVolatility,
        double earningsJumpVolatility,
        double timeToExpiry)
    {
        if (baseVolatility < 0)
        {
            throw new ArgumentException("Base volatility must be non-negative.", nameof(baseVolatility));
        }

        if (earningsJumpVolatility < 0)
        {
            throw new ArgumentException("Earnings jump volatility must be non-negative.", nameof(earningsJumpVolatility));
        }

        if (timeToExpiry <= 0)
        {
            throw new ArgumentException("Time to expiry must be positive.", nameof(timeToExpiry));
        }

        // Ensure minimum time to avoid numerical issues
        double effectiveTimeToExpiry = Math.Max(timeToExpiry, MinTimeToExpiry);

        // Leung & Santoli (2014) formula: I(t) = sqrt(sigma^2 + sigma_e^2 / (T-t))
        double varianceComponent = earningsJumpVolatility * earningsJumpVolatility / effectiveTimeToExpiry;
        double totalVariance = (baseVolatility * baseVolatility) + varianceComponent;

        return Math.Sqrt(totalVariance);
    }

    /// <summary>
    /// Computes the IV mispricing signal by comparing market IV to theoretical L&amp;S IV.
    /// Positive values indicate market IV is higher than theoretical (potentially overpriced).
    /// Negative values indicate market IV is lower than theoretical (potentially underpriced).
    /// </summary>
    /// <param name="marketIV">The observed market implied volatility.</param>
    /// <param name="baseVolatility">The base (diffusion) volatility sigma.</param>
    /// <param name="earningsJumpVolatility">The earnings jump volatility sigma_e.</param>
    /// <param name="timeToExpiry">Time to option expiration in years (T - t).</param>
    /// <returns>The mispricing signal (market IV - theoretical IV).</returns>
    public static double ComputeMispricingSTCR004A(
        double marketIV,
        double baseVolatility,
        double earningsJumpVolatility,
        double timeToExpiry)
    {
        double theoreticalIV = ComputeTheoreticalIV(baseVolatility, earningsJumpVolatility, timeToExpiry);
        return marketIV - theoreticalIV;
    }

    /// <summary>
    /// Computes the expected IV crush magnitude after earnings announcement.
    /// This represents the drop from pre-EA IV to post-EA IV.
    /// </summary>
    /// <param name="baseVolatility">The base (diffusion) volatility sigma.</param>
    /// <param name="earningsJumpVolatility">The earnings jump volatility sigma_e.</param>
    /// <param name="timeToExpiry">Time to option expiration in years (T - t).</param>
    /// <returns>The expected IV crush in volatility points.</returns>
    public static double ComputeExpectedIVCrush(
        double baseVolatility,
        double earningsJumpVolatility,
        double timeToExpiry)
    {
        double preEaIV = ComputeTheoreticalIV(baseVolatility, earningsJumpVolatility, timeToExpiry);
        // Post-EA IV reverts to base volatility (sigma_e component disappears)
        return preEaIV - baseVolatility;
    }

    /// <summary>
    /// Computes the IV crush ratio (relative magnitude of expected crush).
    /// </summary>
    /// <param name="baseVolatility">The base (diffusion) volatility sigma.</param>
    /// <param name="earningsJumpVolatility">The earnings jump volatility sigma_e.</param>
    /// <param name="timeToExpiry">Time to option expiration in years (T - t).</param>
    /// <returns>The IV crush as a percentage of pre-EA IV.</returns>
    public static double ComputeIVCrushRatio(
        double baseVolatility,
        double earningsJumpVolatility,
        double timeToExpiry)
    {
        double preEaIV = ComputeTheoreticalIV(baseVolatility, earningsJumpVolatility, timeToExpiry);
        if (preEaIV <= 0)
        {
            return 0;
        }

        double crush = ComputeExpectedIVCrush(baseVolatility, earningsJumpVolatility, timeToExpiry);
        return crush / preEaIV;
    }

    /// <summary>
    /// Extracts the earnings jump volatility (sigma_e) from observed market IV.
    /// This is the inverse of the L&amp;S formula, solving for sigma_e given market IV.
    /// </summary>
    /// <param name="marketIV">The observed market implied volatility.</param>
    /// <param name="baseVolatility">The base (diffusion) volatility sigma.</param>
    /// <param name="timeToExpiry">Time to option expiration in years (T - t).</param>
    /// <returns>The implied earnings jump volatility sigma_e.</returns>
    public static double ExtractEarningsJumpVolatility(
        double marketIV,
        double baseVolatility,
        double timeToExpiry)
    {
        if (marketIV < 0)
        {
            throw new ArgumentException("Market IV must be non-negative.", nameof(marketIV));
        }

        if (baseVolatility < 0)
        {
            throw new ArgumentException("Base volatility must be non-negative.", nameof(baseVolatility));
        }

        if (timeToExpiry <= 0)
        {
            throw new ArgumentException("Time to expiry must be positive.", nameof(timeToExpiry));
        }

        // Rearranging I(t)^2 = sigma^2 + sigma_e^2/(T-t) gives:
        // sigma_e = sqrt((I(t)^2 - sigma^2) * (T-t))
        double ivSquared = marketIV * marketIV;
        double baseSquared = baseVolatility * baseVolatility;
        double variance = ivSquared - baseSquared;

        if (variance <= 0)
        {
            // Market IV is below base volatility - no earnings premium implied
            return 0;
        }

        return Math.Sqrt(variance * timeToExpiry);
    }

    /// <summary>
    /// Computes the term structure of theoretical IV at different DTEs.
    /// Useful for comparing against market term structure.
    /// </summary>
    /// <param name="baseVolatility">The base (diffusion) volatility sigma.</param>
    /// <param name="earningsJumpVolatility">The earnings jump volatility sigma_e.</param>
    /// <param name="dtePoints">Array of days-to-expiry points.</param>
    /// <returns>Array of (DTE, TheoreticalIV) tuples.</returns>
    public static (int DTE, double TheoreticalIV)[] ComputeSTTM001A(
        double baseVolatility,
        double earningsJumpVolatility,
        int[] dtePoints)
    {
        ArgumentNullException.ThrowIfNull(dtePoints);

        (int DTE, double TheoreticalIV)[] result = new (int DTE, double TheoreticalIV)[dtePoints.Length];

        for (int i = 0; i < dtePoints.Length; i++)
        {
            int dte = dtePoints[i];
            if (dte <= 0)
            {
                result[i] = (dte, baseVolatility);
                continue;
            }

            double timeToExpiry = dte / 252.0; // Convert to years
            result[i] = (dte, ComputeTheoreticalIV(baseVolatility, earningsJumpVolatility, timeToExpiry));
        }

        return result;
    }
}
