using Alaris.Strategy.Model;
using Alaris.Strategy.Pricing;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Bridge;

/// <summary>
/// Unified pricing engine that automatically selects between Alaris.Double and Alaris.Quantlib
/// based on interest rate regime. Supports both positive and negative interest rates.
/// </summary>
/// <remarks>
/// Regime Detection:
/// - If r >= 0: Use Alaris.Quantlib (standard American option pricing)
/// - If r < 0 and q < r: Use Alaris.Double (double boundary method for negative rates)
/// - If r < 0 and q >= r: Use Alaris.Quantlib (single boundary still applies)
/// </remarks>
public sealed class UnifiedPricingEngine : IOptionPricingEngine, IDisposable
{
    private readonly ILogger<UnifiedPricingEngine>? _logger;
    private bool _disposed;

    // Constants for numerical calculations
    private const double BumpSize = 0.0001; // 1 basis point for finite differences
    private const double VolBumpSize = 0.01; // 1% volatility bump for vega
    private const int MaxIVIterations = 100;
    private const double IVTolerance = 1e-6;

    /// <summary>
    /// Initializes a new instance of the UnifiedPricingEngine.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public UnifiedPricingEngine(ILogger<UnifiedPricingEngine>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Determines the appropriate pricing regime based on interest rate and dividend yield.
    /// </summary>
    /// <param name="riskFreeRate">The risk-free interest rate (can be negative).</param>
    /// <param name="dividendYield">The continuous dividend yield (can be negative).</param>
    /// <returns>The pricing regime to use.</returns>
    public static PricingRegime DetermineRegime(double riskFreeRate, double dividendYield)
    {
        if (riskFreeRate >= 0)
        {
            // Standard regime: positive rates
            return PricingRegime.PositiveRates;
        }
        else if (dividendYield < riskFreeRate)
        {
            // Double boundary regime: r < 0 and q < r
            return PricingRegime.DoubleBoundary;
        }
        else
        {
            // Negative rates but no double boundary: r < 0 and q >= r
            return PricingRegime.NegativeRatesSingleBoundary;
        }
    }

    /// <summary>
    /// Prices a single American option using the appropriate pricing engine.
    /// </summary>
    public async Task<OptionPricing> PriceOption(OptionParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate();

        var regime = DetermineRegime(parameters.RiskFreeRate, parameters.DividendYield);

        _logger?.LogDebug(
            "Pricing option: S={S}, K={K}, r={r}, q={q}, σ={sigma}, T={T:F4}, Regime={regime}",
            parameters.UnderlyingPrice, parameters.Strike, parameters.RiskFreeRate,
            parameters.DividendYield, parameters.ImpliedVolatility,
            CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry),
            regime);

