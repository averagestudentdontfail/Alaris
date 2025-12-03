# Alaris.Lean

QuantConnect LEAN integration for Alaris earnings volatility trading strategy.

## Overview

Alaris.Lean provides the orchestration layer that integrates all Alaris components with the QuantConnect LEAN trading engine for production deployment.

**Component ID:** STLN001A

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                  Alaris.Lean (QCAlgorithm)                   │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────────────────────────────────────────────┐    │
│  │          Daily Evaluation Workflow                   │    │
│  │          (Scheduled at 9:31 AM ET)                   │    │
│  └──────────────────────────────────────────────────────┘    │
│                            │                                 │
│                            ▼                                 │
│            1. Universe Selection                             │
│               (Earnings in 6 days)                           │
│                            │                                 │
│                            ▼                                 │
│            2. Market Data Acquisition                        │
│               (Alaris.Data Bridge)                           │
│                            │                                 │
│                            ▼                                 │
│            3. Realized Volatility                            │
│               (Yang-Zhang OHLC)                              │
│                            │                                 │
│                            ▼                                 │
│            4. Term Structure Analysis                        │
│               (30/60/90 DTE IV)                              │
│                            │                                 │
│                            ▼                                 │
│            5. Signal Generation                              │
│               (Atilgan Criteria)                             │
│                            │                                 │
│                            ▼                                 │
│            6. Production Validation                          │
│               (4-Stage Validators)                           │
│                            │                                 │
│                            ▼                                 │
│            7. Execution Pricing                              │
│               (IBKR Snapshot Quotes)                         │
│                            │                                 │
│                            ▼                                 │
│            8. Position Sizing                                │
│               (Kelly Criterion)                              │
│                            │                                 │
│                            ▼                                 │
│            9. Order Execution                                │
│               (Lean Combo Orders)                            │
│                            │                                 │
│                            ▼                                 │
│           10. Audit Trail                                    │
│               (Alaris.Events)                                │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

## Features

- **Automated Universe Selection:** Daily screening for symbols with earnings in 6 days
- **Complete Strategy Workflow:** End-to-end implementation from data to execution
- **Production Validation:** 4-stage validation before every trade
- **Real-Time Execution:** IBKR snapshot quotes for accurate pricing
- **Risk Management:** Kelly criterion position sizing + portfolio limits
- **Audit Trail:** Complete logging via Alaris.Events
- **LEAN Integration:** Native QuantConnect engine compatibility

## Configuration

### appsettings.json

```json
{
  "Polygon": {
    "ApiKey": "your_polygon_api_key"
  },
  "FMP": {
    "ApiKey": "your_fmp_api_key"
  },
  "InteractiveBrokers": {
    "Host": "127.0.0.1",
    "Port": 4001,
    "ClientId": 1
  },
  "Alaris": {
    "DaysBeforeEarnings": 6,
    "MinimumVolume": 1500000,
    "PortfolioAllocationLimit": 0.80,
    "KellyFraction": 0.25
  }
}
```

### Environment Variables

```bash
export ALARIS_Polygon__ApiKey="your_key"
export ALARIS_FMP__ApiKey="your_key"
export ALARIS_InteractiveBrokers__Host="127.0.0.1"
export ALARIS_InteractiveBrokers__Port="4001"
```

## Deployment

### Prerequisites

1. **QuantConnect LEAN Engine**
   - Clone: `git clone https://github.com/QuantConnect/Lean.git`
   - Build: `dotnet build Lean.sln`

2. **Interactive Brokers Gateway**
   - Install IB Gateway
   - Configure paper trading or live account
   - Start Gateway on port 4001

3. **API Keys**
   - Polygon Options Starter: $99/month
   - Financial Modeling Prep: Free tier

### Build

```bash
# Build Alaris components
dotnet build Alaris.Data/Alaris.Data.csproj --configuration Release
dotnet build Alaris.Strategy/Alaris.Strategy.csproj --configuration Release
dotnet build Alaris.Events/Alaris.Events.csproj --configuration Release
dotnet build Alaris.Lean/Alaris.Lean.csproj --configuration Release
```

### Run in LEAN

```bash
# Copy algorithm to LEAN
cp Alaris.Lean/AlarisEarningsAlgorithm.cs \
   Lean/Algorithm.CSharp/AlarisEarningsAlgorithm.cs

# Configure LEAN
cd Lean
nano Launcher/config.json  # Set algorithm-type-name to "AlarisEarningsAlgorithm"

# Run backtest
dotnet run --project Launcher/QuantConnect.Lean.Launcher.csproj

# Run live (paper trading)
dotnet run --project Launcher/QuantConnect.Lean.Launcher.csproj --live
```

## Strategy Parameters

### Atilgan (2014) Criteria

