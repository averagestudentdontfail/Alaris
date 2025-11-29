// =============================================================================
// STCS005A.cs - Constant Fee Model
// Component: STCS005A | Category: Cost | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.2
// Reference: QuantConnect.Lean.Common.Orders.Fees.ConstantFeeModel
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Cost;

/// <summary>
/// Implements a constant per-contract fee model for option execution costs.
/// </summary>
/// <remarks>
/// <para>
/// This brokerage-agnostic model applies a fixed fee per contract, suitable for:
/// - Initial strategy validation before brokerage integration
/// - Conservative cost estimation with configurable fees
/// - Backtesting scenarios where exact brokerage fees are not required
/// </para>
/// <para>
/// The model computes slippage by assuming fills occur at bid (sell) or ask (buy)
/// rather than mid-price, providing realistic execution cost estimates.
/// </para>
/// <para>
/// Reference: Modelled after QuantConnect's ConstantFeeModel pattern.
/// </para>
/// </remarks>
public sealed class STCS005A : STCS001A
{
    private readonly ILogger<STCS005A>? _logger;
    private readonly double _feePerContract;
    private readonly double _exchangeFeePerContract;
    private readonly double _regulatoryFeePerContract;

    // LoggerMessage delegates (Rule 5: avoid allocation in hot paths)
    private static readonly Action<ILogger, string, int, double, Exception?> LogCostComputed =
        LoggerMessage.Define<string, int, double>(
            LogLevel.Debug,
            new EventId(1, nameof(LogCostComputed)),
            "Cost computed for {Symbol}: {Contracts} contracts, total cost ${TotalCost:F4}");

    private static readonly Action<ILogger, double, double, double, Exception?> LogSpreadCostComputed =
        LoggerMessage.Define<double, double, double>(
            LogLevel.Debug,
            new EventId(2, nameof(LogSpreadCostComputed)),
            "Spread cost: theoretical ${TheoreticalDebit:F4}, execution ${ExecutionDebit:F4}, slippage {SlippagePercent:F2}%");

    /// <summary>
    /// Default commission fee per contract (conservative estimate).
    /// </summary>
    /// <remarks>
    /// Based on typical retail option commission: $0.65/contract.
    /// IBKR tiered ranges from $0.15 to $0.70 depending on volume and premium.
    /// </remarks>
    public const double DefaultFeePerContract = 0.65;

    /// <summary>
    /// Default exchange fee per contract.
    /// </summary>
    /// <remarks>
    /// Exchange fees vary by exchange and order type. Using CBOE average.
    /// </remarks>
    public const double DefaultExchangeFee = 0.30;

    /// <summary>
    /// Default regulatory fee per contract.
    /// </summary>
    /// <remarks>
    /// Includes SEC, FINRA, and ORF fees (typically small).
    /// </remarks>
    public const double DefaultRegulatoryFee = 0.02;

    /// <summary>
    /// Initialises a new instance of the constant fee model.
    /// </summary>
    /// <param name="feePerContract">
    /// Commission fee per contract. Default: $0.65.
    /// </param>
    /// <param name="exchangeFeePerContract">
    /// Exchange fee per contract. Default: $0.30.
    /// </param>
    /// <param name="regulatoryFeePerContract">
    /// Regulatory fee per contract. Default: $0.02.
    /// </param>
    /// <param name="logger">Optional logger instance.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any fee is negative.
    /// </exception>
    public STCS005A(
        double feePerContract = DefaultFeePerContract,
        double exchangeFeePerContract = DefaultExchangeFee,
        double regulatoryFeePerContract = DefaultRegulatoryFee,
        ILogger<STCS005A>? logger = null)
    {
        if (feePerContract < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(feePerContract),
                feePerContract,
                "Fee per contract cannot be negative.");
        }

        if (exchangeFeePerContract < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exchangeFeePerContract),
                exchangeFeePerContract,
                "Exchange fee cannot be negative.");
        }

        if (regulatoryFeePerContract < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(regulatoryFeePerContract),
                regulatoryFeePerContract,
                "Regulatory fee cannot be negative.");
        }

        _feePerContract = feePerContract;
        _exchangeFeePerContract = exchangeFeePerContract;
        _regulatoryFeePerContract = regulatoryFeePerContract;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ModelName => "ConstantFeeModel";

    /// <inheritdoc />
    public STCS003A ComputeOptionCost(STCS002A parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate();

        // Compute execution price based on direction
        double executionPrice = parameters.Direction == OrderDirection.Buy
            ? parameters.AskPrice
            : parameters.BidPrice;

        // Compute slippage (difference from mid to execution price)
        double slippagePerContract = Math.Abs(executionPrice - parameters.MidPrice)
            * parameters.ContractMultiplier;
        double totalSlippage = slippagePerContract * parameters.Contracts;

        // Compute fees
        double commission = _feePerContract * parameters.Contracts;
        double exchangeFees = _exchangeFeePerContract * parameters.Contracts;
        double regulatoryFees = _regulatoryFeePerContract * parameters.Contracts;

        var result = new STCS003A
        {
            Commission = commission,
            ExchangeFees = exchangeFees,
            RegulatoryFees = regulatoryFees,
            Slippage = totalSlippage,
            TheoreticalPrice = parameters.MidPrice,
            ExecutionPrice = executionPrice,
            Contracts = parameters.Contracts
        };

        SafeLog(() => LogCostComputed(_logger!, parameters.Symbol, parameters.Contracts, result.TotalCost, null));

        return result;
    }

    /// <inheritdoc />
    public STCS004A ComputeSpreadCost(STCS002A frontLegParameters, STCS002A backLegParameters)
    {
        ArgumentNullException.ThrowIfNull(frontLegParameters);
        ArgumentNullException.ThrowIfNull(backLegParameters);

        frontLegParameters.Validate();
        backLegParameters.Validate();

        // Front leg: SELL (receive bid)
        var adjustedFrontParams = frontLegParameters with { Direction = OrderDirection.Sell };
        STCS003A frontCost = ComputeOptionCost(adjustedFrontParams);

        // Back leg: BUY (pay ask)
        var adjustedBackParams = backLegParameters with { Direction = OrderDirection.Buy };
        STCS003A backCost = ComputeOptionCost(adjustedBackParams);

        // Theoretical debit (mid-price based)
        double theoreticalDebit = backLegParameters.MidPrice - frontLegParameters.MidPrice;

        // Execution debit (bid-ask adjusted)
        // Buy back at ask, sell front at bid
        double executionDebit = backLegParameters.AskPrice - frontLegParameters.BidPrice;

        int contracts = Math.Min(frontLegParameters.Contracts, backLegParameters.Contracts);
        double contractMultiplier = frontLegParameters.ContractMultiplier;

        var result = new STCS004A
        {
            FrontLegCost = frontCost,
            BackLegCost = backCost,
            TheoreticalDebit = theoreticalDebit,
            ExecutionDebit = executionDebit,
            Contracts = contracts,
            ContractMultiplier = contractMultiplier
        };

        SafeLog(() => LogSpreadCostComputed(
            _logger!,
            result.TheoreticalDebit,
            result.ExecutionDebit,
            result.SlippagePercent,
            null));

        return result;
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