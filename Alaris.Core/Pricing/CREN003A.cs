// CREN003A.cs - Unified American Option Pricing Engine
// Component ID: CREN003A
//
// Dispatches to appropriate pricing method based on rate regime:
// - Standard regime (r >= 0 OR r < q < 0): Use FD engine (CREN002A)
// - Double boundary (q < r < 0 for puts, 0 < r < q for calls): Use QD+ (DBAP002A)
//
// References:
// - Healy (2021) "American Options Under Negative Rates"
// - Kim, I.J. (1990) "Analytic Valuation of American Options"

using Alaris.Core.Options;

namespace Alaris.Core.Pricing;

/// <summary>
/// Unified American option pricing engine results.
/// </summary>
public sealed class UnifiedPricingResult
{
    /// <summary>
    /// Option price.
    /// </summary>
    public double Price { get; init; }

    /// <summary>
    /// Delta (∂V/∂S).
    /// </summary>
    public double Delta { get; init; }

    /// <summary>
    /// Gamma (∂²V/∂S²).
    /// </summary>
    public double Gamma { get; init; }

    /// <summary>
    /// Theta (∂V/∂t per year, divide by 252 for daily).
    /// </summary>
    public double Theta { get; init; }

    /// <summary>
    /// Vega (∂V/∂σ).
    /// </summary>
    public double Vega { get; init; }

    /// <summary>
    /// Rho (∂V/∂r).
    /// </summary>
    public double Rho { get; init; }

    /// <summary>
    /// The rate regime used for pricing.
    /// </summary>
    public RateRegime Regime { get; init; }

    /// <summary>
    /// The pricing method used.
    /// </summary>
    public PricingMethod Method { get; init; }

    /// <summary>
    /// Early exercise premium (American - European value).
    /// </summary>
    public double EarlyExercisePremium { get; init; }

    /// <summary>
    /// Upper exercise boundary (for double boundary regime).
    /// </summary>
    public double? UpperBoundary { get; init; }

    /// <summary>
    /// Lower exercise boundary (for double boundary regime).
    /// </summary>
    public double? LowerBoundary { get; init; }
}

/// <summary>
/// Pricing method used.
/// </summary>
public enum PricingMethod
{
    /// <summary>
    /// Finite difference method (Crank-Nicolson).
    /// </summary>
    FiniteDifference,

    /// <summary>
    /// QD+ approximation for double boundary.
    /// </summary>
    QDPlus,

    /// <summary>
    /// Hybrid approach (QD+ with FD refinement).
    /// </summary>
    Hybrid
}

/// <summary>
/// Unified American option pricing engine.
/// Automatically selects the appropriate method based on rate regime.
/// </summary>
/// <remarks>
/// <para><b>Rate Regime Classification (Healy 2021)</b></para>
/// <list type="table">
///   <listheader>
///     <term>Regime</term>
///     <term>Condition</term>
///     <term>Method</term>
///   </listheader>
///   <item>
///     <term>Standard</term>
///     <term>r ≥ 0 OR r &lt; q &lt; 0</term>
///     <term>FD (CREN002A)</term>
///   </item>
///   <item>
///     <term>Double Boundary</term>
///     <term>q &lt; r &lt; 0 (puts) OR 0 &lt; r &lt; q (calls)</term>
///     <term>QD+ (DBAP002A)</term>
///   </item>
/// </list>
/// </remarks>
public sealed class CREN003A : IAmericanOptionEngine
{
    private readonly CREN002A _fdEngine;

    /// <summary>
    /// Initialises the unified pricing engine.
    /// </summary>
    /// <param name="timeSteps">Number of time steps for FD method.</param>
    /// <param name="spotSteps">Number of spot steps for FD method.</param>
    public CREN003A(int timeSteps = 100, int spotSteps = 200)
    {
        _fdEngine = new CREN002A(timeSteps, spotSteps);
        _nearExpiryHandler = new CREX001A();
    }

    private readonly CREX001A _nearExpiryHandler;

