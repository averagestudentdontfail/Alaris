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
    /// Determines the appropriate pricing regime based on interest rate, dividend yield, and option type.
    /// </summary>
    /// <param name="riskFreeRate">The risk-free interest rate (can be negative).</param>
    /// <param name="dividendYield">The continuous dividend yield (can be negative).</param>
    /// <param name="isCall">True for call options, false for put options.</param>
    /// <returns>The pricing regime to use.</returns>
    public static PricingRegime DetermineRegime(double riskFreeRate, double dividendYield, bool isCall)
    {
        if (riskFreeRate >= 0)
        {
            // Check for call double boundary: 0 < r < q
            if (isCall && dividendYield > riskFreeRate)
            {
                return PricingRegime.DoubleBoundary;
            }
            // Standard regime: positive rates, single boundary
            return PricingRegime.PositiveRates;
        }
        else
        {
            // Negative rate regime
            if (!isCall && dividendYield < riskFreeRate)
            {
                // Put double boundary regime: q < r < 0
                return PricingRegime.DoubleBoundary;
            }
            else
            {
                // Negative rates but single boundary
                return PricingRegime.NegativeRatesSingleBoundary;
            }
        }
    }

    /// <summary>
    /// Prices a single American option using the appropriate pricing engine.
    /// </summary>
    public async Task<OptionPricing> PriceOption(OptionParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate();

        var isCall = parameters.OptionType == Option.Type.Call;
        var regime = DetermineRegime(parameters.RiskFreeRate, parameters.DividendYield, isCall);

        _logger?.LogDebug(
            "Pricing option: S={S}, K={K}, r={r}, q={q}, σ={sigma}, T={T:F4}, Type={type}, Regime={regime}",
            parameters.UnderlyingPrice, parameters.Strike, parameters.RiskFreeRate,
            parameters.DividendYield, parameters.ImpliedVolatility,
            CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry),
            isCall ? "Call" : "Put",
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

        var isCall = parameters.OptionType == Option.Type.Call;
        var regime = DetermineRegime(parameters.RiskFreeRate, parameters.DividendYield, isCall);

        _logger?.LogInformation(
            "Pricing calendar spread: Regime={regime}, Type={type}, Strike={strike}, Front={frontDte}, Back={backDte}",
            regime,
            isCall ? "Call" : "Put",
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

        // Max loss is the debit paid (spread cost)
        var maxLoss = spreadCost;

        // Calculate accurate max profit using grid search at front expiration
        var maxProfit = await CalculateMaxProfit(parameters, spreadCost);

        // Calculate accurate breakeven using numerical solver
        var breakEven = await CalculateBreakEven(parameters, spreadCost);

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

                // Calculate price - force recalculation to avoid stale cache
                option.setPricingEngine(fdEngine);
                var price = option.NPV();

                // Calculate Greeks using finite differences
                var delta = CalculateDelta(option, underlyingQuote, bsmProcess, fdEngine);
                var gamma = CalculateGamma(option, underlyingQuote, bsmProcess, fdEngine);
                var vega = CalculateVegaQuantlib(option, flatVolTs, bsmProcess, parameters, fdEngine);
                var theta = CalculateThetaQuantlib(option, fdEngine, bsmProcess, parameters);
                var rho = CalculateRhoQuantlib(option, flatRateTs, bsmProcess, parameters, fdEngine);

                var timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);
                var moneyness = parameters.UnderlyingPrice / parameters.Strike;

                // Clean up (dispose in reverse order of creation)
                fdEngine.Dispose();
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
    /// Prices an option using the Alaris.Double Healy (2021) framework for negative rates.
    /// Uses DoubleBoundaryApproximation with QD+ method and early exercise premium calculation.
    /// </summary>
    private Task<OptionPricing> PriceWithDouble(OptionParameters parameters)
    {
        return Task.Run(() =>
        {
            try
            {
                // Set evaluation date
                Settings.instance().setEvaluationDate(parameters.ValuationDate);

                var timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

                // Use DoubleBoundaryApproximation for Healy (2021) framework
                var approx = new Alaris.Double.DoubleBoundaryApproximation(
                    parameters.UnderlyingPrice,
                    parameters.Strike,
                    timeToExpiry,
                    parameters.RiskFreeRate,
                    parameters.DividendYield,
                    parameters.ImpliedVolatility,
                    parameters.OptionType == Option.Type.Call);

                // Calculate option price using QD+ approximation + early exercise premium
                var price = approx.ApproximateValue();

                // Calculate Greeks using finite differences
                var delta = CalculateDeltaWithDouble(parameters);
                var gamma = CalculateGammaWithDouble(parameters);
                var vega = CalculateVegaWithDouble(parameters);
                var theta = CalculateThetaWithDouble(parameters);
                var rho = CalculateRhoWithDouble(parameters);

                var moneyness = parameters.UnderlyingPrice / parameters.Strike;

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
                _logger?.LogError(ex, "Error pricing option with Alaris.Double Healy (2021) framework");
                throw;
            }
        });
    }

    // Greek calculations for Alaris.Double (Healy 2021 framework)

    private double CalculateDeltaWithDouble(OptionParameters parameters)
    {
        var originalSpot = parameters.UnderlyingPrice;
        var timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

        // Up bump
        var paramsUp = CloneParameters(parameters);
        paramsUp.UnderlyingPrice = originalSpot + BumpSize;
        var approxUp = new Alaris.Double.DoubleBoundaryApproximation(
            paramsUp.UnderlyingPrice, paramsUp.Strike, timeToExpiry,
            paramsUp.RiskFreeRate, paramsUp.DividendYield, paramsUp.ImpliedVolatility,
            paramsUp.OptionType == Option.Type.Call);
        var priceUp = approxUp.ApproximateValue();

        // Down bump
        var paramsDown = CloneParameters(parameters);
        paramsDown.UnderlyingPrice = originalSpot - BumpSize;
        var approxDown = new Alaris.Double.DoubleBoundaryApproximation(
            paramsDown.UnderlyingPrice, paramsDown.Strike, timeToExpiry,
            paramsDown.RiskFreeRate, paramsDown.DividendYield, paramsDown.ImpliedVolatility,
            paramsDown.OptionType == Option.Type.Call);
        var priceDown = approxDown.ApproximateValue();

        return (priceUp - priceDown) / (2 * BumpSize);
    }

    private double CalculateGammaWithDouble(OptionParameters parameters)
    {
        var originalSpot = parameters.UnderlyingPrice;
        var timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

        // Original price
        var approxOriginal = new Alaris.Double.DoubleBoundaryApproximation(
            originalSpot, parameters.Strike, timeToExpiry,
            parameters.RiskFreeRate, parameters.DividendYield, parameters.ImpliedVolatility,
            parameters.OptionType == Option.Type.Call);
        var priceOriginal = approxOriginal.ApproximateValue();

        // Up bump
        var paramsUp = CloneParameters(parameters);
        paramsUp.UnderlyingPrice = originalSpot + BumpSize;
        var approxUp = new Alaris.Double.DoubleBoundaryApproximation(
            paramsUp.UnderlyingPrice, paramsUp.Strike, timeToExpiry,
            paramsUp.RiskFreeRate, paramsUp.DividendYield, paramsUp.ImpliedVolatility,
            paramsUp.OptionType == Option.Type.Call);
        var priceUp = approxUp.ApproximateValue();

        // Down bump
        var paramsDown = CloneParameters(parameters);
        paramsDown.UnderlyingPrice = originalSpot - BumpSize;
        var approxDown = new Alaris.Double.DoubleBoundaryApproximation(
            paramsDown.UnderlyingPrice, paramsDown.Strike, timeToExpiry,
            paramsDown.RiskFreeRate, paramsDown.DividendYield, paramsDown.ImpliedVolatility,
            paramsDown.OptionType == Option.Type.Call);
        var priceDown = approxDown.ApproximateValue();

        return (priceUp - 2 * priceOriginal + priceDown) / (BumpSize * BumpSize);
    }

    private double CalculateVegaWithDouble(OptionParameters parameters)
    {
        var originalVol = parameters.ImpliedVolatility;
        var timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

        // Up bump
        var paramsUp = CloneParameters(parameters);
        paramsUp.ImpliedVolatility = originalVol + VolBumpSize;
        var approxUp = new Alaris.Double.DoubleBoundaryApproximation(
            paramsUp.UnderlyingPrice, paramsUp.Strike, timeToExpiry,
            paramsUp.RiskFreeRate, paramsUp.DividendYield, paramsUp.ImpliedVolatility,
            paramsUp.OptionType == Option.Type.Call);
        var priceUp = approxUp.ApproximateValue();

        // Down bump
        var paramsDown = CloneParameters(parameters);
        paramsDown.ImpliedVolatility = originalVol - VolBumpSize;
        var approxDown = new Alaris.Double.DoubleBoundaryApproximation(
            paramsDown.UnderlyingPrice, paramsDown.Strike, timeToExpiry,
            paramsDown.RiskFreeRate, paramsDown.DividendYield, paramsDown.ImpliedVolatility,
            paramsDown.OptionType == Option.Type.Call);
        var priceDown = approxDown.ApproximateValue();

        return (priceUp - priceDown) / (2 * VolBumpSize);
    }

    private double CalculateThetaWithDouble(OptionParameters parameters)
    {
        var originalDate = parameters.ValuationDate;
        var timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

        // Original price
        var approxOriginal = new Alaris.Double.DoubleBoundaryApproximation(
            parameters.UnderlyingPrice, parameters.Strike, timeToExpiry,
            parameters.RiskFreeRate, parameters.DividendYield, parameters.ImpliedVolatility,
            parameters.OptionType == Option.Type.Call);
        var priceOriginal = approxOriginal.ApproximateValue();

        // Forward date by 1 day
        var dayBump = 1;
        var forwardDate = originalDate.Add(new Period(dayBump, TimeUnit.Days));
        var timeToExpiryForward = CalculateTimeToExpiry(forwardDate, parameters.Expiry);

        var approxForward = new Alaris.Double.DoubleBoundaryApproximation(
            parameters.UnderlyingPrice, parameters.Strike, timeToExpiryForward,
            parameters.RiskFreeRate, parameters.DividendYield, parameters.ImpliedVolatility,
            parameters.OptionType == Option.Type.Call);
        var priceForward = approxForward.ApproximateValue();

        // Theta is price change per day (negative for time decay)
        return (priceForward - priceOriginal) / dayBump;
    }

    private double CalculateRhoWithDouble(OptionParameters parameters)
    {
        var originalRate = parameters.RiskFreeRate;
        var timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);
        var rateBump = 0.01; // 1% rate bump

        // Up bump
        var paramsUp = CloneParameters(parameters);
        paramsUp.RiskFreeRate = originalRate + rateBump;
        var approxUp = new Alaris.Double.DoubleBoundaryApproximation(
            paramsUp.UnderlyingPrice, paramsUp.Strike, timeToExpiry,
            paramsUp.RiskFreeRate, paramsUp.DividendYield, paramsUp.ImpliedVolatility,
            paramsUp.OptionType == Option.Type.Call);
        var priceUp = approxUp.ApproximateValue();

        // Down bump
        var paramsDown = CloneParameters(parameters);
        paramsDown.RiskFreeRate = originalRate - rateBump;
        var approxDown = new Alaris.Double.DoubleBoundaryApproximation(
            paramsDown.UnderlyingPrice, paramsDown.Strike, timeToExpiry,
            paramsDown.RiskFreeRate, paramsDown.DividendYield, paramsDown.ImpliedVolatility,
            paramsDown.OptionType == Option.Type.Call);
        var priceDown = approxDown.ApproximateValue();

        return (priceUp - priceDown) / (2 * rateBump);
    }

    private OptionParameters CloneParameters(OptionParameters original)
    {
        return new OptionParameters
        {
            UnderlyingPrice = original.UnderlyingPrice,
            Strike = original.Strike,
            Expiry = original.Expiry,
            ImpliedVolatility = original.ImpliedVolatility,
            RiskFreeRate = original.RiskFreeRate,
            DividendYield = original.DividendYield,
            OptionType = original.OptionType,
            ValuationDate = original.ValuationDate
        };
    }

    // Helper methods for Greek calculations

    private double CalculateDelta(VanillaOption option, SimpleQuote underlyingQuote, BlackScholesMertonProcess process, FdBlackScholesVanillaEngine engine)
    {
        var originalSpot = underlyingQuote.value();

        // Up bump
        underlyingQuote.setValue(originalSpot + BumpSize);
        option.setPricingEngine(engine); // Force recalculation
        var priceUp = option.NPV();

        // Down bump
        underlyingQuote.setValue(originalSpot - BumpSize);
        option.setPricingEngine(engine); // Force recalculation
        var priceDown = option.NPV();

        // Restore
        underlyingQuote.setValue(originalSpot);
        option.setPricingEngine(engine); // Restore state

        return (priceUp - priceDown) / (2 * BumpSize);
    }

    private double CalculateGamma(VanillaOption option, SimpleQuote underlyingQuote, BlackScholesMertonProcess process, FdBlackScholesVanillaEngine engine)
    {
        var originalSpot = underlyingQuote.value();
        option.setPricingEngine(engine); // Ensure fresh calculation
        option.setPricingEngine(engine); // Force complete cache invalidation
        var priceOriginal = option.NPV();

        // Up bump
        underlyingQuote.setValue(originalSpot + BumpSize);
        option.setPricingEngine(engine); // Force recalculation
        var priceUp = option.NPV();

        // Down bump
        underlyingQuote.setValue(originalSpot - BumpSize);
        option.setPricingEngine(engine); // Force recalculation
        var priceDown = option.NPV();

        // Restore
        underlyingQuote.setValue(originalSpot);
        option.setPricingEngine(engine); // Restore state

        return (priceUp - 2 * priceOriginal + priceDown) / (BumpSize * BumpSize);
    }

    private double CalculateVegaQuantlib(VanillaOption option, BlackConstantVol volTs,
        BlackScholesMertonProcess process, OptionParameters parameters, FdBlackScholesVanillaEngine originalEngine)
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

        // Restore original engine
        option.setPricingEngine(originalEngine);

        return (priceUp - priceDown) / (2 * VolBumpSize);
    }

    private double CalculateThetaQuantlib(VanillaOption option, FdBlackScholesVanillaEngine engine,
        BlackScholesMertonProcess process, OptionParameters parameters)
    {
        var originalDate = parameters.ValuationDate;
        var priceOriginal = option.NPV();

        // Forward date by 1 day - need to recreate EVERYTHING with new date
        var dayBump = 1; // 1 day
        var forwardDate = originalDate.Add(new Period(dayBump, TimeUnit.Days));
        Settings.instance().setEvaluationDate(forwardDate);

        // Recreate term structures anchored to forward date
        var dayCounter = new Actual365Fixed();

        var underlyingQuote = new SimpleQuote(parameters.UnderlyingPrice);
        var underlyingHandle = new QuoteHandle(underlyingQuote);

        var flatRateTs = new FlatForward(forwardDate, parameters.RiskFreeRate, dayCounter);
        var riskFreeRateHandle = new YieldTermStructureHandle(flatRateTs);

        var flatDividendTs = new FlatForward(forwardDate, parameters.DividendYield, dayCounter);
        var dividendYieldHandle = new YieldTermStructureHandle(flatDividendTs);

        var flatVolTs = new BlackConstantVol(forwardDate, new TARGET(), parameters.ImpliedVolatility, dayCounter);
        var volatilityHandle = new BlackVolTermStructureHandle(flatVolTs);

        // Recreate BSM process with new term structures
        var forwardProcess = new BlackScholesMertonProcess(
            underlyingHandle,
            dividendYieldHandle,
            riskFreeRateHandle,
            volatilityHandle);

        // Recreate option with new evaluation date as exercise start
        var forwardExercise = new AmericanExercise(forwardDate, parameters.Expiry);
        var payoff = new PlainVanillaPayoff(parameters.OptionType, parameters.Strike);
        var forwardOption = new VanillaOption(payoff, forwardExercise);

        // Price with forward date and forward process
        var forwardEngine = new FdBlackScholesVanillaEngine(forwardProcess, 100, 100);
        forwardOption.setPricingEngine(forwardEngine);
        var priceForward = forwardOption.NPV();

        // Clean up forward objects (in reverse order)
        forwardEngine.Dispose();
        forwardOption.Dispose();
        forwardProcess.Dispose();
        volatilityHandle.Dispose();
        flatVolTs.Dispose();
        dividendYieldHandle.Dispose();
        flatDividendTs.Dispose();
        riskFreeRateHandle.Dispose();
        flatRateTs.Dispose();
        underlyingHandle.Dispose();
        underlyingQuote.Dispose();
        forwardExercise.Dispose();

        // Restore original date and engine
        Settings.instance().setEvaluationDate(originalDate);
        option.setPricingEngine(engine);

        // Theta is price change per day (negative for time decay)
        return (priceForward - priceOriginal) / dayBump;
    }

    private double CalculateRhoQuantlib(VanillaOption option, FlatForward rateTs,
        BlackScholesMertonProcess process, OptionParameters parameters, FdBlackScholesVanillaEngine originalEngine)
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

        // Restore original engine
        option.setPricingEngine(originalEngine);

        return (priceUp - priceDown) / (2 * rateBump);
    }

    /// <summary>
    /// Calculates the maximum profit potential of a calendar spread using grid search.
    /// </summary>
    /// <param name="parameters">Calendar spread parameters.</param>
    /// <param name="spreadCost">The initial cost of the spread (debit paid).</param>
    /// <returns>Maximum profit potential at front expiration.</returns>
    private async Task<double> CalculateMaxProfit(CalendarSpreadParameters parameters, double spreadCost)
    {
        // Grid search across underlying price range at front expiration
        // We're looking for the price that maximizes: BackValue(S) - FrontValue(S) - SpreadCost

        var strike = parameters.Strike;
        var underlyingMin = strike * 0.5;  // Search from 50% to 150% of strike
        var underlyingMax = strike * 1.5;
        var gridPoints = 100;
        var step = (underlyingMax - underlyingMin) / gridPoints;

        var maxProfitValue = double.NegativeInfinity;

        for (int i = 0; i <= gridPoints; i++)
        {
            var underlyingPrice = underlyingMin + i * step;

            // Calculate front option value at expiration (intrinsic value only)
            var frontValue = CalculateIntrinsicValue(underlyingPrice, strike, parameters.OptionType);

            // Calculate back option value at front expiration (has time value remaining)
            var backParams = new OptionParameters
            {
                UnderlyingPrice = underlyingPrice,
                Strike = strike,
                Expiry = parameters.BackExpiry,
                ImpliedVolatility = parameters.ImpliedVolatility,
                RiskFreeRate = parameters.RiskFreeRate,
                DividendYield = parameters.DividendYield,
                OptionType = parameters.OptionType,
                ValuationDate = parameters.FrontExpiry  // Value at front expiration
            };

            var backPricing = await PriceOption(backParams);

            // P&L = Value of long back - Value of short front - Initial cost
            var profitLoss = backPricing.Price - frontValue - spreadCost;

            if (profitLoss > maxProfitValue)
            {
                maxProfitValue = profitLoss;
            }
        }

        return Math.Max(0, maxProfitValue); // Max profit cannot be negative
    }

    /// <summary>
    /// Calculates the breakeven point(s) for a calendar spread using bisection method.
    /// </summary>
    /// <param name="parameters">Calendar spread parameters.</param>
    /// <param name="spreadCost">The initial cost of the spread (debit paid).</param>
    /// <returns>Approximate breakeven underlying price at front expiration.</returns>
    private async Task<double> CalculateBreakEven(CalendarSpreadParameters parameters, double spreadCost)
    {
        // Use bisection to find underlying price where spread P&L = 0
        // At breakeven: BackValue(S) - FrontValue(S) = SpreadCost

        var strike = parameters.Strike;
        var tolerance = 0.01; // $0.01 tolerance
        var maxIterations = 50;

        // Calendar spreads typically have breakeven near the strike
        // Search in a reasonable range around strike
        var lowerBound = strike * 0.7;
        var upperBound = strike * 1.3;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            var midPoint = (lowerBound + upperBound) / 2.0;

            // Calculate spread value at this underlying price
            var frontValue = CalculateIntrinsicValue(midPoint, strike, parameters.OptionType);

            var backParams = new OptionParameters
            {
                UnderlyingPrice = midPoint,
                Strike = strike,
                Expiry = parameters.BackExpiry,
                ImpliedVolatility = parameters.ImpliedVolatility,
                RiskFreeRate = parameters.RiskFreeRate,
                DividendYield = parameters.DividendYield,
                OptionType = parameters.OptionType,
                ValuationDate = parameters.FrontExpiry
            };

            var backPricing = await PriceOption(backParams);
            var spreadValue = backPricing.Price - frontValue;
            var profitLoss = spreadValue - spreadCost;

            if (Math.Abs(profitLoss) < tolerance)
            {
                return midPoint; // Found breakeven
            }

            // Adjust search bounds based on whether we're above or below breakeven
            if (profitLoss > 0)
            {
                // Spread is profitable, breakeven is at a more extreme price
                // For calendar spreads, max profit is typically near strike
                // If we're profitable at midpoint, breakeven is further out
                if (midPoint > strike)
                    lowerBound = midPoint;
                else
                    upperBound = midPoint;
            }
            else
            {
                // Spread is unprofitable, breakeven is closer to strike
                if (midPoint > strike)
                    upperBound = midPoint;
                else
                    lowerBound = midPoint;
            }
        }

        // Return strike as default if no convergence (calendar spreads typically break even near strike)
        return strike;
    }

    /// <summary>
    /// Calculates the intrinsic value of an option at expiration.
    /// </summary>
    private static double CalculateIntrinsicValue(double underlyingPrice, double strike, Option.Type optionType)
    {
        if (optionType == Option.Type.Call)
        {
            return Math.Max(0, underlyingPrice - strike);
        }
        else
        {
            return Math.Max(0, strike - underlyingPrice);
        }
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
