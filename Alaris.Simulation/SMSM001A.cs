// ============================================================================
// Alaris.Simulation - SMSM001A.cs
// Quarterly Earnings Announcement Simulation
// ============================================================================
// 
// Component Code: SMSM001A
// Domain:         SM (Simulation)
// Category:       SM (Simulation Main)
// Sequence:       001
// Variant:        A (Production Implementation)
//
// Purpose:
//   Demonstrates the complete Alaris system workflow during a quarterly earnings
//   announcement, integrating:
//   - Alaris.Double: Negative rate American option pricing (Healy 2021)
//   - Alaris.Strategy: Earnings volatility calendar spread strategy (Atilgan 2014)
//   - Alaris.Strategy.Cost: Execution cost modeling and liquidity validation
//   - Alaris.Strategy.Hedge: Risk validation (Vega correlation, Gamma, Gap risk)
//   - Leung & Santoli (2014): Pre-EA implied volatility model with jump calibration
//   - Yang-Zhang (2000): Realized volatility estimation
//   - Kelly Criterion: Fractional position sizing
//
// Academic References:
//   - Healy (2021): "Pricing American Options Under Negative Rates"
//   - Atilgan (2014): "Implied Volatility Spreads and Expected Market Returns"
//   - Leung & Santoli (2014): "Accounting for Earnings Announcements in the
//                              Pricing of Equity Options"
//   - Yang & Zhang (2000): "Drift-Independent Volatility Estimation"
//   - Kim (1990): "The Analytic Valuation of American Options"
//
// Compliance:
//   Alaris High-Integrity Coding Standard v1.2
//   - Rule 4:  No Recursion
//   - Rule 7:  Null Safety
//   - Rule 9:  Guard Clauses
//   - Rule 10: Specific Exceptions
//   - Rule 13: Function Complexity (60 lines max)
//   - Rule 15: Fault Isolation (SafeLog pattern)
//   - Rule 16: Deterministic Cleanup
//
// Analyzer Suppressions:
//   - CA1303: Literal strings acceptable for console simulation output
//   - CA5394: Deterministic Random acceptable for reproducible simulation
//   - CA1031: Generic exception catch acceptable in Main entry point
//
// ============================================================================

