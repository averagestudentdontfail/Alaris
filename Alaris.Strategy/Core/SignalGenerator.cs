using Alaris.Strategy.Bridge;
using Alaris.Strategy.Model;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Core;

/// <summary>
/// Generates trading signals for earnings calendar spread opportunities.
/// Implements the strategy from Atilgan (2014) and incorporates term structure analysis.
/// Enhanced with Leung &amp; Santoli (2014) model for theoretical IV and mispricing signals.
/// </summary>
public sealed class SignalGenerator
{
    private readonly IMarketDataProvider _marketData;
    private readonly YangZhangEstimator _yangZhang;
    private readonly TermStructureAnalyzer _termAnalyzer;
    private readonly EarningsJumpCalibrator _earningsCalibrator;
    private readonly ILogger<SignalGenerator>? _logger;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, DateTime, Exception?> LogGeneratingSignal =
        LoggerMessage.Define<string, DateTime>(
            LogLevel.Information,
            new EventId(1, nameof(LogGeneratingSignal)),
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

    private static readonly Action<ILogger, string, Exception?> LogInsufficientTermStructure =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(4, nameof(LogInsufficientTermStructure)),
            "Insufficient term structure points for {Symbol}");

    private static readonly Action<ILogger, string, SignalStrength, double, double, long, Exception?> LogSignalGenerated =
        LoggerMessage.Define<string, SignalStrength, double, double, long>(
            LogLevel.Information,
            new EventId(5, nameof(LogSignalGenerated)),
            "Signal generated for {Symbol}: {Strength} (IV/RV={IvRv:F2}, Slope={Slope:F5}, Volume={Volume})");

    private static readonly Action<ILogger, string, Exception?> LogErrorGeneratingSignal =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(6, nameof(LogErrorGeneratingSignal)),
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
    /// Initializes a new instance of the SignalGenerator class.
    /// </summary>
    /// <param name="marketData">Market data provider for prices and options.</param>
    /// <param name="yangZhang">Yang-Zhang volatility estimator.</param>
    /// <param name="termAnalyzer">Term structure analyzer.</param>
    /// <param name="earningsCalibrator">Optional L&amp;S earnings jump calibrator.</param>
    /// <param name="logger">Optional logger instance.</param>
    public SignalGenerator(
        IMarketDataProvider marketData,
        YangZhangEstimator yangZhang,
        TermStructureAnalyzer termAnalyzer,
        EarningsJumpCalibrator? earningsCalibrator = null,
        ILogger<SignalGenerator>? logger = null)
    {
        _marketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
        _yangZhang = yangZhang ?? throw new ArgumentNullException(nameof(yangZhang));
        _termAnalyzer = termAnalyzer ?? throw new ArgumentNullException(nameof(termAnalyzer));
        _earningsCalibrator = earningsCalibrator ?? new EarningsJumpCalibrator();
        _logger = logger;
    }

    /// <summary>
    /// Generates a trading signal for a given symbol before an earnings announcement.
    /// </summary>
    public Signal Generate(string symbol, DateTime earningsDate, DateTime evaluationDate)
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
    public Signal Generate(
        string symbol,
        DateTime earningsDate,
        DateTime evaluationDate,
        IReadOnlyList<DateTime>? historicalEarningsDates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        SafeLog(() => LogGeneratingSignal(_logger!, symbol, earningsDate, null));

        Signal signal = new Signal
        {
            Symbol = symbol,
            EarningsDate = earningsDate,
            SignalDate = evaluationDate
        };

        try
        {
            // Fetch and validate market data
            (List<PriceBar> priceHistory, OptionChain optionChain) = FetchMarketData(symbol, evaluationDate);
            if (priceHistory.Count < 30)
            {
                SafeLog(() => LogInsufficientPriceHistory(_logger!, symbol, null));
                signal.Strength = SignalStrength.Avoid;
                return signal;
            }

            if (optionChain.Expiries.Count == 0)
            {
                SafeLog(() => LogNoOptionData(_logger!, symbol, null));
                signal.Strength = SignalStrength.Avoid;
                return signal;
            }

            // Calculate signal metrics (including L&S model if historical data provided)
            CalculateSignalMetrics(signal, priceHistory, optionChain, earningsDate, evaluationDate, historicalEarningsDates);

            // Log final signal
            SafeLog(() => LogSignalGenerated(_logger!, symbol, signal.Strength, signal.IVRVRatio, signal.TermStructureSlope, signal.AverageVolume, null));
        }
        catch (ArgumentException ex)
        {
            SafeLog(() => LogErrorGeneratingSignal(_logger!, symbol, ex));
            throw;
        }
        catch (InvalidOperationException ex)
        {
            SafeLog(() => LogErrorGeneratingSignal(_logger!, symbol, ex));
            throw;
        }
        catch (DivideByZeroException ex)
        {
            SafeLog(() => LogErrorGeneratingSignal(_logger!, symbol, ex));
            throw;
        }

        return signal;
    }

