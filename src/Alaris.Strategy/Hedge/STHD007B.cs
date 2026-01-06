// STHD007B.cs - rule-based exit monitor with stall detection

// 
// DESIGN RATIONALE:
// The PD controller formulation was rejected because:
// 1. No feedback loop - our exit doesn't affect IV; the plant evolves independently
// 2. Non-linear decision boundary - rate-level relationship is scenario-dependent
// 3. Discrete decision - binary exit/hold, not continuous control signal
// 
// This component implements explicit rule-based logic with interpretable conditions.

using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Hedge;

/// <summary>
/// Rule-based exit monitor with stall detection for calendar spreads.
/// </summary>

public sealed class STHD007B
{
    private readonly ILogger<STHD007B>? _logger;
    private readonly ExitParameters _params;

    // Rate smoothing state
    private double _previousCrushCaptured;
    private double _smoothedRate;
    private bool _isInitialised;
    private int _updateCount;
    private DateTime _entryTime;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, double, double, Exception?> LogExitSignal =
        LoggerMessage.Define<string, double, double>(
            LogLevel.Information,
            new EventId(1, nameof(LogExitSignal)),
            "Exit signal: {Reason} (crush={Crush:P1}, rate={Rate:F4})");

    private static readonly Action<ILogger, double, double, Exception?> LogHoldSignal =
        LoggerMessage.Define<double, double>(
            LogLevel.Debug,
            new EventId(2, nameof(LogHoldSignal)),
            "Hold signal: crush={Crush:P1}, rate={Rate:F4}");

    /// <summary>
    /// Initialises a new rule-based exit monitor.
    /// </summary>
    public STHD007B(ExitParameters? parameters = null, ILogger<STHD007B>? logger = null)
    {
        _params = parameters ?? ExitParameters.Default;
        _logger = logger;
        Reset();
    }

    /// <summary>
    /// Gets whether the monitor has been initialised.
    /// </summary>
    public bool IsInitialised => _isInitialised;

    /// <summary>
    /// Gets the current smoothed crush rate.
    /// </summary>
    public double SmoothedRate => _smoothedRate;

    /// <summary>
    /// Gets the number of updates performed.
    /// </summary>
    public int UpdateCount => _updateCount;

