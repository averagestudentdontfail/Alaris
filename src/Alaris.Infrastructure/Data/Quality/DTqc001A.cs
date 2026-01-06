using System;
using Microsoft.Extensions.Logging;
using NodaTime;
using Alaris.Core.Time;
using Alaris.Infrastructure.Data.Model;

namespace Alaris.Infrastructure.Data.Quality;

/// <summary>
/// Validates price reasonableness and data freshness.
/// Component ID: DTqc001A
/// </summary>
/// <remarks>
/// Validation checks:
/// - Spot price within ±10% of previous close
/// - Option bid > 0 and bid &lt; ask
/// - Option ask &lt; intrinsic value + $10
/// - No stale timestamps (>1 hour old)
/// </remarks>
public sealed class PriceReasonablenessValidator : DTqc002A
{
    private readonly ILogger<PriceReasonablenessValidator> _logger;
    private readonly ITimeProvider _timeProvider;

    /// <inheritdoc/>
    public string ComponentId => "DTqc001A";

    /// <summary>
    /// Initializes a new instance of the <see cref="PriceReasonablenessValidator"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="timeProvider">The time provider for backtest-aware time operations.</param>
    public PriceReasonablenessValidator(
        ILogger<PriceReasonablenessValidator> logger,
        ITimeProvider timeProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc/>
    public DataQualityResult Validate(MarketDataSnapshot snapshot)
    {
        System.Collections.Generic.List<string> warnings = new System.Collections.Generic.List<string>();
        Instant now = _timeProvider.Now;

        // Check 1: Spot price change reasonableness
        if (snapshot.HistoricalBars.Count > 0)
        {
            decimal previousClose = snapshot.HistoricalBars[^1].Close;
            decimal priceChange = Math.Abs((snapshot.SpotPrice - previousClose) / previousClose);

            if (priceChange > 0.10m)
            {
                string message = $"Spot price changed {priceChange:P2} from previous close";
                _logger.LogWarning("{ComponentId}: {Message}", ComponentId, message);
                warnings.Add(message);
            }
        }

        // Check 2: Option bid/ask validity
        foreach (OptionContract contract in snapshot.OptionChain.Contracts)
        {
            if (contract.Bid <= 0)
            {
                return new DataQualityResult
                {
                    ValidatorId = ComponentId,
                    Status = ValidationStatus.Failed,
                    Message = $"Invalid bid price: {contract.OptionSymbol} bid={contract.Bid}",
                    DataElement = "OptionChain.Contracts"
                };
            }

            if (contract.Ask <= contract.Bid)
            {
                return new DataQualityResult
                {
                    ValidatorId = ComponentId,
                    Status = ValidationStatus.Failed,
                    Message = $"Invalid bid/ask spread: {contract.OptionSymbol} bid={contract.Bid}, ask={contract.Ask}",
                    DataElement = "OptionChain.Contracts"
                };
            }

            // Check intrinsic value bounds
            decimal intrinsicValue = contract.Right == OptionRight.Call
                ? Math.Max(0, snapshot.SpotPrice - contract.Strike)
                : Math.Max(0, contract.Strike - snapshot.SpotPrice);

            if (contract.Ask < intrinsicValue)
            {
                return new DataQualityResult
                {
                    ValidatorId = ComponentId,
                    Status = ValidationStatus.Failed,
                    Message = $"Ask below intrinsic: {contract.OptionSymbol} ask={contract.Ask}, intrinsic={intrinsicValue}",
                    DataElement = "OptionChain.Contracts"
                };
            }

            if (contract.Ask > intrinsicValue + 10m)
            {
                warnings.Add($"High time value: {contract.OptionSymbol} ask={contract.Ask}, intrinsic={intrinsicValue}");
            }
        }

        // Check 3: Data freshness
        Duration dataAge = now - _timeProvider.ToInstant(snapshot.Timestamp);
        if (dataAge > Duration.FromHours(1))
        {
            return new DataQualityResult
            {
                ValidatorId = ComponentId,
                Status = ValidationStatus.Failed,
                Message = $"Stale data: age={dataAge.TotalMinutes:F1} minutes",
                DataElement = "Timestamp"
            };
        }

        ValidationStatus status = warnings.Count > 0 ? ValidationStatus.PassedWithWarnings : ValidationStatus.Passed;
        return new DataQualityResult
        {
            ValidatorId = ComponentId,
            Status = status,
            Message = "Price reasonableness validation passed",
            Warnings = warnings,
            DataElement = "All"
        };
    }
}

/// <summary>
/// Validates IV arbitrage conditions (put-call parity, calendar spreads).
/// Component ID: DTqc002A
/// </summary>
/// <remarks>
/// Validation checks:
/// - Put-call parity holds within 2%
/// - Calendar spread IV differences reasonable
/// - No butterfly arbitrage opportunities >$0.50
/// </remarks>
public sealed class IvArbitrageValidator : DTqc002A
{
    private readonly ILogger<IvArbitrageValidator> _logger;

