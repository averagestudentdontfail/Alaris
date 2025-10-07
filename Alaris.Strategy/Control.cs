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
        List<Trade> historicalTrades)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        _logger?.LogInformation("Evaluating opportunity for {Symbol} with earnings on {EarningsDate}",
            symbol, earningsDate);

        var opportunity = new TradingOpportunity
        {
            Symbol = symbol,
            EarningsDate = earningsDate,
            EvaluationDate = evaluationDate
        };

        try
        {
            // Generate signal
            var signal = _signalGenerator.Generate(symbol, earningsDate, evaluationDate);
            opportunity.Signal = signal;

            if (signal.Strength == SignalStrength.Avoid)
            {
                _logger?.LogInformation("Signal strength is Avoid for {Symbol}, skipping", symbol);
                return opportunity;
            }

            // Price the calendar spread
            var spreadParams = CreateSpreadParameters(signal, evaluationDate);
            var spreadPricing = await _pricingEngine.PriceCalendarSpread(spreadParams);
            opportunity.SpreadPricing = spreadPricing;

            // Calculate position size
            var positionSize = _positionSizer.CalculateFromHistory(
                portfolioValue,
                historicalTrades,
                spreadPricing.SpreadCost,
                signal);
            opportunity.PositionSize = positionSize;

            _logger?.LogInformation(
                "Opportunity evaluated for {Symbol}: Signal={Strength}, Contracts={Contracts}, Cost={Cost:C}",
                symbol, signal.Strength, positionSize.Contracts, spreadPricing.SpreadCost);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error evaluating opportunity for {Symbol}", symbol);
        }

        return opportunity;
    }

    /// <summary>
    /// Creates calendar spread parameters from a signal.
    /// </summary>
    private CalendarSpreadParameters CreateSpreadParameters(Signal signal, DateTime evaluationDate)
    {
        // Determine appropriate expiration dates
        var frontExpiry = FindFrontMonthExpiry(signal.EarningsDate, evaluationDate);
        var backExpiry = FindBackMonthExpiry(frontExpiry);

        return new CalendarSpreadParameters
        {
            UnderlyingPrice = signal.ExpectedMove, // This should be actual price from market data
            Strike = signal.ExpectedMove, // This should be ATM strike
            FrontExpiry = ConvertToQuantlibDate(frontExpiry),
            BackExpiry = ConvertToQuantlibDate(backExpiry),
            ImpliedVolatility = signal.ImpliedVolatility30,
            RiskFreeRate = 0.05, // Should come from market data
            DividendYield = 0.0, // Should come from market data
            OptionType = Alaris.Quantlib.Option.Type.Call,
            ValuationDate = ConvertToQuantlibDate(evaluationDate)
        };
    }

    private DateTime FindFrontMonthExpiry(DateTime earningsDate, DateTime evaluationDate)
    {
        // Find the Friday after earnings (standard monthly expiration)
        var daysAfterEarnings = 0;
        while (true)
        {
            var candidate = earningsDate.AddDays(daysAfterEarnings);
            if (candidate.DayOfWeek == DayOfWeek.Friday && candidate > earningsDate)
                return candidate;
            daysAfterEarnings++;
            if (daysAfterEarnings > 14) // Safety limit
                return earningsDate.AddDays(7);
        }
    }

    private DateTime FindBackMonthExpiry(DateTime frontExpiry)
    {
        // Typically 28-35 days after front month
        return frontExpiry.AddDays(30);
    }

    private Alaris.Quantlib.Date ConvertToQuantlibDate(DateTime date)
    {
        return new Alaris.Quantlib.Date(date.Day, date.Month, date.Year);
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