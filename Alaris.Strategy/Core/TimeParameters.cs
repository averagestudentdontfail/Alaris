namespace Alaris.Strategy.Core;

/// <summary>
/// Comprehensive time parameter specification for IV models.
/// Handles time-to-expiration, earnings dates, and trading calendar conversions.
/// </summary>
public sealed class TimeParameters
{
    /// <summary>
    /// Trading days per year (US equity markets).
    /// </summary>
    public const double TradingDaysPerYear = 252.0;

    /// <summary>
    /// Trading hours per day (6.5 hours for US markets).
    /// </summary>
    public const double TradingHoursPerDay = 6.5;

    /// <summary>
    /// Minimum time to expiry in years (~1 hour).
    /// </summary>
    public const double MinTimeToExpiry = 1.0 / (TradingDaysPerYear * TradingHoursPerDay);

    /// <summary>
    /// Maximum reasonable time to expiry (3 years for LEAPS).
    /// </summary>
    public const double MaxTimeToExpiry = 3.0;

    /// <summary>
    /// Current valuation date.
    /// </summary>
    public DateTime ValuationDate { get; init; }

    /// <summary>
    /// Option expiration date.
    /// </summary>
    public DateTime ExpirationDate { get; init; }

    /// <summary>
    /// Earnings announcement date (null if no earnings before expiry).
    /// </summary>
    public DateTime? EarningsDate { get; init; }

    /// <summary>
    /// Time to expiration in years (T - t).
    /// </summary>
    public double TimeToExpiry { get; private init; }

    /// <summary>
    /// Time to earnings in years (T_e - t), null if no earnings.
    /// </summary>
    public double? TimeToEarnings { get; private init; }

    /// <summary>
    /// Days to expiration.
    /// </summary>
    public int DaysToExpiry { get; private init; }

    /// <summary>
    /// Days to earnings, null if no earnings before expiry.
    /// </summary>
    public int? DaysToEarnings { get; private init; }

    /// <summary>
    /// Whether we are in the pre-earnings regime.
    /// </summary>
    public bool IsPreEarnings => EarningsDate.HasValue && ValuationDate < EarningsDate.Value;

    /// <summary>
    /// Whether we are in the post-earnings regime.
    /// </summary>
    public bool IsPostEarnings => EarningsDate.HasValue && ValuationDate >= EarningsDate.Value;

    /// <summary>
    /// Whether earnings fall before option expiration.
    /// </summary>
    public bool HasEarningsBeforeExpiry => EarningsDate.HasValue && EarningsDate.Value < ExpirationDate;

    private TimeParameters() { }

    /// <summary>
    /// Creates and validates time parameters.
    /// </summary>
    /// <param name="valuationDate">Current valuation date.</param>
    /// <param name="expirationDate">Option expiration date.</param>
    /// <param name="earningsDate">Optional earnings announcement date.</param>
    /// <returns>Validated time parameters.</returns>
    public static TimeParameters Create(
        DateTime valuationDate,
        DateTime expirationDate,
        DateTime? earningsDate = null)
    {
        TimeConstraints constraints = TimeConstraints.Default;
        return Create(valuationDate, expirationDate, earningsDate, constraints);
    }

    /// <summary>
    /// Creates and validates time parameters with custom constraints.
    /// </summary>
    public static TimeParameters Create(
        DateTime valuationDate,
        DateTime expirationDate,
        DateTime? earningsDate,
        TimeConstraints constraints)
    {
        ArgumentNullException.ThrowIfNull(constraints);

        // Basic validation
        if (expirationDate <= valuationDate)
        {
            throw new ArgumentException(
                $"Expiration date ({expirationDate:yyyy-MM-dd}) must be after valuation date ({valuationDate:yyyy-MM-dd}).",
                nameof(expirationDate));
        }

        // Calculate time values
        int dte = CalculateTradingDays(valuationDate, expirationDate);
        double timeToExpiry = dte / TradingDaysPerYear;

        // Validate time constraints
        if (timeToExpiry < constraints.MinTimeToExpiry)
        {
            throw new ArgumentException(
                $"Time to expiry ({timeToExpiry:F6} years) is below minimum ({constraints.MinTimeToExpiry:F6} years).",
                nameof(expirationDate));
        }

        if (timeToExpiry > constraints.MaxTimeToExpiry)
        {
            throw new ArgumentException(
                $"Time to expiry ({timeToExpiry:F2} years) exceeds maximum ({constraints.MaxTimeToExpiry:F2} years).",
                nameof(expirationDate));
        }

        // Process earnings date
        int? daysToEarnings = null;
        double? timeToEarnings = null;

        if (earningsDate.HasValue)
        {
            if (earningsDate.Value <= valuationDate)
            {
                // Earnings already passed - post-earnings regime
                daysToEarnings = -CalculateTradingDays(earningsDate.Value, valuationDate);
                timeToEarnings = daysToEarnings.Value / TradingDaysPerYear;
            }
            else if (earningsDate.Value < expirationDate)
            {
                // Earnings before expiry - pre-earnings regime
                daysToEarnings = CalculateTradingDays(valuationDate, earningsDate.Value);
                timeToEarnings = daysToEarnings.Value / TradingDaysPerYear;
            }
            // else: earnings after expiry, treat as no earnings event
        }

        return new TimeParameters
        {
            ValuationDate = valuationDate,
            ExpirationDate = expirationDate,
            EarningsDate = earningsDate,
            TimeToExpiry = timeToExpiry,
            TimeToEarnings = timeToEarnings,
            DaysToExpiry = dte,
            DaysToEarnings = daysToEarnings
        };
    }