    /// <inheritdoc/>
    public double Price(double spot, double strike, double timeToExpiry, double riskFreeRate, double dividendYield, double volatility, OptionType optionType)
    {
        bool isCall = optionType == OptionType.Call;

        // Near-expiry handling: use intrinsic blending for τ < 3/252 (3 trading days)
        if (_nearExpiryHandler.IsNearExpiry(timeToExpiry))
        {
            // If intrinsic-only regime, return pure intrinsic
            if (_nearExpiryHandler.IsIntrinsicOnly(timeToExpiry))
            {
                return CREX001A.CalculateIntrinsic(spot, strike, isCall);
            }

            // Blending zone: compute FD price and blend with intrinsic
            double fdPrice = _fdEngine.Price(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
            return _nearExpiryHandler.BlendWithIntrinsic(fdPrice, spot, strike, isCall, timeToExpiry);
        }

        return _fdEngine.Price(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
    }

    /// <inheritdoc/>
    public double Delta(double spot, double strike, double timeToExpiry, double riskFreeRate, double dividendYield, double volatility, OptionType optionType)
    {
        bool isCall = optionType == OptionType.Call;

        // Near-expiry: use limiting behaviour (step function)
        if (_nearExpiryHandler.IsNearExpiry(timeToExpiry))
        {
            NearExpiryGreeks greeks = _nearExpiryHandler.CalculateNearExpiryGreeks(spot, strike, isCall, timeToExpiry);
            return greeks.Delta;
        }

        return _fdEngine.Delta(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
    }

    /// <inheritdoc/>
    public double Gamma(double spot, double strike, double timeToExpiry, double riskFreeRate, double dividendYield, double volatility, OptionType optionType)
    {
        bool isCall = optionType == OptionType.Call;

        // Near-expiry: use limiting behaviour (capped ATM spike)
        if (_nearExpiryHandler.IsNearExpiry(timeToExpiry))
        {
            NearExpiryGreeks greeks = _nearExpiryHandler.CalculateNearExpiryGreeks(spot, strike, isCall, timeToExpiry);
            return greeks.Gamma;
        }

        return _fdEngine.Gamma(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
    }

    /// <inheritdoc/>
    public double Theta(double spot, double strike, double timeToExpiry, double riskFreeRate, double dividendYield, double volatility, OptionType optionType)
    {
        bool isCall = optionType == OptionType.Call;

        // Near-expiry: use limiting behaviour (time decay rate)
        if (_nearExpiryHandler.IsNearExpiry(timeToExpiry))
        {
            NearExpiryGreeks greeks = _nearExpiryHandler.CalculateNearExpiryGreeks(spot, strike, isCall, timeToExpiry);
            return greeks.Theta;
        }

        return _fdEngine.Theta(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
    }

    /// <inheritdoc/>
    public double Vega(double spot, double strike, double timeToExpiry, double riskFreeRate, double dividendYield, double volatility, OptionType optionType)
    {
        bool isCall = optionType == OptionType.Call;

        // Near-expiry: Vega → 0 (no time for vol to matter)
        if (_nearExpiryHandler.IsNearExpiry(timeToExpiry))
        {
            NearExpiryGreeks greeks = _nearExpiryHandler.CalculateNearExpiryGreeks(spot, strike, isCall, timeToExpiry);
            return greeks.Vega;
        }

        return _fdEngine.Vega(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
    }

    /// <inheritdoc/>
    public double Rho(double spot, double strike, double timeToExpiry, double riskFreeRate, double dividendYield, double volatility, OptionType optionType)
    {
        return _fdEngine.Rho(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
    }

    /// <summary>
    /// Prices an American option with full result details including regime and method used.
    /// </summary>
    /// <param name="spot">Current spot price (must be > 0).</param>
    /// <param name="strike">Strike price (must be > 0).</param>
    /// <param name="timeToExpiry">Time to expiry in years (must be > 0).</param>
    /// <param name="volatility">Volatility σ (must be > 0).</param>
    /// <param name="riskFreeRate">Risk-free rate r (can be negative).</param>
    /// <param name="dividendYield">Dividend yield q (can be negative).</param>
    /// <param name="optionType">Option type (Call or Put).</param>
    /// <returns>Full pricing result with Greeks and regime information.</returns>
    public UnifiedPricingResult PriceWithDetails(
        double spot,
        double strike,
        double timeToExpiry,
        double volatility,
        double riskFreeRate,
        double dividendYield,
        OptionType optionType)
    {
        // Validate parameters
        ValidateParameters(spot, strike, timeToExpiry, volatility);

        bool isCall = optionType == OptionType.Call;

        // Classify regime
        RateRegime regime = CRRE001A.Classify(riskFreeRate, dividendYield, isCall);

        // Use FD engine for pricing (reliable for all regimes)
        double price = _fdEngine.Price(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
        double delta = _fdEngine.Delta(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
        double gamma = _fdEngine.Gamma(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
        double theta = _fdEngine.Theta(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
        double vega = _fdEngine.Vega(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);
        double rho = _fdEngine.Rho(spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, optionType);

        // Calculate early exercise premium (American - European)
        double europeanPrice = Alaris.Core.Math.CRMF001A.BSPrice(
            spot, strike, timeToExpiry, volatility, riskFreeRate, dividendYield, isCall);
        double earlyExercisePremium = price - europeanPrice;

        // Calculate boundaries for double-boundary regime using QD+ approximation
        double? upperBoundary = null;
        double? lowerBoundary = null;
        PricingMethod method = PricingMethod.FiniteDifference;

        if (regime == RateRegime.DoubleBoundary)
        {
            // Use QD+ to calculate boundaries for the double-boundary regime
            // Only attempt if parameters are valid (spot, strike, tau, vol all positive)
            CRAP001A qdPlusEngine = new CRAP001A(
                spot, strike, timeToExpiry, riskFreeRate, dividendYield, volatility, isCall);
            (double upper, double lower) = qdPlusEngine.CalculateBoundaries();

            // Only set boundaries if they are finite and valid
            if (!double.IsInfinity(upper) && !double.IsNaN(upper))
            {
                upperBoundary = upper;
            }

            if (!double.IsInfinity(lower) && !double.IsNaN(lower))
            {
                lowerBoundary = lower;
            }

            method = PricingMethod.Hybrid; // FD price + QD+ boundaries
        }

        return new UnifiedPricingResult
        {
            Price = price,
            Delta = delta,
            Gamma = gamma,
            Theta = theta,
            Vega = vega,
            Rho = rho,
            Regime = regime,
            Method = method,
            EarlyExercisePremium = System.Math.Max(0, earlyExercisePremium),
            UpperBoundary = upperBoundary,
            LowerBoundary = lowerBoundary
        };
    }

    /// <summary>
    /// Validates input parameters.
    /// </summary>
    private static void ValidateParameters(double spot, double strike, double timeToExpiry, double volatility)
    {
        if (spot <= 0)
        {
            throw new ArgumentException("Spot price must be positive", nameof(spot));
        }

        if (strike <= 0)
        {
            throw new ArgumentException("Strike must be positive", nameof(strike));
        }

        if (timeToExpiry <= 0)
        {
            throw new ArgumentException("Time to expiry must be positive", nameof(timeToExpiry));
        }

        if (volatility <= 0)
        {
            throw new ArgumentException("Volatility must be positive", nameof(volatility));
        }
    }

}
