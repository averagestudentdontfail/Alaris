// STDD001A.cs - dividend ex-date detector

using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Core;

/// <summary>
/// Detects dividend ex-dates and evaluates early exercise risk for calendar spreads.
/// </summary>

public sealed class STDD001A
{
    private readonly ILogger<STDD001A>? _logger;

    // LoggerMessage delegates
    private static readonly Action<ILogger, DateTime, double, double, Exception?> LogExDateDetected =
        LoggerMessage.Define<DateTime, double, double>(
            LogLevel.Warning,
            new EventId(1, nameof(LogExDateDetected)),
            "Ex-date detected: {ExDate}, Dividend={Dividend:F2}, EarlyExerciseRisk={Risk:P1}");

    /// <summary>
    /// Initialises a new instance of the dividend date detector.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public STDD001A(ILogger<STDD001A>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Finds dividend ex-dates within a specified window.
    /// </summary>
    /// <param name="dividendSchedule">List of known dividend events.</param>
    /// <param name="windowStart">Start of the earnings window.</param>
    /// <param name="windowEnd">End of the earnings window.</param>
    /// <returns>List of ex-dates within the window.</returns>
    public IReadOnlyList<STDD002A> FindExDatesInWindow(
        IReadOnlyList<STDD002A> dividendSchedule,
        DateTime windowStart,
        DateTime windowEnd)
    {
        ArgumentNullException.ThrowIfNull(dividendSchedule);

        return dividendSchedule
            .Where(d => d.ExDate >= windowStart && d.ExDate <= windowEnd)
            .OrderBy(d => d.ExDate)
            .ToList();
    }

    /// <summary>
    /// Calculates early exercise probability for American call near ex-date.
    /// </summary>
    
    /// <param name="dividendAmount">Dividend amount per share.</param>
    /// <param name="strike">Option strike price.</param>
    /// <param name="riskFreeRate">Risk-free interest rate (annualised).</param>
    /// <param name="timeFromExToExpiry">Time from ex-date to option expiry (years).</param>
    /// <returns>Early exercise risk score (0-1).</returns>
    public double CalculateEarlyExerciseRisk(
        double dividendAmount,
        double strike,
        double riskFreeRate,
        double timeFromExToExpiry)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dividendAmount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strike);
        ArgumentOutOfRangeException.ThrowIfNegative(timeFromExToExpiry);

        if (dividendAmount <= 0)
        {
            return 0.0;
        }

        // Merton threshold: K × (1 - e^(-r×τ))
        double threshold = strike * (1.0 - Math.Exp(-riskFreeRate * timeFromExToExpiry));

        // Early exercise is optimal if D > threshold
        if (dividendAmount > threshold)
        {
            // Risk proportional to how much dividend exceeds threshold
            double excess = (dividendAmount - threshold) / dividendAmount;
            return Math.Min(1.0, 0.5 + (0.5 * excess));
        }

        // Partial risk if close to threshold
        double ratio = dividendAmount / Math.Max(threshold, 0.001);
        return Math.Min(0.5, ratio * 0.5);
    }

    /// <summary>
    /// Evaluates dividend risk for a calendar spread position.
    /// </summary>
    /// <param name="dividendSchedule">Known dividend events.</param>
    /// <param name="spotPrice">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="shortLegExpiry">Short leg expiration date.</param>
    /// <param name="earningsDate">Earnings announcement date.</param>
    /// <param name="riskFreeRate">Risk-free rate.</param>
    /// <returns>Dividend risk evaluation result.</returns>
    public STDD003A EvaluateDividendRisk(
        IReadOnlyList<STDD002A> dividendSchedule,
        double spotPrice,
        double strike,
        DateTime shortLegExpiry,
        DateTime earningsDate,
        double riskFreeRate)
    {
        ArgumentNullException.ThrowIfNull(dividendSchedule);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spotPrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strike);

        // Define window: 7 days before earnings to short leg expiry
        DateTime windowStart = earningsDate.AddDays(-7);
        DateTime windowEnd = shortLegExpiry;

        IReadOnlyList<STDD002A> exDatesInWindow = FindExDatesInWindow(
            dividendSchedule, windowStart, windowEnd);

        if (exDatesInWindow.Count == 0)
        {
            return new STDD003A
            {
                HasExDateInWindow = false,
                ExDatesFound = exDatesInWindow,
                MaxEarlyExerciseRisk = 0.0,
                RiskLevel = STDD004A.None,
                SignalAdjustment = 1.0,
                Rationale = "No ex-dates in earnings window"
            };
        }

        // Calculate early exercise risk for each ex-date
        double maxRisk = 0.0;
        foreach (STDD002A dividend in exDatesInWindow)
        {
            double timeToExpiry = (shortLegExpiry - dividend.ExDate).TotalDays / 365.0;
            double risk = CalculateEarlyExerciseRisk(
                dividend.Amount, strike, riskFreeRate, timeToExpiry);

            SafeLog(() => LogExDateDetected(_logger!, dividend.ExDate, dividend.Amount, risk, null));

            maxRisk = Math.Max(maxRisk, risk);
        }

        // Determine risk level and signal adjustment
        STDD004A riskLevel;
        double signalAdjustment;
        if (maxRisk > 0.70)
        {
            riskLevel = STDD004A.High;
            signalAdjustment = 0.0; // Avoid trade
        }
        else if (maxRisk > 0.40)
        {
            riskLevel = STDD004A.Elevated;
            signalAdjustment = 0.50; // Reduce exposure
        }
        else if (maxRisk > 0.20)
        {
            riskLevel = STDD004A.Moderate;
            signalAdjustment = 0.75;
        }
        else
        {
            riskLevel = STDD004A.Low;
            signalAdjustment = 1.0;
        }

        return new STDD003A
        {
            HasExDateInWindow = true,
            ExDatesFound = exDatesInWindow,
            MaxEarlyExerciseRisk = maxRisk,
            RiskLevel = riskLevel,
            SignalAdjustment = signalAdjustment,
            Rationale = riskLevel switch
            {
                STDD004A.High => $"High early exercise risk ({maxRisk:P0}) - avoid trade",
                STDD004A.Elevated => $"Elevated early exercise risk ({maxRisk:P0}) - reduce position",
                STDD004A.Moderate => $"Moderate early exercise risk ({maxRisk:P0})",
                _ => "Low early exercise risk from dividends"
            }
        };
    }

    /// <summary>
    /// Safely executes logging operation with fault isolation.
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
            // Swallow logging exceptions
        }
