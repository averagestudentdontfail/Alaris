using Alaris.Core.HotPath;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Calendar;
using Alaris.Strategy.Model;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Core;

/// <summary>
/// Generates trading signals for earnings calendar spread opportunities.
/// Implements the strategy from Atilgan (2014) and incorporates term structure analysis.
/// Enhanced with Leung &amp; Santoli (2014) model for theoretical IV and mispricing signals.
/// </summary>
public sealed class STCR001A
{
    private readonly STDT001A _marketData;
    private readonly STCR003A _yangZhang;
    private readonly STTM001A _termAnalyzer;
    private readonly STIV005A _earningsCalibrator;
    private readonly ILogger<STCR001A>? _logger;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, DateTime, Exception?> LogGeneratingSTCR004A =
        LoggerMessage.Define<string, DateTime>(
            LogLevel.Information,
            new EventId(1, nameof(LogGeneratingSTCR004A)),
            "Generating signal for {Symbol} with earnings on {EarningsDate}");

    private static readonly Action<ILogger, string, Exception?> LogInsufficientPriceHistory =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, nameof(LogInsufficientPriceHistory)),
            "Insufficient price history for {Symbol}");

    private static readonly Action<ILogger, string, Exception?> LogNoOptionData =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3, nameof(LogNoOptionData)),
            "No option data available for {Symbol}");

    private static readonly Action<ILogger, string, Exception?> LogInsufficientSTTM001A =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(4, nameof(LogInsufficientSTTM001A)),
            "Insufficient term structure points for {Symbol}");

    private static readonly Action<ILogger, string, STCR004AStrength, double, double, long, Exception?> LogSTCR004AGenerated =
        LoggerMessage.Define<string, STCR004AStrength, double, double, long>(
            LogLevel.Information,
            new EventId(5, nameof(LogSTCR004AGenerated)),
            "STCR004A generated for {Symbol}: {Strength} (IV/RV={IvRv:F2}, Slope={Slope:F5}, Volume={Volume})");

    private static readonly Action<ILogger, string, Exception?> LogErrorGeneratingSTCR004A =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(6, nameof(LogErrorGeneratingSTCR004A)),
            "Error generating signal for {Symbol}");

    private static readonly Action<ILogger, string, double, double, double, Exception?> LogLeungSantoliMetrics =
        LoggerMessage.Define<string, double, double, double>(
            LogLevel.Information,
            new EventId(7, nameof(LogLeungSantoliMetrics)),
            "L&S model for {Symbol}: sigmaE={SigmaE:P2}, theoreticalIV={TheoreticalIV:P2}, mispricing={Mispricing:P2}");

    // Strategy thresholds from research
    private const double MinIvRvRatio = 1.25;
    private const double MaxTermSlope = -0.00406;
    private const long MinAverageVolume = 1_500_000;

    /// <summary>
    /// Initializes a new instance of the STCR001A class.
    /// </summary>
    /// <param name="marketData">Market data provider for prices and options.</param>
    /// <param name="yangZhang">Yang-Zhang volatility estimator.</param>
    /// <param name="termAnalyzer">Term structure analyzer.</param>
    /// <param name="earningsCalibrator">Optional L&amp;S earnings jump calibrator.</param>
    /// <param name="logger">Optional logger instance.</param>
    public STCR001A(
        STDT001A marketData,
        STCR003A yangZhang,
        STTM001A termAnalyzer,
        STIV005A? earningsCalibrator = null,
        ILogger<STCR001A>? logger = null)
    {
        _marketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
        _yangZhang = yangZhang ?? throw new ArgumentNullException(nameof(yangZhang));
        _termAnalyzer = termAnalyzer ?? throw new ArgumentNullException(nameof(termAnalyzer));
        _earningsCalibrator = earningsCalibrator ?? new STIV005A();
        _logger = logger;
    }

    /// <summary>
    /// Generates a trading signal for a given symbol before an earnings announcement.
    /// </summary>
    public STCR004A Generate(string symbol, DateTime earningsDate, DateTime evaluationDate)
    {
        return Generate(symbol, earningsDate, evaluationDate, null);
    }

    /// <summary>
    /// Generates a trading signal with Leung &amp; Santoli (2014) model calibration.
    /// </summary>
    /// <param name="symbol">The security symbol.</param>
    /// <param name="earningsDate">Upcoming earnings announcement date.</param>
    /// <param name="evaluationDate">Date when signal is being evaluated.</param>
    /// <param name="historicalEarningsDates">Historical earnings dates for sigma_e calibration.</param>
    /// <returns>Trading signal with L&amp;S model metrics if calibration data is provided.</returns>
    public STCR004A Generate(
        string symbol,
        DateTime earningsDate,
        DateTime evaluationDate,
        IReadOnlyList<DateTime>? historicalEarningsDates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        SafeLog(() => LogGeneratingSTCR004A(_logger!, symbol, earningsDate, null));

        STCR004A signal = new STCR004A
        {
            Symbol = symbol,
            EarningsDate = earningsDate,
            STCR004ADate = evaluationDate
        };

        try
        {
            // Fetch and validate market data
            (List<PriceBar> priceHistory, STDT002A optionChain) = FetchMarketData(symbol, evaluationDate);
            if (priceHistory.Count < 30)
            {
                SafeLog(() => LogInsufficientPriceHistory(_logger!, symbol, null));
                signal.Strength = STCR004AStrength.Avoid;
                return signal;
            }

            // Fail-fast: Real market IV is mandatory - no synthetic fallbacks
            // Options data must be bootstrapped via CLI before running backtest
            if (optionChain.Expiries.Count == 0 || !HasValidIVData(optionChain))
            {
                SafeLog(() => LogNoOptionData(_logger!, symbol, null));
                throw new InvalidOperationException(
                    $"No valid options data with IV available for {symbol} on {evaluationDate:yyyy-MM-dd}. " +
                    "Run 'alaris backtest run --auto-bootstrap' to download historical options data from Polygon API.");
            }

            // Calculate signal metrics (including L&S model if historical data provided)
            CalculateSTCR004AMetrics(signal, priceHistory, optionChain, earningsDate, evaluationDate, historicalEarningsDates);

            // Log final signal
            SafeLog(() => LogSTCR004AGenerated(_logger!, symbol, signal.Strength, signal.IVRVRatio, signal.STTM001ASlope, signal.AverageVolume, null));
        }
        catch (ArgumentException ex)
        {
            SafeLog(() => LogErrorGeneratingSTCR004A(_logger!, symbol, ex));
            throw;
        }
        catch (InvalidOperationException ex)
        {
            SafeLog(() => LogErrorGeneratingSTCR004A(_logger!, symbol, ex));
            throw;
        }
        catch (DivideByZeroException ex)
        {
            SafeLog(() => LogErrorGeneratingSTCR004A(_logger!, symbol, ex));
            throw;
        }

        return signal;
    }

    /// <summary>
    /// Fetches market data required for signal generation.
    /// </summary>
    private (List<PriceBar> priceHistory, STDT002A optionChain) FetchMarketData(string symbol, DateTime evaluationDate)
    {
        List<PriceBar> priceHistory = _marketData.GetHistoricalPrices(symbol, 90).ToList();
        STDT002A optionChain = _marketData.GetSTDT002A(symbol, evaluationDate);
        return (priceHistory, optionChain);
    }

    /// <summary>
    /// Calculates all signal metrics including volatility, term structure, and criteria.
    /// Enhanced with Leung &amp; Santoli (2014) model calculations.
    /// </summary>
    private void CalculateSTCR004AMetrics(
        STCR004A signal,
        List<PriceBar> priceHistory,
        STDT002A optionChain,
        DateTime earningsDate,
        DateTime evaluationDate,
        IReadOnlyList<DateTime>? historicalEarningsDates)
    {
        // Calculate 30-day Yang-Zhang realized volatility
        signal.RealizedVolatility30 = _yangZhang.Calculate(priceHistory, 30);

        // Extract and analyze term structure
        List<STTM001APoint> termPoints = ExtractSTTM001APoints(optionChain, evaluationDate);
        if (termPoints.Count < 2)
        {
            SafeLog(() => LogInsufficientSTTM001A(_logger!, signal.Symbol, null));
            signal.Strength = STCR004AStrength.Avoid;
            return;
        }

        STTM001AAnalysis termAnalysis = _termAnalyzer.Analyze(termPoints);
        signal.STTM001ASlope = termAnalysis.Slope;
        signal.ImpliedVolatility30 = termAnalysis.GetIVAt(30);

        // Calculate IV/RV ratio
        signal.IVRVRatio = (signal.RealizedVolatility30 > 0)
            ? (signal.ImpliedVolatility30 / signal.RealizedVolatility30)
            : 0;

        // Calculate average volume (Index-based loop for performance)
        long totalVolume = 0;
        int volumeStart = Math.Max(0, priceHistory.Count - 30);
        int volumeCount = priceHistory.Count - volumeStart;
        for (int i = volumeStart; i < priceHistory.Count; i++)
        {
            totalVolume += priceHistory[i].Volume;
        }
        signal.AverageVolume = volumeCount > 0 ? totalVolume / volumeCount : 0;

        // Calculate expected move and volatility spread
        signal.ExpectedMove = CalculateExpectedMove(optionChain, earningsDate, evaluationDate);
        signal.VolatilitySpread = CalculateVolatilitySpread(optionChain, evaluationDate);

        // Leung & Santoli (2014) Model Calculations
        CalculateLeungSantoliMetrics(signal, priceHistory, optionChain, earningsDate, evaluationDate, historicalEarningsDates);

        // Evaluate criteria
        signal.Criteria["Volume"] = signal.AverageVolume >= MinAverageVolume;
        signal.Criteria["IV/RV"] = signal.IVRVRatio >= MinIvRvRatio;
        signal.Criteria["TermSlope"] = signal.STTM001ASlope <= MaxTermSlope;

        signal.EvaluateStrength();
    }

    /// <summary>
    /// Calculates Leung &amp; Santoli (2014) model metrics including:
    /// - Earnings jump volatility (sigma_e) calibrated from historical EA moves
    /// - Theoretical pre-EA implied volatility: I(t) = sqrt(sigma^2 + sigma_e^2/(T-t))
    /// - IV mispricing signal (market IV vs theoretical)
    /// - Expected IV crush magnitude
    /// </summary>
    private void CalculateLeungSantoliMetrics(
        STCR004A signal,
        List<PriceBar> priceHistory,
        STDT002A optionChain,
        DateTime earningsDate,
        DateTime evaluationDate,
        IReadOnlyList<DateTime>? historicalEarningsDates)
    {
        if (TryCalibrateFromHistory(signal, priceHistory, historicalEarningsDates))
        {
            ComputeTheoreticalMetrics(signal, optionChain, earningsDate, evaluationDate);
            SafeLog(() => LogLeungSantoliMetrics(_logger!, signal.Symbol, signal.EarningsJumpVolatility, signal.TheoreticalIV, signal.IVMispricingSTCR004A, null));
            return;
        }

        if (TryCalibrateFromTermStructure(signal, optionChain, evaluationDate))
        {
            ComputeTheoreticalMetrics(signal, optionChain, earningsDate, evaluationDate);
            SafeLog(() => LogLeungSantoliMetrics(_logger!, signal.Symbol, signal.EarningsJumpVolatility, signal.TheoreticalIV, signal.IVMispricingSTCR004A, null));
        }
    }

    private bool TryCalibrateFromHistory(STCR004A signal, List<PriceBar> priceHistory, IReadOnlyList<DateTime>? historicalEarningsDates)
    {
        if (historicalEarningsDates == null || historicalEarningsDates.Count < 4)
        {
            return false;
        }

        EarningsJumpCalibration calibration = _earningsCalibrator.Calibrate(signal.Symbol, priceHistory, historicalEarningsDates);
        if (!calibration.IsValid || !calibration.SigmaE.HasValue)
        {
            return false;
        }

        signal.EarningsJumpVolatility = calibration.SigmaE.Value;
        signal.HistoricalEarningsCount = calibration.SampleCount;
        signal.IsLeungSantoliCalibrated = true;
        signal.BaseVolatility = signal.RealizedVolatility30;
        return true;
    }

    private bool TryCalibrateFromTermStructure(STCR004A signal, STDT002A optionChain, DateTime evaluationDate)
    {
        List<STTM001APoint> termPoints = ExtractSTTM001APoints(optionChain, evaluationDate);
        if (termPoints.Count < 2)
        {
            return false;
        }

        // Find min and second min dte - ZERO ALLOC
        IReadOnlyList<STTM001APoint> points = termPoints;
        STTM001APoint? nearTerm = CRFN001A.FindMinBy(points, p => p.DaysToExpiry);
        STTM001APoint? farTerm = CRFN001A.FindMinBy(points, p => p.DaysToExpiry, p => p != nearTerm);

        if (nearTerm == null || farTerm == null)
        {
            return false;
        }

        double? sigmaE = STIV005A.STTM001AEstimator(nearTerm.ImpliedVolatility, nearTerm.DaysToExpiry, farTerm.ImpliedVolatility, farTerm.DaysToExpiry);
        double? baseVol = STIV005A.BaseVolatilityEstimator(nearTerm.ImpliedVolatility, nearTerm.DaysToExpiry, farTerm.ImpliedVolatility, farTerm.DaysToExpiry);

        if (sigmaE.HasValue && sigmaE.Value > 0)
        {
            signal.EarningsJumpVolatility = sigmaE.Value;
            signal.BaseVolatility = baseVol ?? signal.RealizedVolatility30;
            signal.IsLeungSantoliCalibrated = true;
            signal.HistoricalEarningsCount = 0;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Computes theoretical IV, mispricing signal, and IV crush metrics.
    /// </summary>
    private static void ComputeTheoreticalMetrics(
        STCR004A signal,
        STDT002A optionChain,
        DateTime earningsDate,
        DateTime evaluationDate)
    {
        // Find the front-month expiry (closest to but after earnings) - ZERO ALLOC
        OptionExpiry? targetExpiry = CRFN001A.FindMinBy<OptionExpiry>(
            optionChain.Expiries,
            e => (e.ExpiryDate - earningsDate).TotalDays,
            e => e.ExpiryDate >= earningsDate);

        if (targetExpiry == null)
        {
            return;
        }

        int dte = targetExpiry.GetDaysToExpiry(evaluationDate);
        if (dte <= 0)
        {
            return;
        }

        double timeToExpiry = TradingCalendarDefaults.DteToYears(dte);

        // Compute theoretical IV using L&S formula: I(t) = sqrt(sigma^2 + sigma_e^2/(T-t))
        signal.TheoreticalIV = STIV004A.ComputeTheoreticalIV(
            signal.BaseVolatility,
            signal.EarningsJumpVolatility,
            timeToExpiry);

        // Compute mispricing signal (market IV - theoretical IV)
        signal.IVMispricingSTCR004A = signal.ImpliedVolatility30 - signal.TheoreticalIV;

        // Compute expected IV crush
        signal.ExpectedIVCrush = STIV004A.ComputeExpectedIVCrush(
            signal.BaseVolatility,
            signal.EarningsJumpVolatility,
            timeToExpiry);

        signal.IVCrushRatio = STIV004A.ComputeIVCrushRatio(
            signal.BaseVolatility,
            signal.EarningsJumpVolatility,
            timeToExpiry);
    }

    /// <summary>
    /// Extracts term structure points from the option chain.
    /// </summary>
    private List<STTM001APoint> ExtractSTTM001APoints(STDT002A chain, DateTime evaluationDate)
    {
        List<STTM001APoint> points = new List<STTM001APoint>();
        double underlyingPrice = chain.UnderlyingPrice;

        foreach (OptionExpiry expiry in chain.Expiries.OrderBy(e => e.ExpiryDate))
        {
            int dte = expiry.GetDaysToExpiry(evaluationDate);
            if ((dte < 1) || (dte > 60))
            {
                continue;
            }

            // Find ATM options - ZERO ALLOC
            OptionContract? atmCall = CRFN001A.FindMinBy<OptionContract>(
                expiry.Calls,
                c => Math.Abs(c.Strike - underlyingPrice),
                c => (c.OpenInterest > 0) && (c.ImpliedVolatility > 0));

            OptionContract? atmPut = CRFN001A.FindMinBy<OptionContract>(
                expiry.Puts,
                p => Math.Abs(p.Strike - underlyingPrice),
                p => (p.OpenInterest > 0) && (p.ImpliedVolatility > 0));

            if ((atmCall is not null) && (atmPut is not null))
            {
                // Average the call and put IV for ATM
                double avgIV = (atmCall.ImpliedVolatility + atmPut.ImpliedVolatility) / 2.0;
                
                points.Add(new STTM001APoint
                {
                    DaysToExpiry = dte,
                    ImpliedVolatility = avgIV,
                    Strike = atmCall.Strike
                });
            }
        }

        return points;
    }

    /// <summary>
    /// Calculates the expected move from the ATM straddle price.
    /// </summary>
    private double CalculateExpectedMove(STDT002A chain, DateTime earningsDate, DateTime evaluationDate)
    {
        // Find the expiry closest to (but after) earnings date - ZERO ALLOC
        OptionExpiry? targetExpiry = CRFN001A.FindMinBy<OptionExpiry>(
            chain.Expiries,
            e => (e.ExpiryDate - earningsDate).TotalDays,
            e => e.ExpiryDate >= earningsDate);

        if (targetExpiry is null)
        {
            return 0;
        }

        double underlyingPrice = chain.UnderlyingPrice;

        // Find ATM straddle - ZERO ALLOC
        OptionContract? atmCall = CRFN001A.FindMinBy<OptionContract>(
            targetExpiry.Calls,
            c => Math.Abs(c.Strike - underlyingPrice),
            c => (c.Bid > 0) && (c.Ask > 0));

        OptionContract? atmPut = CRFN001A.FirstWhere<OptionContract>(
            targetExpiry.Puts,
            p => (p.Bid > 0) && (p.Ask > 0) && (Math.Abs(p.Strike - (atmCall?.Strike ?? 0)) < 0.01));

        if ((atmCall is null) || (atmPut is null))
        {
            return 0;
        }

        double straddlePrice = atmCall.MidPrice + atmPut.MidPrice;
        return straddlePrice / underlyingPrice;
    }

    /// <summary>
    /// Calculates the weighted volatility spread (put IV - call IV) as per Atilgan (2014).
    /// </summary>
    private double CalculateVolatilitySpread(STDT002A chain, DateTime evaluationDate)
    {
        List<(double spread, double weight)> spreads = new List<(double spread, double weight)>();
        double underlyingPrice = chain.UnderlyingPrice;

        foreach (OptionExpiry expiry in chain.Expiries)
        {
            int dte = expiry.GetDaysToExpiry(evaluationDate);
            if ((dte < 10) || (dte > 60))
            {
                continue;
            }

            // Match put-call pairs
            IEnumerable<(double Strike, double Spread, double Weight)> pairs = from call in expiry.Calls
                        join put in expiry.Puts on call.Strike equals put.Strike
                        where (call.OpenInterest > 0) && (put.OpenInterest > 0)
                           && (call.ImpliedVolatility > 0) && (put.ImpliedVolatility > 0)
                        select (call.Strike, put.ImpliedVolatility - call.ImpliedVolatility, (call.OpenInterest + put.OpenInterest) / 2.0);

            foreach ((double Strike, double Spread, double Weight) pair in pairs)
            {
                spreads.Add((pair.Spread, pair.Weight));
            }
        }

        if (spreads.Count == 0)
        {
            return 0;
        }

        double totalWeight = spreads.Sum(s => s.weight);
        return spreads.Sum(s => s.spread * s.weight) / totalWeight;
    }

    /// <summary>
    /// Checks if the option chain has valid IV data for signal generation.
    /// Returns false if all IVs are zero or missing.
    /// </summary>
    private static bool HasValidIVData(STDT002A optionChain)
    {
        foreach (var expiry in optionChain.Expiries)
        {
            foreach (var call in expiry.Calls)
            {
                if (call.ImpliedVolatility > 0.001)
                {
                    return true;
                }
            }
            foreach (var put in expiry.Puts)
            {
                if (put.ImpliedVolatility > 0.001)
                {
                    return true;
                }
            }
        }
        return false;
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
}