    /// <summary>
    /// Resets the monitor state for a new position.
    /// </summary>
    /// <param name="entryTime">Position entry timestamp.</param>
    public void Reset(DateTime? entryTime = null)
    {
        _previousCrushCaptured = 0;
        _smoothedRate = 0;
        _isInitialised = false;
        _updateCount = 0;
        _entryTime = entryTime ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Evaluates exit conditions and returns recommendation.
    /// </summary>
    /// <param name="crushCaptured">Fraction of expected crush captured: (IV_expected - IV_observed) / ExpectedCrush.</param>
    /// <param name="daysElapsed">Trading days since entry.</param>
    /// <param name="daysRemaining">Trading days until expiry.</param>
    /// <returns>Exit signal with reason and diagnostics.</returns>
    public ExitSignal Evaluate(double crushCaptured, double daysElapsed, double daysRemaining)
    {
        // Rate Estimation

        double rawRate = 0;
        if (_isInitialised)
        {
            // Rate of change in crush captured per day
            rawRate = crushCaptured - _previousCrushCaptured;
        }

        // Exponential smoothing to reduce noise
        _smoothedRate = (_params.RateSmoothingAlpha * rawRate)
                       + ((1 - _params.RateSmoothingAlpha) * _smoothedRate);

        _previousCrushCaptured = crushCaptured;
        _isInitialised = true;
        _updateCount++;

        // Rule-Based Exit Evaluation

        // Rule 1: Target capture - we got what we came for
        if (crushCaptured >= _params.TargetCrushRatio)
        {
            return CreateExitSignal(
                ExitReason.TargetCaptured,
                $"Captured {crushCaptured:P0} of expected crush (target: {_params.TargetCrushRatio:P0})",
                crushCaptured,
                _smoothedRate,
                daysRemaining);
        }

        // Rule 2: Stall detection - significant capture but rate has stalled
        if (crushCaptured >= _params.StallCrushThreshold &&
            Math.Abs(_smoothedRate) < _params.StallRateThreshold &&
            _updateCount >= _params.MinUpdatesForStall)
        {
            return CreateExitSignal(
                ExitReason.CrushStalled,
                $"Crush stalled at {crushCaptured:P0} (rate: {_smoothedRate:F4}/day)",
                crushCaptured,
                _smoothedRate,
                daysRemaining);
        }

        // Rule 3: Protective exit - trade not working
        if (crushCaptured < _params.MinExpectedCrush &&
            _smoothedRate > _params.AdverseRateThreshold &&
            daysElapsed > _params.MaxWaitDays)
        {
            return CreateExitSignal(
                ExitReason.TradeNotWorking,
                $"Insufficient crush ({crushCaptured:P0}) after {daysElapsed:F1} days, rate unfavourable",
                crushCaptured,
                _smoothedRate,
                daysRemaining);
        }

        // Rule 4: Time decay - approaching expiry with partial profit
        if (crushCaptured >= _params.PartialCaptureThreshold &&
            daysRemaining < _params.MinDaysRemaining)
        {
            return CreateExitSignal(
                ExitReason.TimeDecay,
                $"Locking in {crushCaptured:P0} with only {daysRemaining:F1} days remaining",
                crushCaptured,
                _smoothedRate,
                daysRemaining);
        }

        // Rule 5: Rate reversal warning - IV moving against us
        if (crushCaptured >= _params.PartialCaptureThreshold &&
            _smoothedRate < _params.ReversalRateThreshold &&
            _updateCount >= _params.MinUpdatesForStall)
        {
            return CreateExitSignal(
                ExitReason.RateReversal,
                $"IV reversing with {crushCaptured:P0} captured (rate: {_smoothedRate:F4}/day)",
                crushCaptured,
                _smoothedRate,
                daysRemaining);
        }

        // Default: Hold
        SafeLog(() => LogHoldSignal(_logger!, crushCaptured, _smoothedRate, null));

        return new ExitSignal(
            Action: ExitAction.Hold,
            Reason: ExitReason.None,
            Message: "Continue holding",
            CrushCaptured: crushCaptured,
            SmoothedRate: _smoothedRate,
            DaysRemaining: daysRemaining,
            Confidence: ComputeHoldConfidence(crushCaptured, _smoothedRate, daysRemaining));
    }

    /// <summary>
    /// Computes crush captured from IV values.
    /// </summary>
    /// <param name="ivObserved">Current observed implied volatility.</param>
    /// <param name="ivExpected">Leung-Santoli theoretical IV for this time.</param>
    /// <param name="expectedCrush">Total expected IV crush magnitude.</param>
    /// <returns>Fraction of expected crush captured.</returns>
    public static double ComputeCrushCaptured(double ivObserved, double ivExpected, double expectedCrush)
    {
        if (expectedCrush <= 0)
        {
            return 0;
        }

        // How much IV has dropped from expected theoretical level
        double actualCrush = ivExpected - ivObserved;
        return actualCrush / expectedCrush;
    }

    /// <summary>
    /// Computes expected crush from Leung-Santoli parameters.
    /// </summary>
    /// <param name="baseVolatility">Base (diffusion) volatility σ.</param>
    /// <param name="earningsJumpVolatility">Earnings jump volatility σ_e.</param>
    /// <param name="timeToExpiryAtEntry">Time to expiry at position entry (years).</param>
    /// <returns>Expected IV crush in volatility points.</returns>
    public static double ComputeExpectedCrush(
        double baseVolatility,
        double earningsJumpVolatility,
        double timeToExpiryAtEntry)
    {
        if (timeToExpiryAtEntry <= 0)
        {
            return 0;
        }

        // Pre-EA IV from L&S: I(t) = √(σ² + σ_e²/(T-t))
        double preEaIV = Math.Sqrt(
            (baseVolatility * baseVolatility) +
            (earningsJumpVolatility * earningsJumpVolatility / timeToExpiryAtEntry));

        // Expected crush = pre-EA IV - base vol
        return preEaIV - baseVolatility;
    }

    private ExitSignal CreateExitSignal(
        ExitReason reason,
        string message,
        double crushCaptured,
        double rate,
        double daysRemaining)
    {
        SafeLog(() => LogExitSignal(_logger!, reason.ToString(), crushCaptured, rate, null));

        return new ExitSignal(
            Action: ExitAction.Exit,
            Reason: reason,
            Message: message,
            CrushCaptured: crushCaptured,
            SmoothedRate: rate,
            DaysRemaining: daysRemaining,
            Confidence: ComputeExitConfidence(reason, crushCaptured, rate));
    }

    private static double ComputeExitConfidence(ExitReason reason, double crushCaptured, double rate)
    {
        return reason switch
        {
            ExitReason.TargetCaptured => 0.95,  // Very confident
            ExitReason.CrushStalled => 0.75 + (0.15 * Math.Min(1, crushCaptured)),
            ExitReason.TimeDecay => 0.80,
            ExitReason.TradeNotWorking => 0.70,
            ExitReason.RateReversal => 0.65 + (0.20 * Math.Min(1, crushCaptured)),
            _ => 0.50
        };
    }

    private double ComputeHoldConfidence(double crushCaptured, double rate, double daysRemaining)
    {
        // Higher confidence in hold when:
        // - Crush is progressing (rate negative = IV declining)
        // - Plenty of time remaining
        // - Crush is not yet at target

        double rateScore = rate < 0 ? 0.3 : 0.0;  // Reward declining IV
        double timeScore = Math.Min(0.3, daysRemaining / 20.0);  // More time = more confidence
        double progressScore = 0.4 * (1 - (crushCaptured / _params.TargetCrushRatio));  // Room to grow

        return Math.Min(0.95, 0.5 + rateScore + timeScore + progressScore);
    }

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
        catch
        {
            /* Fault isolation per Rule 15 */
        }
#pragma warning restore CA1031
    }

}

