// STIV020A.cs - Synthetic IV generator using Leung-Santoli model for backtest mode

using Alaris.Strategy.Bridge;
using Alaris.Strategy.Calendar;
using Alaris.Strategy.Model;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Core;

/// <summary>
/// Generates synthetic implied volatility for backtesting when historical options data is unavailable.
/// Uses the Leung &amp; Santoli (2014) model which derives theoretical IV from:
///   - Realized volatility (σ) from historical price data
///   - Earnings jump volatility (σ_e) calibrated from historical earnings moves
/// </summary>
/// <remarks>
/// The L&amp;S model gives pre-earnings IV as:
///     I(t; K, T) = √(σ² + σ_e²/(T-t))
/// 
/// This is theoretically grounded and provides a reasonable estimate when actual
/// options market data is not available (e.g., in backtest mode).
/// 
/// Reference: "Accounting for Earnings Announcements in the Pricing of Equity Options"
/// Tim Leung &amp; Marco Santoli (2014), Journal of Derivatives
/// </remarks>
public sealed class STIV020A
{
    private readonly STCR003A _yangZhang;
    private readonly STIV005A _earningsCalibrator;
    private readonly ILogger<STIV020A>? _logger;

    // Minimum required historical earnings for calibration
    private const int MinHistoricalEarnings = 4;

    // Default base volatility if RV calculation fails
    private const double DefaultBaseVolatility = 0.25;

    // Default earnings jump volatility if calibration fails
    private const double DefaultEarningsJumpVolatility = 0.05;

    // LoggerMessage delegates for high-performance structured logging
    private static readonly Action<ILogger, string, double, double, Exception?> LogSyntheticIVGenerated =
        LoggerMessage.Define<string, double, double>(
            LogLevel.Information,
            new EventId(1, nameof(LogSyntheticIVGenerated)),
            "STIV020A: {Symbol} base σ={BaseVol:P2}, σ_e={JumpVol:P2}");