        return regime switch
        {
            PricingRegime.PositiveRates => await PriceWithQuantlib(parameters),
            PricingRegime.DoubleBoundary => await PriceWithDouble(parameters),
            PricingRegime.NegativeRatesSingleBoundary => await PriceWithQuantlib(parameters),
            _ => throw new InvalidOperationException($"Unknown pricing regime: {regime}")
        };
    }

    /// <summary>
    /// Prices a calendar spread using the appropriate pricing engine for each leg.
    /// </summary>
    public async Task<CalendarSpreadPricing> PriceCalendarSpread(CalendarSpreadParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate();

        var regime = DetermineRegime(parameters.RiskFreeRate, parameters.DividendYield);

        _logger?.LogInformation(
            "Pricing calendar spread: Regime={regime}, Strike={strike}, Front={frontDte}, Back={backDte}",
            regime,
            parameters.Strike,
            CalculateDaysToExpiry(parameters.ValuationDate, parameters.FrontExpiry),
            CalculateDaysToExpiry(parameters.ValuationDate, parameters.BackExpiry));

        // Price front month (short position)
        var frontParams = new OptionParameters
        {
            UnderlyingPrice = parameters.UnderlyingPrice,
            Strike = parameters.Strike,
            Expiry = parameters.FrontExpiry,
            ImpliedVolatility = parameters.ImpliedVolatility,
            RiskFreeRate = parameters.RiskFreeRate,
            DividendYield = parameters.DividendYield,
            OptionType = parameters.OptionType,
            ValuationDate = parameters.ValuationDate
        };

        var frontPricing = await PriceOption(frontParams);

        // Price back month (long position)
        var backParams = new OptionParameters
        {
            UnderlyingPrice = parameters.UnderlyingPrice,
            Strike = parameters.Strike,
            Expiry = parameters.BackExpiry,
            ImpliedVolatility = parameters.ImpliedVolatility,
            RiskFreeRate = parameters.RiskFreeRate,
            DividendYield = parameters.DividendYield,
            OptionType = parameters.OptionType,
            ValuationDate = parameters.ValuationDate
        };

        var backPricing = await PriceOption(backParams);

        // Calculate spread Greeks (long back - short front)
        var spreadCost = backPricing.Price - frontPricing.Price;
        var spreadDelta = backPricing.Delta - frontPricing.Delta;
        var spreadGamma = backPricing.Gamma - frontPricing.Gamma;
        var spreadVega = backPricing.Vega - frontPricing.Vega;
        var spreadTheta = backPricing.Theta - frontPricing.Theta;
        var spreadRho = backPricing.Rho - frontPricing.Rho;

        // Estimate max profit and max loss
        // Max loss is the debit paid (spread cost)
        var maxLoss = spreadCost;

        // Max profit occurs when underlying is at strike at front expiration
        // Approximation: back option retains most value, front expires worthless
        var maxProfit = EstimateMaxProfit(backPricing, frontPricing, spreadCost);

        // Breakeven is approximate (requires more sophisticated modeling)
        var breakEven = parameters.Strike;

        return new CalendarSpreadPricing
        {
            FrontOption = frontPricing,
            BackOption = backPricing,
            SpreadCost = spreadCost,
            SpreadDelta = spreadDelta,
            SpreadGamma = spreadGamma,
            SpreadVega = spreadVega,
            SpreadTheta = spreadTheta,
            SpreadRho = spreadRho,
            MaxProfit = maxProfit,
            MaxLoss = maxLoss,
            BreakEven = breakEven,
            HasPositiveExpectedValue = spreadVega > 0 && spreadTheta > 0
        };
    }

    /// <summary>
    /// Calculates implied volatility from market price using bisection method.
    /// </summary>
    public async Task<double> CalculateImpliedVolatility(double marketPrice, OptionParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (marketPrice <= 0)
            throw new ArgumentException("Market price must be positive", nameof(marketPrice));

        // Use bisection method to find IV
        double volLow = 0.01;  // 1% vol
        double volHigh = 5.0;   // 500% vol (extreme)
        double volMid = 0;
        int iterations = 0;

        while (iterations < MaxIVIterations && (volHigh - volLow) > IVTolerance)
        {
            volMid = (volLow + volHigh) / 2.0;

            var testParams = new OptionParameters
            {
                UnderlyingPrice = parameters.UnderlyingPrice,
                Strike = parameters.Strike,
                Expiry = parameters.Expiry,
                ImpliedVolatility = volMid,
                RiskFreeRate = parameters.RiskFreeRate,
                DividendYield = parameters.DividendYield,
                OptionType = parameters.OptionType,
                ValuationDate = parameters.ValuationDate
            };

            var pricing = await PriceOption(testParams);
            var priceDiff = pricing.Price - marketPrice;

            if (Math.Abs(priceDiff) < IVTolerance)
                break;

            if (priceDiff > 0)
            {
                // Model price too high, reduce volatility
                volHigh = volMid;
            }
            else
            {
                // Model price too low, increase volatility
                volLow = volMid;
            }

            iterations++;
        }

        _logger?.LogDebug(
            "Implied volatility calculation converged in {iterations} iterations: IV={iv:F4}",
            iterations, volMid);

        return volMid;
    }

    /// <summary>
    /// Prices an option using the Alaris.Quantlib engine (standard American option pricing).
    /// </summary>
    private Task<OptionPricing> PriceWithQuantlib(OptionParameters parameters)
    {
        return Task.Run(() =>
        {
            try
            {
                // Set evaluation date
                Settings.instance().setEvaluationDate(parameters.ValuationDate);

                // Create underlying quote
                var underlyingQuote = new SimpleQuote(parameters.UnderlyingPrice);
                var underlyingHandle = new QuoteHandle(underlyingQuote);

                // Create term structures
                var dayCounter = new Actual365Fixed();

                var flatRateTs = new FlatForward(
                    parameters.ValuationDate,
                    parameters.RiskFreeRate,
                    dayCounter);
                var riskFreeRateHandle = new YieldTermStructureHandle(flatRateTs);

                var flatDividendTs = new FlatForward(
                    parameters.ValuationDate,
                    parameters.DividendYield,
                    dayCounter);
                var dividendYieldHandle = new YieldTermStructureHandle(flatDividendTs);

                var flatVolTs = new BlackConstantVol(
                    parameters.ValuationDate,
                    new TARGET(),
                    parameters.ImpliedVolatility,
                    dayCounter);
                var volatilityHandle = new BlackVolTermStructureHandle(flatVolTs);

                // Create Black-Scholes-Merton process
                var bsmProcess = new BlackScholesMertonProcess(
                    underlyingHandle,
                    dividendYieldHandle,
                    riskFreeRateHandle,
                    volatilityHandle);

                // Create option
                var exercise = new AmericanExercise(parameters.ValuationDate, parameters.Expiry);
                var payoff = new PlainVanillaPayoff(parameters.OptionType, parameters.Strike);
                var option = new VanillaOption(payoff, exercise);

                // Create pricing engine (using FD for Americans by default)
                var fdEngine = new FdBlackScholesVanillaEngine(bsmProcess, 100, 100);
                option.setPricingEngine(fdEngine);

                // Calculate price
                var price = option.NPV();

                // Calculate Greeks using finite differences
                var delta = CalculateDelta(option, underlyingQuote, bsmProcess);
                var gamma = CalculateGamma(option, underlyingQuote, bsmProcess);
                var vega = CalculateVegaQuantlib(option, flatVolTs, bsmProcess, parameters);
                var theta = CalculateThetaQuantlib(option, parameters);
                var rho = CalculateRhoQuantlib(option, flatRateTs, bsmProcess, parameters);

                var timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);
                var moneyness = parameters.UnderlyingPrice / parameters.Strike;

                // Clean up
                option.Dispose();
                fdEngine.Dispose();
                bsmProcess.Dispose();
                volatilityHandle.Dispose();
                flatVolTs.Dispose();
                dividendYieldHandle.Dispose();
                flatDividendTs.Dispose();
                riskFreeRateHandle.Dispose();
                flatRateTs.Dispose();
                underlyingHandle.Dispose();
                underlyingQuote.Dispose();

                return new OptionPricing
                {
                    Price = price,
                    Delta = delta,
                    Gamma = gamma,
                    Vega = vega,
                    Theta = theta,
                    Rho = rho,
                    ImpliedVolatility = parameters.ImpliedVolatility,
                    TimeToExpiry = timeToExpiry,
                    Moneyness = moneyness
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error pricing option with Quantlib");
                throw;
            }
        });
    }

    /// <summary>
    /// Prices an option using the Alaris.Double engine (double boundary method for negative rates).
    /// </summary>
    private Task<OptionPricing> PriceWithDouble(OptionParameters parameters)
    {
        return Task.Run(() =>
        {
            try
            {
                // Set evaluation date
                Settings.instance().setEvaluationDate(parameters.ValuationDate);

                // Create underlying quote
                var underlyingQuote = new SimpleQuote(parameters.UnderlyingPrice);
                var underlyingHandle = new QuoteHandle(underlyingQuote);

                // Create term structures
                var dayCounter = new Actual365Fixed();

                var flatRateTs = new FlatForward(
                    parameters.ValuationDate,
                    parameters.RiskFreeRate,
                    dayCounter);
                var riskFreeRateHandle = new YieldTermStructureHandle(flatRateTs);

                var flatDividendTs = new FlatForward(
                    parameters.ValuationDate,
                    parameters.DividendYield,
                    dayCounter);
                var dividendYieldHandle = new YieldTermStructureHandle(flatDividendTs);

                var flatVolTs = new BlackConstantVol(
                    parameters.ValuationDate,
                    new TARGET(),
                    parameters.ImpliedVolatility,
                    dayCounter);
                var volatilityHandle = new BlackVolTermStructureHandle(flatVolTs);

                // Create Black-Scholes-Merton process
                var bsmProcess = new BlackScholesMertonProcess(
                    underlyingHandle,
                    dividendYieldHandle,
                    riskFreeRateHandle,
                    volatilityHandle);

                // Create option
                var exercise = new AmericanExercise(parameters.ValuationDate, parameters.Expiry);
                var payoff = new PlainVanillaPayoff(parameters.OptionType, parameters.Strike);
                var option = new VanillaOption(payoff, exercise);

                // Use DoubleBoundaryEngine for negative rates
                using var doubleEngine = new Alaris.Double.DoubleBoundaryEngine(
                    bsmProcess,
                    underlyingQuote);

                var result = doubleEngine.Calculate(option);

                var timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);
                var moneyness = parameters.UnderlyingPrice / parameters.Strike;

                // Clean up
                option.Dispose();
                bsmProcess.Dispose();
                volatilityHandle.Dispose();
                flatVolTs.Dispose();
                dividendYieldHandle.Dispose();
                flatDividendTs.Dispose();
                riskFreeRateHandle.Dispose();
                flatRateTs.Dispose();
                underlyingHandle.Dispose();
                underlyingQuote.Dispose();

                return new OptionPricing
                {
                    Price = result.Price,
                    Delta = result.Delta,
                    Gamma = result.Gamma,
                    Vega = result.Vega,
                    Theta = result.Theta,
                    Rho = result.Rho,
                    ImpliedVolatility = parameters.ImpliedVolatility,
                    TimeToExpiry = timeToExpiry,
                    Moneyness = moneyness
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error pricing option with Alaris.Double");
                throw;
            }
        });
    }

    // Helper methods for Greek calculations

    private double CalculateDelta(VanillaOption option, SimpleQuote underlyingQuote, BlackScholesMertonProcess process)
    {
        var originalSpot = underlyingQuote.value();

        // Up bump
        underlyingQuote.setValue(originalSpot + BumpSize);
        var priceUp = option.NPV();

        // Down bump
        underlyingQuote.setValue(originalSpot - BumpSize);
        var priceDown = option.NPV();

        // Restore
        underlyingQuote.setValue(originalSpot);

        return (priceUp - priceDown) / (2 * BumpSize);
    }

    private double CalculateGamma(VanillaOption option, SimpleQuote underlyingQuote, BlackScholesMertonProcess process)
    {
        var originalSpot = underlyingQuote.value();
        var priceOriginal = option.NPV();

        // Up bump
        underlyingQuote.setValue(originalSpot + BumpSize);
        var priceUp = option.NPV();

        // Down bump
        underlyingQuote.setValue(originalSpot - BumpSize);
        var priceDown = option.NPV();

        // Restore
        underlyingQuote.setValue(originalSpot);

        return (priceUp - 2 * priceOriginal + priceDown) / (BumpSize * BumpSize);
    }

    private double CalculateVegaQuantlib(VanillaOption option, BlackConstantVol volTs,
        BlackScholesMertonProcess process, OptionParameters parameters)
    {
        var originalVol = parameters.ImpliedVolatility;
        var dayCounter = new Actual365Fixed();

        // Up bump
        var volUp = new BlackConstantVol(
            parameters.ValuationDate,
            new TARGET(),
            originalVol + VolBumpSize,
            dayCounter);
        var volHandleUp = new BlackVolTermStructureHandle(volUp);
        var processUp = new BlackScholesMertonProcess(
            process.stateVariable(),
            process.dividendYield(),
            process.riskFreeRate(),
            volHandleUp);
        var engineUp = new FdBlackScholesVanillaEngine(processUp, 100, 100);
        option.setPricingEngine(engineUp);
        var priceUp = option.NPV();

        // Down bump
        var volDown = new BlackConstantVol(
            parameters.ValuationDate,
            new TARGET(),
            originalVol - VolBumpSize,
            dayCounter);
        var volHandleDown = new BlackVolTermStructureHandle(volDown);
        var processDown = new BlackScholesMertonProcess(
            process.stateVariable(),
            process.dividendYield(),
            process.riskFreeRate(),
            volHandleDown);
        var engineDown = new FdBlackScholesVanillaEngine(processDown, 100, 100);
        option.setPricingEngine(engineDown);
        var priceDown = option.NPV();

        // Clean up
        engineDown.Dispose();
        processDown.Dispose();
        volHandleDown.Dispose();
        volDown.Dispose();
        engineUp.Dispose();
        processUp.Dispose();
        volHandleUp.Dispose();
        volUp.Dispose();

        return (priceUp - priceDown) / (2 * VolBumpSize);
    }

    private double CalculateThetaQuantlib(VanillaOption option, OptionParameters parameters)
    {
        var originalDate = parameters.ValuationDate;
        var dayBump = 1; // 1 day

        // Forward date by 1 day
        var forwardDate = originalDate.Add(new Period(dayBump, TimeUnit.Days));
        Settings.instance().setEvaluationDate(forwardDate);
        var priceForward = option.NPV();

        // Restore date and get original price
        Settings.instance().setEvaluationDate(originalDate);
        var priceOriginal = option.NPV();

        // Theta is price change per day (negative for time decay)
        return (priceForward - priceOriginal) / dayBump;
    }

    private double CalculateRhoQuantlib(VanillaOption option, FlatForward rateTs,
        BlackScholesMertonProcess process, OptionParameters parameters)
    {
        var originalRate = parameters.RiskFreeRate;
        var dayCounter = new Actual365Fixed();
        var rateBump = 0.01; // 1% rate bump

        // Up bump
        var rateUp = new FlatForward(
            parameters.ValuationDate,
            originalRate + rateBump,
            dayCounter);
        var rateHandleUp = new YieldTermStructureHandle(rateUp);
        var processUp = new BlackScholesMertonProcess(
            process.stateVariable(),
            process.dividendYield(),
            rateHandleUp,
            process.blackVolatility());
        var engineUp = new FdBlackScholesVanillaEngine(processUp, 100, 100);
        option.setPricingEngine(engineUp);
        var priceUp = option.NPV();

        // Down bump
        var rateDown = new FlatForward(
            parameters.ValuationDate,
            originalRate - rateBump,
            dayCounter);
        var rateHandleDown = new YieldTermStructureHandle(rateDown);
        var processDown = new BlackScholesMertonProcess(
            process.stateVariable(),
            process.dividendYield(),
            rateHandleDown,
            process.blackVolatility());
        var engineDown = new FdBlackScholesVanillaEngine(processDown, 100, 100);
        option.setPricingEngine(engineDown);
        var priceDown = option.NPV();

        // Clean up
        engineDown.Dispose();
        processDown.Dispose();
        rateHandleDown.Dispose();
        rateDown.Dispose();
        engineUp.Dispose();
        processUp.Dispose();
        rateHandleUp.Dispose();
        rateUp.Dispose();

        return (priceUp - priceDown) / (2 * rateBump);
    }

    private double EstimateMaxProfit(OptionPricing backOption, OptionPricing frontOption, double spreadCost)
    {
        // Simplified estimation: assumes front option expires worthless
        // and back option retains significant value
        // More accurate calculation would require Monte Carlo or grid search
        return backOption.Price * 0.8; // Conservative estimate
    }

    private static double CalculateTimeToExpiry(Date valuationDate, Date expiryDate)
    {
        var dayCounter = new Actual365Fixed();
        return dayCounter.yearFraction(valuationDate, expiryDate);
    }

    private static int CalculateDaysToExpiry(Date valuationDate, Date expiryDate)
    {
        return expiryDate.serialNumber() - valuationDate.serialNumber();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }
}

/// <summary>
/// Represents the pricing regime based on interest rate conditions.
/// </summary>
public enum PricingRegime
{
    /// <summary>
    /// Standard regime with positive interest rates (r >= 0).
    /// Uses Alaris.Quantlib with single boundary.
    /// </summary>
    PositiveRates,

    /// <summary>
    /// Double boundary regime with negative rates where q < r < 0.
    /// Uses Alaris.Double with double boundary method.
    /// </summary>
    DoubleBoundary,

    /// <summary>
    /// Negative rates but single boundary regime where r < 0 and q >= r.
    /// Uses Alaris.Quantlib with single boundary.
    /// </summary>
    NegativeRatesSingleBoundary
}
