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
public sealed class Control
{
    private readonly SignalGenerator _signalGenerator;
    private readonly IOptionPricingEngine _pricingEngine;
    private readonly KellyPositionSizer _positionSizer;
    private readonly ILogger<Control>? _logger;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, DateTime, Exception?> LogEvaluatingOpportunity =
        LoggerMessage.Define<string, DateTime>(
            LogLevel.Information,
            new EventId(1, nameof(LogEvaluatingOpportunity)),
            "Evaluating opportunity for {Symbol} with earnings on {EarningsDate}");

    private static readonly Action<ILogger, string, Exception?> LogSignalAvoid =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, nameof(LogSignalAvoid)),
            "Signal strength is Avoid for {Symbol}, skipping");

    private static readonly Action<ILogger, string, SignalStrength, int, double, Exception?> LogOpportunityEvaluated =
        LoggerMessage.Define<string, SignalStrength, int, double>(
            LogLevel.Information,
            new EventId(3, nameof(LogOpportunityEvaluated)),
            "Opportunity evaluated for {Symbol}: Signal={Strength}, Contracts={Contracts}, Cost={Cost:C}");

    private static readonly Action<ILogger, string, Exception?> LogErrorEvaluatingOpportunity =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(4, nameof(LogErrorEvaluatingOpportunity)),
            "Error evaluating opportunity for {Symbol}");

    public Control(
        SignalGenerator signalGenerator,
        IOptionPricingEngine pricingEngine,
        KellyPositionSizer positionSizer,
        ILogger<Control>? logger = null)
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
            Signal signal = _signalGenerator.Generate(symbol, earningsDate, evaluationDate);
            opportunity.Signal = signal;

            if (signal.Strength == SignalStrength.Avoid)
            {
                if (_logger != null)
                {
                    LogSignalAvoid(_logger, symbol, null);
                }
                return opportunity;
            }

            // Price the calendar spread
            CalendarSpreadParameters spreadParams = CreateSpreadParameters(signal, evaluationDate);
            CalendarSpreadPricing spreadPricing = await _pricingEngine.PriceCalendarSpread(spreadParams).ConfigureAwait(false);
            opportunity.SpreadPricing = spreadPricing;

            // Calculate position size
            PositionSize positionSize = _positionSizer.CalculateFromHistory(
                portfolioValue,
                historicalTrades,
                spreadPricing.SpreadCost,
                signal);
            opportunity.PositionSize = positionSize;

            if (_logger != null)
            {
                LogOpportunityEvaluated(_logger, symbol, signal.Strength, positionSize.Contracts, spreadPricing.SpreadCost, null);
            }
        }
        catch (Exception ex)
        {
            if (_logger != null)
            {
                LogErrorEvaluatingOpportunity(_logger, symbol, ex);
            }
            throw;
        }

        return opportunity;
    }

    /// <summary>
    /// Creates calendar spread parameters from a signal.
    /// </summary>
    private CalendarSpreadParameters CreateSpreadParameters(Signal signal, DateTime evaluationDate)
    {
        // Determine appropriate expiration dates
        DateTime frontExpiry = FindFrontMonthExpiry(signal.EarningsDate, evaluationDate);
        DateTime backExpiry = FindBackMonthExpiry(frontExpiry);

        return new CalendarSpreadParameters
        {
            UnderlyingPrice = signal.ExpectedMove, // This should be actual price from market data
            Strike = signal.ExpectedMove, // This should be ATM strike
            FrontExpiry = ConvertToQuantlibDate(frontExpiry),
            BackExpiry = ConvertToQuantlibDate(backExpiry),
            ImpliedVolatility = signal.ImpliedVolatility30,
            RiskFreeRate = 0.05, // Should come from market data
            DividendYield = 0.0, // Should come from market data
            OptionType = Option.Type.Call,
            ValuationDate = ConvertToQuantlibDate(evaluationDate)
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

    private Date ConvertToQuantlibDate(DateTime date)
    {
        // SWIG-generated Date constructor: Date(int day, Month month, int year)
        Month month = (Month)date.Month;
        return new Date(date.Day, month, date.Year);
    }
}

/// <summary>
/// Represents a complete trading opportunity evaluation.
/// </summary>
public sealed class TradingOpportunity
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime EarningsDate { get; set; }
    public DateTime EvaluationDate { get; set; }
    public Signal? Signal { get; set; }
    public CalendarSpreadPricing? SpreadPricing { get; set; }
    public PositionSize? PositionSize { get; set; }

    /// <summary>
    /// Gets whether this opportunity is actionable.
    /// </summary>
    public bool IsActionable =>
        Signal?.Strength != SignalStrength.Avoid &&
        PositionSize?.Contracts > 0;
}