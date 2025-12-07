// =============================================================================
// DTmd003A.cs - Corporate Action Model
// Component: DTmd003A | Category: Model | Variant: A (Primary)
// =============================================================================
// Data model for corporate actions (splits, dividends, mergers, etc.).
// Used by DTca001A provider for backtesting data integrity.
// =============================================================================

namespace Alaris.Data.Model;

/// <summary>
/// Types of corporate actions that affect price/strike adjustments.
/// </summary>
public enum CorporateActionType
{
    /// <summary>Stock split (e.g., 2:1 doubles shares, halves price).</summary>
    Split = 0,

    /// <summary>Reverse split (e.g., 1:5 reduces shares, increases price).</summary>
    ReverseSplit = 1,

    /// <summary>Cash dividend (affects option pricing near ex-date).</summary>
    CashDividend = 2,

    /// <summary>Stock dividend (additional shares issued).</summary>
    StockDividend = 3,

    /// <summary>Spin-off (new company shares issued).</summary>
    SpinOff = 4,

    /// <summary>Merger or acquisition.</summary>
    Merger = 5,

    /// <summary>Rights offering.</summary>
    RightsOffering = 6
}

/// <summary>
/// Corporate Action Data Model.
/// Component ID: DTmd003A
/// </summary>
/// <remarks>
/// <para>
/// Represents a single corporate action event that may require
/// price adjustments in historical backtesting data.
/// </para>
/// <para>
/// <b>Key Fields:</b>
/// </para>
/// <list type="bullet">
///   <item><b>ExDate</b>: The ex-dividend/split date (price adjusts on this date)</item>
///   <item><b>Factor</b>: The adjustment multiplier (e.g., 2.0 for 2:1 split)</item>
///   <item><b>PayDate</b>: When the action is paid/effective</item>
/// </list>
/// </remarks>
/// <param name="Symbol">Ticker symbol affected by this action.</param>
/// <param name="ExDate">Ex-date when the adjustment takes effect.</param>
/// <param name="Type">Type of corporate action.</param>
/// <param name="Factor">
/// Adjustment factor. For splits: new shares per old share.
/// For dividends: cash amount per share.
/// </param>
/// <param name="Description">Human-readable description of the action.</param>
/// <param name="PayDate">Optional payment/effective date.</param>
/// <param name="RecordDate">Optional record date.</param>
public sealed record DTmd003A(
    string Symbol,
    DateTime ExDate,
    CorporateActionType Type,
    decimal Factor,
    string Description,
    DateTime? PayDate = null,
    DateTime? RecordDate = null)
{
    /// <summary>
    /// Gets whether this action requires price adjustment.
    /// </summary>
    public bool RequiresPriceAdjustment => Type is
        CorporateActionType.Split or
        CorporateActionType.ReverseSplit or
        CorporateActionType.StockDividend;

    /// <summary>
    /// Gets whether this action requires strike adjustment for options.
    /// </summary>
    public bool RequiresStrikeAdjustment => Type is
        CorporateActionType.Split or
        CorporateActionType.ReverseSplit;

    /// <summary>
    /// Computes the price adjustment multiplier.
    /// </summary>
    /// <returns>
    /// The multiplier to apply to pre-action prices.
    /// For a 2:1 split, returns 0.5 (price is halved).
    /// </returns>
    public decimal GetPriceMultiplier()
    {
        return Type switch
        {
            CorporateActionType.Split => 1m / Factor,
            CorporateActionType.ReverseSplit => Factor,
            CorporateActionType.StockDividend => 1m / (1m + Factor),
            _ => 1m
        };
    }

    /// <summary>
    /// Computes the strike adjustment multiplier for options.
    /// </summary>
    /// <returns>
    /// The multiplier to apply to pre-action strikes.
    /// For a 2:1 split, returns 0.5 (strikes are halved).
    /// </returns>
    public decimal GetStrikeMultiplier()
    {
        return Type switch
        {
            CorporateActionType.Split => 1m / Factor,
            CorporateActionType.ReverseSplit => Factor,
            _ => 1m
        };
    }
}
