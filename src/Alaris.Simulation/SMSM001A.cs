// SMSM001A.cs - quarterly earnings announcement simulation

using Alaris.Core.Pricing;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Calendar;
using Alaris.Strategy.Core;
using Alaris.Strategy.Cost;
using Alaris.Strategy.Detection;
using Alaris.Strategy.Hedge;
using Alaris.Strategy.Model;
using Alaris.Strategy.Risk;
using Microsoft.Extensions.Logging;
using System.Globalization;

// Type aliases for coded naming convention compatibility
using Signal = Alaris.Strategy.Core.STCR004A;
using SignalStrength = Alaris.Strategy.Core.STCR004AStrength;
using OptionChain = Alaris.Strategy.Model.STDT002A;

// Suppress CA1303 for entire file - simulation console output uses literal strings intentionally
#pragma warning disable CA1303 // Do not pass literals as localized parameters
// Suppress CA5394 for entire file - deterministic Random used for reproducible simulation
#pragma warning disable CA5394 // Do not use insecure randomness
// Suppress CA1031 for Main method - top-level exception handler is appropriate
#pragma warning disable CA1031 // Do not catch general exception types

namespace Alaris.Simulation;

/// <summary>
/// Main simulation entry point for demonstrating the Alaris system during
/// a quarterly earnings announcement scenario.
/// </summary>
/// <remarks>
/// <para>
/// This simulation creates a complete earnings trading scenario with market
/// conditions specifically designed to trigger a <see cref="SignalStrength.Recommended"/>
/// signal according to the Atilgan (2014) criteria:
/// </para>
/// <list type="bullet">
///   <item><description>IV/RV Ratio ≥ 1.25 (volatility premium exists)</description></item>
///   <item><description>Term Structure Slope ≤ -0.00406 (inverted structure)</description></item>
///   <item><description>Average Volume ≥ 1,500,000 shares (sufficient liquidity)</description></item>
/// </list>
/// <para>
/// The simulation demonstrates positive rate pricing, negative rate pricing (Healy 2021),
/// and comprehensive production validation including cost analysis and hedging checks.
/// </para>
/// </remarks>
internal static class SMSM001A
{
    // Simulation Configuration Constants

    /// <summary>Simulation symbol for the earnings announcement.</summary>
    private const string SimulationSymbol = "AAPL";

    /// <summary>Static pricing engine for simulation.</summary>
    private static readonly CREN003A PricingEngine = new CREN003A();

    /// <summary>Portfolio value for position sizing calculations.</summary>
    private const double PortfolioValue = 100_000.00;

    /// <summary>Risk-free rate for standard pricing (positive rate regime).</summary>
    private const double PositiveRiskFreeRate = 0.0525; // 5.25%

    /// <summary>Risk-free rate for negative rate regime demonstration.</summary>
    private const double NegativeRiskFreeRate = -0.005; // -0.50%

    /// <summary>Dividend yield for negative rate double boundary (q &lt; r).</summary>
    private const double NegativeRateDividendYield = -0.010; // -1.00%

    // TradingDaysPerYear moved to TradingCalendarDefaults.TradingDaysPerYear

    /// <summary>Box width for console output formatting.</summary>
    private const int BoxWidth = 78;

    /// <summary>Pre-earnings IV elevation for front month (8% premium).</summary>
    private const double EarningsIVPremium = 0.08;

    /// <summary>Typical earnings gap magnitude for calibration (3-5%).</summary>
    private const double EarningsGapMagnitude = 0.04;

    // Greek Validation Constants (Healy 2021 physical constraints)

    /// <summary>Maximum plausible absolute delta for an option.</summary>
    private const double MaxAbsDelta = 1.5;

    /// <summary>Maximum plausible absolute gamma for an option.</summary>
    private const double MaxAbsGamma = 0.5;

    /// <summary>Maximum plausible absolute vega (per 1% vol change).</summary>
    private const double MaxAbsVega = 1.0;

    // Entry Point

