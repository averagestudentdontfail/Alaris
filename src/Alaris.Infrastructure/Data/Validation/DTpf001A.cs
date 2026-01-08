// DTpf001A.cs - Pre-flight validator for backtest sessions
// Component ID: DTpf001A

using System.Text.Json;
using Alaris.Core.Model;
using Microsoft.Extensions.Logging;

namespace Alaris.Infrastructure.Data.Validation;

/// <summary>
/// Comprehensive pre-flight validator for backtest sessions.
/// Validates all data requirements before execution and provides remediation actions.
/// </summary>
/// <remarks>
/// Governance compliance:
/// - Rule 7 (Fail Fast): Invalid state detected before execution
/// - Rule 5 (No Silent Failures): All issues reported with remediation paths
/// - Rule 19 (Structured Logging): Validation results logged with structured parameters
/// </remarks>
public sealed class DTpf001A
{
    private readonly ILogger? _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Check IDs for consistent referencing
    private const string CheckIdPriceData = "PRICE_DATA";
    private const string CheckIdEarningsData = "EARNINGS_DATA";
    private const string CheckIdOptionsExists = "OPTIONS_EXISTS";
    private const string CheckIdOptionsValid = "OPTIONS_VALID";
    private const string CheckIdSystemFiles = "SYSTEM_FILES";
    private const string CheckIdRatesData = "RATES_DATA";

    public DTpf001A(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Performs comprehensive pre-flight validation for a backtest session.
    /// </summary>
    /// <param name="requirements">Session data requirements.</param>
    /// <param name="sessionDataPath">Path to session data directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed validation result with remediation actions.</returns>
    public async Task<STDT011A> ValidateAsync(
        STDT010A requirements,
        string sessionDataPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDataPath);

        _logger?.LogInformation(
            "Starting pre-flight validation for session: {Summary}",
            requirements.GetSummary());

        List<PreflightCheck> checks = new();
        List<RemediationAction> actions = new();

        // Phase 1: System files
        PreflightCheck systemCheck = ValidateSystemFiles(sessionDataPath);
        checks.Add(systemCheck);
        if (systemCheck.Status == CheckStatus.Failed)
        {
            actions.Add(CreateSystemFilesRemediation());
        }

        // Phase 2: Price data
        (PreflightCheck priceCheck, List<string> missingPriceSymbols) =
            ValidatePriceData(requirements, sessionDataPath);
        checks.Add(priceCheck);
        if (missingPriceSymbols.Count > 0)
        {
            actions.Add(CreatePriceDataRemediation(missingPriceSymbols));
        }

        // Phase 3: Earnings data
        (PreflightCheck earningsCheck, List<DateTime> missingEarningsDates) =
            ValidateEarningsData(requirements, sessionDataPath);
        checks.Add(earningsCheck);
        if (missingEarningsDates.Count > 0)
        {
            actions.Add(CreateEarningsRemediation(missingEarningsDates));
        }

        // Phase 4: Options data (existence and quality)
        (List<PreflightCheck> optionsChecks,
         List<OptionsDataQuality> optionsQuality,
         List<(string symbol, DateTime date)> missingOptions,
         List<(string symbol, DateTime date)> invalidOptions) =
            await ValidateOptionsDataAsync(requirements, sessionDataPath, cancellationToken);

        checks.AddRange(optionsChecks);
        if (missingOptions.Count > 0)
        {
            actions.Add(CreateMissingOptionsRemediation(missingOptions));
        }
        if (invalidOptions.Count > 0)
        {
            actions.Add(CreateInvalidOptionsRemediation(invalidOptions));
        }

        // Phase 5: Interest rates (optional, warning only)
        PreflightCheck ratesCheck = ValidateRatesData(sessionDataPath);
        checks.Add(ratesCheck);
        if (ratesCheck.Status == CheckStatus.Warning)
        {
            actions.Add(CreateRatesRemediation());
        }

        // Build coverage stats
        DataCoverageStats coverage = BuildCoverageStats(
            requirements, sessionDataPath, optionsQuality, missingOptions, invalidOptions);

        // Sort actions by priority
        actions.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        // Determine overall result
        bool hasCritical = checks.Any(c => c.Status == CheckStatus.Failed);

        STDT011A result = hasCritical || actions.Count > 0
            ? STDT011A.Failure(checks, actions, coverage)
            : STDT011A.Success(checks, coverage);

        _logger?.LogInformation(
            "Pre-flight validation complete: Status={Status}, Checks={CheckCount}, Actions={ActionCount}, " +
            "PriceCoverage={PriceCov:F1}%, EarningsCoverage={EarningsCov:F1}%, OptionsCoverage={OptCov:F1}%",
            result.Status,
            checks.Count,
            actions.Count,
            coverage.PriceCoveragePercent,
            coverage.EarningsCoveragePercent,
            coverage.OptionsValidCoveragePercent);

        return result;
    }

