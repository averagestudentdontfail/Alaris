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
/// - If r &gt;= 0: Use Alaris.Quantlib (standard American option pricing)
/// - If r &lt; 0 and q &lt; r: Use Alaris.Double (double boundary method for negative rates)
/// - If r &lt; 0 and q &gt;= r: Use Alaris.Quantlib (single boundary still applies)
/// </remarks>
public sealed class UnifiedPricingEngine : IOptionPricingEngine, IDisposable
{
    private readonly ILogger<UnifiedPricingEngine>? _logger;
    private bool _disposed;

    // LoggerMessage delegates
    private static readonly Action<ILogger, double, double, string, string, PricingRegime, Exception?> LogPricingOption =
        LoggerMessage.Define<double, double, string, string, PricingRegime>(
            LogLevel.Debug,
            new EventId(1, nameof(LogPricingOption)),
            "Pricing option: S={UnderlyingPrice}, K={Strike}, Params={Parameters}, Type={OptionType}, Regime={Regime}");

    private static readonly Action<ILogger, PricingRegime, string, double, int, int, Exception?> LogPricingCalendarSpread =
        LoggerMessage.Define<PricingRegime, string, double, int, int>(
            LogLevel.Information,
            new EventId(2, nameof(LogPricingCalendarSpread)),
            "Pricing calendar spread: Regime={Regime}, Type={Type}, Strike={Strike}, Front={FrontDte}, Back={BackDte}");

    private static readonly Action<ILogger, int, double, Exception?> LogImpliedVolatilityConverged =
        LoggerMessage.Define<int, double>(
            LogLevel.Debug,
            new EventId(3, nameof(LogImpliedVolatilityConverged)),
            "Implied volatility calculation converged in {Iterations} iterations: IV={Iv:F4}");