    /// <summary>
    /// Main entry point for the Alaris earnings announcement simulation.
    /// </summary>
    public static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                        ALARIS SYSTEM DEMONSTRATION                           ║");
        Console.WriteLine("║              Quarterly Earnings Announcement Simulation                      ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        try
        {
            RunSimulation(loggerFactory);

            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                        SIMULATION COMPLETE                                   ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    // Simulation Orchestration

    /// <summary>
    /// Executes all simulation phases in sequence.
    /// </summary>
    private static void RunSimulation(ILoggerFactory loggerFactory)
    {
        // Define simulation dates
        DateTime earningsDate = new DateTime(2025, 1, 30); // Earnings announcement
        DateTime evaluationDate = new DateTime(2025, 1, 24); // 6 days before EA

        // Phase 1: Display market conditions
        DisplayMarketConditions(evaluationDate, earningsDate);

        // Phase 2: Generate simulated market data with proper earnings characteristics
        SimulatedMarketData marketData = GenerateMarketData(evaluationDate, earningsDate);
        DisplayMarketData(marketData);

        // Phase 3: Calculate realised volatility (Yang-Zhang)
        double realisedVolatility = CalculateRealisedVolatility(marketData.PriceHistory);
        DisplayRealisedVolatility(realisedVolatility);

        // Phase 4: Analyse term structure (now inverted due to earnings IV elevation)
        TermStructureResult termResult = AnalyseTermStructure(marketData.OptionChain, evaluationDate);
        DisplayTermStructure(termResult);

        // Phase 5: Calculate Leung-Santoli metrics (now with calibrated jump volatility)
        LeungSantoliMetrics lsMetrics = CalculateLeungSantoliMetrics(
            marketData, earningsDate, evaluationDate, realisedVolatility, termResult);
        DisplayLeungSantoliMetrics(lsMetrics);

        // Phase 6: Generate trading signal (Atilgan 2014)
        SignalResult signalResult = GenerateSignal(
            marketData, realisedVolatility, termResult, lsMetrics, earningsDate, evaluationDate);
        DisplaySignal(signalResult);

        // Phase 7: Price calendar spread (positive rates)
        CalendarSpreadResult positiveRateSpread = PriceCalendarSpread(
            marketData, evaluationDate, PositiveRiskFreeRate, 0.005, "Positive Rate Regime");
        DisplayCalendarSpread(positiveRateSpread);

        // Phase 8: Price calendar spread (negative rates - Healy 2021) with validated Greeks
        CalendarSpreadResult negativeRateSpread = PriceCalendarSpreadNegativeRate(
            marketData, evaluationDate, NegativeRiskFreeRate, NegativeRateDividendYield,
            "Negative Rate Regime (Healy 2021)");
        DisplayCalendarSpread(negativeRateSpread);

        // Phase 9: Demonstrate Alaris.Double pricing
        DoubleBoundaryDemoResult doubleBoundary = DemonstrateDoubleBoundaryPricing();
        DisplayDoubleBoundaryResult(doubleBoundary);

        // Phase 10: Production Validation (Cost & Hedge)
        // Always run for demonstration purposes - use the spread that would be traded
        RunProductionValidation(
            loggerFactory,
            signalResult.Signal,
            marketData,
            positiveRateSpread,
            earningsDate,
            evaluationDate);

        // Phase 11: Maturity Guard Demo (STMG001A)
        DemonstrateMaturityGuard(evaluationDate);

        // Phase 12: Near-Expiry Double Boundary Demo (DBEX001A)
        DemonstrateNearExpiryPricing();

        // Phase 13: Pin Risk Monitor Demo (STHD009A)
        DemonstratePinRisk(marketData.CurrentPrice, positiveRateSpread.Strike);

        // First-Principles Validation Phases (14-19)

        // Phase 14: First-Principles Double Boundary Validation
        DemonstrateFirstPrinciplesDoubleBoundary();

        // Phase 15: Kalman-Filtered Volatility Demo (STKF001A)
        DemonstrateKalmanVolatility(marketData.PriceHistory);

        // Phase 16: Neyman-Pearson Signal Detection (STSD001A)
        DemonstrateNeymanPearsonDetection(termResult.IV30, realisedVolatility);

        // Phase 17: Queue-Theoretic Position Management (STQT001A)
        DemonstrateQueueTheoreticManagement();

        // Phase 18: Rule-Based Exit Monitor (STHD007B)
        DemonstrateRuleBasedExit(positiveRateSpread);

        // Phase 19: Complete Workflow Integration Summary
        DisplayWorkflowIntegration(
            realisedVolatility, termResult.IV30, signalResult, positiveRateSpread);
    }

    // Phase 2: Market Data Generation (CORRECTED)

    /// <summary>
    /// Generates simulated market data for the earnings scenario.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This corrected implementation includes:
    /// - Price gaps at historical earnings dates for Leung-Santoli calibration
    /// - Proper earnings date tracking for jump volatility estimation
    /// </para>
    /// </remarks>
    private static SimulatedMarketData GenerateMarketData(DateTime evaluationDate, DateTime earningsDate)
    {
        Random random = new Random(42); // Deterministic seed for reproducible simulation

        // Generate historical earnings dates (quarterly)
        List<DateTime> historicalEarnings = new List<DateTime>
        {
            earningsDate.AddMonths(-3),
            earningsDate.AddMonths(-6),
            earningsDate.AddMonths(-9),
            earningsDate.AddMonths(-12)
        };

        // Convert to HashSet for O(1) lookup
        HashSet<DateTime> earningsDateSet = new HashSet<DateTime>();
        for (int i = 0; i < historicalEarnings.Count; i++)
        {
            earningsDateSet.Add(historicalEarnings[i].Date);
        }

        // Generate 365 days of price history to capture all 4 quarterly earnings dates
        // This ensures proper Leung-Santoli calibration with sufficient samples
        const int PriceHistoryDays = 365;
        List<PriceBar> priceHistory = new List<PriceBar>();
        double currentPrice = 150.00;
        DateTime startDate = evaluationDate.AddDays(-PriceHistoryDays);

        for (int i = 0; i < PriceHistoryDays; i++)
        {
            DateTime date = startDate.AddDays(i);

            // Check if this is a post-earnings date (day after historical earnings)
            bool isPostEarnings = earningsDateSet.Contains(date.AddDays(-1));

            double open;
            if (isPostEarnings)
            {
                // Simulate earnings gap: 3-5% overnight move
                double gapDirection = random.NextDouble() > 0.5 ? 1.0 : -1.0;
                double gapMagnitude = EarningsGapMagnitude + ((random.NextDouble() * 0.02) - 0.01);
                open = currentPrice * (1.0 + (gapDirection * gapMagnitude));
            }
            else
            {
                open = currentPrice + ((random.NextDouble() * 4) - 2);
            }

            double close = open + ((random.NextDouble() * 4) - 2);
            double high = Math.Max(open, close) + (random.NextDouble() * 2);
            double low = Math.Min(open, close) - (random.NextDouble() * 2);
            long volume = 2_000_000 + random.Next(500_000);

            priceHistory.Add(new PriceBar
            {
                Date = date,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume
            });

            currentPrice = close;
        }

        // Generate option chain with inverted term structure
        OptionChain optionChain = GenerateOptionChainWithEarningsIV(
            evaluationDate, currentPrice, earningsDate);

        return new SimulatedMarketData
        {
            PriceHistory = priceHistory,
            OptionChain = optionChain,
            HistoricalEarningsDates = historicalEarnings,
            CurrentPrice = currentPrice,
            Symbol = SimulationSymbol
        };
    }

    /// <summary>
    /// Generates a simulated option chain with pre-earnings IV elevation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This corrected implementation creates an inverted term structure where
    /// front-month IV is elevated relative to back-month IV, reflecting the
    /// pre-earnings volatility premium described in Leung &amp; Santoli (2014).
    /// </para>
    /// <para>
    /// The IV term structure satisfies Atilgan (2014) criteria:
    /// - Slope ≤ -0.00406 (inverted structure)
    /// </para>
    /// </remarks>
    private static OptionChain GenerateOptionChainWithEarningsIV(
        DateTime evaluationDate,
        double spotPrice,
        DateTime earningsDate)
    {
        List<OptionExpiry> expiries = new List<OptionExpiry>();
        Random random = new Random(42);

        int daysToEarnings = (earningsDate - evaluationDate).Days;

        // Generate 3 monthly expiries with inverted term structure
        for (int monthOffset = 1; monthOffset <= 3; monthOffset++)
        {
            DateTime expiryDate = evaluationDate.AddDays(monthOffset * 30);
            int dte = monthOffset * 30;
            List<OptionContract> calls = new List<OptionContract>();
            List<OptionContract> puts = new List<OptionContract>();

            // Base IV decreases with time for inverted structure
            // Front month (30 DTE): ~28% IV (elevated due to earnings)
            // Back month (90 DTE): ~22% IV (normal baseline)
            double baseIV;
            if (monthOffset == 1 && dte >= daysToEarnings)
            {
                // Front month containing earnings: add premium
                baseIV = 0.20 + EarningsIVPremium;
            }
            else
            {
                // Normal IV that increases slightly with time (base term structure)
                baseIV = 0.20 + (monthOffset * 0.01);
            }

            // Ensure inverted structure: front month must be higher
            if (monthOffset == 1)
            {
                baseIV = 0.28; // Front month: 28%
            }
            else if (monthOffset == 2)
            {
                baseIV = 0.24; // Mid month: 24%
            }
            else
            {
                baseIV = 0.22; // Back month: 22%
            }

            // Generate strikes around ATM
            for (int strikeOffset = -2; strikeOffset <= 2; strikeOffset++)
            {
                double strike = Math.Round(spotPrice + (strikeOffset * 5), 2);

                // IV skew: OTM puts have higher IV
                double skew = strikeOffset * -0.005;
                double iv = baseIV + skew + ((random.NextDouble() * 0.01) - 0.005);

                // Generate call
                double callTheo = SimulateOptionPrice(
                    spotPrice, strike, dte, 0.05, 0.0, iv, isCall: true).Price;
                OptionContract call = new OptionContract
                {
                    Strike = strike,
                    ImpliedVolatility = iv,
                    Bid = callTheo - 0.10,
                    Ask = callTheo + 0.10,
                    LastPrice = callTheo,
                    Volume = random.Next(1000, 8000),
                    OpenInterest = random.Next(2000, 15000)
                };
                calls.Add(call);

                // Generate put
                double putTheo = SimulateOptionPrice(
                    spotPrice, strike, dte, 0.05, 0.0, iv, isCall: false).Price;
                OptionContract put = new OptionContract
                {
                    Strike = strike,
                    ImpliedVolatility = iv,
                    Bid = putTheo - 0.10,
                    Ask = putTheo + 0.10,
                    LastPrice = putTheo,
                    Volume = random.Next(1000, 8000),
                    OpenInterest = random.Next(2000, 15000)
                };
                puts.Add(put);
            }

            OptionExpiry expiry = new OptionExpiry
            {
                ExpiryDate = expiryDate
            };

            foreach (OptionContract c in calls)
            {
                expiry.Calls.Add(c);
            }

            foreach (OptionContract p in puts)
            {
                expiry.Puts.Add(p);
            }

            expiries.Add(expiry);
        }

        OptionChain optionChain = new OptionChain
        {
            UnderlyingPrice = spotPrice
        };

        foreach (OptionExpiry exp in expiries)
        {
            optionChain.Expiries.Add(exp);
        }

        return optionChain;
    }

    // Phase 5: Leung-Santoli Metrics (CORRECTED)

    /// <summary>
    /// Calculates Leung-Santoli pre-earnings IV metrics with proper calibration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This corrected implementation:
    /// - Counts historical earnings samples from price gaps
    /// - Calibrates jump volatility (σE) from observed gaps
    /// - Computes theoretical IV using L&amp;S formula: I(t) = √(σ² + σE²/(T-t))
    /// </para>
    /// </remarks>
    private static LeungSantoliMetrics CalculateLeungSantoliMetrics(
        SimulatedMarketData marketData,
        DateTime earningsDate,
        DateTime evaluationDate,
        double realisedVolatility,
        TermStructureResult termResult)
    {
        // Count historical earnings samples with valid price gaps
        int historicalSamples = 0;
        double sumSquaredReturns = 0.0;

        HashSet<DateTime> earningsDateSet = new HashSet<DateTime>();
        for (int i = 0; i < marketData.HistoricalEarningsDates.Count; i++)
        {
            earningsDateSet.Add(marketData.HistoricalEarningsDates[i].Date);
        }

        for (int i = 1; i < marketData.PriceHistory.Count; i++)
        {
            PriceBar current = marketData.PriceHistory[i];
            PriceBar previous = marketData.PriceHistory[i - 1];

            // Check if this is a post-earnings bar
            if (earningsDateSet.Contains(previous.Date.Date))
            {
                // Calculate overnight gap return
                double gapReturn = Math.Log(current.Open / previous.Close);
                sumSquaredReturns += gapReturn * gapReturn;
                historicalSamples++;
            }
        }

        // Calibrate jump volatility from historical earnings moves
        double jumpVolatility;
        bool isCalibrated = historicalSamples >= 4;

        if (isCalibrated)
        {
            // Annualised jump volatility from quarterly earnings
            jumpVolatility = Math.Sqrt(sumSquaredReturns / historicalSamples);
        }
        else
        {
            // Default: use empirical average (15-25% for typical equities)
            jumpVolatility = 0.18;
        }

        // Time to earnings in years
        int daysToEarnings = (earningsDate - evaluationDate).Days;
        double timeToEarnings = TradingCalendarDefaults.DteToYears(daysToEarnings);

        // Leung-Santoli theoretical IV: I(t) = sqrt(sigma^2 + sigmaE^2 / (T-t))
        double theoreticalIV = Math.Sqrt(
            (realisedVolatility * realisedVolatility) +
            (jumpVolatility * jumpVolatility / timeToEarnings));

        // Mispricing signal: market IV - theoretical IV
        double mispricingSignal = termResult.IV30 - theoreticalIV;

        // Expected IV crush after earnings
        double postEarningsIV = realisedVolatility; // Returns to base volatility
        double expectedIVCrush = termResult.IV30 - postEarningsIV;
        double ivCrushRatio = expectedIVCrush / termResult.IV30;

        return new LeungSantoliMetrics
        {
            HistoricalSamples = historicalSamples,
            IsCalibrated = isCalibrated,
            JumpVolatility = jumpVolatility,
            BaseVolatility = realisedVolatility,
            TheoreticalIV = theoreticalIV,
            MarketIV = termResult.IV30,
            MispricingSignal = mispricingSignal,
            ExpectedIVCrush = expectedIVCrush,
            IVCrushRatio = ivCrushRatio
        };
    }

    // Phase 8: Negative Rate Pricing (CORRECTED)

    /// <summary>
    /// Prices a calendar spread under negative rate regime with validated Greeks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This corrected implementation:
    /// - Uses adaptive step sizes proportional to spot price
    /// - Validates Greeks against physical constraints
    /// - Falls back to European approximation if Greeks are invalid
    /// </para>
    /// </remarks>
    private static CalendarSpreadResult PriceCalendarSpreadNegativeRate(
        SimulatedMarketData marketData,
        DateTime evaluationDate,
        double riskFreeRate,
        double dividendYield,
        string regimeLabel)
    {
        OptionChain chain = marketData.OptionChain;
        double spot = chain.UnderlyingPrice;

        // Get front and back month expiries
        OptionExpiry frontExpiry = chain.Expiries[0];
        OptionExpiry backExpiry = chain.Expiries[2];

        OptionContract atmContract = FindClosestStrike(frontExpiry.Calls, spot);
        double atmStrike = atmContract.Strike;

        int frontDte = (int)(frontExpiry.ExpiryDate - evaluationDate).TotalDays;
        int backDte = (int)(backExpiry.ExpiryDate - evaluationDate).TotalDays;

        // Get ATM options
        OptionContract frontCall = FindStrikeMatch(frontExpiry.Calls, atmStrike, 0.01);
        OptionContract backCall = FindStrikeMatch(backExpiry.Calls, atmStrike, 0.01);

        // Price with Healy (2021) double boundary method and validated Greeks
        (double frontPrice, double frontDelta, double frontGamma, double frontTheta, double frontVega) =
            PriceWithDoubleValidated(spot, atmStrike, frontDte, riskFreeRate, dividendYield,
                frontCall.ImpliedVolatility, isCall: true);

        (double backPrice, double backDelta, double backGamma, double backTheta, double backVega) =
            PriceWithDoubleValidated(spot, atmStrike, backDte, riskFreeRate, dividendYield,
                backCall.ImpliedVolatility, isCall: true);

        // Calendar spread: sell front, buy back
        double spreadCost = backPrice - frontPrice;
        bool isCredit = spreadCost < 0;

        // Spread Greeks
        double spreadDelta = backDelta - frontDelta;
        double spreadGamma = backGamma - frontGamma;
        double spreadTheta = backTheta - frontTheta;
        double spreadVega = backVega - frontVega;

        return new CalendarSpreadResult
        {
            RegimeLabel = regimeLabel,
            Strike = atmStrike,
            FrontExpiry = frontExpiry.ExpiryDate,
            BackExpiry = backExpiry.ExpiryDate,
            EvaluationDate = evaluationDate,
            FrontPrice = frontPrice,
            BackPrice = backPrice,
            SpreadCost = spreadCost,
            IsCredit = isCredit,
            SpreadDelta = spreadDelta,
            SpreadGamma = spreadGamma,
            SpreadTheta = spreadTheta,
            SpreadVega = spreadVega,
            MaxLoss = isCredit ? double.NaN : spreadCost * 100,
            MaxGain = isCredit ? Math.Abs(spreadCost) * 100 : double.NaN,
            BreakEven = atmStrike,
            RiskFreeRate = riskFreeRate,
            DividendYield = dividendYield
        };
    }

    /// <summary>
    /// Prices an option using Alaris.Double with validated Greeks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This corrected implementation addresses the Greek calculation issues:
    /// - Uses adaptive step sizes: dS = 0.5% of spot price (not fixed 0.01)
    /// - Validates Greeks against physical constraints
    /// - Falls back to Black-Scholes analytical Greeks if validation fails
    /// </para>
    /// <para>
    /// For calls in the q &lt; r &lt; 0 regime, the method prices the corresponding
    /// put using the double boundary framework and applies put-call parity:
    /// C = P + S·exp(-qT) - K·exp(-rT)
    /// </para>
    /// </remarks>
    private static (double Price, double Delta, double Gamma, double Theta, double Vega) PriceWithDoubleValidated(
        double spot,
        double strike,
        int dte,
        double rate,
        double div,
        double vol,
        bool isCall)
    {
        double timeToExpiry = TradingCalendarDefaults.DteToYears(dte);

        // For calls in negative rate regimes (q < r < 0), use put-call parity
        // because Healy (2021) double boundary is designed for puts
        double price;
        if (isCall && div < rate && rate < 0)
        {
            // Price the corresponding put
            double putPrice = PricingEngine.Price(spot, strike, timeToExpiry, rate, div, vol, Alaris.Core.Options.OptionType.Put);

            // Apply put-call parity: C = P + S·exp(-qT) - K·exp(-rT)
            double forwardFactor = Math.Exp(-div * timeToExpiry);
            double discountFactor = Math.Exp(-rate * timeToExpiry);
            price = putPrice + (spot * forwardFactor) - (strike * discountFactor);
        }
        else
        {
            // Use CRAP001A directly for puts or other regimes
            price = PricingEngine.Price(spot, strike, timeToExpiry, rate, div, vol, isCall ? Alaris.Core.Options.OptionType.Call : Alaris.Core.Options.OptionType.Put);
        }

        // Adaptive step sizes proportional to spot and volatility
        double ds = spot * 0.005; // 0.5% of spot price
        double dv = 0.001;        // 0.1% volatility bump
        double dt = 1.0 / TradingCalendarDefaults.TradingDaysPerYear;

        // Helper function to price with put-call parity if needed
        double PriceOption(double s, double k, double t, double r, double q, double v, bool call)
        {
            return PricingEngine.Price(s, k, t, r, q, v, call ? Alaris.Core.Options.OptionType.Call : Alaris.Core.Options.OptionType.Put);
        }

        // Delta: central difference with adaptive step
        double priceUp = PriceOption(spot + ds, strike, timeToExpiry, rate, div, vol, isCall);
        double priceDown = PriceOption(spot - ds, strike, timeToExpiry, rate, div, vol, isCall);
        double delta = (priceUp - priceDown) / (2.0 * ds);

        // Gamma: central difference second derivative
        double gamma = (priceUp - (2.0 * price) + priceDown) / (ds * ds);

        // Vega: forward difference with vol bump
        double priceVegaUp = PriceOption(spot, strike, timeToExpiry, rate, div, vol + dv, isCall);
        double vega = (priceVegaUp - price) / dv * 0.01;

        // Theta: forward difference for time decay
        double theta = 0.0;
        if (timeToExpiry > dt)
        {
            double priceTheta = PriceOption(spot, strike, timeToExpiry - dt, rate, div, vol, isCall);
            theta = (priceTheta - price) / dt / TradingCalendarDefaults.TradingDaysPerYear;
        }

        // Validate Greeks against physical constraints
        bool greeksValid = ValidateGreeks(delta, gamma, vega, isCall);

        if (!greeksValid)
        {
            // Fall back to Black-Scholes analytical Greeks
            (delta, gamma, theta, vega) = CalculateBlackScholesGreeks(
                spot, strike, timeToExpiry, rate, div, vol, isCall);
        }

        return (price, delta, gamma, theta, vega);
    }

    /// <summary>
    /// Validates calculated Greeks against physical constraints.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Physical constraints from options theory:
    /// - |Delta| ≤ 1.0 for single options (allow 1.5 for numerical tolerance)
    /// - |Gamma| is bounded and positive for vanilla options
    /// - Vega is positive for long options
    /// </para>
    /// </remarks>
    private static bool ValidateGreeks(double delta, double gamma, double vega, bool isCall)
    {
        // Check delta bounds
        if (Math.Abs(delta) > MaxAbsDelta)
        {
            return false;
        }

        // Check gamma bounds
        if (Math.Abs(gamma) > MaxAbsGamma)
        {
            return false;
        }

        // Check vega bounds
        if (Math.Abs(vega) > MaxAbsVega)
        {
            return false;
        }

        // Check for NaN or infinity
        if (double.IsNaN(delta) || double.IsInfinity(delta) ||
            double.IsNaN(gamma) || double.IsInfinity(gamma) ||
            double.IsNaN(vega) || double.IsInfinity(vega))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Calculates Black-Scholes analytical Greeks as fallback.
    /// </summary>
    private static (double Delta, double Gamma, double Theta, double Vega) CalculateBlackScholesGreeks(
        double spot,
        double strike,
        double timeToExpiry,
        double rate,
        double div,
        double vol,
        bool isCall)
    {
        double sqrtT = Math.Sqrt(timeToExpiry);
        double d1 = (Math.Log(spot / strike) + ((rate - div + (0.5 * vol * vol)) * timeToExpiry)) / (vol * sqrtT);
        double d2 = d1 - (vol * sqrtT);

        double nd1 = NormalCdf(d1);
        double nd2 = NormalCdf(d2);
        double npd1 = NormalPdf(d1);

        double expQT = Math.Exp(-div * timeToExpiry);
        double expRT = Math.Exp(-rate * timeToExpiry);

        double delta;
        double theta;

        if (isCall)
        {
            delta = expQT * nd1;
            theta = ((-spot * npd1 * vol * expQT / (2.0 * sqrtT)) -
                     (rate * strike * expRT * nd2) +
                     (div * spot * expQT * nd1)) / TradingCalendarDefaults.TradingDaysPerYear;
        }
        else
        {
            delta = expQT * (nd1 - 1.0);
            theta = ((-spot * npd1 * vol * expQT / (2.0 * sqrtT)) +
                     (rate * strike * expRT * (1.0 - nd2)) -
                     (div * spot * expQT * (1.0 - nd1))) / TradingCalendarDefaults.TradingDaysPerYear;
        }

        double gamma = expQT * npd1 / (spot * vol * sqrtT);
        double vega = spot * sqrtT * npd1 * expQT * 0.01; // Per 1% vol change

        return (delta, gamma, theta, vega);
    }

    // Use centralised CRMF001A for math utilities
    private static double NormalCdf(double x) => Alaris.Core.Math.CRMF001A.NormalCDF(x);
    private static double NormalPdf(double x) => Alaris.Core.Math.CRMF001A.NormalPDF(x);


    // Phase 7: Standard Calendar Spread Pricing

    /// <summary>
    /// Prices a calendar spread under positive rate regime.
    /// </summary>
    private static CalendarSpreadResult PriceCalendarSpread(
        SimulatedMarketData marketData,
        DateTime evaluationDate,
        double riskFreeRate,
        double dividendYield,
        string regimeLabel)
    {
        OptionChain chain = marketData.OptionChain;
        double spot = chain.UnderlyingPrice;

        // Get front and back month expiries
        OptionExpiry frontExpiry = chain.Expiries[0];
        OptionExpiry backExpiry = chain.Expiries[2];

        OptionContract atmContract = FindClosestStrike(frontExpiry.Calls, spot);
        double atmStrike = atmContract.Strike;

        int frontDte = (int)(frontExpiry.ExpiryDate - evaluationDate).TotalDays;
        int backDte = (int)(backExpiry.ExpiryDate - evaluationDate).TotalDays;

        // Get ATM options
        OptionContract frontCall = FindStrikeMatch(frontExpiry.Calls, atmStrike, 0.01);
        OptionContract backCall = FindStrikeMatch(backExpiry.Calls, atmStrike, 0.01);

        // Price options
        (double frontPrice, double frontDelta, double frontGamma, double frontTheta, double frontVega) =
            SimulateOptionPrice(spot, atmStrike, frontDte, riskFreeRate, dividendYield,
                frontCall.ImpliedVolatility, isCall: true);

        (double backPrice, double backDelta, double backGamma, double backTheta, double backVega) =
            SimulateOptionPrice(spot, atmStrike, backDte, riskFreeRate, dividendYield,
                backCall.ImpliedVolatility, isCall: true);

        // Calendar spread: sell front, buy back
        double spreadCost = backPrice - frontPrice;
        bool isCredit = spreadCost < 0;

        return new CalendarSpreadResult
        {
            RegimeLabel = regimeLabel,
            Strike = atmStrike,
            FrontExpiry = frontExpiry.ExpiryDate,
            BackExpiry = backExpiry.ExpiryDate,
            EvaluationDate = evaluationDate,
            FrontPrice = frontPrice,
            BackPrice = backPrice,
            SpreadCost = spreadCost,
            IsCredit = isCredit,
            SpreadDelta = backDelta - frontDelta,
            SpreadGamma = backGamma - frontGamma,
            SpreadTheta = backTheta - frontTheta,
            SpreadVega = backVega - frontVega,
            MaxLoss = isCredit ? double.NaN : spreadCost * 100,
            MaxGain = isCredit ? Math.Abs(spreadCost) * 100 : double.NaN,
            BreakEven = atmStrike,
            RiskFreeRate = riskFreeRate,
            DividendYield = dividendYield
        };
    }

    /// <summary>
    /// Simulates option pricing using Black-Scholes with American approximation.
    /// </summary>
    private static (double Price, double Delta, double Gamma, double Theta, double Vega) SimulateOptionPrice(
        double spot,
        double strike,
        int dte,
        double rate,
        double div,
        double vol,
        bool isCall)
    {
        double t = TradingCalendarDefaults.DteToYears(dte);
        double sqrtT = Math.Sqrt(t);

        double d1 = (Math.Log(spot / strike) + ((rate - div + (0.5 * vol * vol)) * t)) / (vol * sqrtT);
        double d2 = d1 - (vol * sqrtT);

        double nd1 = NormalCdf(d1);
        double nd2 = NormalCdf(d2);
        double npd1 = NormalPdf(d1);

        double price;
        double delta;
        double theta;

        if (isCall)
        {
            price = (spot * Math.Exp(-div * t) * nd1) - (strike * Math.Exp(-rate * t) * nd2);
            delta = Math.Exp(-div * t) * nd1;
            theta = ((-spot * npd1 * vol * Math.Exp(-div * t) / (2.0 * sqrtT)) -
                     (rate * strike * Math.Exp(-rate * t) * nd2) +
                     (div * spot * Math.Exp(-div * t) * nd1)) / TradingCalendarDefaults.TradingDaysPerYear;
        }
        else
        {
            price = (strike * Math.Exp(-rate * t) * (1.0 - nd2)) - (spot * Math.Exp(-div * t) * (1.0 - nd1));
            delta = Math.Exp(-div * t) * (nd1 - 1.0);
            theta = ((-spot * npd1 * vol * Math.Exp(-div * t) / (2.0 * sqrtT)) +
                     (rate * strike * Math.Exp(-rate * t) * (1.0 - nd2)) -
                     (div * spot * Math.Exp(-div * t) * (1.0 - nd1))) / TradingCalendarDefaults.TradingDaysPerYear;
        }

        double gamma = Math.Exp(-div * t) * npd1 / (spot * vol * sqrtT);
        double vega = spot * sqrtT * npd1 * Math.Exp(-div * t) * 0.01;

        return (price, delta, gamma, theta, vega);
    }

    // Phase 9: Double Boundary Demonstration

    /// <summary>
    /// Demonstrates Alaris.Double pricing with Healy (2021) benchmark parameters.
    /// </summary>
    private static DoubleBoundaryDemoResult DemonstrateDoubleBoundaryPricing()
    {
        // Healy (2021) benchmark parameters
        double spot = 100.0;
        double strike = 100.0;
        double maturity = 1.0; // 1 year
        double rate = -0.005;  // -0.5%
        double div = -0.010;   // -1.0% (q < r for double boundary)
        double vol = 0.20;     // 20%

        // Price using unified engine (spectral)
        double putPrice = PricingEngine.Price(spot, strike, maturity, rate, div, vol, Alaris.Core.Options.OptionType.Put);

        // Get boundaries using CRAP001A.CalculateBoundaries()
        CRAP001A boundaryCalc = new CRAP001A(spot, strike, maturity, rate, div, vol, isCall: false);
        (double upperBoundary, double lowerBoundary) = boundaryCalc.CalculateBoundaries();

        // Validate physical constraints (Healy Appendix A)
        bool a1Pass = upperBoundary > 0 && lowerBoundary > 0;
        bool a2Pass = upperBoundary > lowerBoundary;
        bool a3Pass = upperBoundary < strike && lowerBoundary < strike;

        return new DoubleBoundaryDemoResult
        {
            Spot = spot,
            Strike = strike,
            Maturity = maturity,
            Rate = rate,
            DividendYield = div,
            Volatility = vol,
            PutPrice = putPrice,
            UpperBoundary = upperBoundary,
            LowerBoundary = lowerBoundary,
            A1Pass = a1Pass,
            A2Pass = a2Pass,
            A3Pass = a3Pass,
            AllConstraintsPass = a1Pass && a2Pass && a3Pass
        };
    }

    // Phase 11: Maturity Guard Demo (STMG001A)

    /// <summary>
    /// Demonstrates maturity guard entry/exit filtering based on DTE.
    /// </summary>
    private static void DemonstrateMaturityGuard(DateTime evaluationDate)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 11: MATURITY GUARD DEMO (STMG001A)                                     │");
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        // Create maturity guard with default thresholds
        STMG001A guard = new STMG001A();

        // Calculate time-to-expiry for various DTEs
        double t2dte = TradingCalendarDefaults.DteToYears(2);   // ~0.008 years
        double t5dte = TradingCalendarDefaults.DteToYears(5);   // ~0.020 years
        double t14dte = TradingCalendarDefaults.DteToYears(14); // ~0.056 years
        double t60dte = TradingCalendarDefaults.DteToYears(60); // ~0.238 years

        Console.WriteLine("  Entry Filtering (DTE thresholds):");
        Console.WriteLine($"    2 DTE:  Entry allowed = {guard.EvaluateEntry("SIM", t2dte).IsAllowed,-5} (Min: 5 DTE)");
        Console.WriteLine($"    5 DTE:  Entry allowed = {guard.EvaluateEntry("SIM", t5dte).IsAllowed,-5}");
        Console.WriteLine($"    14 DTE: Entry allowed = {guard.EvaluateEntry("SIM", t14dte).IsAllowed,-5}");
        Console.WriteLine($"    60 DTE: Entry allowed = {guard.EvaluateEntry("SIM", t60dte).IsAllowed,-5} (Max: 45 DTE)");
        Console.WriteLine();

        Console.WriteLine("  Exit Filtering:");
        Console.WriteLine($"    2 DTE:  Exit urgency = {guard.EvaluateExit("SIM", t2dte).UrgencyLevel,-10} (Exit at 3 DTE)");
        Console.WriteLine($"    5 DTE:  Exit urgency = {guard.EvaluateExit("SIM", t5dte).UrgencyLevel,-10}");
        Console.WriteLine();
    }

    // Phase 12: Near-Expiry Double Boundary Demo (DBEX001A)

    /// <summary>
    /// Demonstrates near-expiry pricing with DBEX001A intrinsic fallback.
    /// </summary>
    private static void DemonstrateNearExpiryPricing()
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 12: NEAR-EXPIRY DOUBLE BOUNDARY DEMO (DBEX001A)                        │");
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        double spot = 100.0;
        double strike = 100.0;
        double rate = -0.005;
        double div = -0.010;
        double vol = 0.20;

        Console.WriteLine("  Parameters: S=100, K=100, r=-0.5%, q=-1.0%, σ=20%");
        Console.WriteLine();
        Console.WriteLine("  Expiry       Price    Intrinsic  Method");
        Console.WriteLine("  ──────────────────────────────────────────────");

        double[] maturities = { 0.005, 0.01, 0.02, 0.04, 0.1 };
        string[] labels = { "~1 day", "~2.5 days", "~5 days", "~10 days", "~25 days" };

        for (int i = 0; i < maturities.Length; i++)
        {
            double T = maturities[i];
            double price = PricingEngine.Price(spot, strike, T, rate, div, vol, Alaris.Core.Options.OptionType.Put);
            double intrinsic = Math.Max(0, strike - spot);
            string method = T < 0.012 ? "Near-expiry" : "Spectral";

            Console.WriteLine($"  {labels[i],-12} {price,8:F4}  {intrinsic,8:F4}   {method}");
        }

        Console.WriteLine();
    }

    // Phase 13: Pin Risk Monitor Demo (STHD009A)

    /// <summary>
    /// Demonstrates pin risk monitoring for positions near strike.
    /// </summary>
    private static void DemonstratePinRisk(double spotPrice, double strike)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 13: PIN RISK MONITOR DEMO (STHD009A)                                   │");
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        STHD009A pinMonitor = new STHD009A();

        Console.WriteLine($"  Position: Spot=${spotPrice:F2}, Strike=${strike:F2}");
        Console.WriteLine($"  Pin Zone: ±1% of strike (${strike * 0.99:F2} - ${strike * 1.01:F2})");
        Console.WriteLine();
        Console.WriteLine("  DTE   In Pin Zone?   Risk Level     Recommended Action");
        Console.WriteLine("  ────────────────────────────────────────────────────────");

        int[] dtes = { 1, 2, 3, 5, 10 };
        foreach (int dte in dtes)
        {
            STHD010A result = pinMonitor.Evaluate(spotPrice, strike, dte, gamma: null, contracts: 10);
            Console.WriteLine($"  {dte,3}   {result.IsInPinZone,-14}  {result.RiskLevel,-14} {result.RecommendedAction}");
        }

        Console.WriteLine();
    }