    /// <summary>
    /// Validates system files (market-hours, symbol-properties, etc.).
    /// </summary>
    private PreflightCheck ValidateSystemFiles(string sessionDataPath)
    {
        string[] requiredFiles =
        {
            "market-hours/market-hours-database.json",
            "symbol-properties/symbol-properties-database.csv"
        };

        List<string> missing = new();
        foreach (string file in requiredFiles)
        {
            string path = Path.Combine(sessionDataPath, file);
            if (!File.Exists(path))
            {
                missing.Add(file);
            }
        }

        if (missing.Count > 0)
        {
            return PreflightCheck.Fail(
                CheckIdSystemFiles,
                "System Files",
                CheckCategory.System,
                $"Missing {missing.Count} required system file(s): {string.Join(", ", missing)}");
        }

        return PreflightCheck.Pass(
            CheckIdSystemFiles,
            "System Files",
            CheckCategory.System,
            "All required system files present");
    }

    /// <summary>
    /// Validates price data coverage for all symbols.
    /// </summary>
    private (PreflightCheck check, List<string> missingSymbols) ValidatePriceData(
        STDT010A requirements,
        string sessionDataPath)
    {
        string dailyPath = Path.Combine(sessionDataPath, "equity", "usa", "daily");
        List<string> missing = new();

        foreach (string symbol in requirements.AllSymbols)
        {
            string zipPath = Path.Combine(dailyPath, $"{symbol.ToLowerInvariant()}.zip");
            if (!File.Exists(zipPath))
            {
                missing.Add(symbol);
            }
        }

        if (missing.Count == requirements.AllSymbols.Count)
        {
            return (
                PreflightCheck.Fail(
                    CheckIdPriceData,
                    "Price Data",
                    CheckCategory.PriceData,
                    $"No price data found for any symbol. Missing: {string.Join(", ", missing)}",
                    missing),
                missing);
        }

        if (missing.Count > 0)
        {
            return (
                PreflightCheck.Fail(
                    CheckIdPriceData,
                    "Price Data",
                    CheckCategory.PriceData,
                    $"Missing price data for {missing.Count}/{requirements.AllSymbols.Count} symbols: {string.Join(", ", missing)}",
                    missing),
                missing);
        }

        return (
            PreflightCheck.Pass(
                CheckIdPriceData,
                "Price Data",
                CheckCategory.PriceData,
                $"Price data available for all {requirements.AllSymbols.Count} symbols"),
            missing);
    }

    /// <summary>
    /// Validates earnings data coverage.
    /// </summary>
    private (PreflightCheck check, List<DateTime> missingDates) ValidateEarningsData(
        STDT010A requirements,
        string sessionDataPath)
    {
        string earningsPath = Path.Combine(sessionDataPath, "earnings", "nasdaq");
        List<DateTime> missing = new();

        // Check for earnings files in the required date range
        DateTime current = requirements.PriceDataStart;
        int totalDates = 0;
        int foundDates = 0;

        while (current <= requirements.EarningsLookaheadEnd)
        {
            // Skip weekends
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                totalDates++;
                string filePath = Path.Combine(earningsPath, $"{current:yyyy-MM-dd}.json");
                if (File.Exists(filePath))
                {
                    foundDates++;
                }
                else
                {
                    missing.Add(current);
                }
            }
            current = current.AddDays(1);
        }

        double coverage = totalDates > 0 ? (double)foundDates / totalDates * 100 : 0;

        if (foundDates == 0)
        {
            return (
                PreflightCheck.Fail(
                    CheckIdEarningsData,
                    "Earnings Data",
                    CheckCategory.EarningsData,
                    $"No earnings data found. Expected {totalDates} dates.",
                    affectedDates: missing.Take(10).ToList()),
                missing);
        }

