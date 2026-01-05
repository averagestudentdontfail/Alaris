// APsv003A.cs - Data verification service for backtesting

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Alaris.Core.Model;
using Microsoft.Extensions.Logging;

namespace Alaris.Host.Application.Service;

/// <summary>
/// Verifies all required data exists before backtest execution.
/// Implements fail-fast philosophy with actionable diagnostics.
/// Component ID: APsv003A
/// </summary>
public sealed class APsv003A
{
    private readonly ILogger<APsv003A>? _logger;
    
    public APsv003A(ILogger<APsv003A>? logger = null)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Verifies all data requirements are met for a session.
    /// </summary>
    /// <param name="requirements">Session data requirements.</param>
    /// <param name="sessionDataPath">Path to session data directory.</param>
    /// <returns>Detailed verification report.</returns>
    public DataVerificationReport Verify(STDT010A requirements, string sessionDataPath)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDataPath);
        
        DataVerificationReport report = new DataVerificationReport
        {
            SessionDataPath = sessionDataPath,
            VerifiedAt = DateTime.UtcNow
        };
        
        _logger?.LogInformation("Verifying session data at {Path}", sessionDataPath);
        
        // Check 1: Price data for all symbols
        VerifyPriceData(requirements, sessionDataPath, report);
        
        // Check 2: Map and factor files
        VerifyMapAndFactorFiles(requirements, sessionDataPath, report);
        
        // Check 3: Earnings calendar coverage
        VerifyEarningsCalendar(requirements, sessionDataPath, report);
        
        // Check 4: Options data for signal generation dates
        VerifyOptionsData(requirements, sessionDataPath, report);
        
        // Check 5: Benchmark data
        VerifyBenchmarkData(requirements, sessionDataPath, report);
        
        // Compute completeness
        report.IsComplete = 
            report.MissingPriceData.Count == 0 &&
            report.MissingMapFiles.Count == 0 &&
            report.MissingFactorFiles.Count == 0 &&
            report.MissingEarningsDates.Count == 0 &&
            report.MissingOptionsData.Count == 0 &&
            report.BenchmarkAvailable;
        
        _logger?.LogInformation(
            "Verification complete: {Status} (Price: {Price}, Maps: {Maps}, Earnings: {Earnings}, Options: {Options})",
            report.IsComplete ? "PASS" : "FAIL",
            report.MissingPriceData.Count == 0 ? "OK" : $"{report.MissingPriceData.Count} missing",
            report.MissingMapFiles.Count == 0 ? "OK" : $"{report.MissingMapFiles.Count} missing",
            report.MissingEarningsDates.Count == 0 ? "OK" : $"{report.MissingEarningsDates.Count} missing",
            report.MissingOptionsData.Count == 0 ? "OK" : $"{report.MissingOptionsData.Count} missing");
        
        return report;
    }
    
    private void VerifyPriceData(STDT010A requirements, string sessionDataPath, DataVerificationReport report)
    {
        string dailyPath = Path.Combine(sessionDataPath, "equity", "usa", "daily");
        
        foreach (string symbol in requirements.AllSymbols)
        {
            string zipPath = Path.Combine(dailyPath, $"{symbol.ToLowerInvariant()}.zip");
            if (!File.Exists(zipPath))
            {
                report.MissingPriceData.Add(symbol);
                _logger?.LogWarning("Missing price data: {Symbol}", symbol);
            }
        }
    }
    
    private void VerifyMapAndFactorFiles(STDT010A requirements, string sessionDataPath, DataVerificationReport report)
    {
        string mapFilesPath = Path.Combine(sessionDataPath, "equity", "usa", "map_files");
        string factorFilesPath = Path.Combine(sessionDataPath, "equity", "usa", "factor_files");
        
        foreach (string symbol in requirements.AllSymbols)
        {
            string ticker = symbol.ToLowerInvariant();
            
            string mapPath = Path.Combine(mapFilesPath, $"{ticker}.csv");
            if (!File.Exists(mapPath))
            {
                report.MissingMapFiles.Add(symbol);
                _logger?.LogWarning("Missing map file: {Symbol}", symbol);
            }
            
            string factorPath = Path.Combine(factorFilesPath, $"{ticker}.csv");
            if (!File.Exists(factorPath))
            {
                report.MissingFactorFiles.Add(symbol);
                _logger?.LogWarning("Missing factor file: {Symbol}", symbol);
            }
        }
    }
    
    private void VerifyEarningsCalendar(STDT010A requirements, string sessionDataPath, DataVerificationReport report)
    {
        string nasdaqPath = Path.Combine(sessionDataPath, "earnings", "nasdaq");
        
        for (DateTime date = requirements.StartDate; date <= requirements.EarningsLookaheadEnd; date = date.AddDays(1))
        {
            // Skip weekends
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                continue;
            
            string cachePath = Path.Combine(nasdaqPath, $"{date:yyyy-MM-dd}.json");
            if (!File.Exists(cachePath))
            {
                report.MissingEarningsDates.Add(date);
            }
        }
        
        if (report.MissingEarningsDates.Count > 0)
        {
            DateTime firstMissing = report.MissingEarningsDates[0];
            DateTime lastMissing = report.MissingEarningsDates[0];
            foreach (DateTime missing in report.MissingEarningsDates)
            {
                if (missing < firstMissing)
                {
                    firstMissing = missing;
                }

                if (missing > lastMissing)
                {
                    lastMissing = missing;
                }
            }

            _logger?.LogWarning(
                "Missing {Count} earnings dates (first: {First}, last: {Last})",
                report.MissingEarningsDates.Count,
                firstMissing,
                lastMissing);
        }
    }
    
    private void VerifyOptionsData(STDT010A requirements, string sessionDataPath, DataVerificationReport report)
    {
        string optionsPath = Path.Combine(sessionDataPath, "options");
        
        foreach (DateTime date in requirements.OptionsRequiredDates)
        {
            foreach (string symbol in requirements.Symbols) // Benchmark doesn't need options
            {
                string ticker = symbol.ToLowerInvariant();
                string optionFile = Path.Combine(optionsPath, $"{ticker}_{date:yyyyMMdd}.json");
                
                if (!File.Exists(optionFile))
                {
                    report.MissingOptionsData.Add((symbol, date));
                }
            }
        }
        
        if (report.MissingOptionsData.Count > 0)
        {
            _logger?.LogWarning(
                "Missing {Count} options data entries",
                report.MissingOptionsData.Count);
        }
    }
    
    private void VerifyBenchmarkData(STDT010A requirements, string sessionDataPath, DataVerificationReport report)
    {
        string benchmarkPath = Path.Combine(
            sessionDataPath, "equity", "usa", "daily", 
            $"{requirements.BenchmarkSymbol.ToLowerInvariant()}.zip");
        
        report.BenchmarkAvailable = File.Exists(benchmarkPath);
        
        if (!report.BenchmarkAvailable)
        {
            _logger?.LogWarning("Missing benchmark data: {Symbol}", requirements.BenchmarkSymbol);
        }
    }
}