#pragma warning restore CA1031
    }
}

// Supporting Types

/// <summary>
/// Represents a dividend event with ex-date and amount.
/// </summary>
public sealed record STDD002A
{
    /// <summary>Ex-dividend date.</summary>
    public required DateTime ExDate { get; init; }

    /// <summary>Dividend amount per share.</summary>
    public required double Amount { get; init; }

    /// <summary>Payment date (optional).</summary>
    public DateTime? PaymentDate { get; init; }

    /// <summary>Dividend type (regular, special, etc.).</summary>
    public string DividendType { get; init; } = "Regular";
}

/// <summary>
/// Result of dividend risk evaluation.
/// </summary>
public sealed record STDD003A
{
    /// <summary>Whether any ex-dates fall within the earnings window.</summary>
    public required bool HasExDateInWindow { get; init; }

    /// <summary>List of ex-dates found in window.</summary>
    public required IReadOnlyList<STDD002A> ExDatesFound { get; init; }

    /// <summary>Maximum early exercise risk across all ex-dates.</summary>
    public required double MaxEarlyExerciseRisk { get; init; }

    /// <summary>Risk level classification.</summary>
    public required STDD004A RiskLevel { get; init; }

    /// <summary>Recommended signal adjustment (0.0 = avoid, 1.0 = no adjustment).</summary>
    public required double SignalAdjustment { get; init; }

    /// <summary>Human-readable rationale.</summary>
    public required string Rationale { get; init; }
}

/// <summary>
/// Dividend-related early exercise risk levels.
/// </summary>
public enum STDD004A
{
    /// <summary>No ex-dates in window.</summary>
    None = 0,

    /// <summary>Low early exercise probability.</summary>
    Low = 1,

    /// <summary>Moderate early exercise probability.</summary>
    Moderate = 2,

    /// <summary>Elevated early exercise probability.</summary>
    Elevated = 3,

    /// <summary>High early exercise probability - avoid trade.</summary>
    High = 4
}