    /// <summary>
    /// Calculates trading days between two dates using production-grade trading calendar.
    /// Excludes weekends and US market holidays.
    /// </summary>
    private static int CalculateTradingDays(DateTime start, DateTime end)
    {
        // Production implementation: Use proper trading calendar with holiday awareness
        int days = TradingCalendar.GetTradingDays(start, end, includeEndDate: false);
        return Math.Max(1, days); // Minimum 1 day
    }

    /// <summary>
    /// Converts days to expiration to annualized time.
    /// </summary>
    public static double DteToYears(int dte) =>
        Math.Max(dte, 1) / TradingDaysPerYear;

    /// <summary>
    /// Converts annualized time to days to expiration.
    /// </summary>
    public static int YearsToDte(double years) =>
        (int)Math.Round(years * TradingDaysPerYear);
}

/// <summary>
/// Constraint specifications for time parameters.
/// </summary>
public sealed class TimeConstraints
{
    /// <summary>
    /// Minimum time to expiry in years.
    /// </summary>
    public double MinTimeToExpiry { get; init; } = TimeParameters.MinTimeToExpiry;

    /// <summary>
    /// Maximum time to expiry in years.
    /// </summary>
    public double MaxTimeToExpiry { get; init; } = TimeParameters.MaxTimeToExpiry;

    /// <summary>
    /// Minimum days before earnings for pre-EA trades.
    /// </summary>
    public int MinDaysBeforeEarnings { get; init; } = 1;

    /// <summary>
    /// Maximum days before earnings for pre-EA strategy eligibility.
    /// </summary>
    public int MaxDaysBeforeEarnings { get; init; } = 30;

    /// <summary>
    /// Days after earnings for post-EA regime (volatility normalization).
    /// </summary>
    public int PostEarningsNormalizationDays { get; init; } = 5;

    /// <summary>
    /// Default constraints.
    /// </summary>
    public static TimeConstraints Default { get; } = new();

    /// <summary>
    /// Strict constraints for short-dated options.
    /// </summary>
    public static TimeConstraints ShortDated { get; } = new()
    {
        MinTimeToExpiry = 1.0 / TimeParameters.TradingDaysPerYear, // 1 day
        MaxTimeToExpiry = 0.25, // ~63 days (quarterly)
        MinDaysBeforeEarnings = 1,
        MaxDaysBeforeEarnings = 14
    };

    /// <summary>
    /// Relaxed constraints for LEAPS.
    /// </summary>
    public static TimeConstraints Leaps { get; } = new()
    {
        MinTimeToExpiry = 0.25, // ~63 days
        MaxTimeToExpiry = 3.0, // 3 years
        MinDaysBeforeEarnings = 1,
        MaxDaysBeforeEarnings = 90
    };

    /// <summary>
    /// Validates that time parameters satisfy these constraints for pre-earnings trades.
    /// </summary>
    public ValidationResult ValidatePreEarnings(TimeParameters timeParams)
    {
        ArgumentNullException.ThrowIfNull(timeParams);

        var errors = new List<string>();

        if (!timeParams.IsPreEarnings)
        {
            errors.Add("Not in pre-earnings regime.");
        }
        else if (timeParams.DaysToEarnings.HasValue)
        {
            if (timeParams.DaysToEarnings.Value < MinDaysBeforeEarnings)
            {
                errors.Add($"Days to earnings ({timeParams.DaysToEarnings.Value}) below minimum ({MinDaysBeforeEarnings}).");
            }

            if (timeParams.DaysToEarnings.Value > MaxDaysBeforeEarnings)
            {
                errors.Add($"Days to earnings ({timeParams.DaysToEarnings.Value}) exceeds maximum ({MaxDaysBeforeEarnings}).");
            }
        }

        if (timeParams.TimeToExpiry < MinTimeToExpiry)
        {
            errors.Add($"Time to expiry ({timeParams.TimeToExpiry:F4}) below minimum ({MinTimeToExpiry:F4}).");
        }

        if (timeParams.TimeToExpiry > MaxTimeToExpiry)
        {
            errors.Add($"Time to expiry ({timeParams.TimeToExpiry:F2}) exceeds maximum ({MaxTimeToExpiry:F2}).");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}

/// <summary>
/// Result of constraint validation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Whether validation passed.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Validation error messages.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    public ValidationResult(bool isValid, IReadOnlyList<string>? errors = null)
    {
        IsValid = isValid;
        Errors = errors ?? Array.Empty<string>();
    }

    /// <summary>
    /// Throws if validation failed.
    /// </summary>
    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException(
                $"Validation failed: {string.Join("; ", Errors)}");
        }
    }
}