        if (coverage < 80)
        {
            return (
                PreflightCheck.Warn(
                    CheckIdEarningsData,
                    "Earnings Data",
                    CheckCategory.EarningsData,
                    $"Low earnings coverage: {foundDates}/{totalDates} ({coverage:F1}%). Some signal windows may be missed.",
                    affectedDates: missing.Take(10).ToList()),
                missing);
        }

        return (
            PreflightCheck.Pass(
                CheckIdEarningsData,
                "Earnings Data",
                CheckCategory.EarningsData,
                $"Earnings data coverage: {foundDates}/{totalDates} ({coverage:F1}%)"),
            new List<DateTime>());
    }

    /// <summary>
    /// Validates options data existence and quality.
    /// </summary>
    private async Task<(
        List<PreflightCheck> checks,
        List<OptionsDataQuality> quality,
        List<(string symbol, DateTime date)> missing,
        List<(string symbol, DateTime date)> invalid)> ValidateOptionsDataAsync(
        STDT010A requirements,
        string sessionDataPath,
        CancellationToken cancellationToken)
    {
        List<PreflightCheck> checks = new();
        List<OptionsDataQuality> quality = new();
        List<(string symbol, DateTime date)> missing = new();
        List<(string symbol, DateTime date)> invalid = new();

        string optionsPath = Path.Combine(sessionDataPath, "options");

        // Get options-required dates from earnings calendar
        IReadOnlyList<DateTime> optionsDates = requirements.OptionsRequiredDates.Count > 0
            ? requirements.OptionsRequiredDates
            : ComputeOptionsRequiredDatesFromEarnings(requirements, sessionDataPath);

        if (optionsDates.Count == 0)
        {
            checks.Add(PreflightCheck.Warn(
                CheckIdOptionsExists,
                "Options Data Availability",
                CheckCategory.OptionsData,
                "No options-required dates computed. Earnings calendar may be incomplete."));
            return (checks, quality, missing, invalid);
        }

        // Check each symbol-date combination
        int totalCombinations = requirements.Symbols.Count * optionsDates.Count;
        int existsCount = 0;
        int validCount = 0;

        foreach (string symbol in requirements.Symbols)
        {
            foreach (DateTime date in optionsDates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                OptionsDataQuality q = await AssessOptionsQualityAsync(
                    symbol, date, optionsPath, cancellationToken);
                quality.Add(q);

                if (!q.CacheExists)
                {
                    missing.Add((symbol, date));
                }
                else
                {
                    existsCount++;
                    if (q.IsValidForTermStructure)
                    {
                        validCount++;
                    }
                    else
                    {
                        invalid.Add((symbol, date));
                    }
                }
            }
        }

        // Generate existence check
        if (existsCount == 0 && totalCombinations > 0)
        {
            checks.Add(PreflightCheck.Fail(
                CheckIdOptionsExists,
                "Options Data Availability",
                CheckCategory.OptionsData,
                $"No options cache files found. Missing {totalCombinations} symbol-date combinations.",
                requirements.Symbols.ToList(),
                optionsDates.ToList()));
        }
        else if (missing.Count > 0)
        {
            checks.Add(PreflightCheck.Warn(
                CheckIdOptionsExists,
                "Options Data Availability",
                CheckCategory.OptionsData,
                $"Options cache missing for {missing.Count}/{totalCombinations} symbol-date combinations.",
                missing.Select(m => m.symbol).Distinct().ToList(),
                missing.Select(m => m.date).Distinct().ToList()));
        }
        else
        {
            checks.Add(PreflightCheck.Pass(
                CheckIdOptionsExists,
                "Options Data Availability",
                CheckCategory.OptionsData,
                $"Options cache files present for all {totalCombinations} symbol-date combinations."));
        }

        // Generate validity check
        if (existsCount > 0)
        {
            double validPercent = (double)validCount / existsCount * 100;

            if (validCount == 0)
            {
                checks.Add(PreflightCheck.Fail(
                    CheckIdOptionsValid,
                    "Options Data Quality",
                    CheckCategory.OptionsData,
                    $"No valid options data for term structure analysis. All {existsCount} cached files are invalid " +
                    "(missing IVs, insufficient expirations, or missing put-call coverage).",
                    invalid.Select(i => i.symbol).Distinct().ToList(),
                    invalid.Select(i => i.date).Distinct().ToList()));
            }
            else if (validPercent < 50)
            {
                checks.Add(PreflightCheck.Warn(
                    CheckIdOptionsValid,
                    "Options Data Quality",
                    CheckCategory.OptionsData,
                    $"Low options quality: {validCount}/{existsCount} ({validPercent:F1}%) valid for term structure. " +
                    $"Issues: {invalid.Count} invalid caches.",
                    invalid.Select(i => i.symbol).Distinct().ToList(),
                    invalid.Select(i => i.date).Distinct().ToList()));
            }
            else
            {
                checks.Add(PreflightCheck.Pass(
                    CheckIdOptionsValid,
                    "Options Data Quality",
                    CheckCategory.OptionsData,
                    $"Options quality: {validCount}/{existsCount} ({validPercent:F1}%) valid for term structure analysis."));
            }
        }

        return (checks, quality, missing, invalid);
    }

    /// <summary>
    /// Assesses options data quality for a specific symbol-date.
    /// </summary>
    private async Task<OptionsDataQuality> AssessOptionsQualityAsync(
        string symbol,
        DateTime date,
        string optionsPath,
        CancellationToken cancellationToken)
    {
        string symbolLower = symbol.ToLowerInvariant();
        string dateSuffix = date.ToString("yyyyMMdd");

        // Try date-specific file first
        string jsonPath = Path.Combine(optionsPath, $"{symbolLower}_{dateSuffix}.json");
        string sbePath = Path.Combine(optionsPath, $"{symbolLower}_{dateSuffix}.sbe");

        string? filePath = File.Exists(sbePath) ? sbePath : (File.Exists(jsonPath) ? jsonPath : null);

        if (filePath == null)
        {
            return OptionsDataQuality.Missing(symbol, date);
        }

        try
        {
            // For SBE files, we'd need the deserializer - for now, handle JSON
            if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                string json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return AssessOptionsQualityFromJson(symbol, date, json);
            }

            // SBE files - assume valid if exists (would need proper deserialization)
            return new OptionsDataQuality
            {
                Symbol = symbol,
                Date = date,
                CacheExists = true,
                // Mark as needing validation - conservative approach
                FailureReason = "SBE file validation not implemented in pre-flight"
            };
        }
        catch (Exception ex)
        {
            return OptionsDataQuality.Invalid(symbol, date, $"Failed to read: {ex.Message}");
        }
    }

    /// <summary>
    /// Assesses options quality from JSON content.
    /// </summary>
    private OptionsDataQuality AssessOptionsQualityFromJson(string symbol, DateTime date, string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("Contracts", out JsonElement contracts) ||
                contracts.ValueKind != JsonValueKind.Array)
            {
                return OptionsDataQuality.Invalid(symbol, date, "No Contracts array in cache");
            }

            int totalContracts = 0;
            int contractsWithValidIV = 0;
            int callsWithValidIV = 0;
            int putsWithValidIV = 0;
            bool hasCalls = false;
            bool hasPuts = false;
            HashSet<DateTime> futureExpirations = new();

            foreach (JsonElement contract in contracts.EnumerateArray())
            {
                totalContracts++;

                // Check expiration
                if (contract.TryGetProperty("Expiration", out JsonElement expEl) &&
                    DateTime.TryParse(expEl.GetString(), out DateTime expDate))
                {
                    if (expDate.Date > date.Date)
                    {
                        futureExpirations.Add(expDate.Date);
                    }
                }

                // Check right (0 = Call, 1 = Put)
                bool isCall = false;
                bool isPut = false;
                if (contract.TryGetProperty("Right", out JsonElement rightEl))
                {
                    int right = rightEl.GetInt32();
                    isCall = right == 0;
                    isPut = right == 1;
                    if (isCall) hasCalls = true;
                    if (isPut) hasPuts = true;
                }

                // Check IV validity (0 < IV <= 5.0 i.e. 500%)
                if (contract.TryGetProperty("ImpliedVolatility", out JsonElement ivEl) &&
                    ivEl.ValueKind == JsonValueKind.Number)
                {
                    decimal iv = ivEl.GetDecimal();
                    if (iv > 0m && iv <= 5.0m)
                    {
                        contractsWithValidIV++;
                        if (isCall) callsWithValidIV++;
                        if (isPut) putsWithValidIV++;
                    }
                }
            }

            OptionsDataQuality result = new()
            {
                Symbol = symbol,
                Date = date,
                CacheExists = true,
                TotalContracts = totalContracts,
                ContractsWithValidIV = contractsWithValidIV,
                FutureExpirations = futureExpirations.Count,
                HasCalls = hasCalls,
                HasPuts = hasPuts,
                CallsWithValidIV = callsWithValidIV,
                PutsWithValidIV = putsWithValidIV
            };

            // Determine failure reason if invalid
            if (!result.IsValidForTermStructure)
            {
                List<string> reasons = new();
                if (futureExpirations.Count < 2)
                    reasons.Add($"only {futureExpirations.Count} future expirations (need ≥2)");
                if (contractsWithValidIV == 0)
                    reasons.Add("no valid IVs");
                if (!hasCalls || !hasPuts)
                    reasons.Add($"missing {(!hasCalls ? "calls" : "puts")}");
                if (callsWithValidIV == 0 || putsWithValidIV == 0)
                    reasons.Add("no put-call IV coverage");

                return result with { FailureReason = string.Join("; ", reasons) };
            }

            return result;
        }
        catch (JsonException ex)
        {
            return OptionsDataQuality.Invalid(symbol, date, $"Invalid JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates interest rate data.
    /// </summary>
    private PreflightCheck ValidateRatesData(string sessionDataPath)
    {
        string ratesPath = Path.Combine(sessionDataPath, "alternative", "interest-rate", "usa", "interest-rate.csv");

        if (!File.Exists(ratesPath))
        {
            return PreflightCheck.Warn(
                CheckIdRatesData,
                "Interest Rate Data",
                CheckCategory.RatesData,
                "Interest rate data not found. Will use default risk-free rate.");
        }

        return PreflightCheck.Pass(
            CheckIdRatesData,
            "Interest Rate Data",
            CheckCategory.RatesData,
            "Interest rate data available");
    }

    /// <summary>
    /// Computes options-required dates from earnings calendar.
    /// </summary>
    private IReadOnlyList<DateTime> ComputeOptionsRequiredDatesFromEarnings(
        STDT010A requirements,
        string sessionDataPath)
    {
        string earningsPath = Path.Combine(sessionDataPath, "earnings", "nasdaq");
        HashSet<DateTime> optionsDates = new();

        if (!Directory.Exists(earningsPath))
        {
            return Array.Empty<DateTime>();
        }

        // Scan earnings files for each symbol
        foreach (string symbol in requirements.Symbols)
        {
            string symbolUpper = symbol.ToUpperInvariant();

            foreach (string file in Directory.GetFiles(earningsPath, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    using JsonDocument doc = JsonDocument.Parse(json);

                    foreach (JsonElement item in doc.RootElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("symbol", out JsonElement symEl) &&
                            symEl.GetString()?.ToUpperInvariant() == symbolUpper &&
                            item.TryGetProperty("date", out JsonElement dateEl) &&
                            DateTime.TryParse(dateEl.GetString(), out DateTime earningsDate))
                        {
                            // Options needed 5-7 days before earnings
                            for (int i = requirements.SignalWindowMinDays; i <= requirements.SignalWindowMaxDays; i++)
                            {
                                DateTime signalDate = earningsDate.AddDays(-i);
                                if (signalDate >= requirements.StartDate && signalDate <= requirements.EndDate)
                                {
                                    optionsDates.Add(signalDate);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Skip invalid files
                }
            }
        }

        return optionsDates.OrderBy(d => d).ToList();
    }

    /// <summary>
    /// Builds data coverage statistics.
    /// </summary>
    private DataCoverageStats BuildCoverageStats(
        STDT010A requirements,
        string sessionDataPath,
        List<OptionsDataQuality> optionsQuality,
        List<(string symbol, DateTime date)> missingOptions,
        List<(string symbol, DateTime date)> invalidOptions)
    {
        // Price coverage
        string dailyPath = Path.Combine(sessionDataPath, "equity", "usa", "daily");
        int symbolsWithPrice = 0;
        foreach (string symbol in requirements.AllSymbols)
        {
            if (File.Exists(Path.Combine(dailyPath, $"{symbol.ToLowerInvariant()}.zip")))
            {
                symbolsWithPrice++;
            }
        }

        // Earnings coverage
        string earningsPath = Path.Combine(sessionDataPath, "earnings", "nasdaq");
        int earningsTotal = 0;
        int earningsFound = 0;
        DateTime current = requirements.PriceDataStart;
        while (current <= requirements.EarningsLookaheadEnd)
        {
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                earningsTotal++;
                if (File.Exists(Path.Combine(earningsPath, $"{current:yyyy-MM-dd}.json")))
                {
                    earningsFound++;
                }
            }
            current = current.AddDays(1);
        }

        // Options coverage
        int totalOptionsDates = optionsQuality.Select(q => q.Date).Distinct().Count();
        int optionsDatesWithAny = optionsQuality.Where(q => q.CacheExists).Select(q => q.Date).Distinct().Count();
        int optionsDatesWithValid = optionsQuality.Where(q => q.IsValidForTermStructure).Select(q => q.Date).Distinct().Count();

        int totalSymbolDates = optionsQuality.Count;
        int validSymbolDates = optionsQuality.Count(q => q.IsValidForTermStructure);

        return new DataCoverageStats
        {
            TotalSymbols = requirements.AllSymbols.Count,
            SymbolsWithPriceData = symbolsWithPrice,
            TotalEarningsDates = earningsTotal,
            EarningsDatesWithData = earningsFound,
            TotalOptionsDates = totalOptionsDates,
            OptionsDatesWithValidData = optionsDatesWithValid,
            OptionsDatesWithAnyData = optionsDatesWithAny,
            TotalOptionsSymbolDates = totalSymbolDates,
            ValidOptionsSymbolDates = validSymbolDates
        };
    }

    // Remediation action factories

    private static RemediationAction CreateSystemFilesRemediation() => new()
    {
        ActionId = "REMEDIATE_SYSTEM",
        Description = "Copy required system files (market-hours, symbol-properties)",
        Type = RemediationType.CopySystemFiles,
        CanAutomate = true,
        EstimatedSeconds = 5,
        Priority = 1,
        AddressesCheckIds = new[] { CheckIdSystemFiles }
    };

    private static RemediationAction CreatePriceDataRemediation(List<string> symbols) => new()
    {
        ActionId = "REMEDIATE_PRICES",
        Description = $"Download price data for {symbols.Count} symbol(s)",
        Type = RemediationType.DownloadPriceData,
        CanAutomate = true,
        EstimatedSeconds = symbols.Count * 10, // ~10s per symbol
        Priority = 2,
        Symbols = symbols,
        AddressesCheckIds = new[] { CheckIdPriceData }
    };

    private static RemediationAction CreateEarningsRemediation(List<DateTime> dates) => new()
    {
        ActionId = "REMEDIATE_EARNINGS",
        Description = $"Download earnings calendar for {dates.Count} date(s)",
        Type = RemediationType.DownloadEarningsData,
        CanAutomate = true,
        EstimatedSeconds = Math.Min(dates.Count / 10, 60), // Batch download
        Priority = 3,
        Dates = dates,
        AddressesCheckIds = new[] { CheckIdEarningsData }
    };

    private static RemediationAction CreateMissingOptionsRemediation(List<(string symbol, DateTime date)> items) => new()
    {
        ActionId = "REMEDIATE_OPTIONS_MISSING",
        Description = $"Download options data for {items.Count} missing symbol-date combination(s)",
        Type = RemediationType.DownloadOptionsData,
        CanAutomate = true,
        EstimatedSeconds = items.Count * 5, // ~5s per symbol-date
        Priority = 4,
        Symbols = items.Select(i => i.symbol).Distinct().ToList(),
        Dates = items.Select(i => i.date).Distinct().ToList(),
        AddressesCheckIds = new[] { CheckIdOptionsExists }
    };

    private static RemediationAction CreateInvalidOptionsRemediation(List<(string symbol, DateTime date)> items) => new()
    {
        ActionId = "REMEDIATE_OPTIONS_INVALID",
        Description = $"Re-download {items.Count} invalid options cache(s) (missing IVs, expirations, or put-call coverage)",
        Type = RemediationType.RedownloadOptionsData,
        CanAutomate = true,
        EstimatedSeconds = items.Count * 5,
        Priority = 5,
        Symbols = items.Select(i => i.symbol).Distinct().ToList(),
        Dates = items.Select(i => i.date).Distinct().ToList(),
        AddressesCheckIds = new[] { CheckIdOptionsValid }
    };

    private static RemediationAction CreateRatesRemediation() => new()
    {
        ActionId = "REMEDIATE_RATES",
        Description = "Download interest rate data",
        Type = RemediationType.DownloadRatesData,
        CanAutomate = true,
        EstimatedSeconds = 10,
        Priority = 6,
        AddressesCheckIds = new[] { CheckIdRatesData }
    };
}
