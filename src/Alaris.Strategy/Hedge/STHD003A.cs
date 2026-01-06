// STHD003A.cs - gamma risk manager

using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Hedge;

/// <summary>
/// Monitors and manages gamma risk for calendar spread positions.
/// </summary>

public sealed class STHD003A
{
    private readonly ILogger<STHD003A>? _logger;
    private readonly double _deltaRehedgeThreshold;
    private readonly double _gammaWarningThreshold;
    private readonly double _moneynessAlertThreshold;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, double, RehedgeAction, Exception?> LogRiskAssessment =
        LoggerMessage.Define<string, double, RehedgeAction>(
            LogLevel.Information,
            new EventId(1, nameof(LogRiskAssessment)),
            "Gamma risk for {Symbol}: delta={Delta:F4}, action={Action}");

    private static readonly Action<ILogger, string, double, double, Exception?> LogRehedgeRequired =
        LoggerMessage.Define<string, double, double>(
            LogLevel.Warning,
            new EventId(2, nameof(LogRehedgeRequired)),
            "Rehedge required for {Symbol}: delta {Delta:F4} exceeds threshold {Threshold:F4}");

    /// <summary>
    /// Default delta threshold triggering re-centering.
    /// </summary>
    
    public const double DefaultDeltaThreshold = 0.10;

    /// <summary>
    /// Default gamma threshold for warnings.
    /// </summary>
    
    public const double DefaultGammaThreshold = -0.05;

    /// <summary>
    /// Default moneyness alert threshold.
    /// </summary>
    
    public const double DefaultMoneynessThreshold = 0.03;

