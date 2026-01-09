// STDT011A.cs - Pre-flight validation result model
// Component ID: STDT011A

namespace Alaris.Core.Model;

/// <summary>
/// Comprehensive pre-flight validation result for backtest sessions.
/// Provides detailed diagnostics and remediation actions.
/// </summary>
/// <remarks>
/// Governance compliance (Rule 7 - Fail Fast):
/// Invalid state is detected and reported before backtest execution,
/// with specific remediation actions to resolve each issue.
/// </remarks>
public sealed record STDT011A
{
    /// <summary>
    /// Overall validation status.
    /// </summary>
    public required PreflightStatus Status { get; init; }

    /// <summary>
    /// Human-readable summary of the validation result.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Individual validation check results.
    /// </summary>
    public required IReadOnlyList<PreflightCheck> Checks { get; init; }

    /// <summary>
    /// Remediation actions required to fix issues.
    /// Empty if Status is Ready.
    /// </summary>
    public required IReadOnlyList<RemediationAction> RemediationActions { get; init; }

    /// <summary>
    /// Statistics about data coverage.
    /// </summary>
    public required DataCoverageStats Coverage { get; init; }

    /// <summary>
    /// Timestamp when validation was performed.
    /// </summary>
    public DateTime ValidatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the session is ready to run (Ready or Warning status).
    /// Warning status indicates partial data coverage but backtest can proceed.
    /// </summary>
    public bool IsReady => Status == PreflightStatus.Ready || Status == PreflightStatus.Warning;

    /// <summary>
    /// Whether automatic remediation can fix all issues.
    /// </summary>
    public bool CanAutoRemediate => RemediationActions.Count == 0 ||
        RemediationActions.All(a => a.CanAutomate);

    /// <summary>
    /// Total estimated time for all remediation actions.
    /// </summary>
    public TimeSpan EstimatedRemediationTime =>
        TimeSpan.FromSeconds(RemediationActions.Sum(a => a.EstimatedSeconds));

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static STDT011A Success(IReadOnlyList<PreflightCheck> checks, DataCoverageStats coverage) =>
        new()
        {
            Status = PreflightStatus.Ready,
            Summary = "All pre-flight checks passed. Session is ready to run.",
            Checks = checks,
            RemediationActions = Array.Empty<RemediationAction>(),
            Coverage = coverage
        };

    /// <summary>
    /// Creates a failed validation result with remediation actions.
    /// </summary>
    public static STDT011A Failure(
        IReadOnlyList<PreflightCheck> checks,
        IReadOnlyList<RemediationAction> actions,
        DataCoverageStats coverage)
    {
        int criticalCount = checks.Count(c => c.Status == CheckStatus.Failed);
        int warningCount = checks.Count(c => c.Status == CheckStatus.Warning);

        string summary = criticalCount > 0
            ? $"Pre-flight validation failed: {criticalCount} critical issue(s), {warningCount} warning(s). Remediation required."
            : $"Pre-flight validation passed with {warningCount} warning(s). Consider remediation for optimal results.";

        return new()
        {
            Status = criticalCount > 0 ? PreflightStatus.Failed : PreflightStatus.Warning,
            Summary = summary,
            Checks = checks,
            RemediationActions = actions,
            Coverage = coverage
        };
    }
}

/// <summary>
/// Pre-flight validation status.
/// </summary>
public enum PreflightStatus
{
    /// <summary>Session is ready to run.</summary>
    Ready = 0,

    /// <summary>Session can run but may have suboptimal results.</summary>
    Warning = 1,

    /// <summary>Session cannot run without remediation.</summary>
    Failed = 2
}

/// <summary>
/// Individual pre-flight check result.
/// </summary>
public sealed record PreflightCheck
{
    /// <summary>Unique identifier for this check type.</summary>
    public required string CheckId { get; init; }

    /// <summary>Human-readable check name.</summary>
    public required string Name { get; init; }

    /// <summary>Check result status.</summary>
    public required CheckStatus Status { get; init; }

    /// <summary>Detailed message explaining the result.</summary>
    public required string Message { get; init; }

    /// <summary>Category of the check.</summary>
    public required CheckCategory Category { get; init; }

    /// <summary>Affected symbols (if applicable).</summary>
    public IReadOnlyList<string> AffectedSymbols { get; init; } = Array.Empty<string>();

