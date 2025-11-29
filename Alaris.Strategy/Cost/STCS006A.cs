// =============================================================================
// STCS006A.cs - Signal Cost Validator
// Component: STCS006A | Category: Cost | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.2
// Reference: Atilgan (2014) - IV/RV threshold validation post-costs
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

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
/// <remarks>
/// <para>
/// This component addresses a critical pre-production requirement: verifying
/// that the theoretical edge identified by signal generation persists after
/// accounting for realistic transaction costs.
/// </para>
/// <para>
/// The validation process:
/// 1. Computes execution-adjusted spread costs
/// 2. Recalculates effective IV/RV ratio incorporating costs
/// 3. Validates against minimum threshold (default: 1.20 post-costs)
/// 4. Assesses whether the position maintains defined risk properties
/// </para>
/// <para>
/// Reference: Atilgan (2014) uses IV/RV ≥ 1.25 as entry threshold.
/// Post-cost threshold of 1.20 provides a 5% cost buffer.
/// </para>
/// </remarks>
public sealed class STCS006A
{
    private readonly STCS001A _costModel;
    private readonly ILogger<STCS006A>? _logger;
    private readonly double _minimumPostCostIVRVRatio;
    private readonly double _maximumSlippagePercent;
    private readonly double _maximumExecutionCostPercent;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, double, double, bool, Exception?> LogValidationResult =
        LoggerMessage.Define<string, double, double, bool>(
            LogLevel.Information,
            new EventId(1, nameof(LogValidationResult)),
            "Cost validation for {Symbol}: pre-cost IV/RV={PreCostRatio:F3}, post-cost={PostCostRatio:F3}, passed={Passed}");

    private static readonly Action<ILogger, string, double, double, Exception?> LogSlippageWarning =
        LoggerMessage.Define<string, double, double>(
            LogLevel.Warning,
            new EventId(2, nameof(LogSlippageWarning)),
            "High slippage for {Symbol}: {SlippagePercent:F2}% exceeds threshold {Threshold:F2}%");

    private static readonly Action<ILogger, string, Exception?> LogValidationError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(3, nameof(LogValidationError)),
            "Error validating costs for {Symbol}");

    /// <summary>
    /// Default minimum IV/RV ratio after costs.
    /// </summary>
    /// <remarks>
    /// Set 5% below the entry threshold (1.25) to allow for cost degradation
    /// whilst maintaining profitability.
    /// </remarks>
    public const double DefaultMinimumPostCostRatio = 1.20;

    /// <summary>
    /// Default maximum acceptable slippage percentage.
    /// </summary>
    /// <remarks>
    /// Slippage exceeding 10% of theoretical debit indicates poor liquidity.
    /// </remarks>
    public const double DefaultMaximumSlippagePercent = 10.0;

    /// <summary>
    /// Default maximum execution cost as percentage of capital.
    /// </summary>
    /// <remarks>
    /// Total execution costs (fees + slippage) exceeding 5% of capital
    /// significantly degrade expected returns.
    /// </remarks>
    public const double DefaultMaximumExecutionCostPercent = 5.0;

    /// <summary>
    /// Initialises a new instance of the signal cost validator.
    /// </summary>
    /// <param name="costModel">The execution cost model to use.</param>
    /// <param name="minimumPostCostRatio">
    /// Minimum IV/RV ratio after costs. Default: 1.20.
    /// </param>
    /// <param name="maximumSlippagePercent">
    /// Maximum acceptable slippage percentage. Default: 10%.
    /// </param>
    /// <param name="maximumExecutionCostPercent">
    /// Maximum execution cost as percentage of capital. Default: 5%.
    /// </param>
    /// <param name="logger">Optional logger instance.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="costModel"/> is null.
    /// </exception>
    public STCS006A(
        STCS001A costModel,
        double minimumPostCostRatio = DefaultMinimumPostCostRatio,
        double maximumSlippagePercent = DefaultMaximumSlippagePercent,
        double maximumExecutionCostPercent = DefaultMaximumExecutionCostPercent,
        ILogger<STCS006A>? logger = null)
    {
        _costModel = costModel ?? throw new ArgumentNullException(nameof(costModel));
        _minimumPostCostIVRVRatio = minimumPostCostRatio;
        _maximumSlippagePercent = maximumSlippagePercent;
        _maximumExecutionCostPercent = maximumExecutionCostPercent;
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
            double postCostRatio = ComputePostCostRatio(preCostRatio, spreadCost);

            // Validate against thresholds
            bool passesRatioThreshold = postCostRatio >= _minimumPostCostIVRVRatio;
            bool passesSlippageThreshold = spreadCost.SlippagePercent <= _maximumSlippagePercent;
            bool passesCostThreshold = spreadCost.ExecutionCostPercent <= _maximumExecutionCostPercent;

            // Check for high slippage warning
            if (!passesSlippageThreshold)
            {
                SafeLog(() => LogSlippageWarning(
                    _logger!,
                    signal.Symbol,
                    spreadCost.SlippagePercent,
                    _maximumSlippagePercent,
                    null));
            }

            bool overallPass = passesRatioThreshold && passesSlippageThreshold && passesCostThreshold;

            var result = new STCS007A
            {
                Symbol = signal.Symbol,
                PreCostIVRVRatio = preCostRatio,
                PostCostIVRVRatio = postCostRatio,
                MinimumRequiredRatio = _minimumPostCostIVRVRatio,
                SpreadCost = spreadCost,
                PassesRatioThreshold = passesRatioThreshold,
                PassesSlippageThreshold = passesSlippageThreshold,
                PassesCostThreshold = passesCostThreshold,
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
    /// <remarks>
    /// <para>
    /// The cost adjustment follows this logic:
    /// - The IV/RV ratio measures the premium of implied volatility over realised
    /// - Execution costs effectively reduce the captured premium
    /// - We model this as a proportional reduction based on cost percentage
    /// </para>
    /// <para>
    /// Post-cost ratio = Pre-cost ratio × (1 - ExecutionCostPercent/100)
    /// </para>
    /// <para>
    /// This is a conservative approximation. A more precise computation would
    /// recompute expected P&amp;L under cost-adjusted entry prices, but this
    /// requires additional market data (expected IV crush magnitude, holding period).
    /// </para>
    /// </remarks>
    private static double ComputePostCostRatio(double preCostRatio, STCS004A spreadCost)
    {
        // Execution cost degrades the ratio proportionally
        // If costs are 5% of capital, the effective ratio is reduced by 5%
        double costFactor = 1.0 - (spreadCost.ExecutionCostPercent / 100.0);

        // Ensure factor is non-negative
        costFactor = Math.Max(0.0, costFactor);

        return preCostRatio * costFactor;
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

        var results = new List<STCS007A>(signalsWithParams.Count);

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