    private static readonly Action<ILogger, int, Exception?> LogInsufficientPriceHistory =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(2, nameof(LogInsufficientPriceHistory)),
            "STIV020A: Insufficient price history ({Count} bars), using default σ");

    private static readonly Action<ILogger, string, Exception?> LogRVCalculationFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3, nameof(LogRVCalculationFailed)),
            "STIV020A: RV calculation failed: {Message}");

    private static readonly Action<ILogger, string, int, Exception?> LogInsufficientEarnings =
        LoggerMessage.Define<string, int>(
            LogLevel.Debug,
            new EventId(4, nameof(LogInsufficientEarnings)),
            "STIV020A: {Symbol} insufficient historical earnings ({Count}), using default σ_e");

    private static readonly Action<ILogger, string, Exception?> LogInvalidCalibration =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(5, nameof(LogInvalidCalibration)),
            "STIV020A: {Symbol} calibration returned invalid σ_e, using default");

    private static readonly Action<ILogger, string, string, Exception?> LogCalibrationFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(6, nameof(LogCalibrationFailed)),
            "STIV020A: {Symbol} σ_e calibration failed: {Message}");

    /// <summary>
    /// Initializes a new instance of the synthetic IV generator.
    /// </summary>
    /// <param name="yangZhang">Yang-Zhang volatility estimator for RV calculation.</param>
    /// <param name="earningsCalibrator">Leung-Santoli earnings jump calibrator.</param>
    /// <param name="logger">Optional logger instance.</param>
    public STIV020A(
        STCR003A yangZhang,
        STIV005A? earningsCalibrator = null,
        ILogger<STIV020A>? logger = null)
    {
        _yangZhang = yangZhang ?? throw new ArgumentNullException(nameof(yangZhang));
        _earningsCalibrator = earningsCalibrator ?? new STIV005A();
        _logger = logger;
    }

    /// <summary>
    /// Generates a synthetic option chain with estimated IVs for backtesting.
    /// </summary>
    /// <param name="symbol">The security symbol.</param>
    /// <param name="spotPrice">Current spot price.</param>
    /// <param name="priceHistory">Historical price bars for RV and σ_e calculation.</param>
    /// <param name="earningsDate">Next earnings announcement date.</param>
    /// <param name="evaluationDate">Date of signal evaluation.</param>
    /// <param name="historicalEarningsDates">Historical earnings dates for σ_e calibration.</param>
    /// <returns>Synthetic option chain with estimated IVs.</returns>
    public STDT002A GenerateSyntheticChain(
        string symbol,
        double spotPrice,
        IReadOnlyList<PriceBar> priceHistory,
        DateTime earningsDate,
        DateTime evaluationDate,
        IReadOnlyList<DateTime>? historicalEarningsDates = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(priceHistory);

        var chain = new STDT002A
        {
            Symbol = symbol,
            UnderlyingPrice = spotPrice,
            Timestamp = evaluationDate
        };

        // Step 1: Calculate base volatility (σ) using Yang-Zhang
        double baseVolatility = CalculateBaseVolatility(priceHistory);

        // Step 2: Calibrate earnings jump volatility (σ_e)
        double earningsJumpVolatility = CalibrateEarningsJumpVolatility(
            symbol, priceHistory, historicalEarningsDates);

        SafeLog(() => LogSyntheticIVGenerated(_logger!, symbol, baseVolatility, earningsJumpVolatility, null));

        // Step 3: Generate synthetic expiries
        GenerateSyntheticExpiries(
            chain, spotPrice, baseVolatility, earningsJumpVolatility,
            earningsDate, evaluationDate);

        return chain;
    }

    /// <summary>
    /// Computes synthetic IV at a specific DTE using Leung-Santoli formula.
    /// </summary>
    /// <param name="baseVolatility">Base (diffusion) volatility σ.</param>
    /// <param name="earningsJumpVolatility">Earnings jump volatility σ_e.</param>
    /// <param name="daysToExpiry">Days to option expiry.</param>
    /// <returns>Synthetic implied volatility.</returns>
    public static double ComputeSyntheticIV(
        double baseVolatility,
        double earningsJumpVolatility,
        int daysToExpiry)
    {
        if (daysToExpiry <= 0)
        {
            return baseVolatility;
        }

        double timeToExpiry = TradingCalendarDefaults.DteToYears(daysToExpiry);

        // L&S formula: I(t) = √(σ² + σ_e²/(T-t))
        return STIV004A.ComputeTheoreticalIV(
            baseVolatility,
            earningsJumpVolatility,
            timeToExpiry);
    }

    /// <summary>
    /// Calculates base volatility using Yang-Zhang estimator.
    /// </summary>
    private double CalculateBaseVolatility(IReadOnlyList<PriceBar> priceHistory)
    {
        if (priceHistory.Count < 30)
        {
            SafeLog(() => LogInsufficientPriceHistory(_logger!, priceHistory.Count, null));
            return DefaultBaseVolatility;
        }

#pragma warning disable CA1031 // Catch more specific exception - acceptable for volatility fallback
        try
        {
            double rv = _yangZhang.Calculate(priceHistory.ToList(), 30, true);
            return rv > 0 ? rv : DefaultBaseVolatility;
        }
        catch (InvalidOperationException ex)
        {
            SafeLog(() => LogRVCalculationFailed(_logger!, ex.Message, null));
            return DefaultBaseVolatility;
        }
        catch (ArgumentException ex)
        {
            SafeLog(() => LogRVCalculationFailed(_logger!, ex.Message, null));
            return DefaultBaseVolatility;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Calibrates earnings jump volatility from historical earnings moves.
    /// </summary>
    private double CalibrateEarningsJumpVolatility(
        string symbol,
        IReadOnlyList<PriceBar> priceHistory,
        IReadOnlyList<DateTime>? historicalEarningsDates)
    {
        if (historicalEarningsDates == null || historicalEarningsDates.Count < MinHistoricalEarnings)
        {
            SafeLog(() => LogInsufficientEarnings(_logger!, symbol, historicalEarningsDates?.Count ?? 0, null));
            return DefaultEarningsJumpVolatility;
        }

#pragma warning disable CA1031 // Catch more specific exception - acceptable for calibration fallback
        try
        {
            var calibration = _earningsCalibrator.Calibrate(
                symbol,
                priceHistory.ToList(),
                historicalEarningsDates);

            if (calibration.IsValid && calibration.SigmaE.HasValue && calibration.SigmaE.Value > 0)
            {
                return calibration.SigmaE.Value;
            }

            SafeLog(() => LogInvalidCalibration(_logger!, symbol, null));
            return DefaultEarningsJumpVolatility;
        }
        catch (InvalidOperationException ex)
        {
            SafeLog(() => LogCalibrationFailed(_logger!, symbol, ex.Message, null));
            return DefaultEarningsJumpVolatility;
        }
        catch (ArgumentException ex)
        {
            SafeLog(() => LogCalibrationFailed(_logger!, symbol, ex.Message, null));
            return DefaultEarningsJumpVolatility;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Generates synthetic option expiries with ATM options at standard DTEs.
    /// </summary>
    private static void GenerateSyntheticExpiries(
        STDT002A chain,
        double spotPrice,
        double baseVolatility,
        double earningsJumpVolatility,
        DateTime earningsDate,
        DateTime evaluationDate)
    {
        // Generate expiries at standard intervals: 7, 14, 21, 30, 45, 60 days
        int[] standardDtes = [7, 14, 21, 30, 45, 60];

        foreach (int dte in standardDtes)
        {
            DateTime expiryDate = evaluationDate.AddDays(dte);

            // Only include expiries after earnings for meaningful analysis
            bool isPostEarnings = expiryDate > earningsDate;

            // Calculate synthetic IV using L&S model
            double syntheticIV = ComputeSyntheticIV(baseVolatility, earningsJumpVolatility, dte);

            // If pre-earnings, IV should be elevated; if post-earnings, use base vol
            if (!isPostEarnings)
            {
                syntheticIV = baseVolatility;
            }

            var expiry = new OptionExpiry
            {
                ExpiryDate = expiryDate
            };

            // Generate ATM options (calls and puts)
            AddSyntheticATMOptions(expiry, spotPrice, syntheticIV);

            chain.Expiries.Add(expiry);
        }
    }

    /// <summary>
    /// Adds synthetic ATM call and put options to an expiry.
    /// </summary>
    private static void AddSyntheticATMOptions(
        OptionExpiry expiry,
        double spotPrice,
        double impliedVolatility)
    {
        // Round strike to nearest standard increment
        double strike = Math.Round(spotPrice / 5.0) * 5.0;
        if (strike <= 0)
        {
            strike = spotPrice;
        }

        // Synthetic ATM call
        expiry.Calls.Add(new OptionContract
        {
            Strike = strike,
            ImpliedVolatility = impliedVolatility,
            Bid = 0.01,  // Nominal values for structure
            Ask = 0.02,
            LastPrice = 0.015,
            OpenInterest = 1000,
            Volume = 100
        });

        // Synthetic ATM put (same IV for ATM)
        expiry.Puts.Add(new OptionContract
        {
            Strike = strike,
            ImpliedVolatility = impliedVolatility,
            Bid = 0.01,
            Ask = 0.02,
            LastPrice = 0.015,
            OpenInterest = 1000,
            Volume = 100
        });
    }

    /// <summary>
    /// Safely executes logging operation with fault isolation (Rule 15).
    /// </summary>
    private void SafeLog(Action logAction)
    {
        if (_logger == null)
        {
            return;
        }

#pragma warning disable CA1031
        try
        {
            logAction();
        }
        catch (Exception)
        {
            // Swallow logging exceptions per Rule 15
        }
#pragma warning restore CA1031
    }
}