/// <summary>
/// Report of data verification results.
/// </summary>
public sealed class DataVerificationReport
{
    public string SessionDataPath { get; init; } = string.Empty;
    public DateTime VerifiedAt { get; init; }
    public bool IsComplete { get; set; }
    
    public Collection<string> MissingPriceData { get; } = new();
    public Collection<string> MissingMapFiles { get; } = new();
    public Collection<string> MissingFactorFiles { get; } = new();
    public Collection<DateTime> MissingEarningsDates { get; } = new();
    public Collection<(string Symbol, DateTime Date)> MissingOptionsData { get; } = new();
    public bool BenchmarkAvailable { get; set; }
    
    /// <summary>
    /// Gets a summary of issues for display.
    /// </summary>
    public string GetSummary()
    {
        if (IsComplete)
            return "All required data is available.";
        
        List<string> issues = new List<string>();
        
        if (MissingPriceData.Count > 0)
            issues.Add($"Missing price data for: {string.Join(", ", MissingPriceData)}");
        
        if (MissingMapFiles.Count > 0)
            issues.Add($"Missing map files for: {string.Join(", ", MissingMapFiles)}");
        
        if (MissingFactorFiles.Count > 0)
            issues.Add($"Missing factor files for: {string.Join(", ", MissingFactorFiles)}");
        
        if (MissingEarningsDates.Count > 0)
        {
            DateTime firstMissing = MissingEarningsDates[0];
            DateTime lastMissing = MissingEarningsDates[0];
            foreach (DateTime missing in MissingEarningsDates)
            {
                if (missing < firstMissing)
                {
                    firstMissing = missing;
                }

                if (missing > lastMissing)
                {
                    lastMissing = missing;
                }
            }

            issues.Add($"Missing {MissingEarningsDates.Count} earnings dates " +
                $"({firstMissing:yyyy-MM-dd} to {lastMissing:yyyy-MM-dd})");
        }
        
        if (MissingOptionsData.Count > 0)
        {
            HashSet<DateTime> uniqueDates = new HashSet<DateTime>();
            HashSet<string> uniqueSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ((string Symbol, DateTime Date) entry in MissingOptionsData)
            {
                uniqueDates.Add(entry.Date);
                uniqueSymbols.Add(entry.Symbol);
            }

            issues.Add($"Missing options data: {uniqueSymbols.Count} symbols × {uniqueDates.Count} dates = {MissingOptionsData.Count} entries");
        }
        
        if (!BenchmarkAvailable)
            issues.Add("Missing benchmark (SPY) data");
        
        return string.Join("\n", issues);
    }
}