    /// <summary>
    /// Fetches market data required for signal generation.
    /// </summary>
    private (List<PriceBar> priceHistory, OptionChain optionChain) FetchMarketData(string symbol, DateTime evaluationDate)
    {
        List<PriceBar> priceHistory = _marketData.GetHistoricalPrices(symbol, 90).ToList();
        OptionChain optionChain = _marketData.GetOptionChain(symbol, evaluationDate);
        return (priceHistory, optionChain);
    }

    /// <summary>
    /// Calculates all signal metrics including volatility, term structure, and criteria.
    /// Enhanced with Leung &amp; Santoli (2014) model calculations.
    /// </summary>
    private void CalculateSignalMetrics(
        Signal signal,
        List<PriceBar> priceHistory,
        OptionChain optionChain,
        DateTime earningsDate,
        DateTime evaluationDate,
        IReadOnlyList<DateTime>? historicalEarningsDates)
    {
        // Calculate 30-day Yang-Zhang realized volatility
        signal.RealizedVolatility30 = _yangZhang.Calculate(priceHistory, 30);

        // Extract and analyze term structure
        List<TermStructurePoint> termPoints = ExtractTermStructurePoints(optionChain, evaluationDate);
        if (termPoints.Count < 2)
        {
            SafeLog(() => LogInsufficientTermStructure(_logger!, signal.Symbol, null));
            signal.Strength = SignalStrength.Avoid;
            return;
        }

        TermStructureAnalysis termAnalysis = _termAnalyzer.Analyze(termPoints);
        signal.TermStructureSlope = termAnalysis.Slope;
        signal.ImpliedVolatility30 = termAnalysis.GetIVAt(30);

        // Calculate IV/RV ratio
        signal.IVRVRatio = (signal.RealizedVolatility30 > 0)
            ? (signal.ImpliedVolatility30 / signal.RealizedVolatility30)
            : 0;

        // Calculate average volume
        signal.AverageVolume = (long)priceHistory.TakeLast(30).Average(p => p.Volume);

        // Calculate expected move and volatility spread
        signal.ExpectedMove = CalculateExpectedMove(optionChain, earningsDate, evaluationDate);
        signal.VolatilitySpread = CalculateVolatilitySpread(optionChain, evaluationDate);

        // ================================================================
        // Leung & Santoli (2014) Model Calculations
        // ================================================================
        CalculateLeungSantoliMetrics(signal, priceHistory, optionChain, earningsDate, evaluationDate, historicalEarningsDates);

        // Evaluate criteria
        signal.Criteria["Volume"] = signal.AverageVolume >= MinAverageVolume;
        signal.Criteria["IV/RV"] = signal.IVRVRatio >= MinIvRvRatio;
        signal.Criteria["TermSlope"] = signal.TermStructureSlope <= MaxTermSlope;

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
        Signal signal,
        List<PriceBar> priceHistory,
        OptionChain optionChain,
        DateTime earningsDate,
        DateTime evaluationDate,
        IReadOnlyList<DateTime>? historicalEarningsDates)
    {
        // First attempt: Calibrate sigma_e from historical earnings moves
        if (historicalEarningsDates != null && historicalEarningsDates.Count >= 4)
        {
            EarningsJumpCalibration calibration = _earningsCalibrator.Calibrate(
                signal.Symbol,
                priceHistory,
                historicalEarningsDates);

            if (calibration.IsValid && calibration.SigmaE.HasValue)
            {
                signal.EarningsJumpVolatility = calibration.SigmaE.Value;
                signal.HistoricalEarningsCount = calibration.SampleCount;
                signal.IsLeungSantoliCalibrated = true;

                // Use realized volatility as base volatility estimate
                signal.BaseVolatility = signal.RealizedVolatility30;

                // Compute theoretical IV and related metrics
                ComputeTheoreticalMetrics(signal, optionChain, earningsDate, evaluationDate);

                SafeLog(() => LogLeungSantoliMetrics(
                    _logger!,
                    signal.Symbol,
                    signal.EarningsJumpVolatility,
                    signal.TheoreticalIV,
                    signal.IVMispricingSignal,
                    null));
                return;
            }
        }

        // Fallback: Extract sigma_e from term structure using L&S estimator (Section 5.2)
        List<TermStructurePoint> termPoints = ExtractTermStructurePoints(optionChain, evaluationDate);
        if (termPoints.Count >= 2)
        {
            TermStructurePoint nearTerm = termPoints.OrderBy(p => p.DaysToExpiry).First();
            TermStructurePoint farTerm = termPoints.OrderBy(p => p.DaysToExpiry).Skip(1).First();

            double? sigmaE = EarningsJumpCalibrator.TermStructureEstimator(
                nearTerm.ImpliedVolatility,
                nearTerm.DaysToExpiry,
                farTerm.ImpliedVolatility,
                farTerm.DaysToExpiry);

            double? baseVol = EarningsJumpCalibrator.BaseVolatilityEstimator(
                nearTerm.ImpliedVolatility,
                nearTerm.DaysToExpiry,
                farTerm.ImpliedVolatility,
                farTerm.DaysToExpiry);

            if (sigmaE.HasValue && sigmaE.Value > 0)
            {
                signal.EarningsJumpVolatility = sigmaE.Value;
                signal.BaseVolatility = baseVol ?? signal.RealizedVolatility30;
                signal.IsLeungSantoliCalibrated = true;
                signal.HistoricalEarningsCount = 0; // Term structure derived

                ComputeTheoreticalMetrics(signal, optionChain, earningsDate, evaluationDate);

                SafeLog(() => LogLeungSantoliMetrics(
                    _logger!,
                    signal.Symbol,
                    signal.EarningsJumpVolatility,
                    signal.TheoreticalIV,
                    signal.IVMispricingSignal,
                    null));
            }
        }
    }