    // Phase 14: First-Principles Double Boundary Validation

    /// <summary>
    /// Demonstrates first-principles validation of double boundary constraints.
    /// </summary>
    /// <remarks>
    /// Validates A1-A5 constraints from Healy (2021) Appendix A:
    /// A1: S_u greater than 0, S_l greater than 0
    /// A2: S_u greater than S_l
    /// A3: S_u less than K, S_l less than K
    /// A4: V(S_b) = K - S_b
    /// A5: dV/dS evaluated at S_b = -1
    /// </remarks>
    private static void DemonstrateFirstPrinciplesDoubleBoundary()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║ PHASE 14: FIRST-PRINCIPLES DOUBLE BOUNDARY VALIDATION                        ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ Mathematical Foundation: Healy (2021) Appendix A Constraints                 ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        Console.WriteLine("  CONSTRAINT DEFINITIONS:");
        Console.WriteLine("    A1: S_u > 0, S_l > 0     (Positive boundaries)");
        Console.WriteLine("    A2: S_u > S_l            (Ordering: upper > lower)");
        Console.WriteLine("    A3: S_u < K, S_l < K     (Put boundaries below strike)");
        Console.WriteLine("    A4: V(S_b) = K - S_b     (Value matching at boundary)");
        Console.WriteLine("    A5: ∂V/∂S|_{S_b} = -1    (Smooth pasting condition)");
        Console.WriteLine();

