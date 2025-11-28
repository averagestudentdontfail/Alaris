// ============================================================================
// Alaris.Simulation - SMSM001A.cs
// Quarterly Earnings Announcement Simulation
// ============================================================================
// 
// Component Code: SMSM001A
// Domain:         SM (Simulation)
// Category:       SM (Simulation Main)
// Sequence:       001
// Variant:        A (Primary Implementation)
//
// Purpose:
//   Demonstrates the complete Alaris system workflow during a quarterly earnings
//   announcement, integrating:
//   - Alaris.Double: Negative rate American option pricing (Healy 2021)
//   - Alaris.Strategy: Earnings volatility calendar spread strategy (Atilgan 2014)
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
//
// ============================================================================

using Alaris.Double;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Core;
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
/// The simulation demonstrates both positive rate (Alaris.Quantlib) and negative
/// rate (Alaris.Double with Healy 2021 methodology) pricing regimes.
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
    public static async Task Main(string[] args)
    {
        // Suppress unused parameter warning
        _ = args;

        // Configure logging
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        ILogger logger = loggerFactory.CreateLogger("Alaris.Simulation");

        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    ALARIS EARNINGS SIMULATION - SMSM001A                     ║");
        Console.WriteLine("║              Quarterly Earnings Announcement Strategy Demonstration          ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            // Execute simulation phases
            await RunSimulation(loggerFactory).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Simulation failed due to invalid arguments");
            throw;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Simulation failed due to invalid operation");
            throw;
        }

        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                         SIMULATION COMPLETE                                  ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
    }

    // ========================================================================
    // Formatting Helpers
    // ========================================================================

    /// <summary>
    /// Formats a line for box output with proper padding.
    /// </summary>
    private static string FormatBoxLine(string label, string value, char border = '│')
    {
        string content = $"{label}{value}";
        int padding = BoxWidth - content.Length - 2; // -2 for borders
        return $"{border} {content}{new string(' ', Math.Max(0, padding))} {border}";
    }

    /// <summary>
    /// Formats a line for double-border box output.
    /// </summary>
    private static string FormatDoubleBoxLine(string label, string value)
    {
        return FormatBoxLine(label, value, '║');
    }

    // ========================================================================
    // Simulation Orchestration
    // ========================================================================

    /// <summary>
    /// Executes all simulation phases in sequence.
    /// </summary>
    private static async Task RunSimulation(ILoggerFactory loggerFactory)
    {
        _ = loggerFactory; // Reserved for future logging integration

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
        CalendarSpreadResult positiveRateSpread = await PriceCalendarSpread(
            marketData, evaluationDate, PositiveRiskFreeRate, 0.005, "Positive Rate Regime").ConfigureAwait(false);
        DisplayCalendarSpread(positiveRateSpread);

        // Phase 8: Price calendar spread (negative rates - Healy 2021)
        CalendarSpreadResult negativeRateSpread = await PriceCalendarSpread(
            marketData, evaluationDate, NegativeRiskFreeRate, NegativeRateDividendYield,
            "Negative Rate Regime (Healy 2021)").ConfigureAwait(false);
        DisplayCalendarSpread(negativeRateSpread);

        // Phase 9: Demonstrate Alaris.Double pricing
        DoubleBoundaryDemoResult doubleBoundary = DemonstrateDoubleBoundaryPricing();
        DisplayDoubleBoundaryResult(doubleBoundary);

        // Phase 10: Calculate position size (Kelly Criterion)
        PositionSizeResult positionResult = CalculatePositionSize(
            signalResult.Signal, positiveRateSpread.SpreadCost);
        DisplayPositionSize(positionResult);

        // Phase 11: Display final trade recommendation
        DisplayTradeRecommendation(signalResult, positiveRateSpread, positionResult);
    }

    // ========================================================================
    // Phase 1: Market Conditions Display
    // ========================================================================

    /// <summary>
    /// Displays the simulation's market conditions and scenario setup.
    /// </summary>
    private static void DisplayMarketConditions(DateTime evaluationDate, DateTime earningsDate)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 1: MARKET CONDITIONS                                                  │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Symbol:              ", SimulationSymbol));
        Console.WriteLine(FormatBoxLine("Evaluation Date:     ", evaluationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Earnings Date:       ", earningsDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Days to Earnings:    ", (earningsDate - evaluationDate).Days.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Portfolio Value:     ", PortfolioValue.ToString("C0", CultureInfo.InvariantCulture)));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ SCENARIO: Pre-earnings with elevated IV and inverted term structure         │");
        Console.WriteLine("│ OBJECTIVE: Generate Recommended signal per Atilgan (2014) criteria          │");
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    // ========================================================================
    // Phase 2: Market Data Generation
    // ========================================================================

    /// <summary>
    /// Generates simulated market data with conditions that trigger a Recommended signal.
    /// </summary>
    /// <remarks>
    /// Market data is constructed to satisfy Atilgan (2014) entry criteria:
    /// <list type="bullet">
    ///   <item><description>IV30/RV30 ≥ 1.25 → Set IV30 elevated via term structure</description></item>
    ///   <item><description>Term slope ≤ -0.00406 → Create inverted term structure</description></item>
    ///   <item><description>Volume ≥ 1.5M → Set daily volume ~3.5M average</description></item>
    /// </list>
    /// </remarks>
    private static SimulatedMarketData GenerateMarketData(DateTime evaluationDate, DateTime earningsDate)
    {
        // Generate price history with moderate volatility (~25% annualised)
        List<PriceBar> priceHistory = GeneratePriceHistory(evaluationDate, 90);

        // Generate option chain with inverted term structure (pre-earnings IV elevation)
        OptionChain optionChain = GenerateOptionChain(evaluationDate, earningsDate);

        // Generate historical earnings dates for Leung-Santoli calibration
        List<DateTime> historicalEarnings = GenerateHistoricalEarningsDates(earningsDate, 12);

        return new SimulatedMarketData
        {
            PriceHistory = priceHistory,
            OptionChain = optionChain,
            HistoricalEarningsDates = historicalEarnings,
            CurrentPrice = 185.50,
            Symbol = SimulationSymbol
        };
    }

    /// <summary>
    /// Generates 90 days of OHLCV price history with realistic volatility characteristics.
    /// </summary>
    /// <remarks>
    /// Volatility is set to ~22% annualised to ensure IV30/RV30 ratio exceeds 1.25
    /// when IV30 is elevated to ~32% due to pre-earnings term structure.
    /// </remarks>
    private static List<PriceBar> GeneratePriceHistory(DateTime endDate, int days)
    {
        List<PriceBar> bars = new List<PriceBar>(days);
        Random rng = new Random(42); // Deterministic seed for reproducibility

        double price = 180.00; // Starting price
        // Target ~22% annualised vol so that IV30 (~32%) / RV30 (~22%) ≈ 1.45 (> 1.25)
        double dailyVol = 0.22 / Math.Sqrt(TradingDaysPerYear);

        for (int i = days - 1; i >= 0; i--)
        {
            DateTime date = endDate.AddDays(-i);

            // Skip weekends
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                continue;
            }

            // Generate log-normal return
            double returnVal = (rng.NextDouble() - 0.5) * 2 * dailyVol * 1.5;
            price *= Math.Exp(returnVal);

            // Generate OHLC with realistic intraday range
            double range = price * dailyVol * (0.5 + rng.NextDouble());
            double open = price * (1 + ((rng.NextDouble() - 0.5) * dailyVol * 0.3));
            double high = Math.Max(open, price) + (range * 0.5 * rng.NextDouble());
            double low = Math.Min(open, price) - (range * 0.5 * rng.NextDouble());
            double close = price;

            // High volume (averaging ~3.5M to meet ≥1.5M criterion)
            long volume = 2_500_000 + (long)(rng.NextDouble() * 2_000_000);

            bars.Add(new PriceBar
            {
                Date = date,
                Open = Math.Round(open, 2),
                High = Math.Round(high, 2),
                Low = Math.Round(low, 2),
                Close = Math.Round(close, 2),
                Volume = volume
            });
        }

        return bars;
    }

    /// <summary>
    /// Generates an option chain with inverted term structure characteristic of pre-earnings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per Leung &amp; Santoli (2014), pre-earnings IV follows:
    ///   I(t) = sqrt(σ² + σₑ²/(T-t))
    /// where σₑ is earnings jump volatility. This creates higher IV for near-term options.
    /// </para>
    /// <para>
    /// IV30 is elevated to ~32% to ensure IV30/RV30 ratio exceeds 1.25 threshold.
    /// The term structure shows gradual decay from front-month to back-month.
    /// </para>
    /// </remarks>
    private static OptionChain GenerateOptionChain(DateTime evaluationDate, DateTime earningsDate)
    {
        OptionChain chain = new OptionChain
        {
            Symbol = SimulationSymbol,
            UnderlyingPrice = 185.50,
            Timestamp = evaluationDate
        };

        // Parameters for inverted term structure
        // Elevated base vol to ensure IV30/RV30 > 1.25
        double baseVol = 0.28; // σ (base diffusion volatility, elevated for pre-earnings)
        double sigmaE = 0.12;  // σₑ (earnings jump volatility, ~12%)

        // Define expiry dates
        DateTime[] expiryDates = new[]
        {
            evaluationDate.AddDays(7),   // Weekly (before earnings)
            evaluationDate.AddDays(14),  // 2 weeks
            evaluationDate.AddDays(30),  // Monthly (after earnings)
            evaluationDate.AddDays(45),  // 6 weeks
            evaluationDate.AddDays(60),  // 2 months
            evaluationDate.AddDays(90)   // 3 months
        };

        foreach (DateTime expiry in expiryDates)
        {
            int dte = (expiry - evaluationDate).Days;
            double timeToExpiry = dte / TradingDaysPerYear;

            // Leung-Santoli IV formula: I(t) = sqrt(σ² + σₑ²/(T-t))
            // Apply earnings effect that decays with time to expiry
            double varianceComponent = sigmaE * sigmaE / timeToExpiry;
            double iv = Math.Sqrt((baseVol * baseVol) + varianceComponent);

            // Cap IV at reasonable levels
            iv = Math.Min(iv, 0.80);

            OptionExpiry expiryColl = new OptionExpiry { ExpiryDate = expiry };

            // Generate strikes around ATM
            double[] strikes = new[] { 175.0, 180.0, 182.5, 185.0, 187.5, 190.0, 195.0 };

            foreach (double strike in strikes)
            {
                // Slight volatility smile: higher IV for OTM options
                double moneyness = Math.Abs(strike - chain.UnderlyingPrice) / chain.UnderlyingPrice;
                double strikeIV = iv * (1 + (0.15 * moneyness));

                // Create call and put contracts
                expiryColl.Calls.Add(CreateOptionContract(strike, strikeIV, chain.UnderlyingPrice, true));
                expiryColl.Puts.Add(CreateOptionContract(strike, strikeIV, chain.UnderlyingPrice, false));
            }

            chain.Expiries.Add(expiryColl);
        }

        return chain;
    }

    /// <summary>
    /// Creates a single option contract with realistic market data.
    /// </summary>
    private static OptionContract CreateOptionContract(double strike, double iv, double spot, bool isCall)
    {
        // Simplified Black-Scholes approximation for bid/ask
        double intrinsic = isCall ? Math.Max(0, spot - strike) : Math.Max(0, strike - spot);
        double timeValue = spot * iv * 0.1; // Rough approximation
        double midPrice = intrinsic + timeValue;

        double spread = midPrice * 0.02; // 2% bid-ask spread
        double bid = Math.Max(0.01, midPrice - (spread / 2));
        double ask = midPrice + (spread / 2);

        // Delta approximation
        double delta = isCall 
            ? 0.5 + (0.4 * (spot - strike) / spot) 
            : -0.5 + (0.4 * (spot - strike) / spot);
        delta = Math.Max(-1, Math.Min(1, delta));

        Random localRng = new Random((int)(strike * 100));

        return new OptionContract
        {
            Strike = strike,
            Bid = Math.Round(bid, 2),
            Ask = Math.Round(ask, 2),
            LastPrice = Math.Round(midPrice, 2),
            ImpliedVolatility = iv,
            Delta = Math.Round(delta, 4),
            Gamma = Math.Round(0.02 / spot, 6),
            Vega = Math.Round(spot * 0.002, 4),
            Theta = Math.Round(-0.05, 4),
            OpenInterest = 500 + localRng.Next(2000),
            Volume = 100 + localRng.Next(500)
        };
    }

    /// <summary>
    /// Generates historical earnings dates for Leung-Santoli sigma_e calibration.
    /// </summary>
    private static List<DateTime> GenerateHistoricalEarningsDates(DateTime currentEarnings, int quarters)
    {
        List<DateTime> dates = new List<DateTime>(quarters);

        for (int i = 1; i <= quarters; i++)
        {
            // Quarterly earnings approximately every 91 days
            dates.Add(currentEarnings.AddDays(-91 * i));
        }

        return dates;
    }

    /// <summary>
    /// Displays the generated market data summary.
    /// </summary>
    private static void DisplayMarketData(SimulatedMarketData data)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 2: SIMULATED MARKET DATA                                              │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Current Price:       ", $"${data.CurrentPrice:F2}"));
        Console.WriteLine(FormatBoxLine("Price History:       ", $"{data.PriceHistory.Count} trading days"));
        Console.WriteLine(FormatBoxLine("Option Expiries:     ", $"{data.OptionChain.Expiries.Count} dates"));
        Console.WriteLine(FormatBoxLine("Historical Earnings: ", $"{data.HistoricalEarningsDates.Count} quarters (for σₑ calibration)"));
        Console.WriteLine("│                                                                              │");
        Console.WriteLine("│ Term Structure Preview (ATM IV):                                             │");

        foreach (OptionExpiry expiry in data.OptionChain.Expiries.Take(4))
        {
            int dte = expiry.GetDaysToExpiry(data.OptionChain.Timestamp);
            OptionContract? atmCall = expiry.Calls.OrderBy(c => Math.Abs(c.Strike - data.CurrentPrice)).FirstOrDefault();
            if (atmCall != null)
            {
                Console.WriteLine(FormatBoxLine($"  DTE {dte,3}: IV = ", $"{atmCall.ImpliedVolatility:P2}"));
            }
        }

        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    // ========================================================================
    // Phase 3: Realised Volatility (Yang-Zhang)
    // ========================================================================

    /// <summary>
    /// Calculates 30-day realised volatility using the Yang-Zhang estimator.
    /// </summary>
    /// <remarks>
    /// Yang-Zhang (2000) estimator combines overnight, open-close, and Rogers-Satchell
    /// components for superior efficiency with OHLC data:
    ///   σ²_YZ = σ²_overnight + k·σ²_open-close + (1-k)·σ²_RS
    /// </remarks>
    private static double CalculateRealisedVolatility(List<PriceBar> priceHistory)
    {
        // Take last 30 trading days
        List<PriceBar> recentBars = priceHistory.TakeLast(31).ToList();

        if (recentBars.Count < 2)
        {
            return 0.25; // Default fallback
        }

        double n = recentBars.Count - 1;

        // Overnight variance component
        double overnightVariance = 0;
        for (int i = 1; i < recentBars.Count; i++)
        {
            double logReturn = Math.Log(recentBars[i].Open / recentBars[i - 1].Close);
            overnightVariance += logReturn * logReturn;
        }
        overnightVariance /= n - 1;

        // Open-to-close variance component
        double openCloseVariance = 0;
        foreach (PriceBar bar in recentBars.Skip(1))
        {
            double logReturn = Math.Log(bar.Close / bar.Open);
            openCloseVariance += logReturn * logReturn;
        }
        openCloseVariance /= n - 1;

        // Rogers-Satchell variance component
        double rsVariance = 0;
        foreach (PriceBar bar in recentBars.Skip(1))
        {
            double logHC = Math.Log(bar.High / bar.Close);
            double logHO = Math.Log(bar.High / bar.Open);
            double logLC = Math.Log(bar.Low / bar.Close);
            double logLO = Math.Log(bar.Low / bar.Open);
            rsVariance += (logHC * logHO) + (logLC * logLO);
        }
        rsVariance /= n;

        // Yang-Zhang combination (k = 0.34 is optimal for efficiency)
        double k = 0.34;
        double yzVariance = overnightVariance + (k * openCloseVariance) + ((1 - k) * rsVariance);

        // Annualise
        double annualisedVol = Math.Sqrt(yzVariance * TradingDaysPerYear);

        return annualisedVol;
    }

    /// <summary>
    /// Displays the realised volatility calculation results.
    /// </summary>
    private static void DisplayRealisedVolatility(double realisedVol)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 3: REALISED VOLATILITY - Yang-Zhang (2000)                            │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("30-Day Realised Vol: ", $"{realisedVol:P2}"));
        Console.WriteLine("│ Estimator:           Yang-Zhang OHLC-based                                  │");
        Console.WriteLine("│ Reference:           Yang & Zhang (2000) \"Drift-Independent Estimation\"     │");
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    // ========================================================================
    // Phase 4: Term Structure Analysis
    // ========================================================================

    /// <summary>
    /// Analyses the implied volatility term structure.
    /// </summary>
    private static TermStructureResult AnalyseTermStructure(OptionChain chain, DateTime evaluationDate)
    {
        List<(int dte, double iv)> termPoints = new List<(int, double)>();

        foreach (OptionExpiry expiry in chain.Expiries)
        {
            int dte = expiry.GetDaysToExpiry(evaluationDate);
            OptionContract? atmCall = expiry.Calls
                .OrderBy(c => Math.Abs(c.Strike - chain.UnderlyingPrice))
                .FirstOrDefault();

            if (atmCall != null && dte > 0 && dte <= 90)
            {
                termPoints.Add((dte, atmCall.ImpliedVolatility));
            }
        }

        if (termPoints.Count < 2)
        {
            return new TermStructureResult
            {
                Slope = 0,
                IsInverted = false,
                IV30 = 0.30,
                Points = termPoints
            };
        }

        // Calculate slope using linear regression
        double sumX = termPoints.Sum(p => p.dte);
        double sumY = termPoints.Sum(p => p.iv);
        double sumXY = termPoints.Sum(p => p.dte * p.iv);
        double sumX2 = termPoints.Sum(p => p.dte * p.dte);
        double n = termPoints.Count;

        double slope = ((n * sumXY) - (sumX * sumY)) / ((n * sumX2) - (sumX * sumX));

        // Interpolate IV30
        (int dte, double iv) lower = termPoints.Where(p => p.dte <= 30).OrderByDescending(p => p.dte).FirstOrDefault();
        (int dte, double iv) upper = termPoints.Where(p => p.dte > 30).OrderBy(p => p.dte).FirstOrDefault();

        double iv30;
        if (lower.dte > 0 && upper.dte > 0)
        {
            double weight = (30.0 - lower.dte) / (upper.dte - lower.dte);
            iv30 = lower.iv + (weight * (upper.iv - lower.iv));
        }
        else if (lower.dte > 0)
        {
            iv30 = lower.iv;
        }
        else
        {
            iv30 = termPoints.First().iv;
        }

        return new TermStructureResult
        {
            Slope = slope,
            IsInverted = slope < 0,
            MeetsTradingCriterion = slope <= -0.00406, // Atilgan (2014) threshold
            IV30 = iv30,
            Points = termPoints
        };
    }

    /// <summary>
    /// Displays the term structure analysis results.
    /// </summary>
    private static void DisplayTermStructure(TermStructureResult result)
    {
        string shape = result.IsInverted ? "INVERTED (Pre-Earnings)" : "NORMAL";
        string criterion = result.MeetsTradingCriterion ? "✓ PASS" : "✗ FAIL";

        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 4: TERM STRUCTURE ANALYSIS                                            │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Term Structure Shape: ", shape));
        Console.WriteLine(FormatBoxLine("Slope (∂IV/∂DTE):     ", $"{result.Slope:F6}"));
        Console.WriteLine("│ Atilgan Threshold:    ≤ -0.00406                                            │");
        Console.WriteLine(FormatBoxLine("Trading Criterion:    ", criterion));
        Console.WriteLine(FormatBoxLine("Interpolated IV30:    ", $"{result.IV30:P2}"));
        Console.WriteLine("│                                                                              │");
        Console.WriteLine("│ Term Structure Points:                                                       │");

        foreach ((int dte, double iv) in result.Points.Take(4))
        {
            int barLength = Math.Min(40, (int)(iv * 100));
            string bar = new string('█', barLength);
            Console.WriteLine(FormatBoxLine($"  DTE {dte,3}: {iv:P2} ", bar));
        }

        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    // ========================================================================
    // Phase 5: Leung-Santoli (2014) Model
    // ========================================================================

    /// <summary>
    /// Calculates Leung-Santoli pre-earnings implied volatility metrics.
    /// </summary>
    /// <remarks>
    /// Per Leung &amp; Santoli (2014) Equation 2.4:
    ///   I(t; K, T) = √(σ² + σₑ²/(T-t))
    /// 
    /// Where σₑ is calibrated from historical earnings moves.
    /// </remarks>
    private static LeungSantoliMetrics CalculateLeungSantoliMetrics(
        SimulatedMarketData data,
        DateTime earningsDate,
        DateTime evaluationDate,
        double realisedVol,
        TermStructureResult termResult)
    {
        _ = data; // Reserved for future use

        // Calibrate σₑ from historical earnings moves
        // Using simulated jump moves averaging ~4% (realistic for large-cap tech)
        double[] historicalJumps = new[] { 0.045, -0.038, 0.052, -0.041, 0.035, -0.048, 0.055, -0.032, 0.042, -0.039, 0.048, -0.036 };
        double sigmaE = CalculateJumpVolatility(historicalJumps);

        // Base volatility (σ) - use realised vol as proxy
        double baseVol = realisedVol;

        // Time to expiry for front-month option
        int dteToEarnings = (earningsDate - evaluationDate).Days;
        double timeToExpiry = dteToEarnings / TradingDaysPerYear;

        // Theoretical pre-EA IV: I(t) = √(σ² + σₑ²/(T-t))
        double varianceComponent = sigmaE * sigmaE / timeToExpiry;
        double theoreticalIV = Math.Sqrt((baseVol * baseVol) + varianceComponent);

        // IV mispricing signal
        double marketIV = termResult.IV30;
        double mispricingSignal = marketIV - theoreticalIV;

        // Expected IV crush
        double expectedCrush = theoreticalIV - baseVol;
        double crushRatio = theoreticalIV > 0 ? expectedCrush / theoreticalIV : 0;

        return new LeungSantoliMetrics
        {
            SigmaE = sigmaE,
            BaseVolatility = baseVol,
            TheoreticalIV = theoreticalIV,
            MarketIV = marketIV,
            MispricingSignal = mispricingSignal,
            ExpectedIVCrush = expectedCrush,
            IVCrushRatio = crushRatio,
            HistoricalSamples = historicalJumps.Length,
            IsCalibrated = true
        };
    }

    /// <summary>
    /// Calculates jump volatility (σₑ) from historical earnings moves.
    /// </summary>
    private static double CalculateJumpVolatility(double[] jumps)
    {
        double mean = jumps.Average();
        double variance = jumps.Sum(j => (j - mean) * (j - mean)) / (jumps.Length - 1);
        return Math.Sqrt(variance);
    }

    /// <summary>
    /// Displays Leung-Santoli model metrics.
    /// </summary>
    private static void DisplayLeungSantoliMetrics(LeungSantoliMetrics metrics)
    {
        string mispricing = metrics.MispricingSignal > 0 ? "OVERPRICED" : "UNDERPRICED";
        string mispricingArrow = metrics.MispricingSignal > 0 ? "↑" : "↓";

        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 5: LEUNG-SANTOLI (2014) MODEL                                         │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ Reference: \"Accounting for Earnings Announcements in Option Pricing\"        │");
        Console.WriteLine("│ Formula:   I(t) = √(σ² + σₑ²/(T-t))                                          │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Earnings Jump Vol (σₑ):  ", $"{metrics.SigmaE:P2} (calibrated from {metrics.HistoricalSamples} quarters)"));
        Console.WriteLine(FormatBoxLine("Base Volatility (σ):     ", $"{metrics.BaseVolatility:P2}"));
        Console.WriteLine(FormatBoxLine("Theoretical Pre-EA IV:   ", $"{metrics.TheoreticalIV:P2}"));
        Console.WriteLine(FormatBoxLine("Market IV:               ", $"{metrics.MarketIV:P2}"));
        Console.WriteLine(FormatBoxLine("Mispricing Signal:       ", $"{metrics.MispricingSignal:+0.00%;-0.00%} ({mispricing} {mispricingArrow})"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Expected IV Crush:       ", $"{metrics.ExpectedIVCrush:P2} ({metrics.IVCrushRatio:P1} of pre-EA IV)"));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    // ========================================================================
    // Phase 6: Signal Generation (Atilgan 2014)
    // ========================================================================

    /// <summary>
    /// Generates trading signal per Atilgan (2014) criteria.
    /// </summary>
    private static SignalResult GenerateSignal(
        SimulatedMarketData data,
        double realisedVol,
        TermStructureResult termResult,
        LeungSantoliMetrics lsMetrics,
        DateTime earningsDate,
        DateTime evaluationDate)
    {
        // Atilgan (2014) entry criteria
        double ivRvRatio = termResult.IV30 / realisedVol;
        double termSlope = termResult.Slope;
        long avgVolume = (long)data.PriceHistory.TakeLast(30).Average(p => p.Volume);

        // Thresholds from Atilgan (2014)
        bool ivRvPass = ivRvRatio >= 1.25;
        bool termSlopePass = termSlope <= -0.00406;
        bool volumePass = avgVolume >= 1_500_000;

        // Determine signal strength
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
    private static async Task<CalendarSpreadResult> PriceCalendarSpread(
        SimulatedMarketData data,
        DateTime evaluationDate,
        double riskFreeRate,
        double dividendYield,
        string regimeName)
    {
        // Select front and back month expiries
        List<OptionExpiry> sortedExpiries = data.OptionChain.Expiries
            .OrderBy(e => e.ExpiryDate)
            .ToList();

        if (sortedExpiries.Count < 2)
        {
            throw new InvalidOperationException("Insufficient option expiries for calendar spread");
        }

        OptionExpiry frontExpiry = sortedExpiries[0]; // Weekly
        OptionExpiry backExpiry = sortedExpiries[2];  // Monthly

        // Select ATM strike
        double atmStrike = sortedExpiries.First().Calls
            .OrderBy(c => Math.Abs(c.Strike - data.CurrentPrice))
            .First().Strike;

        OptionContract? frontCall = frontExpiry.Calls.FirstOrDefault(c => c.Strike == atmStrike);
        OptionContract? backCall = backExpiry.Calls.FirstOrDefault(c => c.Strike == atmStrike);

        if (frontCall == null || backCall == null)
        {
            throw new InvalidOperationException("Could not find ATM options for calendar spread");
        }

        // Determine pricing regime
        PricingRegime regime = DeterminePricingRegime(riskFreeRate, dividendYield);

        double frontPrice;
        double backPrice;
        double frontDelta;
        double backDelta;
        double frontTheta;
        double backTheta;
        double frontVega;
        double backVega;

        if (regime == PricingRegime.DoubleBoundary)
        {
            // Use Alaris.Double (Healy 2021) for negative rates
            (frontPrice, frontDelta, frontTheta, frontVega) = PriceWithDouble(
                data.CurrentPrice, atmStrike, frontExpiry.GetDaysToExpiry(evaluationDate),
                riskFreeRate, dividendYield, frontCall.ImpliedVolatility, true);

            (backPrice, backDelta, backTheta, backVega) = PriceWithDouble(
                data.CurrentPrice, atmStrike, backExpiry.GetDaysToExpiry(evaluationDate),
                riskFreeRate, dividendYield, backCall.ImpliedVolatility, true);
        }
        else
        {
            // Use simulated pricing (would use Alaris.Quantlib in production)
            (frontPrice, frontDelta, frontTheta, frontVega) = SimulateOptionPrice(
                data.CurrentPrice, atmStrike, frontExpiry.GetDaysToExpiry(evaluationDate),
                riskFreeRate, dividendYield, frontCall.ImpliedVolatility, true);

            (backPrice, backDelta, backTheta, backVega) = SimulateOptionPrice(
                data.CurrentPrice, atmStrike, backExpiry.GetDaysToExpiry(evaluationDate),
                riskFreeRate, dividendYield, backCall.ImpliedVolatility, true);
        }

        // Calendar spread: long back month, short front month
        double spreadCost = backPrice - frontPrice;
        double spreadDelta = backDelta - frontDelta;
        double spreadTheta = backTheta - frontTheta;
        double spreadVega = backVega - frontVega;

        // Determine if this is a credit or debit spread
        bool isCredit = spreadCost < 0;

        await Task.CompletedTask.ConfigureAwait(false); // Placeholder for async pricing engine

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
            SpreadTheta = spreadTheta,
            SpreadVega = spreadVega,
            IsCredit = isCredit,
            MaxLoss = isCredit ? double.NaN : spreadCost * 100, // Credit spreads have undefined max loss
            MaxGain = isCredit ? Math.Abs(spreadCost) * 100 : double.NaN,
            BreakEven = atmStrike,
            RiskFreeRate = riskFreeRate,
            DividendYield = dividendYield
        };
    }

    /// <summary>
    /// Determines the pricing regime based on rate conditions.
    /// </summary>
    private static PricingRegime DeterminePricingRegime(double riskFreeRate, double dividendYield)
    {
        if (riskFreeRate >= 0)
        {
            return PricingRegime.PositiveRates;
        }

        if (dividendYield < riskFreeRate)
        {
            return PricingRegime.DoubleBoundary;
        }

        return PricingRegime.NegativeRatesSingleBoundary;
    }

    /// <summary>
    /// Prices an option using Alaris.Double (Healy 2021 methodology).
    /// </summary>
    private static (double Price, double Delta, double Theta, double Vega) PriceWithDouble(
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

        DBAP002A approxUp = new DBAP002A(spot + ds, strike, timeToExpiry, rate, div, vol, isCall);
        DBAP002A approxDown = new DBAP002A(spot - ds, strike, timeToExpiry, rate, div, vol, isCall);
        double delta = (approxUp.ApproximateValue() - approxDown.ApproximateValue()) / (2 * ds);

        DBAP002A approxVegaUp = new DBAP002A(spot, strike, timeToExpiry, rate, div, vol + dv, isCall);
        double vega = (approxVegaUp.ApproximateValue() - price) / dv * 0.01;

        double theta = 0;
        if (timeToExpiry > dt)
        {
            DBAP002A approxTheta = new DBAP002A(spot, strike, timeToExpiry - dt, rate, div, vol, isCall);
            theta = (approxTheta.ApproximateValue() - price) / dt / TradingDaysPerYear;
        }

        return (price, delta, theta, vega);
    }

    /// <summary>
    /// Simulates option pricing for positive rate regime (simplified Black-Scholes).
    /// </summary>
    private static (double Price, double Delta, double Theta, double Vega) SimulateOptionPrice(
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
        double vega = spot * Math.Exp(-div * t) * npd1 * sqrtT * 0.01;
        double theta = ((-spot * Math.Exp(-div * t) * npd1 * vol / (2 * sqrtT)) -
                       (rate * strike * Math.Exp(-rate * t) * nd2)) / TradingDaysPerYear;

        return (price, delta, theta, vega);
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
    // Phase 10: Position Sizing (Kelly Criterion)
    // ========================================================================

    /// <summary>
    /// Calculates position size using Kelly Criterion with fractional sizing.
    /// </summary>
    private static PositionSizeResult CalculatePositionSize(Signal signal, double spreadCost)
    {
        // Simulated historical trade performance for Kelly calculation
        List<Trade> historicalTrades = GenerateHistoricalTrades(50);

        // Calculate win rate and average win/loss
        List<Trade> winners = historicalTrades.Where(t => t.ProfitLoss > 0).ToList();
        List<Trade> losers = historicalTrades.Where(t => t.ProfitLoss <= 0).ToList();

        double winRate = (double)winners.Count / historicalTrades.Count;
        double avgWin = winners.Count > 0 ? winners.Average(t => t.ProfitLoss) : 0;
        double avgLoss = losers.Count > 0 ? Math.Abs(losers.Average(t => t.ProfitLoss)) : spreadCost * 100;

        double winLossRatio = avgLoss > 0 ? avgWin / avgLoss : 1;

        // Kelly formula: f* = (p*b - q) / b
        // where p = win rate, q = loss rate, b = win/loss ratio
        double fullKelly = ((winRate * winLossRatio) - (1 - winRate)) / winLossRatio;

        // Apply fractional Kelly (25% of full Kelly for safety)
        const double fractionalKelly = 0.25;
        double kellyFraction = fullKelly * fractionalKelly;

        // Cap at maximum allocation (6%)
        const double maxAllocation = 0.06;
        double allocationPercent = Math.Max(0, Math.Min(kellyFraction, maxAllocation));

        // Adjust for signal strength
        double adjustedAllocation = signal.Strength switch
        {
            SignalStrength.Recommended => allocationPercent * 1.0,
            SignalStrength.Consider => allocationPercent * 0.5,
            SignalStrength.Avoid => 0.0,
            _ => 0.0
        };

        // Calculate contract count
        double dollarAllocation = PortfolioValue * adjustedAllocation;
        double costPerContract = Math.Abs(spreadCost) * 100;
        int contracts = costPerContract > 0 ? (int)Math.Floor(dollarAllocation / costPerContract) : 0;

        return new PositionSizeResult
        {
            Contracts = Math.Max(0, contracts),
            AllocationPercent = adjustedAllocation,
            DollarAllocation = dollarAllocation,
            FullKelly = fullKelly,
            FractionalKelly = kellyFraction,
            WinRate = winRate,
            WinLossRatio = winLossRatio,
            HistoricalTrades = historicalTrades.Count,
            MaxLossPerContract = costPerContract,
            TotalRisk = contracts * costPerContract
        };
    }

    /// <summary>
    /// Generates simulated historical trades for Kelly calculation.
    /// </summary>
    private static List<Trade> GenerateHistoricalTrades(int count)
    {
        List<Trade> trades = new List<Trade>(count);
        Random rng = new Random(123); // Deterministic seed

        // Simulate ~55% win rate with 1.8:1 average win/loss ratio
        for (int i = 0; i < count; i++)
        {
            bool isWinner = rng.NextDouble() < 0.55;
            double pnl = isWinner
                ? 150 + (rng.NextDouble() * 200) // Wins: $150-$350
                : -(80 + (rng.NextDouble() * 120)); // Losses: -$80 to -$200

            trades.Add(new Trade
            {
                EntryDate = DateTime.Today.AddDays(-(count - i) * 7),
                ExitDate = DateTime.Today.AddDays((-(count - i) * 7) + 3),
                ProfitLoss = pnl,
                Symbol = SimulationSymbol,
                Strategy = "EarningsCalendarSpread"
            });
        }

        return trades;
    }

    /// <summary>
    /// Displays position sizing results.
    /// </summary>
    private static void DisplayPositionSize(PositionSizeResult result)
    {
        Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ PHASE 10: POSITION SIZING - Kelly Criterion                                 │");
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Historical Trades:   ", result.HistoricalTrades.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Win Rate:            ", $"{result.WinRate:P1}"));
        Console.WriteLine(FormatBoxLine("Win/Loss Ratio:      ", $"{result.WinLossRatio:F2}x"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Full Kelly:          ", $"{result.FullKelly:P2}"));
        Console.WriteLine(FormatBoxLine("Fractional Kelly:    ", $"{result.FractionalKelly:P2} (25% of full)"));
        Console.WriteLine(FormatBoxLine("Final Allocation:    ", $"{result.AllocationPercent:P2}"));
        Console.WriteLine("├──────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine(FormatBoxLine("Portfolio Value:     ", $"${PortfolioValue:N0}"));
        Console.WriteLine(FormatBoxLine("Dollar Allocation:   ", $"${result.DollarAllocation:N2}"));
        Console.WriteLine(FormatBoxLine("Contracts:           ", result.Contracts.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(FormatBoxLine("Max Loss/Contract:   ", $"${result.MaxLossPerContract:N2}"));
        Console.WriteLine(FormatBoxLine("Total Risk:          ", $"${result.TotalRisk:N2}"));
        Console.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }

    // ========================================================================
    // Phase 11: Trade Recommendation
    // ========================================================================

    /// <summary>
    /// Displays the final trade recommendation summary.
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
        Console.WriteLine("║                        TRADE RECOMMENDATION SUMMARY                         ║");
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
/// Position sizing result using Kelly Criterion.
/// </summary>
internal sealed class PositionSizeResult
{
    public int Contracts { get; init; }
    public double AllocationPercent { get; init; }
    public double DollarAllocation { get; init; }
    public double FullKelly { get; init; }
    public double FractionalKelly { get; init; }
    public double WinRate { get; init; }
    public double WinLossRatio { get; init; }
    public int HistoricalTrades { get; init; }
    public double MaxLossPerContract { get; init; }
    public double TotalRisk { get; init; }
}

/// <summary>
/// Simulated trade record for Kelly calculation.
/// </summary>
internal sealed class Trade
{
    public DateTime EntryDate { get; init; }
    public DateTime ExitDate { get; init; }
    public double ProfitLoss { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string Strategy { get; init; } = string.Empty;
}

/// <summary>
/// Pricing regime enumeration.
/// </summary>
internal enum PricingRegime
{
    /// <summary>Standard positive interest rate pricing (Alaris.Quantlib).</summary>
    PositiveRates,

    /// <summary>Negative rates with double boundary (Alaris.Double, Healy 2021).</summary>
    DoubleBoundary,

    /// <summary>Negative rates with single boundary (q ≥ r).</summary>
    NegativeRatesSingleBoundary
}

#pragma warning restore CA5394 // Do not use insecure randomness
#pragma warning restore CA1303 // Do not pass literals as localized parameters