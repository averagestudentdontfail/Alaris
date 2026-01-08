// STCS006A.cs - signal cost validator

using Alaris.Strategy.Core;
using Microsoft.Extensions.Logging;

// Type aliases for coded naming convention compatibility
using Signal = Alaris.Strategy.Core.STCR004A;
using SignalStrength = Alaris.Strategy.Core.STCR004AStrength;
using OptionChain = Alaris.Strategy.Model.STDT002A;

namespace Alaris.Strategy.Cost;

/// <summary>
/// Validates that trading signals survive execution costs.
/// </summary>

public sealed class STCS006A
{
    private readonly STCS001A _costModel;
    private readonly ILogger<STCS006A>? _logger;
    private readonly double _minimumPostCostIVRVRatio;
    private readonly decimal _maximumSlippagePercent;
    private readonly decimal _maximumExecutionCostPercent;
    private readonly decimal _maximumSlippagePerSpread;
    private readonly decimal _maximumExecutionCostPerSpread;
    private readonly decimal _minimumCapitalForCostPercent;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, double, double, bool, Exception?> LogValidationResult =
        LoggerMessage.Define<string, double, double, bool>(
            LogLevel.Information,
            new EventId(1, nameof(LogValidationResult)),
            "Cost validation for {Symbol}: pre-cost IV/RV={PreCostRatio:F3}, post-cost={PostCostRatio:F3}, passed={Passed}");

    private static readonly Action<ILogger, string, decimal, decimal, Exception?> LogSlippageWarning =
        LoggerMessage.Define<string, decimal, decimal>(
            LogLevel.Warning,
            new EventId(2, nameof(LogSlippageWarning)),
            "High slippage for {Symbol}: {SlippagePercent:F2}% exceeds threshold {Threshold:F2}%");

    private static readonly Action<ILogger, string, decimal, decimal, Exception?> LogSlippageAbsoluteWarning =
        LoggerMessage.Define<string, decimal, decimal>(
            LogLevel.Warning,
            new EventId(3, nameof(LogSlippageAbsoluteWarning)),
            "High slippage for {Symbol}: ${SlippagePerSpread:F2} per spread exceeds ${Threshold:F2}");

    private static readonly Action<ILogger, string, decimal, decimal, decimal, Exception?> LogExecutionCostWarning =
        LoggerMessage.Define<string, decimal, decimal, decimal>(
            LogLevel.Warning,
            new EventId(4, nameof(LogExecutionCostWarning)),
            "High execution cost for {Symbol}: {CostPercent:F2}% (basis ${Basis:F2}) exceeds {Threshold:F2}%");

    private static readonly Action<ILogger, string, decimal, decimal, Exception?> LogExecutionCostAbsoluteWarning =
        LoggerMessage.Define<string, decimal, decimal>(
            LogLevel.Warning,
            new EventId(5, nameof(LogExecutionCostAbsoluteWarning)),
            "High execution cost for {Symbol}: ${CostPerSpread:F2} per spread exceeds ${Threshold:F2}");

