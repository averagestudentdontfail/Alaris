// CROP004A.cs - Dividend Schedule and Cash Dividend Models
// Component ID: CROP004A
//
// Handles discrete cash dividends for American option pricing.
// Two models per QuantLib/academic convention:
// - Spot model: Subtract PV(dividends) from spot at pricing time
// - Escrowed model: Create jump conditions at dividend dates in FD grid
//
// References:
// - Wilmott, Howison, Dewynne. "The Mathematics of Financial Derivatives" (1995)
// - QuantLib FdBlackScholesVanillaEngine CashDividendModel

using System.Collections.Immutable;

namespace Alaris.Core.Options;

/// <summary>
/// Cash dividend model determining how discrete dividends affect pricing.
/// </summary>
public enum CashDividendModel
{
    /// <summary>
    /// Spot model: Subtract present value of dividends from current spot price.
    /// Simple, but less accurate for American options near dividend dates.
    /// </summary>
    /// <remarks>
    /// S_adjusted = S - Σ [D_i × exp(-r × t_i)] for all dividends D_i at time t_i.
    /// </remarks>
    Spot,

    /// <summary>
    /// Escrowed model: Handle dividends as jump conditions in the FD grid.
    /// More accurate but computationally expensive.
    /// </summary>
    /// <remarks>
    /// At each dividend date, apply V(S) → V(S - D) transformation in the grid.
    /// Requires grid interpolation at dividend times.
    /// </remarks>
    Escrowed
}

/// <summary>
/// Represents a single discrete cash dividend payment.
/// </summary>
/// <param name="ExDividendDate">Ex-dividend date.</param>
/// <param name="Amount">Dividend amount per share.</param>
public readonly record struct CashDividend(DateTime ExDividendDate, double Amount)
{
    /// <summary>
    /// Validates the dividend parameters.
    /// </summary>
    /// <returns>True if valid.</returns>
    public bool IsValid => Amount >= 0 && ExDividendDate != default;
}

/// <summary>
/// Immutable schedule of discrete cash dividends for option pricing.
/// </summary>
public sealed class DividendSchedule
{
    /// <summary>
    /// Empty dividend schedule (no discrete dividends).
    /// </summary>
    public static readonly DividendSchedule Empty = new();

    private readonly ImmutableArray<CashDividend> _dividends;

    /// <summary>
    /// Creates an empty dividend schedule.
    /// </summary>
    public DividendSchedule()
    {
        _dividends = ImmutableArray<CashDividend>.Empty;
    }

    /// <summary>
    /// Creates a dividend schedule from a list of dividends.
    /// </summary>
    /// <param name="dividends">Dividends to include.</param>
    public DividendSchedule(IEnumerable<CashDividend> dividends)
    {
        ArgumentNullException.ThrowIfNull(dividends);
        _dividends = dividends
            .Where(d => d.IsValid)
            .OrderBy(d => d.ExDividendDate)
            .ToImmutableArray();
    }

    /// <summary>
    /// Gets all dividends in chronological order.
    /// </summary>
    public ImmutableArray<CashDividend> Dividends => _dividends;

    /// <summary>
    /// Gets the number of dividends in the schedule.
    /// </summary>
    public int Count => _dividends.Length;

    /// <summary>
    /// Gets whether the schedule has any dividends.
    /// </summary>
    public bool HasDividends => _dividends.Length > 0;

    /// <summary>
    /// Gets dividends between two dates (exclusive of start, inclusive of end).
    /// </summary>
    /// <param name="startDate">Start date (exclusive).</param>
    /// <param name="endDate">End date (inclusive).</param>
    /// <returns>Dividends in the range.</returns>
    public IEnumerable<CashDividend> GetDividendsBetween(DateTime startDate, DateTime endDate)
    {
        return _dividends.Where(d => d.ExDividendDate > startDate && d.ExDividendDate <= endDate);
    }