| Parameter | Value | Source |
|-----------|-------|--------|
| Days before earnings | 6 | Atilgan et al. (2014) Table 3 |
| IV/RV threshold | 1.25 | Atilgan et al. (2014) Table 5 |
| Term slope threshold | -0.00406 | Atilgan et al. (2014) |
| Minimum volume | 1.5M | Atilgan et al. (2014) |

### Production Validation

| Validator | Threshold | Action |
|-----------|-----------|--------|
| Execution Cost Survival | Post-cost IV/RV ≥ 1.20 | Reject |
| Vega Independence | Correlation < 0.70 | Reject |
| Liquidity Assurance | Vol% ≤ 5%, OI% ≤ 2% | Reject |
| Gamma Risk | |Delta| < 0.10 | Monitor |

### Risk Limits

| Limit | Value |
|-------|-------|
| Max contracts per spread | 10 |
| Max notional per position | $25,000 |
| Max portfolio allocation | 80% |
| Max concurrent positions | 5 |
| Max daily loss | $15,000 |
| Cash reserve | 20% minimum |

## Monitoring

### Real-Time Metrics

- Signals generated per day
- Fill rates (target: >80%)
- Average execution slippage (target: <1%)
- Production validation pass rate (target: >70%)

### Daily Reports

- Position summary
- P&L attribution
- Data quality summary
- Risk metrics (Greeks)

### Alerts

- Critical: Trading halt, system exceptions
- Warning: Data quality issues, unusual signals
- Info: Daily performance summary

See `Alaris.Governance/Operation.md` for complete monitoring procedures.

## Usage Example

```csharp
// In LEAN's Algorithm.CSharp project:

public class MyAlarisAlgorithm : AlarisEarningsAlgorithm
{
    public override void Initialize()
    {
        base.Initialize(); // Initialize Alaris components
        
        // Customize if needed
        SetStartDate(2025, 1, 1);
        SetCash(100_000);
    }
}
```

## Testing

### Backtest Mode

```bash
# Run backtest from 2024-01-01 to 2024-12-31
dotnet run --project Lean/Launcher -- \
  --algorithm-type-name AlarisEarningsAlgorithm \
  --start-date 2024-01-01 \
  --end-date 2024-12-31
```

### Paper Trading

```bash
# Run paper trading with IB Gateway
dotnet run --project Lean/Launcher -- \
  --algorithm-type-name AlarisEarningsAlgorithm \
  --live \
  --brokerage InteractiveBrokersBrokerage \
  --data-queue-handler InteractiveBrokersDataQueueHandler
```

### Live Trading

```bash
# CAUTION: Live money at risk
dotnet run --project Lean/Launcher -- \
  --algorithm-type-name AlarisEarningsAlgorithm \
  --live \
  --brokerage InteractiveBrokersBrokerage \
  --live-cash 100000
```

## Performance Benchmarks

**Expected Performance (from Atilgan 2014):**
- Annualized return: 8-12%
- Sharpe ratio: 1.2-1.8
- Max drawdown: 15-20%
- Win rate: 55-65%
- Average holding period: 30 days

**System Performance:**
- Signal evaluation: <2 seconds
- Order execution: <500ms
- Daily evaluation: <5 minutes (for 20 symbols)

## Troubleshooting

### Common Issues

**Problem:** Algorithm not receiving data
**Solution:** Check LEAN data feeds, verify Polygon API key

**Problem:** Orders not executing
**Solution:** Verify IB Gateway connection, check account permissions

**Problem:** Production validation all failing
**Solution:** Review `DTqc001A-004A` logs, check data quality

**Problem:** Memory usage high
**Solution:** Reduce historical lookback period, implement data caching

## Integration with Alaris Components

| Component | Purpose | Integration Point |
|-----------|---------|-------------------|
| Alaris.Data | Market data | `_dataBridge.GetMarketDataSnapshotAsync()` |
| Alaris.Strategy | Signal generation | `SignalGenerator.Evaluate()` |
| Alaris.Double | Option pricing | Used internally by validators |
| Alaris.Events | Audit trail | `_auditLogger.LogTrade()` |
| Alaris.Quantlib | Standard pricing | Fallback for positive rates |

## Roadmap

**Future Enhancements:**
- [ ] Multi-strategy support (other Atilgan variants)
- [ ] Dynamic parameter optimization
- [ ] Machine learning signal enhancement
- [ ] Real-time portfolio rebalancing
- [ ] Advanced risk analytics dashboard

## References

- **Atilgan et al. (2014):** "Implied Volatility Spreads and Expected Market Returns"
- **Leung & Santoli (2014):** "Modeling Pre-Earnings Announcement Drift"
- **Healy (2021):** "Pricing American Options Under Negative Rates"
- **Yang & Zhang (2000):** "Drift-Independent Volatility Estimation"
- **QuantConnect LEAN:** https://github.com/QuantConnect/Lean