    private static readonly Action<ILogger, string, Exception?> LogValidationError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(6, nameof(LogValidationError)),
            "Error validating costs for {Symbol}");

    /// <summary>
    /// Default minimum IV/RV ratio after costs.
    /// </summary>
    
    public const double DefaultMinimumPostCostRatio = 1.25;

    /// <summary>
    /// Default maximum acceptable slippage percentage.
    /// </summary>
    
    public const decimal DefaultMaximumSlippagePercent = 2.0m;

    /// <summary>
    /// Default maximum execution cost as percentage of capital.
    /// </summary>
    
    public const decimal DefaultMaximumExecutionCostPercent = 5.0m;

    /// <summary>
    /// Default maximum slippage per spread (dollars).
    /// </summary>
    public const decimal DefaultMaximumSlippagePerSpread = 10.0m;

    /// <summary>
    /// Default maximum execution cost per spread (dollars).
    /// </summary>
    public const decimal DefaultMaximumExecutionCostPerSpread = 15.0m;

    /// <summary>
    /// Default minimum capital basis for cost percentage (dollars).
    /// </summary>
    public const decimal DefaultMinimumCapitalForCostPercent = 10.0m;

    /// <summary>
    /// Initialises a new instance of the signal cost validator.
    /// </summary>
    /// <param name="costModel">The execution cost model to use.</param>
    /// <param name="minimumPostCostRatio">
    /// Minimum IV/RV ratio after costs. Default: 1.25.
    /// </param>
    /// <param name="maximumSlippagePercent">
    /// Maximum acceptable slippage percentage. Default: 2%.
    /// </param>
    /// <param name="maximumExecutionCostPercent">
    /// Maximum execution cost as percentage of capital. Default: 5%.
    /// </param>
    /// <param name="maximumSlippagePerSpread">
    /// Maximum slippage per spread in dollars. Default: $10.00.
    /// </param>
    /// <param name="maximumExecutionCostPerSpread">
    /// Maximum execution cost per spread in dollars. Default: $15.00.
    /// </param>
    /// <param name="minimumCapitalForCostPercent">
    /// Minimum capital basis for cost percentage calculations. Default: $10.00.
    /// </param>
    /// <param name="logger">Optional logger instance.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="costModel"/> is null.
    /// </exception>
    public STCS006A(
        STCS001A costModel,
        double minimumPostCostRatio = DefaultMinimumPostCostRatio,
        decimal maximumSlippagePercent = DefaultMaximumSlippagePercent,
        decimal maximumExecutionCostPercent = DefaultMaximumExecutionCostPercent,
        decimal maximumSlippagePerSpread = DefaultMaximumSlippagePerSpread,
        decimal maximumExecutionCostPerSpread = DefaultMaximumExecutionCostPerSpread,
        decimal minimumCapitalForCostPercent = DefaultMinimumCapitalForCostPercent,
        ILogger<STCS006A>? logger = null)
    {
        _costModel = costModel ?? throw new ArgumentNullException(nameof(costModel));
        _minimumPostCostIVRVRatio = minimumPostCostRatio;
        _maximumSlippagePercent = maximumSlippagePercent;
        _maximumExecutionCostPercent = maximumExecutionCostPercent;
        _maximumSlippagePerSpread = maximumSlippagePerSpread;
        _maximumExecutionCostPerSpread = maximumExecutionCostPerSpread;
        _minimumCapitalForCostPercent = minimumCapitalForCostPercent;
        _logger = logger;
    }

    /// <summary>
    /// Validates that a signal survives execution costs.
    /// </summary>
    /// <param name="signal">The trading signal to validate.</param>
    /// <param name="frontLegParams">Front-month option parameters.</param>
    /// <param name="backLegParams">Back-month option parameters.</param>
    /// <returns>Validation result with detailed metrics.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    public STCS007A Validate(
        Signal signal,
        STCS002A frontLegParams,
        STCS002A backLegParams)
    {
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(frontLegParams);
        ArgumentNullException.ThrowIfNull(backLegParams);

        try
        {
            // Compute spread execution costs
            STCS004A spreadCost = _costModel.ComputeSpreadCost(frontLegParams, backLegParams);

            // Compute post-cost IV/RV ratio
            // The execution cost degrades the effective edge
            double preCostRatio = signal.IVRVRatio;
            decimal executionCostPercent = ComputeExecutionCostPercent(spreadCost, out decimal executionCostBasis);
            double postCostRatio = ComputePostCostRatio(preCostRatio, executionCostPercent);

            // Validate against thresholds
            bool passesRatioThreshold = postCostRatio >= _minimumPostCostIVRVRatio;
            decimal debitMagnitude = Math.Abs(spreadCost.TheoreticalDebit);
            bool enforceSlippagePercent = debitMagnitude >= STCS004A.MinimumDebitForPercent;
            bool passesSlippagePercent = !enforceSlippagePercent
                || spreadCost.SlippagePercent <= _maximumSlippagePercent;
            bool passesSlippageAbsolute = spreadCost.SlippagePerSpread <= _maximumSlippagePerSpread;
            decimal capitalMagnitude = Math.Abs(spreadCost.TheoreticalCapitalRequired);
            bool enforceCostPercent = capitalMagnitude >= _minimumCapitalForCostPercent;
            bool passesCostPercent = !enforceCostPercent
                || executionCostPercent <= _maximumExecutionCostPercent;
            bool passesCostAbsolute = spreadCost.CostPerSpread <= _maximumExecutionCostPerSpread;

            // Check for high slippage warning
            if (enforceSlippagePercent && !passesSlippagePercent)
            {
                SafeLog(() => LogSlippageWarning(
                    _logger!,
                    signal.Symbol,
                    spreadCost.SlippagePercent,
                    _maximumSlippagePercent,
                    null));
            }

            if (!passesSlippageAbsolute)
            {
                SafeLog(() => LogSlippageAbsoluteWarning(
                    _logger!,
                    signal.Symbol,
                    spreadCost.SlippagePerSpread,
                    _maximumSlippagePerSpread,
                    null));
            }

            if (enforceCostPercent && !passesCostPercent)
            {
                SafeLog(() => LogExecutionCostWarning(
                    _logger!,
                    signal.Symbol,
                    executionCostPercent,
                    executionCostBasis,
                    _maximumExecutionCostPercent,
                    null));
            }

            if (!passesCostAbsolute)
            {
                SafeLog(() => LogExecutionCostAbsoluteWarning(
                    _logger!,
                    signal.Symbol,
                    spreadCost.CostPerSpread,
                    _maximumExecutionCostPerSpread,
                    null));
            }

            bool overallPass = passesRatioThreshold
                && passesSlippagePercent
                && passesSlippageAbsolute
                && passesCostPercent
                && passesCostAbsolute;

            STCS007A result = new STCS007A
            {
                Symbol = signal.Symbol,
                PreCostIVRVRatio = preCostRatio,
                PostCostIVRVRatio = postCostRatio,
                MinimumRequiredRatio = _minimumPostCostIVRVRatio,
                SpreadCost = spreadCost,
                PassesRatioThreshold = passesRatioThreshold,
                SlippagePercent = spreadCost.SlippagePercent,
                SlippagePerSpread = spreadCost.SlippagePerSpread,
                SlippagePercentThreshold = _maximumSlippagePercent,
                SlippagePerSpreadThreshold = _maximumSlippagePerSpread,
                PassesSlippagePercent = passesSlippagePercent,
                PassesSlippageAbsolute = passesSlippageAbsolute,
                ExecutionCostPercent = executionCostPercent,
                ExecutionCostPercentBasis = executionCostBasis,
                ExecutionCostPerSpread = spreadCost.CostPerSpread,
                ExecutionCostPercentThreshold = _maximumExecutionCostPercent,
                ExecutionCostPerSpreadThreshold = _maximumExecutionCostPerSpread,
                PassesExecutionCostPercent = passesCostPercent,
                PassesExecutionCostAbsolute = passesCostAbsolute,
                OverallPass = overallPass,
                CostModel = _costModel.ModelName
            };

            SafeLog(() => LogValidationResult(
                _logger!,
                signal.Symbol,
                preCostRatio,
                postCostRatio,
                overallPass,
                null));

            return result;
        }
        catch (InvalidOperationException ex)
        {
            SafeLog(() => LogValidationError(_logger!, signal.Symbol, ex));
            throw;
        }
    }

    /// <summary>
    /// Computes the post-cost IV/RV ratio.
    /// </summary>
    /// <param name="preCostRatio">The original IV/RV ratio from signal generation.</param>
    /// <param name="spreadCost">The computed spread execution cost.</param>
    /// <returns>The adjusted IV/RV ratio accounting for costs.</returns>
    
    private static double ComputePostCostRatio(double preCostRatio, decimal executionCostPercent)
    {
        // Execution cost degrades the ratio proportionally
        // If costs are 5% of capital, the effective ratio is reduced by 5%
        double costFactor = 1.0 - ((double)executionCostPercent / 100.0);

        // Ensure factor is non-negative
        costFactor = Math.Max(0.0, costFactor);

        return preCostRatio * costFactor;
    }

    private decimal ComputeExecutionCostPercent(STCS004A spreadCost, out decimal basis)
    {
        decimal theoreticalCapital = Math.Abs(spreadCost.TheoreticalCapitalRequired);
        basis = theoreticalCapital >= _minimumCapitalForCostPercent
            ? theoreticalCapital
            : _minimumCapitalForCostPercent;

        if (basis <= 0.0m)
        {
            return 0.0m;
        }

        return spreadCost.TotalExecutionCost / basis * 100.0m;
    }

    /// <summary>
    /// Validates multiple signals in batch.
    /// </summary>
    /// <param name="signalsWithParams">
    /// Collection of signals with their corresponding option parameters.
    /// </param>
    /// <returns>Validation results for all signals.</returns>
    public IReadOnlyList<STCS007A> ValidateBatch(
        IReadOnlyList<(Signal Signal, STCS002A FrontParams, STCS002A BackParams)> signalsWithParams)
    {
        ArgumentNullException.ThrowIfNull(signalsWithParams);

        List<STCS007A> results = new List<STCS007A>(signalsWithParams.Count);

        foreach ((Signal? signal, STCS002A? frontParams, STCS002A? backParams) in signalsWithParams)
        {
            STCS007A result = Validate(signal, frontParams, backParams);
            results.Add(result);
        }

        return results;
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