    /// <summary>Affected dates (if applicable).</summary>
    public IReadOnlyList<DateTime> AffectedDates { get; init; } = Array.Empty<DateTime>();

    /// <summary>Creates a passed check.</summary>
    public static PreflightCheck Pass(string checkId, string name, CheckCategory category, string message) =>
        new()
        {
            CheckId = checkId,
            Name = name,
            Status = CheckStatus.Passed,
            Message = message,
            Category = category
        };

    /// <summary>Creates a warning check.</summary>
    public static PreflightCheck Warn(
        string checkId,
        string name,
        CheckCategory category,
        string message,
        IReadOnlyList<string>? affectedSymbols = null,
        IReadOnlyList<DateTime>? affectedDates = null) =>
        new()
        {
            CheckId = checkId,
            Name = name,
            Status = CheckStatus.Warning,
            Message = message,
            Category = category,
            AffectedSymbols = affectedSymbols ?? Array.Empty<string>(),
            AffectedDates = affectedDates ?? Array.Empty<DateTime>()
        };

    /// <summary>Creates a failed check.</summary>
    public static PreflightCheck Fail(
        string checkId,
        string name,
        CheckCategory category,
        string message,
        IReadOnlyList<string>? affectedSymbols = null,
        IReadOnlyList<DateTime>? affectedDates = null) =>
        new()
        {
            CheckId = checkId,
            Name = name,
            Status = CheckStatus.Failed,
            Message = message,
            Category = category,
            AffectedSymbols = affectedSymbols ?? Array.Empty<string>(),
            AffectedDates = affectedDates ?? Array.Empty<DateTime>()
        };
}

/// <summary>
/// Check result status.
/// </summary>
public enum CheckStatus
{
    Passed = 0,
    Warning = 1,
    Failed = 2
}

/// <summary>
/// Check category for grouping and prioritization.
/// </summary>
public enum CheckCategory
{
    /// <summary>Price/equity data checks.</summary>
    PriceData = 0,

    /// <summary>Earnings calendar checks.</summary>
    EarningsData = 1,

    /// <summary>Options chain checks.</summary>
    OptionsData = 2,

    /// <summary>Interest rate data checks.</summary>
    RatesData = 3,

    /// <summary>System/configuration checks.</summary>
    System = 4
}

/// <summary>
/// Remediation action to fix a validation issue.
/// </summary>
public sealed record RemediationAction
{
    /// <summary>Unique action identifier.</summary>
    public required string ActionId { get; init; }

    /// <summary>Human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>Type of remediation.</summary>
    public required RemediationType Type { get; init; }

    /// <summary>Whether this can be automated.</summary>
    public required bool CanAutomate { get; init; }

    /// <summary>Estimated time in seconds.</summary>
    public required int EstimatedSeconds { get; init; }

    /// <summary>Priority (lower = more important).</summary>
    public required int Priority { get; init; }

    /// <summary>Symbols to remediate (if applicable).</summary>
    public IReadOnlyList<string> Symbols { get; init; } = Array.Empty<string>();

    /// <summary>Dates to remediate (if applicable).</summary>
    public IReadOnlyList<DateTime> Dates { get; init; } = Array.Empty<DateTime>();