    /// <summary>
    /// Initialises a new instance of the gamma risk manager.
    /// </summary>
    /// <param name="deltaRehedgeThreshold">
    /// Delta threshold for re-centering. Default: 0.10.
    /// </param>
    /// <param name="gammaWarningThreshold">
    /// Gamma threshold for warnings. Default: -0.05.
    /// </param>
    /// <param name="moneynessAlertThreshold">
    /// Moneyness alert threshold. Default: 3%.
    /// </param>
    /// <param name="logger">Optional logger instance.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when thresholds are outside valid ranges.
    /// </exception>
    public STHD003A(
        double deltaRehedgeThreshold = DefaultDeltaThreshold,
        double gammaWarningThreshold = DefaultGammaThreshold,
        double moneynessAlertThreshold = DefaultMoneynessThreshold,
        ILogger<STHD003A>? logger = null)
    {
        if (deltaRehedgeThreshold <= 0 || deltaRehedgeThreshold >= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deltaRehedgeThreshold),
                deltaRehedgeThreshold,
                "Delta threshold must be in range (0, 1).");
        }

        if (moneynessAlertThreshold <= 0 || moneynessAlertThreshold >= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(moneynessAlertThreshold),
                moneynessAlertThreshold,
                "Moneyness threshold must be in range (0, 1).");
        }

        _deltaRehedgeThreshold = deltaRehedgeThreshold;
        _gammaWarningThreshold = gammaWarningThreshold;
        _moneynessAlertThreshold = moneynessAlertThreshold;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates current position Greeks and determines required action.
    /// </summary>
    /// <param name="symbol">The underlying symbol.</param>
    /// <param name="spreadDelta">Current spread delta.</param>
    /// <param name="spreadGamma">Current spread gamma (typically negative).</param>
    /// <param name="spreadVega">Current spread vega.</param>
    /// <param name="spreadTheta">Current spread theta.</param>
    /// <param name="spotPrice">Current underlying price.</param>
    /// <param name="strikePrice">Calendar spread strike.</param>
    /// <param name="daysToEarnings">Days until earnings announcement.</param>
    /// <returns>Gamma risk assessment with recommended action.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when symbol is null or prices are non-positive.
    /// </exception>
    public STHD004A Evaluate(
        string symbol,
        double spreadDelta,
        double spreadGamma,
        double spreadVega,
        double spreadTheta,
        double spotPrice,
        double strikePrice,
        int daysToEarnings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        if (spotPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spotPrice), spotPrice, "Spot price must be positive.");
        }

        if (strikePrice <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strikePrice), strikePrice, "Strike price must be positive.");
        }

        // Compute moneyness (S/K)
        double moneyness = spotPrice / strikePrice;
        double moneynessDeviation = Math.Abs(moneyness - 1.0);

        // Determine recommended action
        (RehedgeAction action, string? rationale) = DetermineAction(
            spreadDelta,
            spreadGamma,
            moneynessDeviation,
            daysToEarnings);

        STHD004A result = new STHD004A
        {
            Symbol = symbol,
            CurrentDelta = spreadDelta,
            CurrentGamma = spreadGamma,
            CurrentVega = spreadVega,
            CurrentTheta = spreadTheta,
            SpotPrice = spotPrice,
            StrikePrice = strikePrice,
            Moneyness = moneyness,
            MoneynessDeviation = moneynessDeviation,
            DaysToEarnings = daysToEarnings,
            DeltaThreshold = _deltaRehedgeThreshold,
            GammaThreshold = _gammaWarningThreshold,
            MoneynessThreshold = _moneynessAlertThreshold,
            RecommendedAction = action,
            Rationale = rationale
        };

        SafeLog(() => LogRiskAssessment(_logger!, symbol, spreadDelta, action, null));

        if (action == RehedgeAction.RecenterRequired)
        {
            SafeLog(() => LogRehedgeRequired(_logger!, symbol, spreadDelta, _deltaRehedgeThreshold, null));
        }

        return result;
    }

    /// <summary>
    /// Determines the recommended action based on current metrics.
    /// </summary>
    private (RehedgeAction Action, string Rationale) DetermineAction(
        double delta,
        double gamma,
        double moneynessDeviation,
        int daysToEarnings)
    {
        double absDelta = Math.Abs(delta);

        // Priority 1: Delta exceeds threshold - must re-centre
        if (absDelta > _deltaRehedgeThreshold)
        {
            return (
                RehedgeAction.RecenterRequired,
                $"Delta ({delta:F4}) exceeds threshold ({_deltaRehedgeThreshold:F2}). " +
                "Close position and re-establish at current ATM strike."
            );
        }

        // Priority 2: Close to earnings with elevated gamma and moneyness deviation
        if (daysToEarnings <= 2 && gamma < _gammaWarningThreshold && moneynessDeviation > _moneynessAlertThreshold)
        {
            return (
                RehedgeAction.ExitPosition,
                $"Elevated gap risk with {daysToEarnings} days to earnings. " +
                $"Gamma ({gamma:F4}) and moneyness deviation ({moneynessDeviation:P1}) indicate exit."
            );
        }

        // Priority 3: Elevated gamma with significant moneyness deviation
        if (gamma < _gammaWarningThreshold && moneynessDeviation > _moneynessAlertThreshold)
        {
            return (
                RehedgeAction.MonitorClosely,
                $"Elevated gamma risk ({gamma:F4}) with underlying {moneynessDeviation * 100:F1}% " +
                "from strike. Consider reducing position size."
            );
        }

        // Priority 4: Approaching delta threshold
        if (absDelta > _deltaRehedgeThreshold * 0.7)
        {
            return (
                RehedgeAction.MonitorClosely,
                $"Delta ({delta:F4}) approaching threshold ({_deltaRehedgeThreshold:F2}). " +
                "Monitor for further drift."
            );
        }

        // All clear
        return (
            RehedgeAction.Hold,
            "Position within acceptable parameters."
        );
    }

    /// <summary>
    /// Computes the required delta hedge to neutralise position.
    /// </summary>
    /// <param name="spreadDelta">Current spread delta per contract.</param>
    /// <param name="contracts">Number of spread contracts.</param>
    /// <param name="sharesPerContract">Shares per option contract (typically 100).</param>
    /// <returns>Number of shares to trade for delta neutrality.</returns>
    
    public static int ComputeDeltaHedge(double spreadDelta, int contracts, int sharesPerContract = 100)
    {
        // To neutralise: trade opposite of current delta exposure
        double totalDelta = spreadDelta * contracts * sharesPerContract;
        return -(int)Math.Round(totalDelta);
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

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            logAction();
        }
        catch (Exception)
        {
            // Swallow logging exceptions (Rule 15: Fault Isolation)
        }
#pragma warning restore CA1031
    }
}

/// <summary>
/// Specifies the recommended hedging action.
/// </summary>
public enum RehedgeAction
{
    /// <summary>
    /// No action required; position is within parameters.
    /// </summary>
    Hold,

    /// <summary>
    /// Position requires close monitoring; risk is elevated but manageable.
    /// </summary>
    MonitorClosely,

    /// <summary>
    /// Position should be closed and re-established at current ATM strike.
    /// </summary>
    RecenterRequired,

    /// <summary>
    /// Position should be exited due to elevated gap or directional risk.
    /// </summary>
    ExitPosition
}
