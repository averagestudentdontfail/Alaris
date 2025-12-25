using Alaris.Strategy.Model;
using Alaris.Strategy.Pricing;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Bridge;

/// <summary>
/// Unified pricing engine that automatically selects between Alaris.Double and Alaris.Quantlib
/// based on interest rate regime. Supports both positive and negative interest rates.
/// </summary>

public sealed class STBR001A : STBR002A, IDisposable
{
    private readonly ILogger<STBR001A>? _logger;
    private readonly STBR003A? _infrastructureCache;
    private bool _disposed;

    // LoggerMessage delegates
    private static readonly Action<ILogger, double, double, string, string, PricingRegime, Exception?> LogPricingOption =
        LoggerMessage.Define<double, double, string, string, PricingRegime>(
            LogLevel.Debug,
            new EventId(1, nameof(LogPricingOption)),
            "Pricing option: S={UnderlyingPrice}, K={Strike}, Params={Parameters}, Type={OptionType}, Regime={Regime}");

    private static readonly Action<ILogger, PricingRegime, string, double, int, int, Exception?> LogPricingSTPR001A =
        LoggerMessage.Define<PricingRegime, string, double, int, int>(
            LogLevel.Information,
            new EventId(2, nameof(LogPricingSTPR001A)),
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
    /// Initializes a new instance of the STBR001A.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="enableCaching">Enable infrastructure caching for Greek calculations (default: true).</param>
    public STBR001A(ILogger<STBR001A>? logger = null, bool enableCaching = true)
    {
        _logger = logger;
        
        // Create infrastructure cache for optimised Greek calculations (Rule 5)
        if (enableCaching)
        {
            _infrastructureCache = new STBR003A();
        }
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
    public async Task<OptionPricing> PriceOption(STDT003As parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate();

        bool isCall = parameters.OptionType == Option.Type.Call;
        PricingRegime regime = DetermineRegime(parameters.RiskFreeRate, parameters.DividendYield, isCall);

        SafeLog(() =>
        {
            double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);
            string paramsStr = $"r={parameters.RiskFreeRate:F4}, q={parameters.DividendYield:F4}, σ={parameters.ImpliedVolatility:F4}, T={timeToExpiry:F4}";
            LogPricingOption(_logger!, parameters.UnderlyingPrice, parameters.Strike, paramsStr,
                isCall ? "Call" : "Put", regime, null);
        });

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
    public async Task<STPR001APricing> PriceSTPR001A(STPR001AParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate();

        bool isCall = parameters.OptionType == Option.Type.Call;
        PricingRegime regime = DetermineRegime(parameters.RiskFreeRate, parameters.DividendYield, isCall);

        SafeLog(() => LogPricingSTPR001A(_logger!, regime, isCall ? "Call" : "Put", parameters.Strike,
            CalculateDaysToExpiry(parameters.ValuationDate, parameters.FrontExpiry),
            CalculateDaysToExpiry(parameters.ValuationDate, parameters.BackExpiry), null));

        // Price both legs of the spread
        OptionPricing frontPricing = await PriceOption(CreateSTDT003As(parameters, parameters.FrontExpiry)).ConfigureAwait(false);
        OptionPricing backPricing = await PriceOption(CreateSTDT003As(parameters, parameters.BackExpiry)).ConfigureAwait(false);

        // Calculate spread metrics
        double spreadCost = backPricing.Price - frontPricing.Price;
        double maxProfit = await CalculateMaxProfit(parameters, spreadCost).ConfigureAwait(false);
        double breakEven = await CalculateBreakEven(parameters, spreadCost).ConfigureAwait(false);

        return BuildSTPR001APricing(frontPricing, backPricing, spreadCost, maxProfit, breakEven);
    }

    /// <summary>
    /// Creates option parameters from calendar spread parameters for a specific expiry.
    /// </summary>
    private static STDT003As CreateSTDT003As(STPR001AParameters parameters, Date expiry)
    {
        return new STDT003As
        {
            UnderlyingPrice = parameters.UnderlyingPrice,
            Strike = parameters.Strike,
            Expiry = expiry,
            ImpliedVolatility = parameters.ImpliedVolatility,
            RiskFreeRate = parameters.RiskFreeRate,
            DividendYield = parameters.DividendYield,
            OptionType = parameters.OptionType,
            ValuationDate = parameters.ValuationDate
        };
    }

    /// <summary>
    /// Builds STPR001APricing result from option pricings and spread metrics.
    /// </summary>
    private static STPR001APricing BuildSTPR001APricing(
        OptionPricing frontPricing,
        OptionPricing backPricing,
        double spreadCost,
        double maxProfit,
        double breakEven)
    {
        return new STPR001APricing
        {
            FrontOption = frontPricing,
            BackOption = backPricing,
            SpreadCost = spreadCost,
            SpreadDelta = backPricing.Delta - frontPricing.Delta,
            SpreadGamma = backPricing.Gamma - frontPricing.Gamma,
            SpreadVega = backPricing.Vega - frontPricing.Vega,
            SpreadTheta = backPricing.Theta - frontPricing.Theta,
            SpreadRho = backPricing.Rho - frontPricing.Rho,
            MaxProfit = maxProfit,
            MaxLoss = spreadCost,
            BreakEven = breakEven,
            HasPositiveExpectedValue = (backPricing.Vega - frontPricing.Vega) > 0 && (backPricing.Theta - frontPricing.Theta) > 0
        };
    }

    /// <summary>
    /// Calculates implied volatility from market price using bisection method.
    /// </summary>
    public async Task<double> CalculateImpliedVolatility(double marketPrice, STDT003As parameters)
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

            STDT003As testParams = new STDT003As
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

        SafeLog(() => LogImpliedVolatilityConverged(_logger!, iterations, volMid, null));

        return volMid;
    }

    /// <summary>
    /// Prices an option using the Alaris.Quantlib engine (standard American option pricing).
    /// </summary>
    private Task<OptionPricing> PriceWithQuantlib(STDT003As parameters)
    {
        return Task.Run(() =>
        {
            try
            {
                // Set evaluation date
                Settings.instance().setEvaluationDate(parameters.ValuationDate);

                // Calculate time to expiry for adaptive grid sizing
                double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

                // Create QuantLib infrastructure and price the option
                double price = PriceWithQuantLibInfrastructure(parameters, timeToExpiry);

                // Calculate Greeks using finite differences
                double delta = CalculateDelta(parameters);
                double gamma = CalculateGamma(parameters);
                double vega = CalculateVega(parameters);
                double theta = CalculateTheta(parameters);
                double rho = CalculateRho(parameters);

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
            catch (ArgumentException ex)
            {
                SafeLog(() => LogErrorPricingQuantlib(_logger!, ex));
                throw;
            }
            catch (InvalidOperationException ex)
            {
                SafeLog(() => LogErrorPricingQuantlib(_logger!, ex));
                throw;
            }
        });
    }

    /// <summary>
    /// Creates QuantLib infrastructure and prices an option.
    /// Handles object creation and proper disposal in reverse order.
    /// </summary>
    private static double PriceWithQuantLibInfrastructure(STDT003As parameters, double timeToExpiry)
    {
        // Create underlying quote
        SimpleQuote underlyingQuote = new SimpleQuote(parameters.UnderlyingPrice);
        QuoteHandle underlyingHandle = new QuoteHandle(underlyingQuote);

        // Create term structures (day counter, rate, dividend, volatility, calendar)
        (Actual365Fixed dayCounter,
         FlatForward flatRateTs,
         YieldTermStructureHandle riskFreeRateHandle,
         FlatForward flatDividendTs,
         YieldTermStructureHandle dividendYieldHandle,
         BlackConstantVol flatVolTs,
         BlackVolTermStructureHandle volatilityHandle,
         TARGET calendar) termStructures =
            CreateTermStructures(parameters);

        // Create Black-Scholes-Merton process
        BlackScholesMertonProcess bsmProcess = new BlackScholesMertonProcess(
            underlyingHandle,
            termStructures.dividendYieldHandle,
            termStructures.riskFreeRateHandle,
            termStructures.volatilityHandle);

        // Create option
        AmericanExercise exercise = new AmericanExercise(parameters.ValuationDate, parameters.Expiry);
        PlainVanillaPayoff payoff = new PlainVanillaPayoff(parameters.OptionType, parameters.Strike);
        VanillaOption option = new VanillaOption(payoff, exercise);

        // Adaptive grid sizing: short maturities need more time steps
        uint timeSteps = (uint)Math.Max(100, (int)(timeToExpiry * 365 * 2));
        uint priceSteps = 100;

        // Create pricing engine and calculate price
        FdBlackScholesVanillaEngine priceEngine = new FdBlackScholesVanillaEngine(bsmProcess, timeSteps, priceSteps);
        option.setPricingEngine(priceEngine);

        // Ensure evaluation date is set correctly before pricing (critical for QuantLib)
        Settings.instance().setEvaluationDate(parameters.ValuationDate);
        double price = option.NPV();

        // Clean up (dispose in reverse order of creation)
        priceEngine.Dispose();
        option.Dispose();
        payoff.Dispose();
        exercise.Dispose();
        bsmProcess.Dispose();
        termStructures.volatilityHandle.Dispose();
        termStructures.flatVolTs.Dispose();
        termStructures.calendar.Dispose();
        termStructures.dividendYieldHandle.Dispose();
        termStructures.flatDividendTs.Dispose();
        termStructures.riskFreeRateHandle.Dispose();
        termStructures.flatRateTs.Dispose();
        termStructures.dayCounter.Dispose();
        underlyingHandle.Dispose();
        underlyingQuote.Dispose();

        return price;
    }

    /// <summary>
    /// Creates QuantLib term structures for option pricing.
    /// Returns tuple of all created objects for proper disposal.
    /// </summary>
    private static (Actual365Fixed dayCounter,
                    FlatForward flatRateTs,
                    YieldTermStructureHandle riskFreeRateHandle,
                    FlatForward flatDividendTs,
                    YieldTermStructureHandle dividendYieldHandle,
                    BlackConstantVol flatVolTs,
                    BlackVolTermStructureHandle volatilityHandle,
                    TARGET calendar)
        CreateTermStructures(STDT003As parameters)
    {
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

        return (dayCounter, flatRateTs, riskFreeRateHandle, flatDividendTs, dividendYieldHandle, flatVolTs, volatilityHandle, calendar);
    }

    /// <summary>
    /// Prices an option using the Alaris.Double Healy (2021) framework for negative rates.
    /// Uses DBAP002A with QD+ method and early exercise premium calculation.
    /// </summary>
    private Task<OptionPricing> PriceWithDouble(STDT003As parameters)
    {
        return Task.Run(() =>
        {
            try
            {
                // Set evaluation date
                Settings.instance().setEvaluationDate(parameters.ValuationDate);

                double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

                // Use DBAP002A for Healy (2021) framework
                Alaris.Double.DBAP002A approx = new Alaris.Double.DBAP002A(
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
            catch (ArgumentException ex)
            {
                SafeLog(() => LogErrorPricingDouble(_logger!, ex));
                throw;
            }
            catch (InvalidOperationException ex)
            {
                SafeLog(() => LogErrorPricingDouble(_logger!, ex));
                throw;
            }
        });
    }

    // Greek calculations for Alaris.Double (Healy 2021 framework)

    private double CalculateDeltaWithDouble(STDT003As parameters)
    {
        double originalSpot = parameters.UnderlyingPrice;
        double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

        // Up bump
        STDT003As paramsUp = CloneParameters(parameters);
        paramsUp.UnderlyingPrice = originalSpot + BumpSize;
        Alaris.Double.DBAP002A approxUp = new Alaris.Double.DBAP002A(
            paramsUp.UnderlyingPrice, paramsUp.Strike, timeToExpiry,
            paramsUp.RiskFreeRate, paramsUp.DividendYield, paramsUp.ImpliedVolatility,
            paramsUp.OptionType == Option.Type.Call);
        double priceUp = approxUp.ApproximateValue();

        // Down bump
        STDT003As paramsDown = CloneParameters(parameters);
        paramsDown.UnderlyingPrice = originalSpot - BumpSize;
        Alaris.Double.DBAP002A approxDown = new Alaris.Double.DBAP002A(
            paramsDown.UnderlyingPrice, paramsDown.Strike, timeToExpiry,
            paramsDown.RiskFreeRate, paramsDown.DividendYield, paramsDown.ImpliedVolatility,
            paramsDown.OptionType == Option.Type.Call);
        double priceDown = approxDown.ApproximateValue();

        return ((priceUp - priceDown) / (2 * BumpSize));
    }

    private double CalculateGammaWithDouble(STDT003As parameters)
    {
        double originalSpot = parameters.UnderlyingPrice;
        double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

        // Original price
        Alaris.Double.DBAP002A approxOriginal = new Alaris.Double.DBAP002A(
            originalSpot, parameters.Strike, timeToExpiry,
            parameters.RiskFreeRate, parameters.DividendYield, parameters.ImpliedVolatility,
            parameters.OptionType == Option.Type.Call);
        double priceOriginal = approxOriginal.ApproximateValue();

        // Up bump
        STDT003As paramsUp = CloneParameters(parameters);
        paramsUp.UnderlyingPrice = originalSpot + BumpSize;
        Alaris.Double.DBAP002A approxUp = new Alaris.Double.DBAP002A(
            paramsUp.UnderlyingPrice, paramsUp.Strike, timeToExpiry,
            paramsUp.RiskFreeRate, paramsUp.DividendYield, paramsUp.ImpliedVolatility,
            paramsUp.OptionType == Option.Type.Call);
        double priceUp = approxUp.ApproximateValue();

        // Down bump
        STDT003As paramsDown = CloneParameters(parameters);
        paramsDown.UnderlyingPrice = originalSpot - BumpSize;
        Alaris.Double.DBAP002A approxDown = new Alaris.Double.DBAP002A(
            paramsDown.UnderlyingPrice, paramsDown.Strike, timeToExpiry,
            paramsDown.RiskFreeRate, paramsDown.DividendYield, paramsDown.ImpliedVolatility,
            paramsDown.OptionType == Option.Type.Call);
        double priceDown = approxDown.ApproximateValue();

        return ((priceUp - (2 * priceOriginal) + priceDown) / (BumpSize * BumpSize));
    }

    private double CalculateVegaWithDouble(STDT003As parameters)
    {
        double originalVol = parameters.ImpliedVolatility;
        double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

        // Up bump
        STDT003As paramsUp = CloneParameters(parameters);
        paramsUp.ImpliedVolatility = originalVol + VolBumpSize;
        Alaris.Double.DBAP002A approxUp = new Alaris.Double.DBAP002A(
            paramsUp.UnderlyingPrice, paramsUp.Strike, timeToExpiry,
            paramsUp.RiskFreeRate, paramsUp.DividendYield, paramsUp.ImpliedVolatility,
            paramsUp.OptionType == Option.Type.Call);
        double priceUp = approxUp.ApproximateValue();

        // Down bump
        STDT003As paramsDown = CloneParameters(parameters);
        paramsDown.ImpliedVolatility = originalVol - VolBumpSize;
        Alaris.Double.DBAP002A approxDown = new Alaris.Double.DBAP002A(
            paramsDown.UnderlyingPrice, paramsDown.Strike, timeToExpiry,
            paramsDown.RiskFreeRate, paramsDown.DividendYield, paramsDown.ImpliedVolatility,
            paramsDown.OptionType == Option.Type.Call);
        double priceDown = approxDown.ApproximateValue();

        return ((priceUp - priceDown) / (2 * VolBumpSize));
    }

    private double CalculateThetaWithDouble(STDT003As parameters)
    {
        Date originalDate = parameters.ValuationDate;
        double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);

        // Original price
        Alaris.Double.DBAP002A approxOriginal = new Alaris.Double.DBAP002A(
            parameters.UnderlyingPrice, parameters.Strike, timeToExpiry,
            parameters.RiskFreeRate, parameters.DividendYield, parameters.ImpliedVolatility,
            parameters.OptionType == Option.Type.Call);
        double priceOriginal = approxOriginal.ApproximateValue();

        // Forward date by 1 day
        int dayBump = 1;
        Period period = new Period(dayBump, TimeUnit.Days);
        Date forwardDate = originalDate.Add(period);
        double timeToExpiryForward = CalculateTimeToExpiry(forwardDate, parameters.Expiry);

        Alaris.Double.DBAP002A approxForward = new Alaris.Double.DBAP002A(
            parameters.UnderlyingPrice, parameters.Strike, timeToExpiryForward,
            parameters.RiskFreeRate, parameters.DividendYield, parameters.ImpliedVolatility,
            parameters.OptionType == Option.Type.Call);
        double priceForward = approxForward.ApproximateValue();

        // Clean up
        period.Dispose();

        // Theta is price change per day (negative for time decay)
        return ((priceForward - priceOriginal) / dayBump);
    }

    private double CalculateRhoWithDouble(STDT003As parameters)
    {
        double originalRate = parameters.RiskFreeRate;
        double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);
        double rateBump = 0.01; // 1% rate bump

        // Up bump
        STDT003As paramsUp = CloneParameters(parameters);
        paramsUp.RiskFreeRate = originalRate + rateBump;
        Alaris.Double.DBAP002A approxUp = new Alaris.Double.DBAP002A(
            paramsUp.UnderlyingPrice, paramsUp.Strike, timeToExpiry,
            paramsUp.RiskFreeRate, paramsUp.DividendYield, paramsUp.ImpliedVolatility,
            paramsUp.OptionType == Option.Type.Call);
        double priceUp = approxUp.ApproximateValue();

        // Down bump
        STDT003As paramsDown = CloneParameters(parameters);
        paramsDown.RiskFreeRate = originalRate - rateBump;
        Alaris.Double.DBAP002A approxDown = new Alaris.Double.DBAP002A(
            paramsDown.UnderlyingPrice, paramsDown.Strike, timeToExpiry,
            paramsDown.RiskFreeRate, paramsDown.DividendYield, paramsDown.ImpliedVolatility,
            paramsDown.OptionType == Option.Type.Call);
        double priceDown = approxDown.ApproximateValue();

        return ((priceUp - priceDown) / (2 * rateBump));
    }

    private STDT003As CloneParameters(STDT003As original)
    {
        return new STDT003As
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
    private double PriceOptionSync(STDT003As parameters)
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

        // Create pricing engine and price (adaptive grid for short maturities)
        double timeToExpiry = CalculateTimeToExpiry(parameters.ValuationDate, parameters.Expiry);
        uint timeSteps = (uint)Math.Max(100, (int)(timeToExpiry * 365 * 2)); // At least 2 steps per day
        uint priceSteps = 100;

        FdBlackScholesVanillaEngine engine = new FdBlackScholesVanillaEngine(bsmProcess, timeSteps, priceSteps);
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

    private double CalculateDelta(STDT003As parameters)
    {
        double originalSpot = parameters.UnderlyingPrice;

        // Price with up bump
        STDT003As paramsUp = CloneParameters(parameters);
        paramsUp.UnderlyingPrice = originalSpot + BumpSize;
        double priceUp = PriceOptionSync(paramsUp);

        // Price with down bump
        STDT003As paramsDown = CloneParameters(parameters);
        paramsDown.UnderlyingPrice = originalSpot - BumpSize;
        double priceDown = PriceOptionSync(paramsDown);

        return ((priceUp - priceDown) / (2 * BumpSize));
    }

    private double CalculateGamma(STDT003As parameters)
    {
        double originalSpot = parameters.UnderlyingPrice;

        // Original price
        double priceOriginal = PriceOptionSync(parameters);

        // Price with up bump
        STDT003As paramsUp = CloneParameters(parameters);
        paramsUp.UnderlyingPrice = originalSpot + BumpSize;
        double priceUp = PriceOptionSync(paramsUp);

        // Price with down bump
        STDT003As paramsDown = CloneParameters(parameters);
        paramsDown.UnderlyingPrice = originalSpot - BumpSize;
        double priceDown = PriceOptionSync(paramsDown);

        return ((priceUp - (2 * priceOriginal) + priceDown) / (BumpSize * BumpSize));
    }

    private double CalculateVega(STDT003As parameters)
    {
        double originalVol = parameters.ImpliedVolatility;

        // Price with up bump
        STDT003As paramsUp = CloneParameters(parameters);
        paramsUp.ImpliedVolatility = originalVol + VolBumpSize;
        double priceUp = PriceOptionSync(paramsUp);

        // Price with down bump
        STDT003As paramsDown = CloneParameters(parameters);
        paramsDown.ImpliedVolatility = originalVol - VolBumpSize;
        double priceDown = PriceOptionSync(paramsDown);

        return ((priceUp - priceDown) / (2 * VolBumpSize));
    }

    private double CalculateTheta(STDT003As parameters)
    {
        Date originalDate = parameters.ValuationDate;

        // Original price
        double priceOriginal = PriceOptionSync(parameters);

        // Price with forward date (1 day)
        int dayBump = 1;
        Period period = new Period(dayBump, TimeUnit.Days);
        Date forwardDate = originalDate.Add(period);
        STDT003As paramsForward = CloneParameters(parameters);
        paramsForward.ValuationDate = forwardDate;
        double priceForward = PriceOptionSync(paramsForward);

        // Clean up
        period.Dispose();

        // Theta is price change per day (negative for time decay)
        return ((priceForward - priceOriginal) / dayBump);
    }

    private double CalculateRho(STDT003As parameters)
    {
        double originalRate = parameters.RiskFreeRate;
        double rateBump = 0.01; // 1% rate bump

        // Price with up bump
        STDT003As paramsUp = CloneParameters(parameters);
        paramsUp.RiskFreeRate = originalRate + rateBump;
        double priceUp = PriceOptionSync(paramsUp);

        // Price with down bump
        STDT003As paramsDown = CloneParameters(parameters);
        paramsDown.RiskFreeRate = originalRate - rateBump;
        double priceDown = PriceOptionSync(paramsDown);

        return ((priceUp - priceDown) / (2 * rateBump));
    }

    private double CalculateVegaQuantlib(VanillaOption option, BlackConstantVol volTs,
        BlackScholesMertonProcess process, STDT003As parameters, FdBlackScholesVanillaEngine originalEngine)
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
        BlackScholesMertonProcess process, STDT003As parameters)
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
        BlackScholesMertonProcess process, STDT003As parameters, FdBlackScholesVanillaEngine originalEngine)
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
    private async Task<double> CalculateMaxProfit(STPR001AParameters parameters, double spreadCost)
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
            STDT003As backParams = new STDT003As
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
    private async Task<double> CalculateBreakEven(STPR001AParameters parameters, double spreadCost)
    {
        double strike = parameters.Strike;
        double tolerance = 0.01; // $0.01 tolerance
        int maxIterations = 50;

        // Calendar spreads typically have breakeven near the strike
        double lowerBound = (strike * 0.7);
        double upperBound = (strike * 1.3);

        for (int iter = 0; iter < maxIterations; iter++)
        {
            double midPoint = ((lowerBound + upperBound) / 2.0);

            // Calculate profit/loss at this price point
            double profitLoss = await CalculateSpreadProfitLoss(parameters, midPoint, spreadCost).ConfigureAwait(false);

            if (Math.Abs(profitLoss) < tolerance)
            {
                return midPoint; // Found breakeven
            }

            // Adjust search bounds using bisection logic
            (lowerBound, upperBound) = AdjustBisectionBounds(profitLoss, midPoint, strike, lowerBound, upperBound);
        }

        // Return strike as default if no convergence
        return strike;
    }

    /// <summary>
    /// Calculates the profit/loss of a calendar spread at a given underlying price.
    /// </summary>
    private async Task<double> CalculateSpreadProfitLoss(STPR001AParameters parameters, double underlyingPrice, double spreadCost)
    {
        // Front option intrinsic value at expiration
        double frontValue = CalculateIntrinsicValue(underlyingPrice, parameters.Strike, parameters.OptionType);

        // Back option still has time value
        STDT003As backParams = CreateSTDT003As(parameters, parameters.BackExpiry);
        backParams.UnderlyingPrice = underlyingPrice;
        backParams.ValuationDate = parameters.FrontExpiry;

        OptionPricing backPricing = await PriceOption(backParams).ConfigureAwait(false);

        // P&L = BackValue - FrontValue - InitialCost
        return (backPricing.Price - frontValue - spreadCost);
    }

    /// <summary>
    /// Adjusts bisection search bounds based on profit/loss and strike relationship.
    /// </summary>
    private static (double lowerBound, double upperBound) AdjustBisectionBounds(
        double profitLoss, double midPoint, double strike, double lowerBound, double upperBound)
    {
        if (profitLoss > 0)
        {
            // Spread is profitable, breakeven is further from strike
            return midPoint > strike ? (midPoint, upperBound) : (lowerBound, midPoint);
        }
        else
        {
            // Spread is unprofitable, breakeven is closer to strike
            return midPoint > strike ? (lowerBound, midPoint) : (midPoint, upperBound);
        }
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

    /// <summary>
    /// Safely executes logging operation with fault isolation (Rule 15).
    /// Prevents logging failures from crashing critical paths.
    /// </summary>
    private void SafeLog(Action logAction)
    {
        if (_logger == null)
        {
            return;
        }

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            logAction();
        }
        catch (Exception)
        {
            // Swallow logging exceptions to prevent them from crashing the application
            // This is acceptable per Rule 10 for non-critical subsystems (Rule 15: Fault Isolation)
        }
#pragma warning restore CA1031
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        
        // Dispose infrastructure cache (Rule 16)
        _infrastructureCache?.Dispose();

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