    /// <summary>
    /// Computes theoretical IV, mispricing signal, and IV crush metrics.
    /// </summary>
    private static void ComputeTheoreticalMetrics(
        Signal signal,
        OptionChain optionChain,
        DateTime earningsDate,
        DateTime evaluationDate)
    {
        // Find the front-month expiry (closest to but after earnings)
        OptionExpiry? targetExpiry = optionChain.Expiries
            .Where(e => e.ExpiryDate >= earningsDate)
            .OrderBy(e => e.ExpiryDate)
            .FirstOrDefault();

        if (targetExpiry == null)
        {
            return;
        }

        int dte = targetExpiry.GetDaysToExpiry(evaluationDate);
        if (dte <= 0)
        {
            return;
        }

        double timeToExpiry = dte / 252.0; // Convert to years

        // Compute theoretical IV using L&S formula: I(t) = sqrt(sigma^2 + sigma_e^2/(T-t))
        signal.TheoreticalIV = LeungSantoliModel.ComputeTheoreticalIV(
            signal.BaseVolatility,
            signal.EarningsJumpVolatility,
            timeToExpiry);

        // Compute mispricing signal (market IV - theoretical IV)
        signal.IVMispricingSignal = signal.ImpliedVolatility30 - signal.TheoreticalIV;

        // Compute expected IV crush
        signal.ExpectedIVCrush = LeungSantoliModel.ComputeExpectedIVCrush(
            signal.BaseVolatility,
            signal.EarningsJumpVolatility,
            timeToExpiry);

        signal.IVCrushRatio = LeungSantoliModel.ComputeIVCrushRatio(
            signal.BaseVolatility,
            signal.EarningsJumpVolatility,
            timeToExpiry);
    }

    /// <summary>
    /// Extracts term structure points from the option chain.
    /// </summary>
    private List<TermStructurePoint> ExtractTermStructurePoints(OptionChain chain, DateTime evaluationDate)
    {
        List<TermStructurePoint> points = new List<TermStructurePoint>();
        double underlyingPrice = chain.UnderlyingPrice;

        foreach (OptionExpiry expiry in chain.Expiries.OrderBy(e => e.ExpiryDate))
        {
            int dte = expiry.GetDaysToExpiry(evaluationDate);
            if ((dte < 1) || (dte > 60))
            {
                continue;
            }

            // Find ATM options
            OptionContract? atmCall = expiry.Calls
                .Where(c => (c.OpenInterest > 0) && (c.ImpliedVolatility > 0))
                .OrderBy(c => Math.Abs(c.Strike - underlyingPrice))
                .FirstOrDefault();

            OptionContract? atmPut = expiry.Puts
                .Where(p => (p.OpenInterest > 0) && (p.ImpliedVolatility > 0))
                .OrderBy(p => Math.Abs(p.Strike - underlyingPrice))
                .FirstOrDefault();

            if ((atmCall is not null) && (atmPut is not null))
            {
                // Average the call and put IV for ATM
                double avgIV = (atmCall.ImpliedVolatility + atmPut.ImpliedVolatility) / 2.0;
                
                points.Add(new TermStructurePoint
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
    private double CalculateExpectedMove(OptionChain chain, DateTime earningsDate, DateTime evaluationDate)
    {
        // Find the expiry closest to (but after) earnings date
        OptionExpiry? targetExpiry = chain.Expiries
            .Where(e => e.ExpiryDate >= earningsDate)
            .OrderBy(e => e.ExpiryDate)
            .FirstOrDefault();

        if (targetExpiry is null)
        {
            return 0;
        }

        double underlyingPrice = chain.UnderlyingPrice;

        // Find ATM straddle
        OptionContract? atmCall = targetExpiry.Calls
            .Where(c => (c.Bid > 0) && (c.Ask > 0))
            .OrderBy(c => Math.Abs(c.Strike - underlyingPrice))
            .FirstOrDefault();

        OptionContract? atmPut = targetExpiry.Puts
            .Where(p => (p.Bid > 0) && (p.Ask > 0) && (Math.Abs(p.Strike - (atmCall?.Strike ?? 0)) < 0.01))
            .FirstOrDefault();

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
    private double CalculateVolatilitySpread(OptionChain chain, DateTime evaluationDate)
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