using Alaris.Strategy.Bridge;
using Alaris.Strategy.Model;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Core;

/// <summary>
/// Generates trading signals for earnings calendar spread opportunities.
/// Implements the strategy from Atilgan (2014) and incorporates term structure analysis.
/// </summary>
public sealed class SignalGenerator
{
    private readonly IMarketDataProvider _marketData;
    private readonly YangZhangEstimator _yangZhang;
    private readonly TermStructureAnalyzer _termAnalyzer;
    private readonly ILogger<SignalGenerator>? _logger;

    // Strategy thresholds from research
    private const double MinIvRvRatio = 1.25;
    private const double MaxTermSlope = -0.00406;
    private const long MinAverageVolume = 1_500_000;

    public SignalGenerator(
        IMarketDataProvider marketData,
        YangZhangEstimator yangZhang,
        TermStructureAnalyzer termAnalyzer,
        ILogger<SignalGenerator>? logger = null)
    {
        _marketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
        _yangZhang = yangZhang ?? throw new ArgumentNullException(nameof(yangZhang));
        _termAnalyzer = termAnalyzer ?? throw new ArgumentNullException(nameof(termAnalyzer));
        _logger = logger;
    }

    /// <summary>
    /// Generates a trading signal for a given symbol before an earnings announcement.
    /// </summary>
    public Signal Generate(string symbol, DateTime earningsDate, DateTime evaluationDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        _logger?.LogInformation("Generating signal for {Symbol} with earnings on {EarningsDate}", 
            symbol, earningsDate);

        var signal = new Signal
        {
            Symbol = symbol,
            EarningsDate = earningsDate,
            SignalDate = evaluationDate,
            Criteria = new Dictionary<string, bool>()
        };

        try
        {
            // Get historical price data for realized volatility calculation
            var priceHistory = _marketData.GetHistoricalPrices(symbol, 90);
            if (priceHistory.Count < 30)
            {
                _logger?.LogWarning("Insufficient price history for {Symbol}", symbol);
                signal.Strength = SignalStrength.Avoid;
                return signal;
            }

            // Calculate 30-day Yang-Zhang realized volatility
            signal.RealizedVolatility30 = _yangZhang.Calculate(priceHistory, 30);

            // Get option chain for implied volatility analysis
            var optionChain = _marketData.GetOptionChain(symbol, evaluationDate);
            if (optionChain.Expiries.Count == 0)
            {
                _logger?.LogWarning("No option data available for {Symbol}", symbol);
                signal.Strength = SignalStrength.Avoid;
                return signal;
            }

            // Extract term structure points
            var termPoints = ExtractTermStructurePoints(optionChain, evaluationDate);
            if (termPoints.Count < 2)
            {
                _logger?.LogWarning("Insufficient term structure points for {Symbol}", symbol);
                signal.Strength = SignalStrength.Avoid;
                return signal;
            }

            // Analyze term structure
            var termAnalysis = _termAnalyzer.Analyze(termPoints);
            signal.TermStructureSlope = termAnalysis.Slope;
            signal.ImpliedVolatility30 = termAnalysis.GetIVAt(30);

            // Calculate IV/RV ratio
            signal.IVRVRatio = signal.RealizedVolatility30 > 0
                ? signal.ImpliedVolatility30 / signal.RealizedVolatility30
                : 0;

            // Calculate average volume
            signal.AverageVolume = (long)priceHistory.TakeLast(30).Average(p => p.Volume);

            // Calculate expected move from ATM straddle
            signal.ExpectedMove = CalculateExpectedMove(optionChain, earningsDate, evaluationDate);

            // Evaluate criteria
            signal.Criteria["Volume"] = signal.AverageVolume >= MinAverageVolume;
            signal.Criteria["IV/RV"] = signal.IVRVRatio >= MinIvRvRatio;
            signal.Criteria["TermSlope"] = signal.TermStructureSlope <= MaxTermSlope;

            // Calculate volatility spread (Atilgan 2014)
            signal.VolatilitySpread = CalculateVolatilitySpread(optionChain, evaluationDate);

            signal.EvaluateStrength();

            _logger?.LogInformation(
                "Signal generated for {Symbol}: {Strength} (IV/RV={IvRv:F2}, Slope={Slope:F5}, Volume={Volume})",
                symbol, signal.Strength, signal.IVRVRatio, signal.TermStructureSlope, signal.AverageVolume);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating signal for {Symbol}", symbol);
            signal.Strength = SignalStrength.Avoid;
        }

        return signal;
    }