    /// <summary>
    /// Computes the present value of all dividends using the Spot model.
    /// </summary>
    /// <param name="valuationDate">Current valuation date.</param>
    /// <param name="expiryDate">Option expiry date.</param>
    /// <param name="riskFreeRate">Continuous risk-free rate.</param>
    /// <returns>Total PV of dividends occurring before expiry.</returns>
    /// <remarks>
    /// PV = Σ [D_i × exp(-r × t_i)] for all dividends before expiry.
    /// Time t_i is measured in years from valuation date.
    /// </remarks>
    public double ComputePresentValue(DateTime valuationDate, DateTime expiryDate, double riskFreeRate)
    {
        double totalPV = 0.0;

        foreach (CashDividend dividend in GetDividendsBetween(valuationDate, expiryDate))
        {
            double timeToDiv = (dividend.ExDividendDate - valuationDate).TotalDays / 365.0;
            totalPV += dividend.Amount * System.Math.Exp(-riskFreeRate * timeToDiv);
        }

        return totalPV;
    }

    /// <summary>
    /// Adjusts the spot price for the Spot dividend model.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="valuationDate">Current valuation date.</param>
    /// <param name="expiryDate">Option expiry date.</param>
    /// <param name="riskFreeRate">Continuous risk-free rate.</param>
    /// <returns>Adjusted spot price (S - PV of dividends).</returns>
    public double AdjustSpotForDividends(double spot, DateTime valuationDate, DateTime expiryDate, double riskFreeRate)
    {
        double pvDividends = ComputePresentValue(valuationDate, expiryDate, riskFreeRate);
        return System.Math.Max(spot - pvDividends, 0.0); // Floor at zero
    }

    /// <summary>
    /// Gets dividend times relative to a valuation date, in years.
    /// </summary>
    /// <param name="valuationDate">Current valuation date.</param>
    /// <param name="expiryDate">Option expiry date.</param>
    /// <returns>Array of (time in years, amount) pairs for FD grid integration.</returns>
    public (double TimeYears, double Amount)[] GetDividendTimesForGrid(DateTime valuationDate, DateTime expiryDate)
    {
        return GetDividendsBetween(valuationDate, expiryDate)
            .Select(d => ((d.ExDividendDate - valuationDate).TotalDays / 365.0, d.Amount))
            .ToArray();
    }

    /// <summary>
    /// Creates a new schedule with an additional dividend.
    /// </summary>
    /// <param name="dividend">Dividend to add.</param>
    /// <returns>New schedule with dividend included.</returns>
    public DividendSchedule Add(CashDividend dividend)
    {
        if (!dividend.IsValid)
        {
            return this;
        }

        return new DividendSchedule(_dividends.Add(dividend));
    }

    /// <summary>
    /// Estimates dividends from a continuous yield over a period.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="continuousYield">Annual continuous dividend yield.</param>
    /// <param name="startDate">Start date.</param>
    /// <param name="endDate">End date.</param>
    /// <param name="frequency">Dividend frequency (per year, e.g., 4 for quarterly).</param>
    /// <returns>DividendSchedule with estimated discrete dividends.</returns>
    /// <remarks>
    /// Each dividend = S × (exp(q/frequency) - 1) ≈ S × q / frequency for small q.
    /// </remarks>
    public static DividendSchedule FromContinuousYield(
        double spot,
        double continuousYield,
        DateTime startDate,
        DateTime endDate,
        int frequency = 4)
    {
        if (continuousYield <= 0 || frequency <= 0)
        {
            return Empty;
        }

        List<CashDividend> dividends = new List<CashDividend>();
        TimeSpan period = TimeSpan.FromDays(365.0 / frequency);
        double dividendAmount = spot * (System.Math.Exp(continuousYield / frequency) - 1);

        DateTime currentDate = startDate.Add(period);
        while (currentDate <= endDate)
        {
            dividends.Add(new CashDividend(currentDate, dividendAmount));
            currentDate = currentDate.Add(period);
        }

        return new DividendSchedule(dividends);
    }
}