    private static readonly Action<ILogger, Exception?> LogErrorPricingQuantlib =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(4, nameof(LogErrorPricingQuantlib)),
            "Error pricing option with Quantlib");

    private static readonly Action<ILogger, Exception?> LogErrorPricingDouble =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5, nameof(LogErrorPricingDouble)),
            "Error pricing option with Alaris.Double Healy (2021) framework");

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

        bool isCall = parameters.OptionType == Option.Type.Call;
        PricingRegime regime = DetermineRegime(parameters.RiskFreeRate, parameters.DividendYield, isCall);

        if (_logger != null)
        {
            double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);
            string paramsStr = $"r={parameters.RiskFreeRate:F4}, q={parameters.DividendYield:F4}, σ={parameters.ImpliedVolatility:F4}, T={timeToExpiry:F4}";
            LogPricingOption(_logger, parameters.UnderlyingPrice, parameters.Strike, paramsStr,
                isCall ? "Call" : "Put", regime, null);
        }

        return regime switch
        {
            PricingRegime.PositiveRates => await PriceWithQuantlib(parameters).ConfigureAwait(false),
            PricingRegime.DoubleBoundary => await PriceWithDouble(parameters).ConfigureAwait(false),
            PricingRegime.NegativeRatesSingleBoundary => await PriceWithQuantlib(parameters).ConfigureAwait(false),
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

        bool isCall = parameters.OptionType == Option.Type.Call;
        PricingRegime regime = DetermineRegime(parameters.RiskFreeRate, parameters.DividendYield, isCall);

        if (_logger != null)
        {
            LogPricingCalendarSpread(_logger, regime, isCall ? "Call" : "Put", parameters.Strike,
                CalculateDaysToExpiry(parameters.ValuationDate, parameters.FrontExpiry),
                CalculateDaysToExpiry(parameters.ValuationDate, parameters.BackExpiry), null);
        }

        // Price front month (short position)
        OptionParameters frontParams = new OptionParameters
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

        OptionPricing frontPricing = await PriceOption(frontParams).ConfigureAwait(false);

        // Price back month (long position)
        OptionParameters backParams = new OptionParameters
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

        OptionPricing backPricing = await PriceOption(backParams).ConfigureAwait(false);

        // Calculate spread Greeks (long back - short front)
        double spreadCost = backPricing.Price - frontPricing.Price;
        double spreadDelta = backPricing.Delta - frontPricing.Delta;
        double spreadGamma = backPricing.Gamma - frontPricing.Gamma;
        double spreadVega = backPricing.Vega - frontPricing.Vega;
        double spreadTheta = backPricing.Theta - frontPricing.Theta;
        double spreadRho = backPricing.Rho - frontPricing.Rho;

        // Max loss is the debit paid (spread cost)
        double maxLoss = spreadCost;

        // Calculate accurate max profit using grid search at front expiration
        double maxProfit = await CalculateMaxProfit(parameters, spreadCost).ConfigureAwait(false);

        // Calculate accurate breakeven using numerical solver
        double breakEven = await CalculateBreakEven(parameters, spreadCost).ConfigureAwait(false);

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
        {
            throw new ArgumentException("Market price must be positive", nameof(marketPrice));
        }

        // Use bisection method to find IV
        double volLow = 0.01;  // 1% vol
        double volHigh = 5.0;   // 500% vol (extreme)
        double volMid = 0;
        int iterations = 0;

        while (iterations < MaxIVIterations && ((volHigh - volLow) > IVTolerance))
        {
            volMid = ((volLow + volHigh) / 2.0);

            OptionParameters testParams = new OptionParameters
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

            OptionPricing pricing = await PriceOption(testParams).ConfigureAwait(false);
            double priceDiff = pricing.Price - marketPrice;

            if (Math.Abs(priceDiff) < IVTolerance)
            {
                break;
            }

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

        if (_logger != null)
        {
            LogImpliedVolatilityConverged(_logger, iterations, volMid, null);
        }

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
                SimpleQuote underlyingQuote = new SimpleQuote(parameters.UnderlyingPrice);
                QuoteHandle underlyingHandle = new QuoteHandle(underlyingQuote);

                // Create term structures
                Actual365Fixed dayCounter = new Actual365Fixed();

                FlatForward flatRateTs = new FlatForward(
                    parameters.ValuationDate,
                    parameters.RiskFreeRate,
                    dayCounter);
                YieldTermStructureHandle riskFreeRateHandle = new YieldTermStructureHandle(flatRateTs);

                FlatForward flatDividendTs = new FlatForward(
                    parameters.ValuationDate,
                    parameters.DividendYield,
                    dayCounter);
                YieldTermStructureHandle dividendYieldHandle = new YieldTermStructureHandle(flatDividendTs);

                BlackConstantVol flatVolTs = new BlackConstantVol(
                    parameters.ValuationDate,
                    new TARGET(),
                    parameters.ImpliedVolatility,
                    dayCounter);
                BlackVolTermStructureHandle volatilityHandle = new BlackVolTermStructureHandle(flatVolTs);

                // Create Black-Scholes-Merton process
                BlackScholesMertonProcess bsmProcess = new BlackScholesMertonProcess(
                    underlyingHandle,
                    dividendYieldHandle,
                    riskFreeRateHandle,
                    volatilityHandle);

                // Create option
                AmericanExercise exercise = new AmericanExercise(parameters.ValuationDate, parameters.Expiry);
                PlainVanillaPayoff payoff = new PlainVanillaPayoff(parameters.OptionType, parameters.Strike);
                VanillaOption option = new VanillaOption(payoff, exercise);

                // Create pricing engine for main price (using FD for Americans by default)
                FdBlackScholesVanillaEngine priceEngine = new FdBlackScholesVanillaEngine(bsmProcess, 100, 100);
                option.setPricingEngine(priceEngine);

                // Ensure evaluation date is set correctly before pricing
                Settings.instance().setEvaluationDate(parameters.ValuationDate);
                double price = option.NPV();
                priceEngine.Dispose();

                // Create fresh pricing engine for Greek calculations
                FdBlackScholesVanillaEngine fdEngine = new FdBlackScholesVanillaEngine(bsmProcess, 100, 100);
                option.setPricingEngine(fdEngine);

                // Calculate Greeks using finite differences
                double delta = CalculateDelta(parameters);
                double gamma = CalculateGamma(parameters);
                double vega = CalculateVega(parameters);
                double theta = CalculateTheta(parameters);
                double rho = CalculateRho(parameters);

                double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);
                double moneyness = (parameters.UnderlyingPrice / parameters.Strike);

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
                if (_logger != null)
                {
                    LogErrorPricingQuantlib(_logger, ex);
                }
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

                double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

                // Use DoubleBoundaryApproximation for Healy (2021) framework
                Alaris.Double.DoubleBoundaryApproximation approx = new Alaris.Double.DoubleBoundaryApproximation(
                    parameters.UnderlyingPrice,
                    parameters.Strike,
                    timeToExpiry,
                    parameters.RiskFreeRate,
                    parameters.DividendYield,
                    parameters.ImpliedVolatility,
                    parameters.OptionType == Option.Type.Call);

                // Calculate option price using QD+ approximation + early exercise premium
                double price = approx.ApproximateValue();

                // Calculate Greeks using finite differences
                double delta = CalculateDeltaWithDouble(parameters);
                double gamma = CalculateGammaWithDouble(parameters);
                double vega = CalculateVegaWithDouble(parameters);
                double theta = CalculateThetaWithDouble(parameters);
                double rho = CalculateRhoWithDouble(parameters);

                double moneyness = (parameters.UnderlyingPrice / parameters.Strike);

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
                if (_logger != null)
                {
                    LogErrorPricingDouble(_logger, ex);
                }
                throw;
            }
        });
    }

    // Greek calculations for Alaris.Double (Healy 2021 framework)

    private double CalculateDeltaWithDouble(OptionParameters parameters)
    {
        double originalSpot = parameters.UnderlyingPrice;
        double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

        // Up bump
        OptionParameters paramsUp = CloneParameters(parameters);
        paramsUp.UnderlyingPrice = originalSpot + BumpSize;
        Alaris.Double.DoubleBoundaryApproximation approxUp = new Alaris.Double.DoubleBoundaryApproximation(
            paramsUp.UnderlyingPrice, paramsUp.Strike, timeToExpiry,
            paramsUp.RiskFreeRate, paramsUp.DividendYield, paramsUp.ImpliedVolatility,
            paramsUp.OptionType == Option.Type.Call);
        double priceUp = approxUp.ApproximateValue();

        // Down bump
        OptionParameters paramsDown = CloneParameters(parameters);
        paramsDown.UnderlyingPrice = originalSpot - BumpSize;
        Alaris.Double.DoubleBoundaryApproximation approxDown = new Alaris.Double.DoubleBoundaryApproximation(
            paramsDown.UnderlyingPrice, paramsDown.Strike, timeToExpiry,
            paramsDown.RiskFreeRate, paramsDown.DividendYield, paramsDown.ImpliedVolatility,
            paramsDown.OptionType == Option.Type.Call);
        double priceDown = approxDown.ApproximateValue();

        return ((priceUp - priceDown) / (2 * BumpSize));
    }

    private double CalculateGammaWithDouble(OptionParameters parameters)
    {
        double originalSpot = parameters.UnderlyingPrice;
        double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

        // Original price
        Alaris.Double.DoubleBoundaryApproximation approxOriginal = new Alaris.Double.DoubleBoundaryApproximation(
            originalSpot, parameters.Strike, timeToExpiry,
            parameters.RiskFreeRate, parameters.DividendYield, parameters.ImpliedVolatility,
            parameters.OptionType == Option.Type.Call);
        double priceOriginal = approxOriginal.ApproximateValue();

        // Up bump
        OptionParameters paramsUp = CloneParameters(parameters);
        paramsUp.UnderlyingPrice = originalSpot + BumpSize;
        Alaris.Double.DoubleBoundaryApproximation approxUp = new Alaris.Double.DoubleBoundaryApproximation(
            paramsUp.UnderlyingPrice, paramsUp.Strike, timeToExpiry,
            paramsUp.RiskFreeRate, paramsUp.DividendYield, paramsUp.ImpliedVolatility,
            paramsUp.OptionType == Option.Type.Call);
        double priceUp = approxUp.ApproximateValue();

        // Down bump
        OptionParameters paramsDown = CloneParameters(parameters);
        paramsDown.UnderlyingPrice = originalSpot - BumpSize;
        Alaris.Double.DoubleBoundaryApproximation approxDown = new Alaris.Double.DoubleBoundaryApproximation(
            paramsDown.UnderlyingPrice, paramsDown.Strike, timeToExpiry,
            paramsDown.RiskFreeRate, paramsDown.DividendYield, paramsDown.ImpliedVolatility,
            paramsDown.OptionType == Option.Type.Call);
        double priceDown = approxDown.ApproximateValue();

        return ((priceUp - (2 * priceOriginal) + priceDown) / (BumpSize * BumpSize));
    }

    private double CalculateVegaWithDouble(OptionParameters parameters)
    {
        double originalVol = parameters.ImpliedVolatility;
        double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

        // Up bump
        OptionParameters paramsUp = CloneParameters(parameters);
        paramsUp.ImpliedVolatility = originalVol + VolBumpSize;
        Alaris.Double.DoubleBoundaryApproximation approxUp = new Alaris.Double.DoubleBoundaryApproximation(
            paramsUp.UnderlyingPrice, paramsUp.Strike, timeToExpiry,
            paramsUp.RiskFreeRate, paramsUp.DividendYield, paramsUp.ImpliedVolatility,
            paramsUp.OptionType == Option.Type.Call);
        double priceUp = approxUp.ApproximateValue();

        // Down bump
        OptionParameters paramsDown = CloneParameters(parameters);
        paramsDown.ImpliedVolatility = originalVol - VolBumpSize;
        Alaris.Double.DoubleBoundaryApproximation approxDown = new Alaris.Double.DoubleBoundaryApproximation(
            paramsDown.UnderlyingPrice, paramsDown.Strike, timeToExpiry,
            paramsDown.RiskFreeRate, paramsDown.DividendYield, paramsDown.ImpliedVolatility,
            paramsDown.OptionType == Option.Type.Call);
        double priceDown = approxDown.ApproximateValue();

        return ((priceUp - priceDown) / (2 * VolBumpSize));
    }

    private double CalculateThetaWithDouble(OptionParameters parameters)
    {
        Date originalDate = parameters.ValuationDate;
        double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

        // Original price
        Alaris.Double.DoubleBoundaryApproximation approxOriginal = new Alaris.Double.DoubleBoundaryApproximation(
            parameters.UnderlyingPrice, parameters.Strike, timeToExpiry,
            parameters.RiskFreeRate, parameters.DividendYield, parameters.ImpliedVolatility,
            parameters.OptionType == Option.Type.Call);
        double priceOriginal = approxOriginal.ApproximateValue();

        // Forward date by 1 day
        int dayBump = 1;
        Period period = new Period(dayBump, TimeUnit.Days);
        Date forwardDate = originalDate.Add(period);
        double timeToExpiryForward = CalculateTimeToExpiry(forwardDate, parameters.Expiry);

        Alaris.Double.DoubleBoundaryApproximation approxForward = new Alaris.Double.DoubleBoundaryApproximation(
            parameters.UnderlyingPrice, parameters.Strike, timeToExpiryForward,
            parameters.RiskFreeRate, parameters.DividendYield, parameters.ImpliedVolatility,
            parameters.OptionType == Option.Type.Call);
        double priceForward = approxForward.ApproximateValue();

        // Clean up
        period.Dispose();

        // Theta is price change per day (negative for time decay)
        return ((priceForward - priceOriginal) / dayBump);
    }

    private double CalculateRhoWithDouble(OptionParameters parameters)
    {
        double originalRate = parameters.RiskFreeRate;
        double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);
        double rateBump = 0.01; // 1% rate bump

        // Up bump
        OptionParameters paramsUp = CloneParameters(parameters);
        paramsUp.RiskFreeRate = originalRate + rateBump;
        Alaris.Double.DoubleBoundaryApproximation approxUp = new Alaris.Double.DoubleBoundaryApproximation(
            paramsUp.UnderlyingPrice, paramsUp.Strike, timeToExpiry,
            paramsUp.RiskFreeRate, paramsUp.DividendYield, paramsUp.ImpliedVolatility,
            paramsUp.OptionType == Option.Type.Call);
        double priceUp = approxUp.ApproximateValue();

        // Down bump
        OptionParameters paramsDown = CloneParameters(parameters);
        paramsDown.RiskFreeRate = originalRate - rateBump;
        Alaris.Double.DoubleBoundaryApproximation approxDown = new Alaris.Double.DoubleBoundaryApproximation(
            paramsDown.UnderlyingPrice, paramsDown.Strike, timeToExpiry,
            paramsDown.RiskFreeRate, paramsDown.DividendYield, paramsDown.ImpliedVolatility,
            paramsDown.OptionType == Option.Type.Call);
        double priceDown = approxDown.ApproximateValue();

        return ((priceUp - priceDown) / (2 * rateBump));
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

    /// <summary>
    /// Synchronously prices an option for Greek calculations (QuantLib only).
    /// Creates a complete, fresh pricing infrastructure for accurate finite differences.
    /// </summary>
    private double PriceOptionSync(OptionParameters parameters)
    {
        // Set evaluation date
        Settings.instance().setEvaluationDate(parameters.ValuationDate);

        // Create underlying quote
        SimpleQuote underlyingQuote = new SimpleQuote(parameters.UnderlyingPrice);
        QuoteHandle underlyingHandle = new QuoteHandle(underlyingQuote);

        // Create term structures
        Actual365Fixed dayCounter = new Actual365Fixed();

        FlatForward flatRateTs = new FlatForward(
            parameters.ValuationDate,
            parameters.RiskFreeRate,
            dayCounter);
        YieldTermStructureHandle riskFreeRateHandle = new YieldTermStructureHandle(flatRateTs);

        FlatForward flatDividendTs = new FlatForward(
            parameters.ValuationDate,
            parameters.DividendYield,
            dayCounter);
        YieldTermStructureHandle dividendYieldHandle = new YieldTermStructureHandle(flatDividendTs);

        TARGET calendar = new TARGET();
        BlackConstantVol flatVolTs = new BlackConstantVol(
            parameters.ValuationDate,
            calendar,
            parameters.ImpliedVolatility,
            dayCounter);
        BlackVolTermStructureHandle volatilityHandle = new BlackVolTermStructureHandle(flatVolTs);

        // Create Black-Scholes-Merton process
        BlackScholesMertonProcess bsmProcess = new BlackScholesMertonProcess(
            underlyingHandle,
            dividendYieldHandle,
            riskFreeRateHandle,
            volatilityHandle);

        // Create option
        AmericanExercise exercise = new AmericanExercise(parameters.ValuationDate, parameters.Expiry);
        PlainVanillaPayoff payoff = new PlainVanillaPayoff(parameters.OptionType, parameters.Strike);
        VanillaOption option = new VanillaOption(payoff, exercise);

        // Create pricing engine and price
        FdBlackScholesVanillaEngine engine = new FdBlackScholesVanillaEngine(bsmProcess, 100, 100);
        option.setPricingEngine(engine);
        double price = option.NPV();

        // Clean up (dispose in reverse order of creation)
        engine.Dispose();
        option.Dispose();
        payoff.Dispose();
        exercise.Dispose();
        bsmProcess.Dispose();
        volatilityHandle.Dispose();
        flatVolTs.Dispose();
        calendar.Dispose();
        dividendYieldHandle.Dispose();
        flatDividendTs.Dispose();
        riskFreeRateHandle.Dispose();
        flatRateTs.Dispose();
        dayCounter.Dispose();
        underlyingHandle.Dispose();
        underlyingQuote.Dispose();

        return price;
    }

    // Helper methods for Greek calculations

    private double CalculateDelta(OptionParameters parameters)
    {
        double originalSpot = parameters.UnderlyingPrice;

        // Price with up bump
        OptionParameters paramsUp = CloneParameters(parameters);
        paramsUp.UnderlyingPrice = originalSpot + BumpSize;
        double priceUp = PriceOptionSync(paramsUp);

        // Price with down bump
        OptionParameters paramsDown = CloneParameters(parameters);
        paramsDown.UnderlyingPrice = originalSpot - BumpSize;
        double priceDown = PriceOptionSync(paramsDown);

        return ((priceUp - priceDown) / (2 * BumpSize));
    }

    private double CalculateGamma(OptionParameters parameters)
    {
        double originalSpot = parameters.UnderlyingPrice;

        // Original price
        double priceOriginal = PriceOptionSync(parameters);

        // Price with up bump
        OptionParameters paramsUp = CloneParameters(parameters);
        paramsUp.UnderlyingPrice = originalSpot + BumpSize;
        double priceUp = PriceOptionSync(paramsUp);

        // Price with down bump
        OptionParameters paramsDown = CloneParameters(parameters);
        paramsDown.UnderlyingPrice = originalSpot - BumpSize;
        double priceDown = PriceOptionSync(paramsDown);

        return ((priceUp - (2 * priceOriginal) + priceDown) / (BumpSize * BumpSize));
    }

    private double CalculateVega(OptionParameters parameters)
    {
        double originalVol = parameters.ImpliedVolatility;

        // Price with up bump
        OptionParameters paramsUp = CloneParameters(parameters);
        paramsUp.ImpliedVolatility = originalVol + VolBumpSize;
        double priceUp = PriceOptionSync(paramsUp);

        // Price with down bump
        OptionParameters paramsDown = CloneParameters(parameters);
        paramsDown.ImpliedVolatility = originalVol - VolBumpSize;
        double priceDown = PriceOptionSync(paramsDown);

        return ((priceUp - priceDown) / (2 * VolBumpSize));
    }

    private double CalculateTheta(OptionParameters parameters)
    {
        Date originalDate = parameters.ValuationDate;

        // Original price
        double priceOriginal = PriceOptionSync(parameters);

        // Price with forward date (1 day)
        int dayBump = 1;
        Period period = new Period(dayBump, TimeUnit.Days);
        Date forwardDate = originalDate.Add(period);
        OptionParameters paramsForward = CloneParameters(parameters);
        paramsForward.ValuationDate = forwardDate;
        double priceForward = PriceOptionSync(paramsForward);

        // Clean up
        period.Dispose();

        // Theta is price change per day (negative for time decay)
        return ((priceForward - priceOriginal) / dayBump);
    }

    private double CalculateRho(OptionParameters parameters)
    {
        double originalRate = parameters.RiskFreeRate;
        double rateBump = 0.01; // 1% rate bump

        // Price with up bump
        OptionParameters paramsUp = CloneParameters(parameters);
        paramsUp.RiskFreeRate = originalRate + rateBump;
        double priceUp = PriceOptionSync(paramsUp);

        // Price with down bump
        OptionParameters paramsDown = CloneParameters(parameters);
        paramsDown.RiskFreeRate = originalRate - rateBump;
        double priceDown = PriceOptionSync(paramsDown);

        return ((priceUp - priceDown) / (2 * rateBump));
    }

    private double CalculateVegaQuantlib(VanillaOption option, BlackConstantVol volTs,
        BlackScholesMertonProcess process, OptionParameters parameters, FdBlackScholesVanillaEngine originalEngine)
    {
        double originalVol = parameters.ImpliedVolatility;
        Actual365Fixed dayCounter = new Actual365Fixed();

        // Up bump
        TARGET calendarUp = new TARGET();
        BlackConstantVol volUp = new BlackConstantVol(
            parameters.ValuationDate,
            calendarUp,
            originalVol + VolBumpSize,
            dayCounter);
        BlackVolTermStructureHandle volHandleUp = new BlackVolTermStructureHandle(volUp);
        BlackScholesMertonProcess processUp = new BlackScholesMertonProcess(
            process.stateVariable(),
            process.dividendYield(),
            process.riskFreeRate(),
            volHandleUp);
        FdBlackScholesVanillaEngine engineUp = new FdBlackScholesVanillaEngine(processUp, 100, 100);
        option.setPricingEngine(engineUp);
        double priceUp = option.NPV();

        // Down bump
        TARGET calendarDown = new TARGET();
        BlackConstantVol volDown = new BlackConstantVol(
            parameters.ValuationDate,
            calendarDown,
            originalVol - VolBumpSize,
            dayCounter);
        BlackVolTermStructureHandle volHandleDown = new BlackVolTermStructureHandle(volDown);
        BlackScholesMertonProcess processDown = new BlackScholesMertonProcess(
            process.stateVariable(),
            process.dividendYield(),
            process.riskFreeRate(),
            volHandleDown);
        FdBlackScholesVanillaEngine engineDown = new FdBlackScholesVanillaEngine(processDown, 100, 100);
        option.setPricingEngine(engineDown);
        double priceDown = option.NPV();

        // Clean up
        engineDown.Dispose();
        processDown.Dispose();
        volHandleDown.Dispose();
        volDown.Dispose();
        calendarDown.Dispose();
        engineUp.Dispose();
        processUp.Dispose();
        volHandleUp.Dispose();
        volUp.Dispose();
        calendarUp.Dispose();
        dayCounter.Dispose();

        // Restore original engine
        option.setPricingEngine(originalEngine);

        return ((priceUp - priceDown) / (2 * VolBumpSize));
    }

    private double CalculateThetaQuantlib(VanillaOption option, FdBlackScholesVanillaEngine engine,
        BlackScholesMertonProcess process, OptionParameters parameters)
    {
        Date originalDate = parameters.ValuationDate;
        double priceOriginal = option.NPV();

        // Forward date by 1 day - need to recreate EVERYTHING with new date
        int dayBump = 1; // 1 day
        Period period = new Period(dayBump, TimeUnit.Days);
        Date forwardDate = originalDate.Add(period);
        Settings.instance().setEvaluationDate(forwardDate);

        // Recreate term structures anchored to forward date
        Actual365Fixed dayCounter = new Actual365Fixed();

        SimpleQuote underlyingQuote = new SimpleQuote(parameters.UnderlyingPrice);
        QuoteHandle underlyingHandle = new QuoteHandle(underlyingQuote);

        FlatForward flatRateTs = new FlatForward(forwardDate, parameters.RiskFreeRate, dayCounter);
        YieldTermStructureHandle riskFreeRateHandle = new YieldTermStructureHandle(flatRateTs);

        FlatForward flatDividendTs = new FlatForward(forwardDate, parameters.DividendYield, dayCounter);
        YieldTermStructureHandle dividendYieldHandle = new YieldTermStructureHandle(flatDividendTs);

        TARGET calendar = new TARGET();
        BlackConstantVol flatVolTs = new BlackConstantVol(forwardDate, calendar, parameters.ImpliedVolatility, dayCounter);
        BlackVolTermStructureHandle volatilityHandle = new BlackVolTermStructureHandle(flatVolTs);

        // Recreate BSM process with new term structures
        BlackScholesMertonProcess forwardProcess = new BlackScholesMertonProcess(
            underlyingHandle,
            dividendYieldHandle,
            riskFreeRateHandle,
            volatilityHandle);

        // Recreate option with new evaluation date as exercise start
        AmericanExercise forwardExercise = new AmericanExercise(forwardDate, parameters.Expiry);
        PlainVanillaPayoff payoff = new PlainVanillaPayoff(parameters.OptionType, parameters.Strike);
        VanillaOption forwardOption = new VanillaOption(payoff, forwardExercise);

        // Price with forward date and forward process
        FdBlackScholesVanillaEngine forwardEngine = new FdBlackScholesVanillaEngine(forwardProcess, 100, 100);
        forwardOption.setPricingEngine(forwardEngine);
        double priceForward = forwardOption.NPV();

        // Clean up forward objects (in reverse order)
        forwardEngine.Dispose();
        forwardOption.Dispose();
        payoff.Dispose();
        forwardExercise.Dispose();
        forwardProcess.Dispose();
        volatilityHandle.Dispose();
        flatVolTs.Dispose();
        calendar.Dispose();
        dividendYieldHandle.Dispose();
        flatDividendTs.Dispose();
        riskFreeRateHandle.Dispose();
        flatRateTs.Dispose();
        dayCounter.Dispose();
        underlyingHandle.Dispose();
        underlyingQuote.Dispose();
        period.Dispose();

        // Restore original date and engine
        Settings.instance().setEvaluationDate(originalDate);
        option.setPricingEngine(engine);

        // Theta is price change per day (negative for time decay)
        return ((priceForward - priceOriginal) / dayBump);
    }

    private double CalculateRhoQuantlib(VanillaOption option, FlatForward rateTs,
        BlackScholesMertonProcess process, OptionParameters parameters, FdBlackScholesVanillaEngine originalEngine)
    {
        double originalRate = parameters.RiskFreeRate;
        Actual365Fixed dayCounter = new Actual365Fixed();
        double rateBump = 0.01; // 1% rate bump

        // Up bump
        FlatForward rateUp = new FlatForward(
            parameters.ValuationDate,
            originalRate + rateBump,
            dayCounter);
        YieldTermStructureHandle rateHandleUp = new YieldTermStructureHandle(rateUp);
        BlackScholesMertonProcess processUp = new BlackScholesMertonProcess(
            process.stateVariable(),
            process.dividendYield(),
            rateHandleUp,
            process.blackVolatility());
        FdBlackScholesVanillaEngine engineUp = new FdBlackScholesVanillaEngine(processUp, 100, 100);
        option.setPricingEngine(engineUp);
        double priceUp = option.NPV();

        // Down bump
        FlatForward rateDown = new FlatForward(
            parameters.ValuationDate,
            originalRate - rateBump,
            dayCounter);
        YieldTermStructureHandle rateHandleDown = new YieldTermStructureHandle(rateDown);
        BlackScholesMertonProcess processDown = new BlackScholesMertonProcess(
            process.stateVariable(),
            process.dividendYield(),
            rateHandleDown,
            process.blackVolatility());
        FdBlackScholesVanillaEngine engineDown = new FdBlackScholesVanillaEngine(processDown, 100, 100);
        option.setPricingEngine(engineDown);
        double priceDown = option.NPV();

        // Clean up
        engineDown.Dispose();
        processDown.Dispose();
        rateHandleDown.Dispose();
        rateDown.Dispose();
        engineUp.Dispose();
        processUp.Dispose();
        rateHandleUp.Dispose();
        rateUp.Dispose();
        dayCounter.Dispose();

        // Restore original engine
        option.setPricingEngine(originalEngine);

        return ((priceUp - priceDown) / (2 * rateBump));
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

        double strike = parameters.Strike;
        double underlyingMin = (strike * 0.5);  // Search from 50% to 150% of strike
        double underlyingMax = (strike * 1.5);
        int gridPoints = 100;
        double step = ((underlyingMax - underlyingMin) / gridPoints);

        double maxProfitValue = double.NegativeInfinity;

        for (int i = 0; i <= gridPoints; i++)
        {
            double underlyingPrice = (underlyingMin + (i * step));

            // Calculate front option value at expiration (intrinsic value only)
            double frontValue = CalculateIntrinsicValue(underlyingPrice, strike, parameters.OptionType);

            // Calculate back option value at front expiration (has time value remaining)
            OptionParameters backParams = new OptionParameters
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

            OptionPricing backPricing = await PriceOption(backParams).ConfigureAwait(false);

            // P&L = Value of long back - Value of short front - Initial cost
            double profitLoss = (backPricing.Price - frontValue - spreadCost);

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

        double strike = parameters.Strike;
        double tolerance = 0.01; // $0.01 tolerance
        int maxIterations = 50;

        // Calendar spreads typically have breakeven near the strike
        // Search in a reasonable range around strike
        double lowerBound = (strike * 0.7);
        double upperBound = (strike * 1.3);

        for (int iter = 0; iter < maxIterations; iter++)
        {
            double midPoint = ((lowerBound + upperBound) / 2.0);

            // Calculate spread value at this underlying price
            double frontValue = CalculateIntrinsicValue(midPoint, strike, parameters.OptionType);

            OptionParameters backParams = new OptionParameters
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

            OptionPricing backPricing = await PriceOption(backParams).ConfigureAwait(false);
            double spreadValue = (backPricing.Price - frontValue);
            double profitLoss = (spreadValue - spreadCost);

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
                {
                    lowerBound = midPoint;
                }
                else
                {
                    upperBound = midPoint;
                }
            }
            else
            {
                // Spread is unprofitable, breakeven is closer to strike
                if (midPoint > strike)
                {
                    upperBound = midPoint;
                }
                else
                {
                    lowerBound = midPoint;
                }
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
        Actual365Fixed dayCounter = new Actual365Fixed();
        double result = dayCounter.yearFraction(valuationDate, expiryDate);
        dayCounter.Dispose();
        return result;
    }

    private static int CalculateDaysToExpiry(Date valuationDate, Date expiryDate)
    {
        return expiryDate.serialNumber() - valuationDate.serialNumber();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }
}

/// <summary>
/// Represents the pricing regime based on interest rate conditions.
/// </summary>
public enum PricingRegime
{
    /// <summary>
    /// Standard regime with positive interest rates (r &gt;= 0).
    /// Uses Alaris.Quantlib with single boundary.
    /// </summary>
    PositiveRates,

    /// <summary>
    /// Double boundary regime with negative rates where q &lt; r &lt; 0.
    /// Uses Alaris.Double with double boundary method.
    /// </summary>
    DoubleBoundary,

    /// <summary>
    /// Negative rates but single boundary regime where r &lt; 0 and q &gt;= r.
    /// Uses Alaris.Quantlib with single boundary.
    /// </summary>
    NegativeRatesSingleBoundary
}