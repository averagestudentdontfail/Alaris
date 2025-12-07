// =============================================================================
// DTca001A.cs - Corporate Actions Provider Interface
// Component: DTca001A | Category: Provider | Variant: A (Primary)
// =============================================================================
// Defines interface for retrieving corporate actions data and computing
// adjustments for backtesting. Critical for maintaining data integrity when
// backtesting options strategies across split/dividend events.
// =============================================================================

namespace Alaris.Data.Provider;

/// <summary>
/// Corporate Actions Provider Interface.
/// Component ID: DTca001A
/// </summary>
/// <remarks>
/// <para>
/// Provides methods for retrieving and applying corporate actions
/// for historical data adjustment in backtesting. This is critical for:
/// </para>
/// <list type="bullet">
///   <item>Maintaining point-in-time data integrity</item>
///   <item>Correctly aligning option strikes with underlying prices</item>
///   <item>Computing accurate IV term structure across events</item>
/// </list>
/// <para>
/// <b>Key Principle</b>: Adjustment is a computation, not stored data.
/// Raw prices are ground truth; adjustments are applied on-demand.
/// </para>
/// <para>
/// <b>Live Trading Note</b>: In live trading, IBKR handles corporate
/// actions automatically (adjusts positions, issues new symbols).
/// This interface is primarily for backtesting use.
/// </para>
/// </remarks>
public interface DTca001A
{
    /// <summary>
    /// Gets corporate actions for a symbol within a date range.
    /// </summary>
    /// <param name="symbol">The ticker symbol.</param>
    /// <param name="startDate">Start of the date range.</param>
    /// <param name="endDate">End of the date range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of corporate actions ordered by ex-date.</returns>
    Task<IReadOnlyList<Model.DTmd003A>> GetActionsAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adjusts a raw price for all corporate actions up to a given date.
    /// </summary>
    /// <param name="rawPrice">The unadjusted historical price.</param>
    /// <param name="priceDate">The date of the raw price.</param>
    /// <param name="actions">Corporate actions to apply.</param>
    /// <returns>The adjusted price as of today's terms.</returns>
    /// <remarks>
    /// Applies cumulative adjustment factors for all actions with
    /// ex-date after priceDate and before today.
    /// </remarks>
    decimal AdjustPrice(
        decimal rawPrice,
        DateTime priceDate,
        IReadOnlyList<Model.DTmd003A> actions);

    /// <summary>
    /// Adjusts an option strike price for corporate actions.
    /// </summary>
    /// <param name="strike">The original strike price.</param>
    /// <param name="optionTradeDate">The date the option was created/traded.</param>
    /// <param name="actions">Corporate actions to apply.</param>
    /// <returns>The adjusted strike price.</returns>
    /// <remarks>
    /// <para>
    /// When a stock splits, the OCC adjusts:
    /// </para>
    /// <list type="bullet">
    ///   <item>Strike price (divided by split ratio)</item>
    ///   <item>Contract multiplier (multiplied by split ratio)</item>
    /// </list>
    /// <para>
    /// This method handles the strike adjustment. For proper
    /// backtesting, always use this when comparing historical
    /// option strikes to adjusted equity prices.
    /// </para>
    /// </remarks>
    decimal AdjustStrike(
        decimal strike,
        DateTime optionTradeDate,
        IReadOnlyList<Model.DTmd003A> actions);

    /// <summary>
    /// Checks if a date falls within a period affected by a corporate action.
    /// </summary>
    /// <param name="checkDate">The date to check.</param>
    /// <param name="actions">Corporate actions to check against.</param>
    /// <param name="bufferDays">Buffer days around ex-date (default 5).</param>
    /// <returns>True if the date is near a corporate action.</returns>
    /// <remarks>
    /// Useful for flagging IV surface data that may be unreliable
    /// due to corporate action effects.
    /// </remarks>
    bool IsAffectedByAction(
        DateTime checkDate,
        IReadOnlyList<Model.DTmd003A> actions,
        int bufferDays = 5);
}