/// <summary>
/// Exit monitor parameters with operational meaning.
/// </summary>

public readonly record struct ExitParameters(
    double TargetCrushRatio,
    double StallCrushThreshold,
    double StallRateThreshold,
    double MinExpectedCrush,
    double AdverseRateThreshold,
    double MaxWaitDays,
    double PartialCaptureThreshold,
    double MinDaysRemaining,
    double ReversalRateThreshold,
    double RateSmoothingAlpha,
    int MinUpdatesForStall)
{
    /// <summary>
    /// Default parameters based on calendar spread dynamics.
    /// </summary>
    public static ExitParameters Default => new ExitParameters(
        TargetCrushRatio: 1.0,           // Exit when 100% of expected crush captured
        StallCrushThreshold: 0.6,        // Consider stall after 60% captured
        StallRateThreshold: 0.02,        // Rate under 2%/day = stalled
        MinExpectedCrush: 0.3,           // Protective exit if < 30% captured
        AdverseRateThreshold: 0.01,      // Positive rate > 1%/day = IV rising
        MaxWaitDays: 5.0,                // Wait max 5 days for non-working trade
        PartialCaptureThreshold: 0.5,    // Time-decay exit above 50% captured
        MinDaysRemaining: 2.0,           // Exit if < 2 days remaining
        ReversalRateThreshold: -0.03,    // Rate more negative than -3%/day = reversal
        RateSmoothingAlpha: 0.3,         // 30% weight on new rate observation
        MinUpdatesForStall: 3);          // Need 3 observations to detect stall

    /// <summary>
    /// Conservative parameters (longer holds, higher thresholds).
    /// </summary>
    public static ExitParameters Conservative => new ExitParameters(
        TargetCrushRatio: 1.3,
        StallCrushThreshold: 0.7,
        StallRateThreshold: 0.01,
        MinExpectedCrush: 0.2,
        AdverseRateThreshold: 0.02,
        MaxWaitDays: 7.0,
        PartialCaptureThreshold: 0.6,
        MinDaysRemaining: 1.5,
        ReversalRateThreshold: -0.05,
        RateSmoothingAlpha: 0.2,
        MinUpdatesForStall: 4);

    /// <summary>
    /// Aggressive parameters (faster exits, lower thresholds).
    /// </summary>
    public static ExitParameters Aggressive => new ExitParameters(
        TargetCrushRatio: 0.8,
        StallCrushThreshold: 0.5,
        StallRateThreshold: 0.03,
        MinExpectedCrush: 0.4,
        AdverseRateThreshold: 0.005,
        MaxWaitDays: 3.0,
        PartialCaptureThreshold: 0.4,
        MinDaysRemaining: 3.0,
        ReversalRateThreshold: -0.02,
        RateSmoothingAlpha: 0.4,
        MinUpdatesForStall: 2);
}

/// <summary>
/// Exit signal result with action, reason, and diagnostics.
/// </summary>
public readonly record struct ExitSignal(
    ExitAction Action,
    ExitReason Reason,
    string Message,
    double CrushCaptured,
    double SmoothedRate,
    double DaysRemaining,
    double Confidence);

/// <summary>
/// Exit action recommendation.
/// </summary>
public enum ExitAction
{
    /// <summary>Continue holding the position.</summary>
    Hold,

    /// <summary>Exit the position.</summary>
    Exit
}

/// <summary>
/// Reason for exit recommendation.
/// </summary>
public enum ExitReason
{
    /// <summary>No exit reason (holding).</summary>
    None,

    /// <summary>Captured target crush percentage.</summary>
    TargetCaptured,

    /// <summary>Significant capture but rate has stalled.</summary>
    CrushStalled,

    /// <summary>Trade not working - insufficient crush despite time.</summary>
    TradeNotWorking,

    /// <summary>Time decay - approaching expiry with partial profit.</summary>
    TimeDecay,

    /// <summary>IV reversing after partial capture.</summary>
    RateReversal
}
