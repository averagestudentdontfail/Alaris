namespace Alaris.Strategy.Core;

/// <summary>
/// Represents a trading signal for earnings calendar spread strategy.
/// Based on Atilgan (2014) volatility spread research.
/// </summary>
public sealed class Signal
{
    /// <summary>
    /// Gets or sets the underlying security symbol.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the signal strength classification.
    /// </summary>
    public SignalStrength Strength { get; set; }

    /// <summary>
    /// Gets or sets the IV/RV ratio (30-day implied volatility / 30-day realized volatility).
    /// Entry criterion: IV30/RV30 >= 1.25
    /// </summary>
    public double IVRVRatio { get; set; }

    /// <summary>
    /// Gets or sets the term structure slope (0-45 days).
    /// Entry criterion: Slope &lt;= -0.00406
    /// </summary>
    public double TermStructureSlope { get; set; }

    /// <summary>
    /// Gets or sets the average trading volume.
    /// Entry criterion: &gt;= 1,500,000 shares
    /// </summary>
    public long AverageVolume { get; set; }

    /// <summary>
    /// Gets or sets the expected move percentage for the earnings event.
    /// </summary>
    public double ExpectedMove { get; set; }

    /// <summary>
    /// Gets or sets the earnings announcement date.
    /// </summary>
    public DateTime EarningsDate { get; set; }

    /// <summary>
    /// Gets or sets the date when the signal was generated.
    /// </summary>
    public DateTime SignalDate { get; set; }

    /// <summary>
    /// Gets the individual criteria results.
    /// </summary>
    public Dictionary<string, bool> Criteria { get; } = new();

    /// <summary>
    /// Gets or sets the volatility spread (put IV - call IV).
    /// </summary>
    public double VolatilitySpread { get; set; }

    /// <summary>
    /// Gets or sets the 30-day implied volatility.
    /// </summary>
    public double ImpliedVolatility30 { get; set; }

    /// <summary>
    /// Gets or sets the 30-day realized volatility (Yang-Zhang estimator).
    /// </summary>
    public double RealizedVolatility30 { get; set; }

    /// <summary>
    /// Evaluates all criteria and determines signal strength.
    /// </summary>
    public void EvaluateStrength()
    {
        bool volumePass = Criteria.GetValueOrDefault("Volume", false);
        bool ivRvPass = Criteria.GetValueOrDefault("IV/RV", false);
        bool tsPass = Criteria.GetValueOrDefault("TermSlope", false);

        Strength = (volumePass, ivRvPass, tsPass) switch
        {
            (true, true, true) => SignalStrength.Recommended,
            (true, false, true) or (false, true, true) => SignalStrength.Consider,
            _ => SignalStrength.Avoid
        };
    }
}

/// <summary>
/// Signal strength classification for trading decisions.
/// </summary>
public enum SignalStrength
{
    /// <summary>
    /// Avoid taking a position - criteria not met.
    /// </summary>
    Avoid = 0,

    /// <summary>
    /// Consider taking a position - partial criteria met.
    /// </summary>
    Consider = 1,

    /// <summary>
    /// Recommended to take a position - all criteria met.
    /// </summary>
    Recommended = 2
}