    /// <summary>
    /// Extracts term structure points from the option chain.
    /// </summary>
    private List<TermStructurePoint> ExtractTermStructurePoints(OptionChain chain, DateTime evaluationDate)
    {
        var points = new List<TermStructurePoint>();
        var underlyingPrice = chain.UnderlyingPrice;

        foreach (var expiry in chain.Expiries.OrderBy(e => e.ExpiryDate))
        {
            var dte = expiry.GetDaysToExpiry(evaluationDate);
            if (dte < 1 || dte > 60)
                continue;

            // Find ATM options
            var atmCall = expiry.Calls
                .Where(c => c.OpenInterest > 0 && c.ImpliedVolatility > 0)
                .OrderBy(c => Math.Abs(c.Strike - underlyingPrice))
                .FirstOrDefault();

            var atmPut = expiry.Puts
                .Where(p => p.OpenInterest > 0 && p.ImpliedVolatility > 0)
                .OrderBy(p => Math.Abs(p.Strike - underlyingPrice))
                .FirstOrDefault();

            if (atmCall is not null && atmPut is not null)
            {
                // Average the call and put IV for ATM
                var avgIV = (atmCall.ImpliedVolatility + atmPut.ImpliedVolatility) / 2.0;
                
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
        var targetExpiry = chain.Expiries
            .Where(e => e.ExpiryDate >= earningsDate)
            .OrderBy(e => e.ExpiryDate)
            .FirstOrDefault();

        if (targetExpiry is null)
            return 0;

        var underlyingPrice = chain.UnderlyingPrice;

        // Find ATM straddle
        var atmCall = targetExpiry.Calls
            .Where(c => c.Bid > 0 && c.Ask > 0)
            .OrderBy(c => Math.Abs(c.Strike - underlyingPrice))
            .FirstOrDefault();

        var atmPut = targetExpiry.Puts
            .Where(p => p.Bid > 0 && p.Ask > 0 && Math.Abs(p.Strike - (atmCall?.Strike ?? 0)) < 0.01)
            .FirstOrDefault();

        if (atmCall is null || atmPut is null)
            return 0;

        var straddlePrice = atmCall.MidPrice + atmPut.MidPrice;
        return straddlePrice / underlyingPrice;
    }

    /// <summary>
    /// Calculates the weighted volatility spread (put IV - call IV) as per Atilgan (2014).
    /// </summary>
    private double CalculateVolatilitySpread(OptionChain chain, DateTime evaluationDate)
    {
        var spreads = new List<(double spread, double weight)>();
        var underlyingPrice = chain.UnderlyingPrice;

        foreach (var expiry in chain.Expiries)
        {
            var dte = expiry.GetDaysToExpiry(evaluationDate);
            if (dte < 10 || dte > 60)
                continue;

            // Match put-call pairs
            var pairs = from call in expiry.Calls
                        join put in expiry.Puts on call.Strike equals put.Strike
                        where call.OpenInterest > 0 && put.OpenInterest > 0
                           && call.ImpliedVolatility > 0 && put.ImpliedVolatility > 0
                        select new
                        {
                            Strike = call.Strike,
                            Spread = put.ImpliedVolatility - call.ImpliedVolatility,
                            Weight = (call.OpenInterest + put.OpenInterest) / 2.0
                        };

            foreach (var pair in pairs)
            {
                spreads.Add((pair.Spread, pair.Weight));
            }
        }

        if (spreads.Count == 0)
            return 0;

        var totalWeight = spreads.Sum(s => s.weight);
        return spreads.Sum(s => s.spread * s.weight) / totalWeight;
    }
}