using Alaris.Double;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Core;
using Alaris.Strategy.Cost;
using Alaris.Strategy.Hedge;
using Alaris.Strategy.Model;
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
    // ========================================================================
    // Simulation Configuration Constants
    // ========================================================================

    /// <summary>Simulation symbol for the earnings announcement.</summary>
    private const string SimulationSymbol = "AAPL";

    /// <summary>Portfolio value for position sizing calculations.</summary>
    private const double PortfolioValue = 100_000.00;

    /// <summary>Risk-free rate for standard pricing (positive rate regime).</summary>
    private const double PositiveRiskFreeRate = 0.0525; // 5.25%

    /// <summary>Risk-free rate for negative rate regime demonstration.</summary>
    private const double NegativeRiskFreeRate = -0.005; // -0.50%

    /// <summary>Dividend yield for negative rate double boundary (q &lt; r).</summary>
    private const double NegativeRateDividendYield = -0.010; // -1.00%

    /// <summary>Trading days per year for annualisation.</summary>
    private const double TradingDaysPerYear = 252.0;

    /// <summary>Box width for console output formatting.</summary>
    private const int BoxWidth = 78;

    // ========================================================================
    // Entry Point
    // ========================================================================

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

    // ========================================================================
    // Simulation Orchestration
    // ========================================================================

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

        // Phase 2: Generate simulated market data
        SimulatedMarketData marketData = GenerateMarketData(evaluationDate, earningsDate);
        DisplayMarketData(marketData);

        // Phase 3: Calculate realised volatility (Yang-Zhang)
        double realisedVolatility = CalculateRealisedVolatility(marketData.PriceHistory);
        DisplayRealisedVolatility(realisedVolatility);

        // Phase 4: Analyse term structure
        TermStructureResult termResult = AnalyseTermStructure(marketData.OptionChain, evaluationDate);
        DisplayTermStructure(termResult);

        // Phase 5: Calculate Leung-Santoli metrics
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

        // Phase 8: Price calendar spread (negative rates - Healy 2021)
        CalendarSpreadResult negativeRateSpread = PriceCalendarSpread(
            marketData, evaluationDate, NegativeRiskFreeRate, NegativeRateDividendYield,
            "Negative Rate Regime (Healy 2021)");
        DisplayCalendarSpread(negativeRateSpread);

        // Phase 9: Demonstrate Alaris.Double pricing
        DoubleBoundaryDemoResult doubleBoundary = DemonstrateDoubleBoundaryPricing();
        DisplayDoubleBoundaryResult(doubleBoundary);

        // Phase 10: Production Validation (Cost & Hedge)
        // Only proceed if signal is recommended
        if (signalResult.Signal.Strength == SignalStrength.Recommended)
        {
            RunProductionValidation(
                loggerFactory,
                signalResult.Signal,
                marketData,
                positiveRateSpread,
                earningsDate,
                evaluationDate);
        }
    }

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
        Console.WriteLine("│ PHASE 10: PRODUCTION VALIDATION (Cost & Hedge Integration)                  │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");

        // Initialize validators
        var costModel = new STCS005A(logger: loggerFactory.CreateLogger<STCS005A>());
        var costValidator = new STCS006A(costModel, logger: loggerFactory.CreateLogger<STCS006A>());
        var liquidityValidator = new STCS008A(logger: loggerFactory.CreateLogger<STCS008A>());
        var vegaAnalyser = new STHD001A(logger: loggerFactory.CreateLogger<STHD001A>());
        var gammaManager = new STHD003A(logger: loggerFactory.CreateLogger<STHD003A>());
        var productionValidator = new STHD005A(
            costValidator,
            vegaAnalyser,
            liquidityValidator,
            gammaManager,
            loggerFactory.CreateLogger<STHD005A>());

        // Extract option contracts
        OptionExpiry frontExpiry = marketData.OptionChain.Expiries[0];
        OptionExpiry backExpiry = marketData.OptionChain.Expiries[2];
        double strike = pricingResult.Strike;

        OptionContract frontCall = frontExpiry.Calls.First(c => c.Strike == strike);
        OptionContract backCall = backExpiry.Calls.First(c => c.Strike == strike);

        // Prepare order parameters
        var frontLegParams = new STCS002A
        {
            Symbol = SimulationSymbol,
            Contracts = 1,
            Direction = OrderDirection.Sell,
            BidPrice = frontCall.Bid,
            AskPrice = frontCall.Ask,
            MidPrice = frontCall.LastPrice,
            Premium = frontCall.LastPrice
        };

        var backLegParams = new STCS002A
        {
            Symbol = SimulationSymbol,
            Contracts = 1,
            Direction = OrderDirection.Buy,
            BidPrice = backCall.Bid,
            AskPrice = backCall.Ask,
            MidPrice = backCall.LastPrice,
            Premium = backCall.LastPrice
        };

        // Prepare spread Greeks
        var spreadGreeks = new SpreadGreeks
        {
            Delta = pricingResult.SpreadDelta,
            Gamma = pricingResult.SpreadGamma,
            Vega = pricingResult.SpreadVega,
            Theta = pricingResult.SpreadTheta
        };

        // Generate synthetic IV history for vega correlation analysis
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
        DisplayTradeRecommendation(signalResult: new SignalResult
        {
            Signal = signal,
            CriteriaResults = new Dictionary<string, (bool Pass, string Value, string Threshold)>()
        }, pricingResult, positionResult);

        // Display final production decision
        DisplayFinalTradeRecommendation(validation, positionResult.Contracts, positionResult.TotalRisk);
    }

    /// <summary>
    /// Generates synthetic IV history for correlation analysis.
    /// </summary>
    private static (List<double> FrontIVHistory, List<double> BackIVHistory) GenerateSyntheticIVHistory(int days)
    {
        var random = new Random(42); // Deterministic seed
        var frontIV = new List<double>();
        var backIV = new List<double>();

        double baseFrontIV = 0.25;
        double baseBackIV = 0.22;
        double correlation = 0.85;

        for (int i = 0; i < days; i++)
        {
            double commonShock = (random.NextDouble() * 0.04) - 0.02;
            double frontIdiosyncratic = (random.NextDouble() * 0.02) - 0.01;
            double backIdiosyncratic = (random.NextDouble() * 0.02) - 0.01;

            frontIV.Add(baseFrontIV + (correlation * commonShock) + frontIdiosyncratic);
            backIV.Add(baseBackIV + (correlation * commonShock) + backIdiosyncratic);
        }

        return (frontIV, backIV);
    }

    // ========================================================================
    // Phase 1: Market Conditions Display
    // ========================================================================

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

    // ========================================================================
    // Phase 2: Market Data Generation
    // ========================================================================

    /// <summary>
    /// Generates simulated market data for the earnings scenario.
    /// </summary>
    private static SimulatedMarketData GenerateMarketData(DateTime evaluationDate, DateTime earningsDate)
    {
        var random = new Random(42); // Deterministic seed for reproducible simulation

        // Generate 60 days of price history
        var priceHistory = new List<PriceBar>();
        double currentPrice = 150.00;
        DateTime startDate = evaluationDate.AddDays(-60);

        for (int i = 0; i < 60; i++)
        {
            DateTime date = startDate.AddDays(i);
            double open = currentPrice + ((random.NextDouble() * 4) - 2);
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

        // Generate option chain
        OptionChain optionChain = GenerateOptionChain(evaluationDate, currentPrice);

        // Generate historical earnings dates
        var historicalEarnings = new List<DateTime>
        {
            earningsDate.AddMonths(-3),
            earningsDate.AddMonths(-6),
            earningsDate.AddMonths(-9),
            earningsDate.AddMonths(-12)
        };

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
    /// Generates a simulated option chain with realistic IV skew.
    /// </summary>
    private static OptionChain GenerateOptionChain(DateTime evaluationDate, double spotPrice)
    {
        var expiries = new List<OptionExpiry>();
        var random = new Random(42);

        // Generate 3 monthly expiries
        for (int monthOffset = 1; monthOffset <= 3; monthOffset++)
        {
            DateTime expiryDate = evaluationDate.AddDays(monthOffset * 30);
            var calls = new List<OptionContract>();
            var puts = new List<OptionContract>();

            // Generate strikes around ATM
            for (int strikeOffset = -2; strikeOffset <= 2; strikeOffset++)
            {
                double strike = Math.Round(spotPrice + (strikeOffset * 5), 2);

                // IV increases with time and has slight skew
                double baseIV = 0.20 + (monthOffset * 0.02);
                double skew = strikeOffset * -0.005; // OTM puts have higher IV
                double iv = baseIV + skew + ((random.NextDouble() * 0.02) - 0.01);

                // Generate call
                double callTheo = SimulateOptionPrice(spotPrice, strike, monthOffset * 30, 0.05, 0.0, iv, true).Price;
                OptionContract call = new OptionContract
                {
                    Strike = strike,
                    ImpliedVolatility = iv,
                    Bid = callTheo - 0.10,
                    Ask = callTheo + 0.10,
                    LastPrice = callTheo,
                    Volume = random.Next(500, 5000),
                    OpenInterest = random.Next(1000, 10000)
                };
                calls.Add(call);

                // Generate put
                double putTheo = SimulateOptionPrice(spotPrice, strike, monthOffset * 30, 0.05, 0.0, iv, false).Price;
                OptionContract put = new OptionContract
                {
                    Strike = strike,
                    ImpliedVolatility = iv,
                    Bid = putTheo - 0.10,
                    Ask = putTheo + 0.10,
                    LastPrice = putTheo,
                    Volume = random.Next(500, 5000),
                    OpenInterest = random.Next(1000, 10000)
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

    /// <summary>
    /// Displays the generated market data summary.
    /// </summary>
    private static void DisplayMarketData(SimulatedMarketData data)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 2: MARKET DATA GENERATION                                              │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Current Price:       ", $"${data.CurrentPrice:F2}"));
        Console.WriteLine(FormatBoxLine("Price Bars:          ", data.PriceHistory.Count.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Option Expiries:     ", data.OptionChain.Expiries.Count.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Historical Earnings: ", data.HistoricalEarningsDates.Count.ToString(CultureInfo.InvariantCulture)));

        double avgVolume = data.PriceHistory.TakeLast(30).Average(p => p.Volume);
        Console.WriteLine(FormatBoxLine("30-Day Avg Volume:   ", $"{avgVolume:N0}"));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    // ========================================================================
    // Phase 3: Realised Volatility (Yang-Zhang)
    // ========================================================================

    /// <summary>
    /// Calculates realised volatility using Yang-Zhang (2000) estimator.
    /// </summary>
    private static double CalculateRealisedVolatility(List<PriceBar> bars)
    {
        int n = bars.Count;
        if (n < 2)
        {
            throw new ArgumentException("Need at least 2 bars for Yang-Zhang volatility", nameof(bars));
        }

        // Overnight volatility
        double sumOvernight = 0;
        for (int i = 1; i < n; i++)
        {
            double u = Math.Log(bars[i].Open / bars[i - 1].Close);
            sumOvernight += u * u;
        }
        double sigmaO = Math.Sqrt(sumOvernight / (n - 1));

        // Open-to-close volatility
        double sumOC = 0;
        for (int i = 0; i < n; i++)
        {
            double u = Math.Log(bars[i].Close / bars[i].Open);
            sumOC += u * u;
        }
        double sigmaC = Math.Sqrt(sumOC / n);

        // Rogers-Satchell volatility
        double sumRS = 0;
        for (int i = 0; i < n; i++)
        {
            double hl = Math.Log(bars[i].High / bars[i].Low);
            double hc = Math.Log(bars[i].High / bars[i].Close);
            double lc = Math.Log(bars[i].Low / bars[i].Close);
            sumRS += hl * (hc + lc);
        }
        double sigmaRS = Math.Sqrt(sumRS / n);

        // Yang-Zhang: combines overnight, open-close, and high-low estimators
        double k = 0.34 / (1.34 + ((n + 1.0) / (n - 1.0)));
        double sigmaYZ = Math.Sqrt((sigmaO * sigmaO) + (k * sigmaC * sigmaC) + ((1 - k) * sigmaRS * sigmaRS));

        // Annualise
        return sigmaYZ * Math.Sqrt(TradingDaysPerYear);
    }

    /// <summary>
    /// Displays the realised volatility calculation.
    /// </summary>
    private static void DisplayRealisedVolatility(double rv)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 3: REALISED VOLATILITY - Yang-Zhang (2000)                            │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Estimator:           ", "Yang-Zhang OHLC"));
        Console.WriteLine(FormatBoxLine("30-Day RV:           ", $"{rv:P2}"));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    // ========================================================================
    // Phase 4: Term Structure Analysis
    // ========================================================================

    /// <summary>
    /// Analyses the implied volatility term structure.
    /// </summary>
    private static TermStructureResult AnalyseTermStructure(OptionChain chain, DateTime evalDate)
    {
        var points = new List<(int dte, double iv)>();

        foreach (OptionExpiry expiry in chain.Expiries)
        {
            int dte = (int)(expiry.ExpiryDate - evalDate).TotalDays;

            // Get ATM IV (closest to spot)
            OptionContract atmCall = expiry.Calls
                .OrderBy(c => Math.Abs(c.Strike - chain.UnderlyingPrice))
                .First();

            points.Add((dte, atmCall.ImpliedVolatility));
        }

        // Calculate slope using linear regression
        double n = points.Count;
        double sumX = points.Sum(p => (double)p.dte);
        double sumY = points.Sum(p => p.iv);
        double sumXY = points.Sum(p => p.dte * p.iv);
        double sumX2 = points.Sum(p => p.dte * p.dte);

        double slope = ((n * sumXY) - (sumX * sumY)) / ((n * sumX2) - (sumX * sumX));
        bool isInverted = slope < 0;
        bool meetsCriterion = slope <= -0.00406;

        // 30-day IV
        double iv30 = points.First(p => p.dte >= 25 && p.dte <= 35).iv;

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

    // ========================================================================
    // Phase 5: Leung-Santoli Model
    // ========================================================================

    /// <summary>
    /// Calculates Leung-Santoli pre-earnings IV metrics.
    /// </summary>
    private static LeungSantoliMetrics CalculateLeungSantoliMetrics(
        SimulatedMarketData data,
        DateTime earningsDate,
        DateTime evalDate,
        double realisedVol,
        TermStructureResult termResult)
    {
        // Calibrate jump volatility from historical earnings
        int historicalCount = data.HistoricalEarningsDates.Count;
        var jumps = new List<double>();

        foreach (DateTime pastEarnings in data.HistoricalEarningsDates)
        {
            // Find price bars around earnings
            PriceBar? before = data.PriceHistory
                .Where(b => b.Date < pastEarnings)
                .OrderByDescending(b => b.Date)
                .FirstOrDefault();

            PriceBar? after = data.PriceHistory
                .Where(b => b.Date >= pastEarnings)
                .OrderBy(b => b.Date)
                .FirstOrDefault();

            if (before != null && after != null)
            {
                double jump = Math.Log(after.Open / before.Close);
                jumps.Add(jump);
            }
        }

        double sigmaE = jumps.Count > 0
            ? Math.Sqrt(jumps.Sum(j => j * j) / jumps.Count) * Math.Sqrt(TradingDaysPerYear)
            : 0.10; // Default if no data

        // Base volatility (ex-jump)
        double baseVol = realisedVol;

        // Time to earnings
        double timeToEarnings = (earningsDate - evalDate).TotalDays / TradingDaysPerYear;

        // Theoretical IV from Leung-Santoli model
        double theoreticalIV = Math.Sqrt((baseVol * baseVol) + (sigmaE * sigmaE * timeToEarnings));

        // Market IV
        double marketIV = termResult.IV30;

        // Mispricing signal
        double mispricingSignal = marketIV - theoreticalIV;

        // Expected IV crush (post-earnings)
        double expectedIVCrush = sigmaE * Math.Sqrt(timeToEarnings);
        double ivCrushRatio = expectedIVCrush / marketIV;

        return new LeungSantoliMetrics
        {
            SigmaE = sigmaE,
            BaseVolatility = baseVol,
            TheoreticalIV = theoreticalIV,
            MarketIV = marketIV,
            MispricingSignal = mispricingSignal,
            ExpectedIVCrush = expectedIVCrush,
            IVCrushRatio = ivCrushRatio,
            HistoricalSamples = jumps.Count,
            IsCalibrated = jumps.Count >= 3
        };
    }

    /// <summary>
    /// Displays the Leung-Santoli model results.
    /// </summary>
    private static void DisplayLeungSantoliMetrics(LeungSantoliMetrics metrics)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 5: LEUNG-SANTOLI PRE-EARNINGS MODEL (2014)                            │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Historical Samples:  ", metrics.HistoricalSamples.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Calibration Status:  ", metrics.IsCalibrated ? "✓ Calibrated" : "✗ Insufficient Data"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Jump Volatility (σE):", $"{metrics.SigmaE:P2}"));
        Console.WriteLine(FormatBoxLine("Base Volatility:     ", $"{metrics.BaseVolatility:P2}"));
        Console.WriteLine(FormatBoxLine("Theoretical IV:      ", $"{metrics.TheoreticalIV:P2}"));
        Console.WriteLine(FormatBoxLine("Market IV:           ", $"{metrics.MarketIV:P2}"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Mispricing Signal:   ", $"{metrics.MispricingSignal:+0.00%;-0.00%;0.00%}"));
        Console.WriteLine(FormatBoxLine("Expected IV Crush:   ", $"{metrics.ExpectedIVCrush:P2}"));
        Console.WriteLine(FormatBoxLine("IV Crush Ratio:      ", $"{metrics.IVCrushRatio:P2}"));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    // ========================================================================
    // Phase 6: Signal Generation
    // ========================================================================

    /// <summary>
    /// Generates a trading signal based on Atilgan (2014) criteria.
    /// </summary>
    private static SignalResult GenerateSignal(
        SimulatedMarketData data,
        double realisedVol,
        TermStructureResult termResult,
        LeungSantoliMetrics lsMetrics,
        DateTime earningsDate,
        DateTime evaluationDate)
    {
        double ivRvRatio = termResult.IV30 / realisedVol;
        double termSlope = termResult.Slope;
        long avgVolume = (long)data.PriceHistory.TakeLast(30).Average(p => p.Volume);

        bool ivRvPass = ivRvRatio >= 1.25;
        bool termSlopePass = termSlope <= -0.00406;
        bool volumePass = avgVolume >= 1_500_000;

        SignalStrength strength = (ivRvPass, termSlopePass, volumePass) switch
        {
            (true, true, true) => SignalStrength.Recommended,
            (true, _, true) or (_, true, true) => SignalStrength.Consider,
            _ => SignalStrength.Avoid
        };

        return new SignalResult
        {
            Signal = new Signal
            {
                Symbol = data.Symbol,
                Strength = strength,
                IVRVRatio = ivRvRatio,
                STTM001ASlope = termSlope,
                AverageVolume = avgVolume,
                ImpliedVolatility30 = termResult.IV30,
                RealizedVolatility30 = realisedVol,
                EarningsDate = earningsDate,
                STCR004ADate = evaluationDate,
                EarningsJumpVolatility = lsMetrics.SigmaE,
                TheoreticalIV = lsMetrics.TheoreticalIV,
                IVMispricingSTCR004A = lsMetrics.MispricingSignal,
                ExpectedIVCrush = lsMetrics.ExpectedIVCrush,
                IVCrushRatio = lsMetrics.IVCrushRatio,
                BaseVolatility = lsMetrics.BaseVolatility,
                HistoricalEarningsCount = lsMetrics.HistoricalSamples,
                IsLeungSantoliCalibrated = lsMetrics.IsCalibrated
            },
            CriteriaResults = new Dictionary<string, (bool Pass, string Value, string Threshold)>
            {
                ["IV/RV Ratio"] = (ivRvPass, $"{ivRvRatio:F3}", "≥ 1.250"),
                ["Term Slope"] = (termSlopePass, $"{termSlope:F6}", "≤ -0.00406"),
                ["Avg Volume"] = (volumePass, $"{avgVolume:N0}", "≥ 1,500,000")
            }
        };
    }

    /// <summary>
    /// Displays the trading signal and criteria evaluation.
    /// </summary>
    private static void DisplaySignal(SignalResult result)
    {
        string strengthDisplay = result.Signal.Strength switch
        {
            SignalStrength.Recommended => "★ RECOMMENDED ★",
            SignalStrength.Consider => "◐ CONSIDER",
            SignalStrength.Avoid => "✗ AVOID",
            _ => "UNKNOWN"
        };

        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 6: TRADING SIGNAL - Atilgan (2014) Criteria                           │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Signal Strength:         ", strengthDisplay));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ Criterion                Value              Threshold          Result       │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");

        foreach (KeyValuePair<string, (bool Pass, string Value, string Threshold)> kvp in result.CriteriaResults)
        {
            string passStr = kvp.Value.Pass ? "✓ PASS" : "✗ FAIL";
            string line = $"{kvp.Key,-22} {kvp.Value.Value,-18} {kvp.Value.Threshold,-18} {passStr}";
            Console.WriteLine(FormatBoxLine("", line));
        }

        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    // ========================================================================
    // Phase 7-8: Calendar Spread Pricing
    // ========================================================================

    /// <summary>
    /// Prices a calendar spread using the appropriate pricing engine.
    /// </summary>
    /// <remarks>
    /// This method is synchronous as it performs all calculations inline without
    /// I/O or true async operations. The async keyword has been removed to comply
    /// with CS1998 (async method lacks await operators).
    /// </remarks>
    private static CalendarSpreadResult PriceCalendarSpread(
        SimulatedMarketData data,
        DateTime evaluationDate,
        double riskFreeRate,
        double dividendYield,
        string regimeName)
    {
        List<OptionExpiry> sortedExpiries = data.OptionChain.Expiries
            .OrderBy(e => e.ExpiryDate)
            .ToList();

        if (sortedExpiries.Count < 2)
        {
            throw new InvalidOperationException("Insufficient option expiries for calendar spread");
        }

        OptionExpiry frontExpiry = sortedExpiries[0];
        OptionExpiry backExpiry = sortedExpiries[2];

        double atmStrike = sortedExpiries.First().Calls
            .OrderBy(c => Math.Abs(c.Strike - data.CurrentPrice))
            .First().Strike;

        OptionContract? frontCall = frontExpiry.Calls.FirstOrDefault(c => c.Strike == atmStrike);
        OptionContract? backCall = backExpiry.Calls.FirstOrDefault(c => c.Strike == atmStrike);

        if (frontCall == null || backCall == null)
        {
            throw new InvalidOperationException("Could not find ATM options for calendar spread");
        }

        PricingRegime regime = DeterminePricingRegime(riskFreeRate, dividendYield);

        double frontPrice, backPrice;
        double frontDelta, backDelta;
        double frontGamma, backGamma;
        double frontTheta, backTheta;
        double frontVega, backVega;

        if (regime == PricingRegime.DoubleBoundary)
        {
            (frontPrice, frontDelta, frontGamma, frontTheta, frontVega) = PriceWithDouble(
                data.CurrentPrice, atmStrike, frontExpiry.GetDaysToExpiry(evaluationDate),
                riskFreeRate, dividendYield, frontCall.ImpliedVolatility, true);

            (backPrice, backDelta, backGamma, backTheta, backVega) = PriceWithDouble(
                data.CurrentPrice, atmStrike, backExpiry.GetDaysToExpiry(evaluationDate),
                riskFreeRate, dividendYield, backCall.ImpliedVolatility, true);
        }
        else
        {
            (frontPrice, frontDelta, frontGamma, frontTheta, frontVega) = SimulateOptionPrice(
                data.CurrentPrice, atmStrike, frontExpiry.GetDaysToExpiry(evaluationDate),
                riskFreeRate, dividendYield, frontCall.ImpliedVolatility, true);

            (backPrice, backDelta, backGamma, backTheta, backVega) = SimulateOptionPrice(
                data.CurrentPrice, atmStrike, backExpiry.GetDaysToExpiry(evaluationDate),
                riskFreeRate, dividendYield, backCall.ImpliedVolatility, true);
        }

        double spreadCost = backPrice - frontPrice;
        double spreadDelta = backDelta - frontDelta;
        double spreadGamma = backGamma - frontGamma;
        double spreadTheta = backTheta - frontTheta;
        double spreadVega = backVega - frontVega;

        bool isCredit = spreadCost < 0;

        return new CalendarSpreadResult
        {
            RegimeName = regimeName,
            Regime = regime,
            Strike = atmStrike,
            FrontExpiry = frontExpiry.ExpiryDate,
            BackExpiry = backExpiry.ExpiryDate,
            FrontDTE = frontExpiry.GetDaysToExpiry(evaluationDate),
            BackDTE = backExpiry.GetDaysToExpiry(evaluationDate),
            FrontPrice = frontPrice,
            BackPrice = backPrice,
            SpreadCost = spreadCost,
            SpreadDelta = spreadDelta,
            SpreadGamma = spreadGamma,
            SpreadTheta = spreadTheta,
            SpreadVega = spreadVega,
            IsCredit = isCredit,
            MaxLoss = isCredit ? double.NaN : spreadCost * 100,
            MaxGain = isCredit ? Math.Abs(spreadCost) * 100 : double.NaN,
            BreakEven = atmStrike,
            RiskFreeRate = riskFreeRate,
            DividendYield = dividendYield
        };
    }

    /// <summary>
    /// Determines the pricing regime based on rate conditions.
    /// </summary>
    private static PricingRegime DeterminePricingRegime(double rate, double dividend)
    {
        if (rate >= 0)
        {
            return PricingRegime.Standard;
        }

        // Negative rate regime
        return dividend < rate ? PricingRegime.DoubleBoundary : PricingRegime.Standard;
    }

    /// <summary>
    /// Prices an option using Alaris.Double (Healy 2021 methodology).
    /// </summary>
    /// <returns>Tuple containing (Price, Delta, Gamma, Theta, Vega).</returns>
    private static (double Price, double Delta, double Gamma, double Theta, double Vega) PriceWithDouble(
        double spot, double strike, int dte, double rate, double div, double vol, bool isCall)
    {
        double timeToExpiry = dte / TradingDaysPerYear;

        // Use Alaris.Double DoubleBoundaryApproximation
        DBAP002A approx = new DBAP002A(spot, strike, timeToExpiry, rate, div, vol, isCall);
        double price = approx.ApproximateValue();

        // Calculate Greeks via finite differences
        const double ds = 0.01;
        const double dv = 0.001;
        const double dt = 1.0 / TradingDaysPerYear;

        // Delta
        DBAP002A approxUp = new DBAP002A(spot + ds, strike, timeToExpiry, rate, div, vol, isCall);
        DBAP002A approxDown = new DBAP002A(spot - ds, strike, timeToExpiry, rate, div, vol, isCall);
        double delta = (approxUp.ApproximateValue() - approxDown.ApproximateValue()) / (2 * ds);

        // Gamma (second derivative with respect to spot)
        double gamma = (approxUp.ApproximateValue() - (2 * price) + approxDown.ApproximateValue()) / (ds * ds);

        // Vega
        DBAP002A approxVegaUp = new DBAP002A(spot, strike, timeToExpiry, rate, div, vol + dv, isCall);
        double vega = (approxVegaUp.ApproximateValue() - price) / dv * 0.01;

        // Theta
        double theta = 0;
        if (timeToExpiry > dt)
        {
            DBAP002A approxTheta = new DBAP002A(spot, strike, timeToExpiry - dt, rate, div, vol, isCall);
            theta = (approxTheta.ApproximateValue() - price) / dt / TradingDaysPerYear;
        }

        return (price, delta, gamma, theta, vega);
    }

    /// <summary>
    /// Simulates option pricing using Black-Scholes with American approximation.
    /// </summary>
    /// <returns>Tuple containing (Price, Delta, Gamma, Theta, Vega).</returns>
    private static (double Price, double Delta, double Gamma, double Theta, double Vega) SimulateOptionPrice(
        double spot, double strike, int dte, double rate, double div, double vol, bool isCall)
    {
        double t = dte / TradingDaysPerYear;
        double sqrtT = Math.Sqrt(t);
        double d1 = (Math.Log(spot / strike) + ((rate - div + (vol * vol / 2)) * t)) / (vol * sqrtT);
        double d2 = d1 - (vol * sqrtT);

        double nd1 = NormCdf(isCall ? d1 : -d1);
        double nd2 = NormCdf(isCall ? d2 : -d2);

        double price = isCall
            ? (spot * Math.Exp(-div * t) * nd1) - (strike * Math.Exp(-rate * t) * nd2)
            : (strike * Math.Exp(-rate * t) * NormCdf(-d2)) - (spot * Math.Exp(-div * t) * NormCdf(-d1));

        double delta = isCall
            ? Math.Exp(-div * t) * nd1
            : Math.Exp(-div * t) * (nd1 - 1);

        double npd1 = Math.Exp(-d1 * d1 / 2) / Math.Sqrt(2 * Math.PI);
        
        // Gamma: second derivative of price with respect to spot
        double gamma = Math.Exp(-div * t) * npd1 / (spot * vol * sqrtT);
        
        double vega = spot * Math.Exp(-div * t) * npd1 * sqrtT * 0.01;
        double theta = ((-spot * Math.Exp(-div * t) * npd1 * vol / (2 * sqrtT)) -
                       (rate * strike * Math.Exp(-rate * t) * nd2)) / TradingDaysPerYear;

        return (price, delta, gamma, theta, vega);
    }

    /// <summary>
    /// Standard normal cumulative distribution function.
    /// </summary>
    private static double NormCdf(double x)
    {
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        int sign = x < 0 ? -1 : 1;
        x = Math.Abs(x) / Math.Sqrt(2);

        double t = 1.0 / (1.0 + (p * x));
        double y = 1.0 - (((((((((a5 * t) + a4) * t) + a3) * t) + a2) * t) + a1) * t * Math.Exp(-x * x));

        return 0.5 * (1.0 + (sign * y));
    }

    /// <summary>
    /// Displays calendar spread pricing results with credit/debit handling.
    /// </summary>
    private static void DisplayCalendarSpread(CalendarSpreadResult result)
    {
        string regimeIndicator = result.Regime == PricingRegime.DoubleBoundary ? "[Healy 2021]" : "[Standard]";
        string spreadType = result.IsCredit ? "Credit" : "Debit";
        string spreadCostLabel = result.IsCredit ? "Spread Credit:     " : "Spread Cost (Debit):";

        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine(FormatBoxLine("CALENDAR SPREAD PRICING - ", result.RegimeName));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Pricing Engine:      ", regimeIndicator));
        Console.WriteLine(FormatBoxLine("Risk-Free Rate:      ", $"{result.RiskFreeRate:P2}"));
        Console.WriteLine(FormatBoxLine("Dividend Yield:      ", $"{result.DividendYield:P2}"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Strike (ATM):        ", $"${result.Strike:F2}"));
        Console.WriteLine(FormatBoxLine("Front Month:         ", $"{result.FrontExpiry:yyyy-MM-dd} (DTE: {result.FrontDTE})"));
        Console.WriteLine(FormatBoxLine("Back Month:          ", $"{result.BackExpiry:yyyy-MM-dd} (DTE: {result.BackDTE})"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Front Option Price:  ", $"${result.FrontPrice:F4}"));
        Console.WriteLine(FormatBoxLine("Back Option Price:   ", $"${result.BackPrice:F4}"));
        Console.WriteLine(FormatBoxLine(spreadCostLabel, $"${Math.Abs(result.SpreadCost):F4} ({spreadType})"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Spread Delta:        ", $"{result.SpreadDelta:F4}"));
        Console.WriteLine(FormatBoxLine("Spread Gamma:        ", $"{result.SpreadGamma:F4}"));
        Console.WriteLine(FormatBoxLine("Spread Theta:        ", $"{result.SpreadTheta:F4}"));
        Console.WriteLine(FormatBoxLine("Spread Vega:         ", $"{result.SpreadVega:F4}"));

        if (result.IsCredit)
        {
            Console.WriteLine(FormatBoxLine("Max Gain/Contract:   ", $"${result.MaxGain:F2} (credit received)"));
            Console.WriteLine(FormatBoxLine("Risk Profile:        ", "Undefined max loss (calendar credit)"));
        }
        else
        {
            Console.WriteLine(FormatBoxLine("Max Loss/Contract:   ", $"${result.MaxLoss:F2}"));
        }

        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    // ========================================================================
    // Phase 9: Alaris.Double Demonstration
    // ========================================================================

    /// <summary>
    /// Demonstrates Alaris.Double double boundary pricing (Healy 2021).
    /// </summary>
    private static DoubleBoundaryDemoResult DemonstrateDoubleBoundaryPricing()
    {
        // Healy (2021) benchmark parameters
        double spot = 100.0;
        double strike = 100.0;
        double maturity = 1.0; // 1 year
        double rate = -0.005; // -0.5%
        double div = -0.010;  // -1.0% (q < r for double boundary)
        double vol = 0.20;    // 20%

        // Create double boundary approximation (QD+ method)
        DBAP002A putApprox = new DBAP002A(spot, strike, maturity, rate, div, vol, isCall: false);
        double putPrice = putApprox.ApproximateValue();

        // Get boundaries using DBAP002A.CalculateBoundaries()
        BoundaryResult boundaryResult = putApprox.CalculateBoundaries();
        double upperBoundary = boundaryResult.UpperBoundary;
        double lowerBoundary = boundaryResult.LowerBoundary;

        // Validate physical constraints
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

    /// <summary>
    /// Displays double boundary pricing demonstration results.
    /// </summary>
    private static void DisplayDoubleBoundaryResult(DoubleBoundaryDemoResult result)
    {
        string constraintsResult = result.AllConstraintsPass ? "✓ ALL PASS" : "✗ SOME FAIL";

        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 9: ALARIS.DOUBLE - Healy (2021) Double Boundary Demonstration         │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ Reference: \"Pricing American Options Under Negative Rates\"                  │");
        Console.WriteLine("│ Method:    QD+ Approximation with Super Halley's iteration                  │");
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
        Console.WriteLine("│ DOUBLE BOUNDARY (Exercise Optimal in Range [S_l, S_u]):                     │");
        Console.WriteLine(FormatBoxLine("  Upper Boundary:    ", $"${result.UpperBoundary:F4}"));
        Console.WriteLine(FormatBoxLine("  Lower Boundary:    ", $"${result.LowerBoundary:F4}"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ PHYSICAL CONSTRAINTS (Healy Appendix A):                                    │");
        Console.WriteLine(FormatBoxLine("  A1 (S_u,S_l > 0):  ", result.A1Pass ? "✓ PASS" : "✗ FAIL"));
        Console.WriteLine(FormatBoxLine("  A2 (S_u > S_l):    ", result.A2Pass ? "✓ PASS" : "✗ FAIL"));
        Console.WriteLine(FormatBoxLine("  A3 (Put < K):      ", result.A3Pass ? "✓ PASS" : "✗ FAIL"));
        Console.WriteLine(FormatBoxLine("  Overall:           ", constraintsResult));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    // ========================================================================
    // Position Sizing (Kelly Criterion)
    // ========================================================================

    /// <summary>
    /// Calculates position size using Kelly Criterion.
    /// </summary>
    private static PositionSizeResult CalculatePositionSize(
        Signal signal,
        CalendarSpreadResult spreadResult,
        double portfolioValue)
    {
        // Estimate win probability based on signal strength
        double winProb = signal.Strength switch
        {
            SignalStrength.Recommended => 0.65,
            SignalStrength.Consider => 0.55,
            _ => 0.45
        };

        // Estimate win/loss ratio from spread metrics
        double avgWin = Math.Abs(spreadResult.SpreadCost) * 0.50; // Assume 50% profit target
        double avgLoss = Math.Abs(spreadResult.SpreadCost);
        double winLossRatio = avgWin / avgLoss;

        // Kelly fraction: f* = (p * b - q) / b
        // where p = win probability, q = loss probability, b = win/loss ratio
        double kellyFraction = ((winProb * winLossRatio) - (1 - winProb)) / winLossRatio;

        // Use fractional Kelly (25%) for safety
        double fractionalKelly = Math.Max(0, kellyFraction * 0.25);

        // Calculate contracts
        double costPerContract = Math.Abs(spreadResult.SpreadCost) * 100;
        double maxRisk = portfolioValue * fractionalKelly;
        int contracts = Math.Max(1, (int)(maxRisk / costPerContract));

        // Cap at 5% of portfolio for safety
        int maxContracts = (int)(portfolioValue * 0.05 / costPerContract);
        contracts = Math.Min(contracts, maxContracts);

        return new PositionSizeResult
        {
            KellyFraction = kellyFraction,
            FractionalKelly = fractionalKelly,
            Contracts = contracts,
            TotalRisk = contracts * costPerContract,
            PortfolioAllocation = contracts * costPerContract / portfolioValue
        };
    }

    /// <summary>
    /// Displays the position sizing calculation.
    /// </summary>
    private static void DisplayPositionSize(PositionSizeResult result)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ POSITION SIZING - Kelly Criterion (Fractional)                              │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Full Kelly:          ", $"{result.KellyFraction:P2}"));
        Console.WriteLine(FormatBoxLine("Fractional Kelly:    ", $"{result.FractionalKelly:P2} (25% of full)"));
        Console.WriteLine(FormatBoxLine("Contracts:           ", result.Contracts.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Total Risk:          ", $"${result.TotalRisk:N2}"));
        Console.WriteLine(FormatBoxLine("Portfolio Allocation:", $"{result.PortfolioAllocation:P2}"));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    // ========================================================================
    // Production Validation Display
    // ========================================================================

    /// <summary>
    /// Displays the production validation results.
    /// </summary>
    private static void DisplayProductionValidation(STHD006A validation)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PRODUCTION VALIDATION RESULTS                                                │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Overall Status:      ", validation.OverallPass ? "✓ PASSED" : "✗ FAILED"));
        Console.WriteLine(FormatBoxLine("Checks Passed:       ", $"{validation.PassedCheckCount}/{validation.TotalCheckCount}"));
        Console.WriteLine(FormatBoxLine("Production Ready:    ", validation.ProductionReady ? "YES" : "NO"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");

        foreach (ValidationCheck check in validation.Checks)
        {
            string status = check.Passed ? "✓" : "✗";
            Console.WriteLine(FormatBoxLine($"{status} {check.Name}:", check.Detail));
        }

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
        Console.WriteLine("║ Strategy:        Earnings Calendar Spread                                   ║");
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
        string action = validation.ProductionReady ? "EXECUTE (LIMIT ORDER)" : "ABORT";

        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                        FINAL PRODUCTION DECISION                             ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine(FormatDoubleBoxLine("Symbol:          ", validation.BaseSignal.Symbol));
        Console.WriteLine(FormatDoubleBoxLine("Status:          ", action));
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");

        if (validation.ProductionReady)
        {
            double totalDebit = contracts * validation.AdjustedDebit * 100;
            Console.WriteLine(FormatDoubleBoxLine("Type:            ", "Calendar Spread (Debit)"));
            Console.WriteLine(FormatDoubleBoxLine("Contracts:       ", contracts.ToString(CultureInfo.InvariantCulture)));
            Console.WriteLine(FormatDoubleBoxLine("Limit Price:     ", $"${validation.AdjustedDebit:F2} (Natural Debit)"));
            Console.WriteLine(FormatDoubleBoxLine("Total Capital:   ", $"${totalDebit:N2}"));
            Console.WriteLine(FormatDoubleBoxLine("Max Risk:        ", $"${totalRisk:N2} (Defined Risk)"));
            Console.WriteLine("║                                                                              ║");
            Console.WriteLine("║ NOTE: Execute 'BUY BACK / SELL FRONT' as a single complex order.             ║");
            Console.WriteLine("║       Do not leg in. Ensure Limit Price respects the Debit.                  ║");
        }
        else
        {
            Console.WriteLine("║ REASON FOR REJECTION:                                                        ║");
            foreach (string fail in validation.FailedChecks)
            {
                Console.WriteLine(FormatDoubleBoxLine("  - ", fail));
            }
        }
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
    }

    // ========================================================================
    // Utility Methods
    // ========================================================================

    /// <summary>
    /// Formats a line for single-line box display.
    /// </summary>
    private static string FormatBoxLine(string label, string value, char border = '│')
    {
        string content = label + value;
        int padding = BoxWidth - content.Length - 4; // Account for borders and spaces
        return $"{border} {content}{new string(' ', Math.Max(0, padding))} {border}";
    }

    /// <summary>
    /// Formats a line for double-line box display.
    /// </summary>
    private static string FormatDoubleBoxLine(string label, string value)
    {
        return FormatBoxLine(label, value, '║');
    }
}

// ============================================================================
// Data Transfer Objects
// ============================================================================

/// <summary>
/// Contains simulated market data for the earnings scenario.
/// </summary>
internal sealed class SimulatedMarketData
{
    public List<PriceBar> PriceHistory { get; init; } = new();
    public OptionChain OptionChain { get; init; } = new();
    public List<DateTime> HistoricalEarningsDates { get; init; } = new();
    public double CurrentPrice { get; init; }
    public string Symbol { get; init; } = string.Empty;
}

/// <summary>
/// Term structure analysis result.
/// </summary>
internal sealed class TermStructureResult
{
    public double Slope { get; init; }
    public bool IsInverted { get; init; }
    public bool MeetsTradingCriterion { get; init; }
    public double IV30 { get; init; }
    public List<(int dte, double iv)> Points { get; init; } = new();
}

/// <summary>
/// Leung-Santoli model metrics.
/// </summary>
internal sealed class LeungSantoliMetrics
{
    public double SigmaE { get; init; }
    public double BaseVolatility { get; init; }
    public double TheoreticalIV { get; init; }
    public double MarketIV { get; init; }
    public double MispricingSignal { get; init; }
    public double ExpectedIVCrush { get; init; }
    public double IVCrushRatio { get; init; }
    public int HistoricalSamples { get; init; }
    public bool IsCalibrated { get; init; }
}

/// <summary>
/// Signal generation result with criteria breakdown.
/// </summary>
internal sealed class SignalResult
{
    public Signal Signal { get; init; } = new();
    public Dictionary<string, (bool Pass, string Value, string Threshold)> CriteriaResults { get; init; } = new();
}

/// <summary>
/// Calendar spread pricing result with credit/debit handling.
/// </summary>
/// <remarks>
/// This internal DTO now includes SpreadGamma to align with the CalendarSpreadPricing
/// class in Alaris.Strategy.Pricing and support comprehensive Greek analysis in
/// production validation (STHD003A gamma risk management).
/// </remarks>
internal sealed class CalendarSpreadResult
{
    public string RegimeName { get; init; } = string.Empty;
    public PricingRegime Regime { get; init; }
    public double Strike { get; init; }
    public DateTime FrontExpiry { get; init; }
    public DateTime BackExpiry { get; init; }
    public int FrontDTE { get; init; }
    public int BackDTE { get; init; }
    public double FrontPrice { get; init; }
    public double BackPrice { get; init; }
    public double SpreadCost { get; init; }
    public double SpreadDelta { get; init; }
    public double SpreadGamma { get; init; }
    public double SpreadTheta { get; init; }
    public double SpreadVega { get; init; }
    public bool IsCredit { get; init; }
    public double MaxLoss { get; init; }
    public double MaxGain { get; init; }
    public double BreakEven { get; init; }
    public double RiskFreeRate { get; init; }
    public double DividendYield { get; init; }
}

/// <summary>
/// Double boundary pricing demonstration result.
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
/// Position sizing calculation result.
/// </summary>
internal sealed class PositionSizeResult
{
    public double KellyFraction { get; init; }
    public double FractionalKelly { get; init; }
    public int Contracts { get; init; }
    public double TotalRisk { get; init; }
    public double PortfolioAllocation { get; init; }
}

/// <summary>
/// Price bar for historical data.
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
/// Pricing regime enumeration.
/// </summary>
internal enum PricingRegime
{
    Standard,
    DoubleBoundary
}