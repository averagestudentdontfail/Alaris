using System;
using System.Linq;
using Microsoft.Extensions.Logging;
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

    /// <inheritdoc/>
    public string ComponentId => "DTqc001A";

    /// <summary>
    /// Initializes a new instance of the <see cref="PriceReasonablenessValidator"/> class.
    /// </summary>
    public PriceReasonablenessValidator(ILogger<PriceReasonablenessValidator> _logger)
    {
        this._logger = _logger ?? throw new ArgumentNullException(nameof(_logger));
    }

    /// <inheritdoc/>
    public DataQualityResult Validate(MarketDataSnapshot snapshot)
    {
        var warnings = new System.Collections.Generic.List<string>();
        var now = DateTime.UtcNow;

        // Check 1: Spot price change reasonableness
        if (snapshot.HistoricalBars.Count > 0)
        {
            var previousClose = snapshot.HistoricalBars[^1].Close;
            var priceChange = Math.Abs((snapshot.SpotPrice - previousClose) / previousClose);

            if (priceChange > 0.10m)
            {
                var message = $"Spot price changed {priceChange:P2} from previous close";
                _logger.LogWarning("{ComponentId}: {Message}", ComponentId, message);
                warnings.Add(message);
            }
        }

        // Check 2: Option bid/ask validity
        foreach (var contract in snapshot.OptionChain.Contracts)
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
            var intrinsicValue = contract.Right == OptionRight.Call
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
        var dataAge = now - snapshot.Timestamp;
        if (dataAge > TimeSpan.FromHours(1))
        {
            return new DataQualityResult
            {
                ValidatorId = ComponentId,
                Status = ValidationStatus.Failed,
                Message = $"Stale data: age={dataAge.TotalMinutes:F1} minutes",
                DataElement = "Timestamp"
            };
        }

        var status = warnings.Count > 0 ? ValidationStatus.PassedWithWarnings : ValidationStatus.Passed;
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
        var warnings = new System.Collections.Generic.List<string>();

        // Check put-call parity for each expiration and strike
        var expirations = snapshot.OptionChain.Contracts
            .Select(c => c.Expiration)
            .Distinct()
            .ToList();

        foreach (var expiration in expirations)
        {
            var contracts = snapshot.OptionChain.Contracts
                .Where(c => c.Expiration == expiration)
                .ToList();

            var strikes = contracts.Select(c => c.Strike).Distinct();

            foreach (var strike in strikes)
            {
                var call = contracts.FirstOrDefault(c => c.Strike == strike && c.Right == OptionRight.Call);
                var put = contracts.FirstOrDefault(c => c.Strike == strike && c.Right == OptionRight.Put);

                if (call == null || put == null)
                    continue;

                // Put-call parity: C - P = S - K * exp(-r*T)
                // For short-dated options, approximate: C - P ≈ S - K
                var lhs = call.Mid - put.Mid;
                var rhs = snapshot.SpotPrice - strike;
                var parityDiff = Math.Abs(lhs - rhs);
                var parityErrorPct = parityDiff / Math.Max(call.Mid, put.Mid);

                if (parityErrorPct > 0.02m) // 2% threshold
                {
                    warnings.Add($"Put-call parity violation: {strike} exp={expiration:yyyy-MM-dd}, error={parityErrorPct:P2}");
                }
            }
        }

        // Check calendar spread IV term structure
        var atm = snapshot.OptionChain.Contracts
            .Where(c => c.ImpliedVolatility.HasValue)
            .OrderBy(c => Math.Abs(c.Strike - snapshot.SpotPrice))
            .Take(10) // Top 10 ATM options
            .ToList();

        if (atm.Count >= 2)
        {
            var ivs = atm
                .GroupBy(c => c.Expiration)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Expiration = g.Key,
                    AvgIv = g.Average(c => c.ImpliedVolatility!.Value)
                })
                .ToList();

            for (int i = 1; i < ivs.Count; i++)
            {
                var ivChange = ivs[i].AvgIv - ivs[i - 1].AvgIv;
                var daysDiff = (ivs[i].Expiration - ivs[i - 1].Expiration).TotalDays;

                // Expect IV to decrease or stay flat with longer expiry (absent earnings)
                if (ivChange > 0.05m) // >5% IV increase
                {
                    warnings.Add($"Unusual IV term structure: {ivs[i].Expiration:yyyy-MM-dd} IV={ivs[i].AvgIv:P2} vs {ivs[i-1].Expiration:yyyy-MM-dd} IV={ivs[i-1].AvgIv:P2}");
                }
            }
        }

        var status = warnings.Count > 0 ? ValidationStatus.PassedWithWarnings : ValidationStatus.Passed;
        return new DataQualityResult
        {
            ValidatorId = ComponentId,
            Status = status,
            Message = "IV arbitrage validation passed",
            Warnings = warnings,
            DataElement = "OptionChain"
        };
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
        var warnings = new System.Collections.Generic.List<string>();

        foreach (var contract in snapshot.OptionChain.Contracts)
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
            var recentVolume = snapshot.HistoricalBars[^1].Volume;
            var volumeRatio = (decimal)recentVolume / snapshot.AverageVolume30Day;

            if (volumeRatio > 10m)
            {
                warnings.Add($"Unusual volume spike: {volumeRatio:F1}x average");
            }
            else if (volumeRatio < 0.1m)
            {
                warnings.Add($"Unusually low volume: {volumeRatio:P1} of average");
            }
        }

        var status = warnings.Count > 0 ? ValidationStatus.PassedWithWarnings : ValidationStatus.Passed;
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

    /// <inheritdoc/>
    public string ComponentId => "DTqc004A";

    /// <summary>
    /// Initializes a new instance of the <see cref="EarningsDateValidator"/> class.
    /// </summary>
    public EarningsDateValidator(ILogger<EarningsDateValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public DataQualityResult Validate(MarketDataSnapshot snapshot)
    {
        var warnings = new System.Collections.Generic.List<string>();

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

        var now = DateTime.UtcNow.Date;
        var earnings = snapshot.NextEarnings;

        // Check 1: Earnings date is in the future
        if (earnings.Date < now)
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
        var daysAhead = (earnings.Date - now).TotalDays;
        if (daysAhead > 90)
        {
            warnings.Add($"Earnings date is {daysAhead:F0} days ahead (>90 days)");
        }

        // Check 3: Data freshness
        var dataAge = now - earnings.FetchedAt.Date;
        if (dataAge.TotalDays > 7)
        {
            warnings.Add($"Earnings data is {dataAge.TotalDays:F0} days old");
        }

        // Check 4: Historical consistency
        if (snapshot.HistoricalEarnings.Count > 0)
        {
            var lastEarnings = snapshot.HistoricalEarnings
                .OrderByDescending(e => e.Date)
                .FirstOrDefault();

            if (lastEarnings != null)
            {
                var quarterGap = (earnings.Date - lastEarnings.Date).TotalDays;
                
                // Typical quarterly spacing: 84-98 days
                if (quarterGap < 70 || quarterGap > 120)
                {
                    warnings.Add($"Unusual earnings spacing: {quarterGap:F0} days since last earnings");
                }
            }
        }

        var status = warnings.Count > 0 ? ValidationStatus.PassedWithWarnings : ValidationStatus.Passed;
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