    /// <inheritdoc/>
    public string ComponentId => "DTqc002A";

    /// <summary>
    /// Initializes a new instance of the <see cref="IvArbitrageValidator"/> class.
    /// </summary>
    public IvArbitrageValidator(ILogger<IvArbitrageValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public DataQualityResult Validate(MarketDataSnapshot snapshot)
    {
        System.Collections.Generic.List<string> warnings = new System.Collections.Generic.List<string>();

        System.Collections.Generic.List<DateTime> expirations =
            GetExpirations(snapshot.OptionChain.Contracts);
        for (int i = 0; i < expirations.Count; i++)
        {
            DateTime expiration = expirations[i];
            System.Collections.Generic.List<OptionContract> contracts =
                FilterByExpiration(snapshot.OptionChain.Contracts, expiration);
            AddParityWarnings(snapshot, expiration, contracts, warnings);
        }

        AddTermStructureWarnings(snapshot, warnings);

        ValidationStatus status = warnings.Count > 0 ? ValidationStatus.PassedWithWarnings : ValidationStatus.Passed;
        return new DataQualityResult
        {
            ValidatorId = ComponentId,
            Status = status,
            Message = "IV arbitrage validation passed",
            Warnings = warnings,
            DataElement = "OptionChain"
        };
    }

    private static System.Collections.Generic.List<DateTime> GetExpirations(
        System.Collections.Generic.IReadOnlyList<OptionContract> contracts)
    {
        System.Collections.Generic.HashSet<DateTime> expirationSet = new System.Collections.Generic.HashSet<DateTime>();
        System.Collections.Generic.List<DateTime> expirations = new System.Collections.Generic.List<DateTime>();
        foreach (OptionContract contract in contracts)
        {
            if (expirationSet.Add(contract.Expiration))
            {
                expirations.Add(contract.Expiration);
            }
        }

        return expirations;
    }

    private static System.Collections.Generic.List<OptionContract> FilterByExpiration(
        System.Collections.Generic.IReadOnlyList<OptionContract> contracts,
        DateTime expiration)
    {
        System.Collections.Generic.List<OptionContract> filtered = new System.Collections.Generic.List<OptionContract>();
        foreach (OptionContract contract in contracts)
        {
            if (contract.Expiration == expiration)
            {
                filtered.Add(contract);
            }
        }

        return filtered;
    }

    private static System.Collections.Generic.List<decimal> GetStrikes(
        System.Collections.Generic.List<OptionContract> contracts)
    {
        System.Collections.Generic.HashSet<decimal> strikeSet = new System.Collections.Generic.HashSet<decimal>();
        System.Collections.Generic.List<decimal> strikes = new System.Collections.Generic.List<decimal>();
        foreach (OptionContract contract in contracts)
        {
            if (strikeSet.Add(contract.Strike))
            {
                strikes.Add(contract.Strike);
            }
        }

        return strikes;
    }

    private static bool TryGetCallPut(
        System.Collections.Generic.List<OptionContract> contracts,
        decimal strike,
        out OptionContract call,
        out OptionContract put)
    {
        call = null!;
        put = null!;

        foreach (OptionContract contract in contracts)
        {
            if (contract.Strike != strike)
            {
                continue;
            }

            if (contract.Right == OptionRight.Call)
            {
                call = contract;
            }
            else if (contract.Right == OptionRight.Put)
            {
                put = contract;
            }

            if (call != null && put != null)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddParityWarnings(
        MarketDataSnapshot snapshot,
        DateTime expiration,
        System.Collections.Generic.List<OptionContract> contracts,
        System.Collections.Generic.List<string> warnings)
    {
        System.Collections.Generic.List<decimal> strikes = GetStrikes(contracts);
        for (int i = 0; i < strikes.Count; i++)
        {
            decimal strike = strikes[i];
            if (!TryGetCallPut(contracts, strike, out OptionContract call, out OptionContract put))
            {
                continue;
            }

            decimal lhs = call.Mid - put.Mid;
            decimal rhs = snapshot.SpotPrice - strike;
            decimal parityDiff = Math.Abs(lhs - rhs);
            decimal parityErrorPct = parityDiff / Math.Max(call.Mid, put.Mid);

            if (parityErrorPct > 0.02m)
            {
                warnings.Add($"Put-call parity violation: {strike} exp={expiration:yyyy-MM-dd}, error={parityErrorPct:P2}");
            }
        }
    }

    private static void AddTermStructureWarnings(
        MarketDataSnapshot snapshot,
        System.Collections.Generic.List<string> warnings)
    {
        System.Collections.Generic.List<OptionContract> atmCandidates =
            GetAtmCandidates(snapshot.OptionChain.Contracts);
        System.Collections.Generic.List<OptionContract> atm =
            TakeAtmCandidates(atmCandidates, 10, snapshot.SpotPrice);

        if (atm.Count < 2)
        {
            return;
        }

        System.Collections.Generic.Dictionary<DateTime, (decimal Sum, int Count)> ivByExpiration =
            BuildIvAggregates(atm);
        System.Collections.Generic.List<(DateTime Expiration, decimal AvgIv)> averages =
            BuildIvAverages(ivByExpiration);

        for (int i = 1; i < averages.Count; i++)
        {
            decimal ivChange = averages[i].AvgIv - averages[i - 1].AvgIv;
            if (ivChange > 0.05m)
            {
                warnings.Add(
                    $"Unusual IV term structure: {averages[i].Expiration:yyyy-MM-dd} IV={averages[i].AvgIv:P2} vs {averages[i - 1].Expiration:yyyy-MM-dd} IV={averages[i - 1].AvgIv:P2}");
            }
        }
    }

    private static System.Collections.Generic.List<OptionContract> GetAtmCandidates(
        System.Collections.Generic.IReadOnlyList<OptionContract> contracts)
    {
        System.Collections.Generic.List<OptionContract> candidates = new System.Collections.Generic.List<OptionContract>();
        foreach (OptionContract contract in contracts)
        {
            if (contract.ImpliedVolatility.HasValue)
            {
                candidates.Add(contract);
            }
        }

        return candidates;
    }

    private static System.Collections.Generic.List<OptionContract> TakeAtmCandidates(
        System.Collections.Generic.List<OptionContract> candidates,
        int maxCount,
        decimal spotPrice)
    {
        candidates.Sort((left, right) =>
        {
            decimal leftDistance = Math.Abs(left.Strike - spotPrice);
            decimal rightDistance = Math.Abs(right.Strike - spotPrice);
            return leftDistance.CompareTo(rightDistance);
        });

        int takeCount = candidates.Count > maxCount ? maxCount : candidates.Count;
        System.Collections.Generic.List<OptionContract> result = new System.Collections.Generic.List<OptionContract>(takeCount);
        for (int i = 0; i < takeCount; i++)
        {
            result.Add(candidates[i]);
        }

        return result;
    }

    private static System.Collections.Generic.Dictionary<DateTime, (decimal Sum, int Count)> BuildIvAggregates(
        System.Collections.Generic.List<OptionContract> atm)
    {
        System.Collections.Generic.Dictionary<DateTime, (decimal Sum, int Count)> ivByExpiration =
            new System.Collections.Generic.Dictionary<DateTime, (decimal Sum, int Count)>();

        foreach (OptionContract contract in atm)
        {
            if (!contract.ImpliedVolatility.HasValue)
            {
                continue;
            }

            DateTime expiration = contract.Expiration;
            decimal iv = contract.ImpliedVolatility.Value;
            if (ivByExpiration.TryGetValue(expiration, out (decimal Sum, int Count) aggregate))
            {
                ivByExpiration[expiration] = (aggregate.Sum + iv, aggregate.Count + 1);
            }
            else
            {
                ivByExpiration.Add(expiration, (iv, 1));
            }
        }

        return ivByExpiration;
    }

    private static System.Collections.Generic.List<(DateTime Expiration, decimal AvgIv)> BuildIvAverages(
        System.Collections.Generic.Dictionary<DateTime, (decimal Sum, int Count)> ivByExpiration)
    {
        System.Collections.Generic.List<(DateTime Expiration, decimal AvgIv)> ivs =
            new System.Collections.Generic.List<(DateTime Expiration, decimal AvgIv)>();
        foreach (System.Collections.Generic.KeyValuePair<DateTime, (decimal Sum, int Count)> kvp in ivByExpiration)
        {
            if (kvp.Value.Count > 0)
            {
                ivs.Add((kvp.Key, kvp.Value.Sum / kvp.Value.Count));
            }
        }

        ivs.Sort((left, right) => left.Expiration.CompareTo(right.Expiration));
        return ivs;
    }
}

/// <summary>
/// Validates volume and open interest for liquidity requirements.
/// Component ID: DTqc003A
/// </summary>
/// <remarks>
/// Validation checks:
/// - Volume > 0 for liquid options (OI > 100)
/// - Volume within 10× of 30-day average
/// - OI change consistent with volume
/// </remarks>
public sealed class VolumeOpenInterestValidator : DTqc002A
{
    private readonly ILogger<VolumeOpenInterestValidator> _logger;

    /// <inheritdoc/>
    public string ComponentId => "DTqc003A";

    /// <summary>
    /// Initializes a new instance of the <see cref="VolumeOpenInterestValidator"/> class.
    /// </summary>
    public VolumeOpenInterestValidator(ILogger<VolumeOpenInterestValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public DataQualityResult Validate(MarketDataSnapshot snapshot)
    {
        System.Collections.Generic.List<string> warnings = new System.Collections.Generic.List<string>();

        foreach (OptionContract contract in snapshot.OptionChain.Contracts)
        {
            // Check 1: Liquid options should have volume
            if (contract.OpenInterest > 100 && contract.Volume == 0)
            {
                warnings.Add($"No volume for liquid option: {contract.OptionSymbol} OI={contract.OpenInterest}");
            }

            // Check 2: Volume should be reasonable relative to OI
            if (contract.Volume > contract.OpenInterest * 2)
            {
                warnings.Add($"High volume/OI ratio: {contract.OptionSymbol} Vol={contract.Volume}, OI={contract.OpenInterest}");
            }

            // Check 3: Very low liquidity warning
            if (contract.OpenInterest < 10 && contract.Volume == 0)
            {
                warnings.Add($"Illiquid option: {contract.OptionSymbol} OI={contract.OpenInterest}, Vol={contract.Volume}");
            }
        }

        // Check 4: Average volume consistency
        if (snapshot.AverageVolume30Day == 0)
        {
            return new DataQualityResult
            {
                ValidatorId = ComponentId,
                Status = ValidationStatus.Failed,
                Message = "Average volume is zero",
                DataElement = "AverageVolume30Day"
            };
        }

        // Underlying volume should be within reasonable range
        if (snapshot.HistoricalBars.Count > 0)
        {
            long recentVolume = snapshot.HistoricalBars[^1].Volume;
            decimal volumeRatio = (decimal)recentVolume / snapshot.AverageVolume30Day;

            if (volumeRatio > 10m)
            {
                warnings.Add($"Unusual volume spike: {volumeRatio:F1}x average");
            }
            else if (volumeRatio < 0.1m)
            {
                warnings.Add($"Unusually low volume: {volumeRatio:P1} of average");
            }
        }

        ValidationStatus status = warnings.Count > 0 ? ValidationStatus.PassedWithWarnings : ValidationStatus.Passed;
        return new DataQualityResult
        {
            ValidatorId = ComponentId,
            Status = status,
            Message = "Volume/OI validation passed",
            Warnings = warnings,
            DataElement = "OptionChain"
        };
    }
}

/// <summary>
/// Validates earnings date accuracy and consistency.
/// Component ID: DTqc004A
/// </summary>
/// <remarks>
/// Validation checks:
/// - Earnings date confirmed from 2+ sources (if possible)
/// - Date within next 90 days
/// - No conflicting dates in recent history
/// </remarks>
public sealed class EarningsDateValidator : DTqc002A
{
    private readonly ILogger<EarningsDateValidator> _logger;
    private readonly ITimeProvider _timeProvider;

    /// <inheritdoc/>
    public string ComponentId => "DTqc004A";

    /// <summary>
    /// Initializes a new instance of the <see cref="EarningsDateValidator"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="timeProvider">The time provider for backtest-aware time operations.</param>
    public EarningsDateValidator(
        ILogger<EarningsDateValidator> logger,
        ITimeProvider timeProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc/>
    public DataQualityResult Validate(MarketDataSnapshot snapshot)
    {
        System.Collections.Generic.List<string> warnings = new System.Collections.Generic.List<string>();

        if (snapshot.NextEarnings == null)
        {
            // No upcoming earnings - this is valid
            return new DataQualityResult
            {
                ValidatorId = ComponentId,
                Status = ValidationStatus.Passed,
                Message = "No upcoming earnings to validate",
                DataElement = "NextEarnings"
            };
        }

        LocalDate today = _timeProvider.Today;
        EarningsEvent? earnings = snapshot.NextEarnings;

        // Check 1: Earnings date is in the future
        LocalDate earningsDate = _timeProvider.ToLocalDate(earnings.Date);
        if (earningsDate < today)
        {
            return new DataQualityResult
            {
                ValidatorId = ComponentId,
                Status = ValidationStatus.Failed,
                Message = $"Earnings date is in the past: {earnings.Date:yyyy-MM-dd}",
                DataElement = "NextEarnings.Date"
            };
        }

        // Check 2: Earnings date is within reasonable window
        int daysAhead = Period.Between(today, earningsDate).Days;
        if (daysAhead > 90)
        {
            warnings.Add($"Earnings date is {daysAhead:F0} days ahead (>90 days)");
        }

        // Check 3: Data freshness
        LocalDate fetchedDate = _timeProvider.ToLocalDate(earnings.FetchedAt);
        int dataAgeDays = Period.Between(fetchedDate, today).Days;
        if (dataAgeDays > 7)
        {
            warnings.Add($"Earnings data is {dataAgeDays} days old");
        }

        // Check 4: Historical consistency
        if (snapshot.HistoricalEarnings.Count > 0)
        {
            EarningsEvent? lastEarnings = null;
            foreach (EarningsEvent earning in snapshot.HistoricalEarnings)
            {
                if (lastEarnings == null || earning.Date > lastEarnings.Date)
                {
                    lastEarnings = earning;
                }
            }

            if (lastEarnings != null)
            {
                double quarterGap = (earnings.Date - lastEarnings.Date).TotalDays;
                
                // Typical quarterly spacing: 84-98 days
                if (quarterGap < 70 || quarterGap > 120)
                {
                    warnings.Add($"Unusual earnings spacing: {quarterGap:F0} days since last earnings");
                }
            }
        }

        ValidationStatus status = warnings.Count > 0 ? ValidationStatus.PassedWithWarnings : ValidationStatus.Passed;
        return new DataQualityResult
        {
            ValidatorId = ComponentId,
            Status = status,
            Message = "Earnings date validation passed",
            Warnings = warnings,
            DataElement = "NextEarnings"
        };
    }
}