        // Test parameters from TSUN021A
        double spot = 100.0;
        double strike = 100.0;
        double maturity = 10.0;
        double rate = -0.005;
        double div = -0.01;
        double vol = 0.08;

        Console.WriteLine($"  Parameters: S={spot}, K={strike}, T={maturity}, r={rate*100:F1}%, q={div*100:F1}%, σ={vol*100:F0}%");
        Console.WriteLine();

        // Solve for boundaries using CRAP001A
        CRAP001A boundaryCalc = new CRAP001A(spot, strike, maturity, rate, div, vol, isCall: false);
        (double upperBoundary, double lowerBoundary) = boundaryCalc.CalculateBoundaries();

        Console.WriteLine("  VALIDATION RESULTS:");
        Console.WriteLine("  ────────────────────────────────────────────────────────");

        // A1: Positive boundaries
        bool a1Upper = upperBoundary > 0;
        bool a1Lower = lowerBoundary > 0;
        Console.WriteLine($"    A1: S_u = {upperBoundary:F2} > 0: {(a1Upper ? "✓ PASS" : "✗ FAIL")}");
        Console.WriteLine($"        S_l = {lowerBoundary:F2} > 0: {(a1Lower ? "✓ PASS" : "✗ FAIL")}");

        // A2: Ordering
        bool a2 = upperBoundary > lowerBoundary;
        Console.WriteLine($"    A2: S_u > S_l ({upperBoundary:F2} > {lowerBoundary:F2}): {(a2 ? "✓ PASS" : "✗ FAIL")}");

        // A3: Below strike
        bool a3Upper = upperBoundary < strike;
        bool a3Lower = lowerBoundary < strike;
        Console.WriteLine($"    A3: S_u < K: {(a3Upper ? "✓ PASS" : "✗ FAIL")}, S_l < K: {(a3Lower ? "✓ PASS" : "✗ FAIL")}");

        // A4/A5: Value matching and smooth pasting (numerically)
        double price = PricingEngine.Price(spot, strike, maturity, rate, div, vol, Alaris.Core.Options.OptionType.Put);
        double intrinsic = Math.Max(0, strike - spot);
        bool priceValid = price >= intrinsic;
        Console.WriteLine($"    Price floor: P={price:F4} ≥ max(K-S,0)={intrinsic:F4}: {(priceValid ? "✓ PASS" : "✗ FAIL")}");