    /// <summary>Related check IDs that this action addresses.</summary>
    public IReadOnlyList<string> AddressesCheckIds { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Type of remediation action.
/// </summary>
public enum RemediationType
{
    /// <summary>Download missing price data.</summary>
    DownloadPriceData = 0,

    /// <summary>Download missing earnings data.</summary>
    DownloadEarningsData = 1,

    /// <summary>Download missing options data.</summary>
    DownloadOptionsData = 2,

    /// <summary>Re-download invalid options data.</summary>
    RedownloadOptionsData = 3,

    /// <summary>Download missing interest rate data.</summary>
    DownloadRatesData = 4,

    /// <summary>Copy system files.</summary>
    CopySystemFiles = 5,

    /// <summary>Manual intervention required.</summary>
    ManualIntervention = 6
}

/// <summary>
/// Statistics about data coverage.
/// </summary>
public sealed record DataCoverageStats
{
    /// <summary>Total symbols in session.</summary>
    public required int TotalSymbols { get; init; }

    /// <summary>Symbols with complete price data.</summary>
    public required int SymbolsWithPriceData { get; init; }

    /// <summary>Total earnings dates required.</summary>
    public required int TotalEarningsDates { get; init; }

    /// <summary>Earnings dates with data available.</summary>
    public required int EarningsDatesWithData { get; init; }

    /// <summary>Total options dates required.</summary>
    public required int TotalOptionsDates { get; init; }

    /// <summary>Options dates with valid data.</summary>
    public required int OptionsDatesWithValidData { get; init; }

    /// <summary>Options dates with any data (valid or invalid).</summary>
    public required int OptionsDatesWithAnyData { get; init; }

    /// <summary>Symbol-date combinations requiring options.</summary>
    public required int TotalOptionsSymbolDates { get; init; }

    /// <summary>Symbol-date combinations with valid options.</summary>
    public required int ValidOptionsSymbolDates { get; init; }

    /// <summary>Price data coverage percentage.</summary>
    public double PriceCoveragePercent =>
        TotalSymbols > 0 ? (double)SymbolsWithPriceData / TotalSymbols * 100 : 0;

    /// <summary>Earnings coverage percentage.</summary>
    public double EarningsCoveragePercent =>
        TotalEarningsDates > 0 ? (double)EarningsDatesWithData / TotalEarningsDates * 100 : 0;

    /// <summary>Options valid coverage percentage.</summary>
    public double OptionsValidCoveragePercent =>
        TotalOptionsSymbolDates > 0 ? (double)ValidOptionsSymbolDates / TotalOptionsSymbolDates * 100 : 0;

    /// <summary>Options any coverage percentage.</summary>
    public double OptionsAnyCoveragePercent =>
        TotalOptionsDates > 0 ? (double)OptionsDatesWithAnyData / TotalOptionsDates * 100 : 0;

    /// <summary>Creates empty stats.</summary>
    public static DataCoverageStats Empty => new()
    {
        TotalSymbols = 0,
        SymbolsWithPriceData = 0,
        TotalEarningsDates = 0,
        EarningsDatesWithData = 0,
        TotalOptionsDates = 0,
        OptionsDatesWithValidData = 0,
        OptionsDatesWithAnyData = 0,
        TotalOptionsSymbolDates = 0,
        ValidOptionsSymbolDates = 0
    };
}

/// <summary>
/// Options data quality assessment for a specific symbol-date.
/// </summary>
public sealed record OptionsDataQuality
{
    /// <summary>Symbol being assessed.</summary>
    public required string Symbol { get; init; }

    /// <summary>Date being assessed.</summary>
    public required DateTime Date { get; init; }

    /// <summary>Whether cache file exists.</summary>
    public required bool CacheExists { get; init; }

    /// <summary>Total contracts in cache.</summary>
    public int TotalContracts { get; init; }

    /// <summary>Contracts with valid IV.</summary>
    public int ContractsWithValidIV { get; init; }

    /// <summary>Number of future expirations.</summary>
    public int FutureExpirations { get; init; }

    /// <summary>Whether calls are present.</summary>
    public bool HasCalls { get; init; }

    /// <summary>Whether puts are present.</summary>
    public bool HasPuts { get; init; }

    /// <summary>Calls with valid IV.</summary>
    public int CallsWithValidIV { get; init; }

    /// <summary>Puts with valid IV.</summary>
    public int PutsWithValidIV { get; init; }

    /// <summary>Validation failure reason (if any).</summary>
    public string? FailureReason { get; init; }

    /// <summary>Whether data is valid for term structure analysis.</summary>
    public bool IsValidForTermStructure =>
        CacheExists &&
        FutureExpirations >= 2 &&
        ContractsWithValidIV > 0 &&
        HasCalls && HasPuts &&
        CallsWithValidIV > 0 && PutsWithValidIV > 0;

    /// <summary>Creates a missing data assessment.</summary>
    public static OptionsDataQuality Missing(string symbol, DateTime date) => new()
    {
        Symbol = symbol,
        Date = date,
        CacheExists = false,
        FailureReason = "No cache file found"
    };

    /// <summary>Creates an invalid data assessment.</summary>
    public static OptionsDataQuality Invalid(string symbol, DateTime date, string reason) => new()
    {
        Symbol = symbol,
        Date = date,
        CacheExists = true,
        FailureReason = reason
    };
}
