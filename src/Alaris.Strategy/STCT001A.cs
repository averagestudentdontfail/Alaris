using Alaris.Strategy.Core;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Pricing;
using Alaris.Strategy.Risk;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy;

/// <summary>
/// Main control class for the earnings volatility calendar spread strategy.
/// Orchestrates signal generation, position sizing, and trade execution.
/// </summary>
public sealed class STCT001A
{
    private readonly STCR001A _signalGenerator;
    private readonly STBR002A _pricingEngine;
    private readonly STRK001A _positionSizer;
    private readonly ILogger<STCT001A>? _logger;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, DateTime, Exception?> LogEvaluatingOpportunity =
        LoggerMessage.Define<string, DateTime>(
            LogLevel.Information,
            new EventId(1, nameof(LogEvaluatingOpportunity)),
            "Evaluating opportunity for {Symbol} with earnings on {EarningsDate}");

    private static readonly Action<ILogger, string, Exception?> LogSTCR004AAvoid =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, nameof(LogSTCR004AAvoid)),
            "STCR004A strength is Avoid for {Symbol}, skipping");

    private static readonly Action<ILogger, string, STCR004AStrength, int, double, Exception?> LogOpportunityEvaluated =
        LoggerMessage.Define<string, STCR004AStrength, int, double>(
            LogLevel.Information,
            new EventId(3, nameof(LogOpportunityEvaluated)),
            "Opportunity evaluated for {Symbol}: STCR004A={Strength}, Contracts={Contracts}, Cost={Cost:C}");

    private static readonly Action<ILogger, string, Exception?> LogErrorEvaluatingOpportunity =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(4, nameof(LogErrorEvaluatingOpportunity)),
            "Error evaluating opportunity for {Symbol}");

    public STCT001A(
        STCR001A signalGenerator,
        STBR002A pricingEngine,
        STRK001A positionSizer,
        ILogger<STCT001A>? logger = null)
    {
        _signalGenerator = signalGenerator ?? throw new ArgumentNullException(nameof(signalGenerator));
        _pricingEngine = pricingEngine ?? throw new ArgumentNullException(nameof(pricingEngine));
        _positionSizer = positionSizer ?? throw new ArgumentNullException(nameof(positionSizer));
        _logger = logger;
    }

    /// <summary>
    /// Evaluates a trading opportunity for a given symbol.
    /// </summary>
    public async Task<TradingOpportunity> EvaluateOpportunity(
        string symbol,
        DateTime earningsDate,
        DateTime evaluationDate,
        double portfolioValue,
        IReadOnlyList<Trade> historicalTrades)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        if (_logger != null)
        {
            LogEvaluatingOpportunity(_logger, symbol, earningsDate, null);
        }

        TradingOpportunity opportunity = new TradingOpportunity
        {
            Symbol = symbol,
            EarningsDate = earningsDate,
            EvaluationDate = evaluationDate
        };

        try
        {
            // Generate signal
            STCR004A signal = _signalGenerator.Generate(symbol, earningsDate, evaluationDate);
            opportunity.STCR004A = signal;

            if (signal.Strength == STCR004AStrength.Avoid)
            {
                SafeLog(() => LogSTCR004AAvoid(_logger!, symbol, null));
                return opportunity;
            }

            // Price the calendar spread
            STPR001AParameters spreadParams = CreateSpreadParameters(signal, evaluationDate);
            STPR001APricing spreadPricing = await _pricingEngine.PriceSTPR001A(spreadParams).ConfigureAwait(false);
            opportunity.SpreadPricing = spreadPricing;

            // Calculate position size
            STRK002A positionSize = _positionSizer.CalculateFromHistory(
                portfolioValue,
                historicalTrades,
                spreadPricing.SpreadCost,
                signal);
            opportunity.STRK002A = positionSize;

            SafeLog(() => LogOpportunityEvaluated(_logger!, symbol, signal.Strength, positionSize.Contracts, spreadPricing.SpreadCost, null));
        }
        catch (ArgumentException ex)
        {
            SafeLog(() => LogErrorEvaluatingOpportunity(_logger!, symbol, ex));
            throw;
        }
        catch (InvalidOperationException ex)
        {
            SafeLog(() => LogErrorEvaluatingOpportunity(_logger!, symbol, ex));
            throw;
        }

        return opportunity;
    }

    /// <summary>
    /// Creates calendar spread parameters from a signal.
    /// </summary>
    private STPR001AParameters CreateSpreadParameters(STCR004A signal, DateTime evaluationDate)
    {
        // Determine appropriate expiration dates
        DateTime frontExpiry = FindFrontMonthExpiry(signal.EarningsDate, evaluationDate);
        DateTime backExpiry = FindBackMonthExpiry(frontExpiry);

        return new STPR001AParameters
        {
            UnderlyingPrice = signal.ExpectedMove, // This should be actual price from market data
            Strike = signal.ExpectedMove, // This should be ATM strike
            FrontExpiry = Alaris.Core.Time.CRTM005A.FromDateTime(frontExpiry),
            BackExpiry = Alaris.Core.Time.CRTM005A.FromDateTime(backExpiry),
            ImpliedVolatility = signal.ImpliedVolatility30,
            RiskFreeRate = 0.05, // Should come from market data
            DividendYield = 0.0, // Should come from market data
            OptionType = Alaris.Core.Options.OptionType.Call,
            ValuationDate = Alaris.Core.Time.CRTM005A.FromDateTime(evaluationDate)
        };
    }

    private DateTime FindFrontMonthExpiry(DateTime earningsDate, DateTime evaluationDate)
    {
        // Find the Friday after earnings (standard monthly expiration)
        int daysAfterEarnings = 0;
        while (true)
        {
            DateTime candidate = earningsDate.AddDays(daysAfterEarnings);
            if (candidate.DayOfWeek == DayOfWeek.Friday && candidate > earningsDate)
            {
                return candidate;
            }

            daysAfterEarnings++;
            if (daysAfterEarnings > 14) // Safety limit
            {
                return earningsDate.AddDays(7);
            }
        }
    }

    private DateTime FindBackMonthExpiry(DateTime frontExpiry)
    {
        // Typically 28-35 days after front month
        return frontExpiry.AddDays(30);
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

/// <summary>
/// Represents a complete trading opportunity evaluation.
/// </summary>
public sealed class TradingOpportunity
{
    public string Symbol { get; init; } = string.Empty;
    public DateTime EarningsDate { get; init; }
    public DateTime EvaluationDate { get; init; }
    public STCR004A? STCR004A { get; set; }
    public STPR001APricing? SpreadPricing { get; set; }
    public STRK002A? STRK002A { get; set; }

    /// <summary>
    /// Gets whether this opportunity is actionable.
    /// </summary>
    public bool IsActionable =>
        STCR004A?.Strength != STCR004AStrength.Avoid &&
        STRK002A?.Contracts > 0;
}