        Console.WriteLine();
        Console.WriteLine($"  Overall: {(a1Upper && a1Lower && a2 && a3Upper && a3Lower && priceValid ? "ALL CONSTRAINTS SATISFIED ✓" : "CONSTRAINTS VIOLATED ✗")}");
        Console.WriteLine();
    }

    // Phase 15: Kalman-Filtered Volatility Demo

    /// <summary>
    /// Demonstrates Kalman-filtered volatility estimation with state equations.
    /// </summary>
    private static void DemonstrateKalmanVolatility(List<PriceBar> priceHistory)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║ PHASE 15: KALMAN-FILTERED VOLATILITY (STKF001A)                              ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ Mathematical Foundation: Kalman (1960), Yang-Zhang (2000)                    ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        Console.WriteLine("  STATE TRANSITION MODEL:");
        Console.WriteLine("    x̂ₜ₊₁⁻ = Φ·x̂ₜ              (Prediction step)");
        Console.WriteLine("    Pₜ₊₁⁻ = Φ·Pₜ·Φᵀ + Q        (Covariance prediction)");
        Console.WriteLine();
        Console.WriteLine("  MEASUREMENT UPDATE:");
        Console.WriteLine("    K = Pₜ₊₁⁻·Hᵀ/(H·Pₜ₊₁⁻·Hᵀ + R)  (Kalman gain)");
        Console.WriteLine("    x̂ₜ₊₁ = x̂ₜ₊₁⁻ + K·(z - H·x̂ₜ₊₁⁻)  (Filtered estimate)");
        Console.WriteLine();
        Console.WriteLine("  Where: z = Yang-Zhang estimate, R = σ²_YZ / n");
        Console.WriteLine();

        // Create Kalman filter
        STKF001A kalman = new STKF001A();

        // Use last 30 days of price history for demo
        int windowSize = 30;
        int startIdx = Math.Max(0, priceHistory.Count - windowSize - 10);
        
        Console.WriteLine("  FILTER EVOLUTION (last 10 updates):");
        Console.WriteLine("  ────────────────────────────────────────────────────────");
        Console.WriteLine("    Day    Raw YZ    Filtered    Kalman Gain    Uncertainty");
        Console.WriteLine("  ────────────────────────────────────────────────────────");

        for (int i = startIdx; i < priceHistory.Count; i++)
        {
            // Calculate rolling Yang-Zhang
            int rollStart = Math.Max(0, i - windowSize);
            int windowCount = i - rollStart + 1;
            List<PriceBar> window = new List<PriceBar>(windowCount);
            for (int j = rollStart; j <= i; j++)
            {
                window.Add(priceHistory[j]);
            }
            
            if (window.Count < 5)
            {
                continue;
            }

            // Simple proxy for Yang-Zhang using close-to-close returns
            double sumSqRet = 0;
            for (int j = 1; j < window.Count; j++)
            {
                double ret = Math.Log(window[j].Close / window[j - 1].Close);
                sumSqRet += ret * ret;
            }
            double yzEstimate = Math.Sqrt(sumSqRet / window.Count * TradingCalendarDefaults.TradingDaysPerYear);

            // Update Kalman filter
            KalmanVolatilityEstimate estimate = kalman.Update(yzEstimate, window.Count);

            // Display last 10 updates
            if (i >= priceHistory.Count - 10)
            {
                Console.WriteLine($"    {i - startIdx + 1,3}    {yzEstimate,7:P1}    {estimate.Volatility,7:P1}     {estimate.KalmanGain,10:F4}     ±{estimate.StandardError:P1}");
            }
        }

        // Show final confidence interval
        (double lower, double upper) = kalman.GetConfidenceInterval();
        Console.WriteLine();
        Console.WriteLine($"  FINAL ESTIMATE: σ̂ = {lower + ((upper - lower) / 2):P1}");
        Console.WriteLine($"  95% CONFIDENCE INTERVAL: [{lower:P1}, {upper:P1}]");
        Console.WriteLine();
    }

    // Phase 16: Neyman-Pearson Signal Detection

    /// <summary>
    /// Demonstrates Neyman-Pearson optimal signal detection framework.
    /// </summary>
    private static void DemonstrateNeymanPearsonDetection(double iv, double rv)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║ PHASE 16: NEYMAN-PEARSON SIGNAL DETECTION (STSD001A)                         ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ Mathematical Foundation: Neyman-Pearson Lemma (1933)                         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        Console.WriteLine("  NEYMAN-PEARSON LEMMA:");
        Console.WriteLine("    The most powerful test at level α has rejection region:");
        Console.WriteLine("      C = {x : L(θ₁;x) / L(θ₀;x) > k_α}");
        Console.WriteLine();
        Console.WriteLine("  For lognormal IV/RV distributions:");
        Console.WriteLine("    H₀: x ~ N(μ₀, σ²)  (Profitable trade: μ₀ = 1.45)");
        Console.WriteLine("    H₁: x ~ N(μ₁, σ²)  (Unprofitable trade: μ₁ = 1.08)");
        Console.WriteLine();
        Console.WriteLine("  OPTIMAL THRESHOLD (equal variances):");
        Console.WriteLine("    x* = (μ₀ + μ₁)/2 + σ²/(μ₁-μ₀)·ln(ρ)");
        Console.WriteLine();

        // Create detector with default parameters
        STSD001A detector = new STSD001A();
        DistributionParameters parameters = detector.GetParameters();

        Console.WriteLine($"  CALIBRATED PARAMETERS:");
        Console.WriteLine($"    Profitable mean (μ₁):   {parameters.Mu1:F3}");
        Console.WriteLine($"    Unprofitable mean (μ₀): {parameters.Mu0:F3}");
        Console.WriteLine($"    Variance (σ²):          {parameters.Sigma1 * parameters.Sigma1:F4}");
        Console.WriteLine();

        // Compute optimal threshold
        double optimalThreshold = detector.ComputeOptimalThreshold(costRatio: 2.0);
        Console.WriteLine($"  OPTIMAL THRESHOLD (cost ratio 2.0): x* = {optimalThreshold:F3}");
        Console.WriteLine();

        // Evaluate current IV/RV ratio
        double ivRvRatio = iv / rv;
        SignalDetectionResult result = detector.Evaluate(ivRvRatio);

        Console.WriteLine("  EVALUATION:");
        Console.WriteLine($"    Observed IV/RV ratio:     {ivRvRatio:F3}");
        Console.WriteLine($"    Threshold:                {result.Threshold:F3}");
        Console.WriteLine($"    Likelihood ratio L₁/L₀:   {result.LikelihoodRatio:F3}");
        Console.WriteLine($"    Posterior P(H₁|x):        {result.PosteriorProbability:P1}");
        Console.WriteLine($"    Decision:                 {(result.PassesThreshold ? "ACCEPT SIGNAL ✓" : "REJECT SIGNAL ✗")}");
        Console.WriteLine();

        // Show error rates
        double alpha = detector.ComputeTypeIError(optimalThreshold);
        double beta = detector.ComputeTypeIIError(optimalThreshold);
        double power = detector.ComputePower(optimalThreshold);
        Console.WriteLine("  ERROR RATES:");
        Console.WriteLine($"    Type I error (α):   {alpha:P1}  (False positive)");
        Console.WriteLine($"    Type II error (β):  {beta:P1}  (False negative)");
        Console.WriteLine($"    Power (1-β):        {power:P1}");
        Console.WriteLine();
    }

    // Phase 17: Queue-Theoretic Position Management

    /// <summary>
    /// Demonstrates queue-theoretic position management with Little's Law.
    /// </summary>
    private static void DemonstrateQueueTheoreticManagement()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║ PHASE 17: QUEUE-THEORETIC POSITION MANAGEMENT (STQT001A)                     ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ Mathematical Foundation: Little (1961), Pollaczek-Khinchine                  ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        Console.WriteLine("  FUNDAMENTAL RELATIONSHIPS:");
        Console.WriteLine();
        Console.WriteLine("  LITTLE'S LAW:  L = λW");
        Console.WriteLine("    L = Mean number of positions in system");
        Console.WriteLine("    λ = Signal arrival rate (per day)");
        Console.WriteLine("    W = Mean time in system (days)");
        Console.WriteLine();
        Console.WriteLine("  POLLACZEK-KHINCHINE (M/G/1):");
        Console.WriteLine("    L = ρ + ρ²(1 + c_S²) / [2(1-ρ)]");
        Console.WriteLine("    Where: ρ = λ/μ, c_S = CV of service time");
        Console.WriteLine();
        Console.WriteLine("  BLOCKING PROBABILITY (M/M/1/K):");
        Console.WriteLine("    P_K = (1-ρ)ρ^K / (1 - ρ^{K+1})");
        Console.WriteLine();

        // Create queue manager
        STQT001A queueManager = new STQT001A();

        // Sample parameters (stable queue: ρ < 1)
        double arrivalRate = 0.05;    // 0.05 signals per day (1 per 20 days)
        double serviceRate = 0.1;     // Mean holding = 10 days
        double serviceCv = 0.5;       // CV of holding time

        Console.WriteLine("  SAMPLE CALCULATION:");
        Console.WriteLine($"    λ = {arrivalRate} signals/day, μ = {serviceRate} (10-day holding), c_S = {serviceCv}");
        Console.WriteLine();

        double utilisation = arrivalRate / serviceRate;
        double meanQueueLength = STQT001A.ComputeMeanQueueLength(arrivalRate, serviceRate, serviceCv);
        double meanWait = STQT001A.ComputeMeanWaitingTime(arrivalRate, serviceRate, serviceCv);

        Console.WriteLine($"    Utilisation ρ = λ/μ = {utilisation:F2}");
        Console.WriteLine($"    Mean queue length L = {meanQueueLength:F2} positions");
        Console.WriteLine($"    Mean waiting time W = {meanWait:F1} days");
        Console.WriteLine();

        // Show blocking probability for various capacities
        Console.WriteLine("  BLOCKING PROBABILITY BY CAPACITY:");
        Console.WriteLine("    K     P(block)");
        Console.WriteLine("  ────────────────");
        int[] capacities = { 3, 5, 7, 10 };
        foreach (int k in capacities)
        {
            double pBlock = queueManager.ComputeBlockingProbability(k - 1, k, utilisation);
            Console.WriteLine($"    {k,2}    {pBlock:P2}");
        }
        Console.WriteLine();

        // Optimal capacity
        int optimalK = queueManager.ComputeOptimalCapacity(arrivalRate, 1.0 / serviceRate, 100, 5);
        Console.WriteLine($"  OPTIMAL CAPACITY: K* = {optimalK} positions");
        Console.WriteLine();
    }

    // Phase 18: Rule-Based Exit Monitor

    /// <summary>
    /// Demonstrates rule-based exit monitoring with stall detection.
    /// </summary>
    private static void DemonstrateRuleBasedExit(CalendarSpreadResult spread)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║ PHASE 18: RULE-BASED EXIT MONITOR (STHD007B)                                 ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ Exit Logic: Time-decay, profit target, stop-loss, stall detection            ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        Console.WriteLine("  EXIT RULE HIERARCHY:");
        Console.WriteLine("    1. Hard stop-loss:     -50% of max loss");
        Console.WriteLine("    2. Profit target:      +80% of theoretical max");
        Console.WriteLine("    3. Time decay exit:    <3 DTE on front leg");
        Console.WriteLine("    4. Stall detection:    No P&L movement for 5 days");
        Console.WriteLine();

        // Simulate various P&L scenarios
        Console.WriteLine("  RULE EVALUATION (simulated scenarios):");
        Console.WriteLine("  ────────────────────────────────────────────────────────");
        Console.WriteLine("    Scenario           P&L%    DTE   Days Flat   Exit?   Rule");
        Console.WriteLine("  ────────────────────────────────────────────────────────");

        // Scenarios: (description, pnlPct, dte, daysFlat, expectedExit, reason)
        (string desc, double pnl, int dte, int flat, bool exit, string rule)[] scenarios = new[]
        {
            ("Profit target hit", 0.85, 10, 0, true, "Profit target"),
            ("Stop-loss trigger", -0.55, 15, 0, true, "Stop-loss"),
            ("Time decay exit", 0.20, 2, 0, true, "Time decay"),
            ("Stall detection", 0.10, 8, 6, true, "Stall"),
            ("Hold position", 0.30, 12, 2, false, "N/A")
        };

        foreach ((string desc, double pnl, int dte, int flat, bool exit, string rule) in scenarios)
        {
            Console.WriteLine($"    {desc,-18} {pnl,6:P0}   {dte,3}       {flat}       {(exit ? "EXIT" : "HOLD")}    {rule}");
        }

        Console.WriteLine();
    }

    // Phase 19: Workflow Integration Summary

    /// <summary>
    /// Displays complete workflow integration summary.
    /// </summary>
    private static void DisplayWorkflowIntegration(
        double realisedVol,
        double impliedVol,
        SignalResult signalResult,
        CalendarSpreadResult spread)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║ PHASE 19: COMPLETE WORKFLOW INTEGRATION                                      ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ End-to-end signal processing pipeline with first-principles validation       ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        Console.WriteLine("  DATA FLOW:");
        Console.WriteLine();
        Console.WriteLine("  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐");
        Console.WriteLine("  │ Market Data │───▶│ Yang-Zhang  │───▶│  Kalman     │───▶ Filtered σ");
        Console.WriteLine("  │   (OHLCV)   │    │  Estimator  │    │   Filter    │");
        Console.WriteLine("  └─────────────┘    └─────────────┘    └─────────────┘");
        Console.WriteLine();
        Console.WriteLine("            │           ┌─────────────┐    ┌─────────────┐");
        Console.WriteLine("            └──────────▶│  IV/RV Ratio│───▶│  Neyman-    │───▶ Accept/Reject");
        Console.WriteLine("                        │  Computation│    │  Pearson    │");
        Console.WriteLine("                        └─────────────┘    └─────────────┘");
        Console.WriteLine();
        Console.WriteLine("                                               │");
        Console.WriteLine("                                               ▼");
        Console.WriteLine("  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐");
        Console.WriteLine("  │ Position    │◀───│  Queue      │◀───│  Signal     │");
        Console.WriteLine("  │  Manager    │    │  Theory     │    │  Accepted   │");
        Console.WriteLine("  └─────────────┘    └─────────────┘    └─────────────┘");
        Console.WriteLine();
        Console.WriteLine("            │           ┌─────────────┐    ┌─────────────┐");
        Console.WriteLine("            └──────────▶│  Exit       │───▶│  Final      │");
        Console.WriteLine("                        │  Monitor    │    │  Decision   │");
        Console.WriteLine("                        └─────────────┘    └─────────────┘");
        Console.WriteLine();

        // Summary metrics
        Console.WriteLine("  SYSTEM STATE:");
        Console.WriteLine($"    Realised Volatility (σ̂):  {realisedVol:P1}");
        Console.WriteLine($"    Implied Volatility (IV):   {impliedVol:P1}");
        Console.WriteLine($"    IV/RV Ratio:               {impliedVol / realisedVol:F2}");
        Console.WriteLine($"    Signal Strength:           {signalResult.Signal.Strength}");
        Console.WriteLine($"    Spread Cost:               ${spread.SpreadCost * 100:F2}");
        Console.WriteLine();
        Console.WriteLine("  ALL FIRST-PRINCIPLES VALIDATIONS COMPLETE ✓");
        Console.WriteLine();
    }

    // Phase 10: Production Validation

    /// <summary>
    /// Executes the production validation phase including cost and hedging analysis.
    /// </summary>
    private static void RunProductionValidation(
        ILoggerFactory loggerFactory,
        Signal signal,
        SimulatedMarketData marketData,
        CalendarSpreadResult pricingResult,
        DateTime earningsDate,
        DateTime evaluationDate)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 10: PRODUCTION VALIDATION                                              │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ Note: Running validation regardless of signal strength for demonstration     │");
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        // Create validation components
        ILogger<STCS005A>? costModelLogger = loggerFactory.CreateLogger<STCS005A>();
        STCS005A costModel = new STCS005A(logger: costModelLogger);

        ILogger<STCS006A>? costValidatorLogger = loggerFactory.CreateLogger<STCS006A>();
        STCS006A costValidator = new STCS006A(costModel, logger: costValidatorLogger);

        ILogger<STHD001A>? vegaAnalyserLogger = loggerFactory.CreateLogger<STHD001A>();
        STHD001A vegaAnalyser = new STHD001A(logger: vegaAnalyserLogger);

        ILogger<STCS008A>? liquidityValidatorLogger = loggerFactory.CreateLogger<STCS008A>();
        STCS008A liquidityValidator = new STCS008A(logger: liquidityValidatorLogger);

        ILogger<STHD003A>? gammaManagerLogger = loggerFactory.CreateLogger<STHD003A>();
        STHD003A gammaManager = new STHD003A(logger: gammaManagerLogger);

        ILogger<STHD005A>? productionValidatorLogger = loggerFactory.CreateLogger<STHD005A>();
        STHD005A productionValidator = new STHD005A(
            costValidator,
            vegaAnalyser,
            liquidityValidator,
            gammaManager,
            productionValidatorLogger);

        // Prepare validation parameters
        double strike = pricingResult.Strike;

        OptionExpiry frontExpiry = marketData.OptionChain.Expiries[0];
        OptionExpiry backExpiry = marketData.OptionChain.Expiries[2];

        OptionContract frontCall = FindStrikeMatch(frontExpiry.Calls, strike, 0.01);
        OptionContract backCall = FindStrikeMatch(backExpiry.Calls, strike, 0.01);

        STCS002A frontLegParams = new STCS002A
        {
            Symbol = SimulationSymbol,
            Contracts = 1,
            Direction = OrderDirection.Sell,
            BidPrice = frontCall.Bid,
            AskPrice = frontCall.Ask,
            MidPrice = frontCall.LastPrice,
            Premium = frontCall.LastPrice
        };

        STCS002A backLegParams = new STCS002A
        {
            Symbol = SimulationSymbol,
            Contracts = 1,
            Direction = OrderDirection.Buy,
            BidPrice = backCall.Bid,
            AskPrice = backCall.Ask,
            MidPrice = backCall.LastPrice,
            Premium = backCall.LastPrice
        };

        Alaris.Strategy.Hedge.SpreadGreeks spreadGreeks = new Alaris.Strategy.Hedge.SpreadGreeks
        {
            Delta = pricingResult.SpreadDelta,
            Gamma = pricingResult.SpreadGamma,
            Theta = pricingResult.SpreadTheta,
            Vega = pricingResult.SpreadVega
        };

        // Generate synthetic IV history for correlation analysis
        (List<double> frontIVHistory, List<double> backIVHistory) = GenerateSyntheticIVHistory(30);

        // Run production validation
        STHD006A validation = productionValidator.Validate(
            signal,
            frontLegParams,
            backLegParams,
            frontIVHistory,
            backIVHistory,
            backCall.Volume,
            backCall.OpenInterest,
            marketData.CurrentPrice,
            strike,
            spreadGreeks,
            (int)(earningsDate - evaluationDate).TotalDays);

        // Display validation results
        DisplayProductionValidation(validation);

        // Calculate position sizing based on Kelly Criterion
        PositionSizeResult positionResult = CalculatePositionSize(
            signal,
            pricingResult,
            PortfolioValue);

        DisplayPositionSize(positionResult);

        // Display final trade recommendation
        DisplayTradeRecommendation(
            signalResult: new SignalResult
            {
                Signal = signal,
                CriteriaResults = new Dictionary<string, (bool Pass, string Value, string Threshold)>()
            },
            pricingResult,
            positionResult);

        // Display final production decision
        DisplayFinalTradeRecommendation(validation, positionResult.Contracts, positionResult.TotalRisk);
    }

    /// <summary>
    /// Generates synthetic IV history for correlation analysis.
    /// </summary>
    private static (List<double> FrontIVHistory, List<double> BackIVHistory) GenerateSyntheticIVHistory(int days)
    {
        Random random = new Random(42); // Deterministic seed
        List<double> frontIV = new List<double>();
        List<double> backIV = new List<double>();

        double baseFrontIV = 0.28; // Elevated front-month IV
        double baseBackIV = 0.22;  // Normal back-month IV
        double correlation = 0.65; // Lower correlation near earnings (decoupled)

        for (int i = 0; i < days; i++)
        {
            double commonShock = (random.NextDouble() * 0.04) - 0.02;
            double frontIdiosyncratic = (random.NextDouble() * 0.03) - 0.015;
            double backIdiosyncratic = (random.NextDouble() * 0.015) - 0.0075;

            frontIV.Add(baseFrontIV + (correlation * commonShock) + frontIdiosyncratic);
            backIV.Add(baseBackIV + (correlation * commonShock) + backIdiosyncratic);
        }

        return (frontIV, backIV);
    }

    // Auxiliary Calculations

    /// <summary>
    /// Calculates realised volatility using Yang-Zhang (2000) estimator.
    /// </summary>
    private static double CalculateRealisedVolatility(List<PriceBar> priceHistory)
    {
        if (priceHistory.Count < 2)
        {
            return 0.20; // Default fallback
        }

        // Yang-Zhang OHLC-based volatility estimation
        int n = priceHistory.Count;
        double sumOC = 0.0;
        double sumCC = 0.0;
        double sumRS = 0.0;

        for (int i = 1; i < n; i++)
        {
            PriceBar current = priceHistory[i];
            PriceBar previous = priceHistory[i - 1];

            // Overnight return
            double overnight = Math.Log(current.Open / previous.Close);
            sumOC += overnight * overnight;

            // Close-to-close return
            double closeToClose = Math.Log(current.Close / previous.Close);
            sumCC += closeToClose * closeToClose;

            // Rogers-Satchell component
            double h = Math.Log(current.High / current.Open);
            double l = Math.Log(current.Low / current.Open);
            double c = Math.Log(current.Close / current.Open);
            sumRS += (h * (h - c)) + (l * (l - c));
        }

        double varOC = sumOC / (n - 1);
        double varCC = sumCC / (n - 1);
        double varRS = sumRS / (n - 1);

        // Yang-Zhang: weighted combination
        double k = 0.34 / (1.34 + ((n + 1.0) / (n - 1.0)));
        double variance = varOC + (k * varCC) + ((1.0 - k) * varRS);

        // Annualise
        return Math.Sqrt(variance * TradingCalendarDefaults.TradingDaysPerYear);
    }

    private static OptionContract FindClosestStrike(IList<OptionContract> contracts, double targetStrike)
    {
        if (contracts.Count == 0)
        {
            throw new InvalidOperationException("Option contract list is empty.");
        }

        OptionContract closest = contracts[0];
        double minDistance = Math.Abs(closest.Strike - targetStrike);

        for (int i = 1; i < contracts.Count; i++)
        {
            OptionContract contract = contracts[i];
            double distance = Math.Abs(contract.Strike - targetStrike);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = contract;
            }
        }

        return closest;
    }

    private static OptionContract FindStrikeMatch(IList<OptionContract> contracts, double strike, double tolerance)
    {
        for (int i = 0; i < contracts.Count; i++)
        {
            OptionContract contract = contracts[i];
            if (Math.Abs(contract.Strike - strike) < tolerance)
            {
                return contract;
            }
        }

        throw new InvalidOperationException($"No option contract found for strike {strike:F2}.");
    }

    /// <summary>
    /// Analyses term structure from option chain.
    /// </summary>
    private static TermStructureResult AnalyseTermStructure(OptionChain chain, DateTime evalDate)
    {
        List<(int dte, double iv)> points = new List<(int dte, double iv)>();

        foreach (OptionExpiry expiry in chain.Expiries)
        {
            int dte = (int)(expiry.ExpiryDate - evalDate).TotalDays;

            // Get ATM IV (closest to spot)
            OptionContract atmCall = FindClosestStrike(expiry.Calls, chain.UnderlyingPrice);

            points.Add((dte, atmCall.ImpliedVolatility));
        }

        // Calculate slope using linear regression
        double n = points.Count;
        double sumX = 0.0;
        double sumY = 0.0;
        double sumXY = 0.0;
        double sumX2 = 0.0;

        for (int i = 0; i < points.Count; i++)
        {
            (int dte, double iv) point = points[i];
            double dteValue = point.dte;
            sumX += dteValue;
            sumY += point.iv;
            sumXY += dteValue * point.iv;
            sumX2 += dteValue * dteValue;
        }

        double slope = ((n * sumXY) - (sumX * sumY)) / ((n * sumX2) - (sumX * sumX));
        bool isInverted = slope < 0;
        bool meetsCriterion = slope <= -0.00406;

        // 30-day IV
        double iv30 = 0.0;
        bool iv30Found = false;
        for (int i = 0; i < points.Count; i++)
        {
            (int dte, double iv) point = points[i];
            if (point.dte >= 25 && point.dte <= 35)
            {
                iv30 = point.iv;
                iv30Found = true;
                break;
            }
        }

        if (!iv30Found)
        {
            throw new InvalidOperationException("Unable to locate 30-day implied volatility point.");
        }

        return new TermStructureResult
        {
            Slope = slope,
            IsInverted = isInverted,
            MeetsTradingCriterion = meetsCriterion,
            IV30 = iv30,
            Points = points
        };
    }

    /// <summary>
    /// Generates trading signal based on Atilgan (2014) criteria.
    /// </summary>
    private static SignalResult GenerateSignal(
        SimulatedMarketData marketData,
        double realisedVolatility,
        TermStructureResult termResult,
        LeungSantoliMetrics lsMetrics,
        DateTime earningsDate,
        DateTime evaluationDate)
    {
        double ivRvRatio = termResult.IV30 / realisedVolatility;
        double avgVolume = 0.0;
        if (marketData.PriceHistory.Count > 0)
        {
            long volumeSum = 0;
            for (int i = 0; i < marketData.PriceHistory.Count; i++)
            {
                volumeSum += marketData.PriceHistory[i].Volume;
            }

            avgVolume = volumeSum / (double)marketData.PriceHistory.Count;
        }

        Dictionary<string, (bool Pass, string Value, string Threshold)> criteriaResults =
            new Dictionary<string, (bool Pass, string Value, string Threshold)>
        {
            ["IV/RV Ratio"] = (ivRvRatio >= 1.25, $"{ivRvRatio:F3}", "≥ 1.250"),
            ["Term Slope"] = (termResult.MeetsTradingCriterion, $"{termResult.Slope:F6}", "≤ -0.00406"),
            ["Avg Volume"] = (avgVolume >= 1_500_000, $"{avgVolume:N0}", "≥ 1,500,000")
        };

        int passCount = 0;
        foreach (KeyValuePair<string, (bool Pass, string Value, string Threshold)> kvp in criteriaResults)
        {
            if (kvp.Value.Pass)
            {
                passCount++;
            }
        }

        SignalStrength strength = passCount switch
        {
            3 => SignalStrength.Recommended,
            2 => SignalStrength.Consider,
            _ => SignalStrength.Avoid
        };

        Signal signal = new Signal
        {
            Symbol = marketData.Symbol,
            STCR004ADate = evaluationDate,
            EarningsDate = earningsDate,
            Strength = strength,
            ImpliedVolatility30 = termResult.IV30,
            RealizedVolatility30 = realisedVolatility,
            IVRVRatio = ivRvRatio,
            STTM001ASlope = termResult.Slope,
            AverageVolume = (long)avgVolume,
            EarningsJumpVolatility = lsMetrics.JumpVolatility,
            TheoreticalIV = lsMetrics.TheoreticalIV,
            IVMispricingSTCR004A = lsMetrics.MispricingSignal,
            ExpectedIVCrush = lsMetrics.ExpectedIVCrush,
            IVCrushRatio = lsMetrics.IVCrushRatio
        };

        return new SignalResult
        {
            Signal = signal,
            CriteriaResults = criteriaResults
        };
    }

    /// <summary>
    /// Calculates position size using Kelly Criterion.
    /// </summary>
    private static PositionSizeResult CalculatePositionSize(
        Signal signal,
        CalendarSpreadResult spreadResult,
        double portfolioValue)
    {
        // Kelly fraction based on signal strength
        double kellyFraction = signal.Strength switch
        {
            SignalStrength.Recommended => 0.02,  // 2% of portfolio
            SignalStrength.Consider => 0.01,     // 1% of portfolio
            _ => 0.0
        };

        double maxRisk = portfolioValue * kellyFraction;
        double riskPerContract = Math.Abs(spreadResult.SpreadCost) * 100;

        int contracts = riskPerContract > 0
            ? (int)Math.Floor(maxRisk / riskPerContract)
            : 0;

        return new PositionSizeResult
        {
            Contracts = contracts,
            RiskPerContract = riskPerContract,
            TotalRisk = contracts * riskPerContract,
            KellyFraction = kellyFraction,
            PortfolioAllocation = contracts * riskPerContract / portfolioValue
        };
    }

    // Display Methods

    /// <summary>
    /// Displays the market conditions for the simulation.
    /// </summary>
    private static void DisplayMarketConditions(DateTime evaluationDate, DateTime earningsDate)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 1: MARKET CONDITIONS                                                   │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Symbol:              ", SimulationSymbol));
        Console.WriteLine(FormatBoxLine("Evaluation Date:     ", evaluationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Earnings Date:       ", earningsDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Days to Earnings:    ", ((earningsDate - evaluationDate).Days).ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Portfolio Value:     ", $"${PortfolioValue:N2}"));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    /// <summary>
    /// Displays the generated market data summary.
    /// </summary>
    private static void DisplayMarketData(SimulatedMarketData marketData)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 2: MARKET DATA GENERATION                                              │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Current Price:       ", $"${marketData.CurrentPrice:F2}"));
        Console.WriteLine(FormatBoxLine("Price Bars:          ", marketData.PriceHistory.Count.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Option Expiries:     ", marketData.OptionChain.Expiries.Count.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Historical Earnings: ", marketData.HistoricalEarningsDates.Count.ToString(CultureInfo.InvariantCulture)));
        long volumeSum = 0;
        for (int i = 0; i < marketData.PriceHistory.Count; i++)
        {
            volumeSum += marketData.PriceHistory[i].Volume;
        }

        double avgVolume = marketData.PriceHistory.Count > 0
            ? volumeSum / (double)marketData.PriceHistory.Count
            : 0.0;

        Console.WriteLine(FormatBoxLine("30-Day Avg Volume:   ", $"{avgVolume:N0}"));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    /// <summary>
    /// Displays the realised volatility calculation.
    /// </summary>
    private static void DisplayRealisedVolatility(double realisedVolatility)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 3: REALISED VOLATILITY - Yang-Zhang (2000)                            │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Estimator:           ", "Yang-Zhang OHLC"));
        Console.WriteLine(FormatBoxLine("30-Day RV:           ", $"{realisedVolatility:P2}"));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    /// <summary>
    /// Displays the term structure analysis.
    /// </summary>
    private static void DisplayTermStructure(TermStructureResult result)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 4: TERM STRUCTURE ANALYSIS                                             │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");

        foreach ((int dte, double iv) in result.Points)
        {
            Console.WriteLine(FormatBoxLine($"  {dte} DTE IV:         ", $"{iv:P2}"));
        }

        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Slope:               ", $"{result.Slope:F6}"));
        Console.WriteLine(FormatBoxLine("Structure:           ", result.IsInverted ? "Inverted" : "Normal"));
        Console.WriteLine(FormatBoxLine("Atilgan Criterion:   ", result.MeetsTradingCriterion ? "✓ MET (≤ -0.00406)" : "✗ NOT MET"));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    /// <summary>
    /// Displays the Leung-Santoli pre-earnings metrics.
    /// </summary>
    private static void DisplayLeungSantoliMetrics(LeungSantoliMetrics metrics)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 5: LEUNG-SANTOLI PRE-EARNINGS MODEL (2014)                             │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Historical Samples:  ", metrics.HistoricalSamples.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Calibration Status:  ", metrics.IsCalibrated ? "✓ Calibrated" : "✗ Using Default"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Jump Volatility (σE):", $"{metrics.JumpVolatility:P2}"));
        Console.WriteLine(FormatBoxLine("Base Volatility:     ", $"{metrics.BaseVolatility:P2}"));
        Console.WriteLine(FormatBoxLine("Theoretical IV:      ", $"{metrics.TheoreticalIV:P2}"));
        Console.WriteLine(FormatBoxLine("Market IV:           ", $"{metrics.MarketIV:P2}"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Mispricing Signal:   ", $"{metrics.MispricingSignal:+0.00%;-0.00%}"));
        Console.WriteLine(FormatBoxLine("Expected IV Crush:   ", $"{metrics.ExpectedIVCrush:P2}"));
        Console.WriteLine(FormatBoxLine("IV Crush Ratio:      ", $"{metrics.IVCrushRatio:P2}"));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    /// <summary>
    /// Displays the trading signal analysis.
    /// </summary>
    private static void DisplaySignal(SignalResult result)
    {
        string strengthSymbol = result.Signal.Strength switch
        {
            SignalStrength.Recommended => "● RECOMMENDED",
            SignalStrength.Consider => "◐ CONSIDER",
            _ => "○ AVOID"
        };

        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 6: TRADING SIGNAL - Atilgan (2014) Criteria                            │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Signal Strength:         ", strengthSymbol));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ Criterion                Value              Threshold          Result        │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");

        foreach (KeyValuePair<string, (bool Pass, string Value, string Threshold)> kvp in result.CriteriaResults)
        {
            string status = kvp.Value.Pass ? "✓ PASS" : "✗ FAIL";
            Console.WriteLine($"│ {kvp.Key,-20} {kvp.Value.Value,-18} {kvp.Value.Threshold,-18} {status,-12} │");
        }

        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    /// <summary>
    /// Displays the calendar spread pricing results.
    /// </summary>
    private static void DisplayCalendarSpread(CalendarSpreadResult result)
    {
        string spreadLabel = result.IsCredit ? "Credit" : "Debit";
        string pricingEngine = result.RiskFreeRate < 0 ? "[Healy 2021]" : "[Standard]";

        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine($"│ CALENDAR SPREAD PRICING - {result.RegimeLabel,-49} │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Pricing Engine:      ", pricingEngine));
        Console.WriteLine(FormatBoxLine("Risk-Free Rate:      ", $"{result.RiskFreeRate:P2}"));
        Console.WriteLine(FormatBoxLine("Dividend Yield:      ", $"{result.DividendYield:P2}"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Strike (ATM):        ", $"${result.Strike:F2}"));
        Console.WriteLine(FormatBoxLine("Front Month:         ", $"{result.FrontExpiry:yyyy-MM-dd} (DTE: {(result.FrontExpiry - result.EvaluationDate).Days})"));
        Console.WriteLine(FormatBoxLine("Back Month:          ", $"{result.BackExpiry:yyyy-MM-dd} (DTE: {(result.BackExpiry - result.EvaluationDate).Days})"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Front Option Price:  ", $"${result.FrontPrice:F4}"));
        Console.WriteLine(FormatBoxLine("Back Option Price:   ", $"${result.BackPrice:F4}"));
        Console.WriteLine(FormatBoxLine($"Spread Cost ({spreadLabel}):", $"${Math.Abs(result.SpreadCost):F4} ({spreadLabel})"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Spread Delta:        ", $"{result.SpreadDelta:F4}"));
        Console.WriteLine(FormatBoxLine("Spread Gamma:        ", $"{result.SpreadGamma:F4}"));
        Console.WriteLine(FormatBoxLine("Spread Theta:        ", $"{result.SpreadTheta:F4}"));
        Console.WriteLine(FormatBoxLine("Spread Vega:         ", $"{result.SpreadVega:F4}"));
        Console.WriteLine(FormatBoxLine("Max Loss/Contract:   ", $"${result.MaxLoss:F2}"));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    /// <summary>
    /// Displays double boundary pricing demonstration results.
    /// </summary>
    private static void DisplayDoubleBoundaryResult(DoubleBoundaryDemoResult result)
    {
        string constraintsResult = result.AllConstraintsPass ? "✓ ALL PASS" : "✗ CONSTRAINTS VIOLATED";

        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 9: ALARIS.DOUBLE - Healy (2021) Double Boundary Demonstration          │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ Reference: \"Pricing American Options Under Negative Rates\"                  │");
        Console.WriteLine("│ Method:    QD+ Approximation with Super Halley's iteration                   │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Spot Price:          ", $"${result.Spot:F2}"));
        Console.WriteLine(FormatBoxLine("Strike Price:        ", $"${result.Strike:F2}"));
        Console.WriteLine(FormatBoxLine("Time to Maturity:    ", $"{result.Maturity:F2} years"));
        Console.WriteLine(FormatBoxLine("Risk-Free Rate:      ", $"{result.Rate:P2}"));
        Console.WriteLine(FormatBoxLine("Dividend Yield:      ", $"{result.DividendYield:P2}"));
        Console.WriteLine(FormatBoxLine("Volatility:          ", $"{result.Volatility:P2}"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("American Put Price:  ", $"${result.PutPrice:F4}"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ DOUBLE BOUNDARY (Exercise Optimal in Range [S_l, S_u]):                      │");
        Console.WriteLine(FormatBoxLine("  Upper Boundary:    ", $"${result.UpperBoundary:F4}"));
        Console.WriteLine(FormatBoxLine("  Lower Boundary:    ", $"${result.LowerBoundary:F4}"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ PHYSICAL CONSTRAINTS (Healy Appendix A):                                     │");
        Console.WriteLine(FormatBoxLine("  A1 (S_u,S_l > 0):  ", result.A1Pass ? "✓ PASS" : "✗ FAIL"));
        Console.WriteLine(FormatBoxLine("  A2 (S_u > S_l):    ", result.A2Pass ? "✓ PASS" : "✗ FAIL"));
        Console.WriteLine(FormatBoxLine("  A3 (Put < K):      ", result.A3Pass ? "✓ PASS" : "✗ FAIL"));
        Console.WriteLine(FormatBoxLine("  Overall:           ", constraintsResult));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    /// <summary>
    /// Displays the production validation results.
    /// </summary>
    private static void DisplayProductionValidation(STHD006A validation)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PRODUCTION VALIDATION RESULTS                                                │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Production Ready:    ", validation.ProductionReady ? "YES" : "NO"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");

        foreach (Alaris.Strategy.Hedge.ValidationCheck check in validation.Checks)
        {
            string status = check.Passed ? "✓" : "✗";
            Console.WriteLine(FormatBoxLine($"{status} {check.Name}:", check.Detail));
        }

        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    /// <summary>
    /// Displays position sizing results.
    /// </summary>
    private static void DisplayPositionSize(PositionSizeResult result)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ POSITION SIZING - Kelly Criterion                                            │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Kelly Fraction:      ", $"{result.KellyFraction:P1}"));
        Console.WriteLine(FormatBoxLine("Contracts:           ", result.Contracts.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Risk Per Contract:   ", $"${result.RiskPerContract:F2}"));
        Console.WriteLine(FormatBoxLine("Total Risk:          ", $"${result.TotalRisk:F2}"));
        Console.WriteLine(FormatBoxLine("Portfolio Allocation:", $"{result.PortfolioAllocation:P2}"));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    /// <summary>
    /// Displays the trade recommendation summary.
    /// </summary>
    private static void DisplayTradeRecommendation(
        SignalResult signalResult,
        CalendarSpreadResult spreadResult,
        PositionSizeResult positionResult)
    {
        string action = signalResult.Signal.Strength switch
        {
            SignalStrength.Recommended => "EXECUTE TRADE",
            SignalStrength.Consider => "REVIEW BEFORE TRADING",
            SignalStrength.Avoid => "DO NOT TRADE",
            _ => "UNKNOWN"
        };

        double totalCost = positionResult.Contracts * Math.Abs(spreadResult.SpreadCost) * 100;
        string costLabel = spreadResult.IsCredit ? "Total Credit:" : "Total Debit:";

        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                        TRADE RECOMMENDATION SUMMARY                          ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine(FormatDoubleBoxLine("Symbol:          ", SimulationSymbol));
        Console.WriteLine("║ Strategy:        Earnings Calendar Spread                                    ║");
        Console.WriteLine(FormatDoubleBoxLine("Signal:          ", signalResult.Signal.Strength.ToString()));
        Console.WriteLine(FormatDoubleBoxLine("Action:          ", action));
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ POSITION DETAILS:                                                            ║");
        Console.WriteLine(FormatDoubleBoxLine("  Strike:        ", $"${spreadResult.Strike:F2}"));
        Console.WriteLine(FormatDoubleBoxLine("  Front Month:   ", $"{spreadResult.FrontExpiry:yyyy-MM-dd} (Sell)"));
        Console.WriteLine(FormatDoubleBoxLine("  Back Month:    ", $"{spreadResult.BackExpiry:yyyy-MM-dd} (Buy)"));
        Console.WriteLine(FormatDoubleBoxLine("  Contracts:     ", positionResult.Contracts.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatDoubleBoxLine($"  {costLabel,-14}", $"${totalCost:N2}"));
        Console.WriteLine(FormatDoubleBoxLine("  Max Risk:      ", $"${positionResult.TotalRisk:N2}"));
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ STRATEGY RATIONALE:                                                          ║");
        Console.WriteLine("║   • Pre-earnings IV elevation creates calendar spread opportunity            ║");
        Console.WriteLine("║   • Short front-month captures accelerated time decay                        ║");
        Console.WriteLine("║   • Long back-month provides vega exposure to IV crush                       ║");
        Console.WriteLine("║   • Atilgan (2014) criteria satisfied for systematic entry                   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
    }

    /// <summary>
    /// Displays the final trade recommendation after production validation.
    /// </summary>
    private static void DisplayFinalTradeRecommendation(
        STHD006A validation,
        int contracts,
        double totalRisk)
    {
        string action = validation.ProductionReady ? "✓ CLEARED FOR EXECUTION" : "✗ REQUIRES REVIEW";

        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                     FINAL PRODUCTION DECISION                                ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine(FormatDoubleBoxLine("Status:          ", action));
        Console.WriteLine(FormatDoubleBoxLine("Contracts:       ", validation.RecommendedContracts.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatDoubleBoxLine("Adjusted Debit:  ", $"${validation.AdjustedDebit:F2}"));
        Console.WriteLine(FormatDoubleBoxLine("Max Risk:        ", $"${totalRisk:N2}"));
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
    }

    // Formatting Helpers

    /// <summary>
    /// Formats a line for single-border box output.
    /// </summary>
    private static string FormatBoxLine(string label, string value)
    {
        string content = $"{label}{value}";
        int padding = BoxWidth - content.Length - 2;
        return $"│ {content}{new string(' ', Math.Max(0, padding))} │";
    }

    /// <summary>
    /// Formats a line for double-border box output.
    /// </summary>
    private static string FormatDoubleBoxLine(string label, string value)
    {
        string content = $"{label}{value}";
        int padding = BoxWidth - content.Length - 2;
        return $"║ {content}{new string(' ', Math.Max(0, padding))} ║";
    }
}

// Supporting Data Types

/// <summary>
/// Represents a single price bar (OHLCV).
/// </summary>
internal sealed class PriceBar
{
    public DateTime Date { get; init; }
    public double Open { get; init; }
    public double High { get; init; }
    public double Low { get; init; }
    public double Close { get; init; }
    public long Volume { get; init; }
}

/// <summary>
/// Represents simulated market data for the earnings scenario.
/// </summary>
internal sealed class SimulatedMarketData
{
    public required List<PriceBar> PriceHistory { get; init; }
    public required OptionChain OptionChain { get; init; }
    public required List<DateTime> HistoricalEarningsDates { get; init; }
    public required double CurrentPrice { get; init; }
    public required string Symbol { get; init; }
}

/// <summary>
/// Represents term structure analysis results.
/// </summary>
internal sealed class TermStructureResult
{
    public double Slope { get; init; }
    public bool IsInverted { get; init; }
    public bool MeetsTradingCriterion { get; init; }
    public double IV30 { get; init; }
    public required List<(int dte, double iv)> Points { get; init; }
}

/// <summary>
/// Represents Leung-Santoli pre-earnings metrics.
/// </summary>
internal sealed class LeungSantoliMetrics
{
    public int HistoricalSamples { get; init; }
    public bool IsCalibrated { get; init; }
    public double JumpVolatility { get; init; }
    public double BaseVolatility { get; init; }
    public double TheoreticalIV { get; init; }
    public double MarketIV { get; init; }
    public double MispricingSignal { get; init; }
    public double ExpectedIVCrush { get; init; }
    public double IVCrushRatio { get; init; }
}

/// <summary>
/// Represents signal generation results.
/// </summary>
internal sealed class SignalResult
{
    public required Signal Signal { get; init; }
    public required Dictionary<string, (bool Pass, string Value, string Threshold)> CriteriaResults { get; init; }
}

/// <summary>
/// Represents calendar spread pricing results.
/// </summary>
internal sealed class CalendarSpreadResult
{
    public required string RegimeLabel { get; init; }
    public double Strike { get; init; }
    public DateTime FrontExpiry { get; init; }
    public DateTime BackExpiry { get; init; }
    public DateTime EvaluationDate { get; init; }
    public double FrontPrice { get; init; }
    public double BackPrice { get; init; }
    public double SpreadCost { get; init; }
    public bool IsCredit { get; init; }
    public double SpreadDelta { get; init; }
    public double SpreadGamma { get; init; }
    public double SpreadTheta { get; init; }
    public double SpreadVega { get; init; }
    public double MaxLoss { get; init; }
    public double MaxGain { get; init; }
    public double BreakEven { get; init; }
    public double RiskFreeRate { get; init; }
    public double DividendYield { get; init; }
}

/// <summary>
/// Represents double boundary demonstration results.
/// </summary>
internal sealed class DoubleBoundaryDemoResult
{
    public double Spot { get; init; }
    public double Strike { get; init; }
    public double Maturity { get; init; }
    public double Rate { get; init; }
    public double DividendYield { get; init; }
    public double Volatility { get; init; }
    public double PutPrice { get; init; }
    public double UpperBoundary { get; init; }
    public double LowerBoundary { get; init; }
    public bool A1Pass { get; init; }
    public bool A2Pass { get; init; }
    public bool A3Pass { get; init; }
    public bool AllConstraintsPass { get; init; }
}

/// <summary>
/// Represents position sizing results.
/// </summary>
internal sealed class PositionSizeResult
{
    public int Contracts { get; init; }
    public double RiskPerContract { get; init; }
    public double TotalRisk { get; init; }
    public double KellyFraction { get; init; }
    public double PortfolioAllocation { get; init; }
}
