# Alaris.Data

Data acquisition, validation, and integration layer for the Alaris quantitative trading system.

## Overview

Alaris.Data provides a unified interface for retrieving market data from multiple sources, validating data quality, and bridging to the Lean trading engine.

**Key Features:**
- Multi-provider data acquisition (Polygon, FMP, IBKR)
- Comprehensive data quality validation
- Real-time snapshot quotes for execution
- Seamless Lean engine integration
- Historical bootstrap capabilities

## Components

### Data Providers

#### DTpl001A - Polygon API Client
**Cost:** $99/month (Options Starter plan)

Provides:
- Historical OHLCV bars (2 years)
- Options chains (all expiries)
- Implied volatility and Greeks
- Unlimited API calls
- 15-minute delayed quotes

#### DTea001A - Financial Modeling Prep
**Cost:** FREE (250 calls/day)

Provides:
- Upcoming earnings calendar
- Historical earnings (2 years)
- EPS estimates and actuals
- Fiscal quarter information

#### DTib005A - IBKR Snapshot Quotes
**Cost:** ~$0.01-0.03 per snapshot

Provides:
- Real-time bid/ask quotes
- On-demand execution pricing
- Calendar spread quotes
- No subscription required

#### DTrf001A - Treasury Direct Rates
**Cost:** FREE (official US government API)

Provides:
- 3-month T-bill rates (risk-free rate)
- Daily updates
- Historical rate data

### Data Quality Validators

#### DTqc001A - Price Reasonableness
Validates:
- Spot price within ±10% of previous close
- Option bid > 0 and bid < ask
- Option ask within reasonable bounds
- Data freshness (<1 hour old)

#### DTqc002A - IV Arbitrage Detection
Validates:
- Put-call parity (within 2%)
- Calendar spread IV consistency
- No butterfly arbitrage opportunities

#### DTqc003A - Volume/OI Validation
Validates:
- Sufficient volume for liquid options
- Volume consistent with open interest
- No unusual volume spikes

#### DTqc004A - Earnings Date Verification
Validates:
- Earnings date in future
- Within reasonable window (<90 days)
- Consistent with historical pattern

### Integration Bridge

#### DTbr001A - Alaris Data Bridge
**Purpose:** Unified interface for strategy components

Features:
- Concurrent data fetching
- Automated validation pipeline
- Universe filtering (upcoming earnings, volume criteria)
- Complete MarketDataSnapshot assembly

## Installation

### Prerequisites
- .NET 9.0 SDK
- Polygon API key (Options Starter plan)
- FMP API key (free tier)
- IBKR Gateway (for production execution)

### Configuration

**appsettings.json:**
```json
{
  "Polygon": {
    "ApiKey": "your_polygon_api_key_here"
  },
  "FMP": {
    "ApiKey": "your_fmp_api_key_here"
  },
  "InteractiveBrokers": {
    "Host": "127.0.0.1",
    "Port": 4001,
    "ClientId": 1
  }
}
```

### Build
```bash
dotnet restore
dotnet build --configuration Release
```

## Usage

### Basic Usage

```csharp
using Alaris.Data.Bridge;
using Alaris.Data.Providers;
using Alaris.Data.Quality;

// 1. Set up providers
var marketDataProvider = new PolygonApiClient(httpClient, config, logger);
var earningsProvider = new FinancialModelingPrepProvider(httpClient, config, logger);
var riskFreeRateProvider = new TreasuryDirectRateProvider(httpClient, logger);

// 2. Set up validators
var validators = new IDataQualityValidator[]
{
    new PriceReasonablenessValidator(logger),
    new IvArbitrageValidator(logger),
    new VolumeOpenInterestValidator(logger),
    new EarningsDateValidator(logger)
};

// 3. Create bridge
var dataBridge = new AlarisDataBridge(
    marketDataProvider,
    earningsProvider,
    riskFreeRateProvider,
    validators,
    logger);

// 4. Get market data snapshot
var snapshot = await dataBridge.GetMarketDataSnapshotAsync("AAPL");

// 5. Use in strategy evaluation
var yangZhangRv = YangZhangEstimator.Calculate(snapshot.HistoricalBars);
var termStructure = TermStructureAnalyzer.Analyze(snapshot.OptionChain);
```

### Universe Selection

```csharp
// Get all symbols with earnings in next 7 days
var symbols = await dataBridge.GetSymbolsWithUpcomingEarningsAsync(
    startDate: DateTime.UtcNow,
    endDate: DateTime.UtcNow.AddDays(7));

// Filter for basic criteria (volume, earnings presence)
var qualified = new List<string>();
foreach (var symbol in symbols)
{
    if (await dataBridge.MeetsBasicCriteriaAsync(symbol))
    {
        qualified.Add(symbol);
    }
}
```

### Execution Pricing

```csharp
using Alaris.Data.Providers.Execution;

var snapshotProvider = new InteractiveBrokersSnapshotProvider(logger);

// Get real-time calendar spread quote
var spreadQuote = await snapshotProvider.GetCalendarSpreadQuoteAsync(
    underlyingSymbol: "AAPL",
    strike: 120m,
    frontExpiration: new DateTime(2025, 2, 21),
    backExpiration: new DateTime(2025, 4, 18),
    right: OptionRight.Call);

// Place limit order at mid price
var limitPrice = spreadQuote.SpreadMid;
```

## Data Quality

All market data passes through validation pipeline before use in strategy:

| Validator | Pass Rate Target | Action on Failure |
|-----------|------------------|-------------------|
| DTqc001A | >98% | Reject data, log error |
| DTqc002A | >95% | Log warning, continue |
| DTqc003A | >95% | Log warning, flag illiquid |
| DTqc004A | >98% | Reject signal, manual review |

Validation failures are logged via `Alaris.Events` for audit trail.

## Performance

**Target Metrics:**
- Market data snapshot assembly: <2 seconds
- Data validation pipeline: <100ms
- IBKR snapshot request: <500ms

**Optimization:**
- Concurrent provider requests
- Minimal allocations in hot paths
- Connection pooling for HTTP clients

## Dependencies

- **System.Net.Http** - HTTP client for REST APIs
- **System.Text.Json** - JSON serialization
- **Microsoft.Extensions.Logging** - Structured logging
- **Alaris.Strategy** - Data models and strategy components

## Testing

```bash
# Run all tests
dotnet test

# Test data providers
dotnet test --filter Category=DataProviders

# Test data quality validators
dotnet test --filter Category=DataQuality

# Integration tests (requires API keys)
dotnet test --filter Category=Integration
```

## Monitoring

**Key Metrics:**
- API response times (Polygon, FMP, IBKR)
- Data quality validation pass rates
- Data freshness/staleness
- API rate limit usage

See `Alaris.Governance/Operation.md` for complete monitoring procedures.

## Troubleshooting

### Common Issues

**Problem:** Polygon API returning 429 (rate limit)
**Solution:** Polygon Starter has unlimited calls - check API key validity

**Problem:** FMP free tier limit exceeded (250/day)
**Solution:** Cache earnings calendar, refresh only daily

**Problem:** IBKR snapshot timeout
**Solution:** Check IB Gateway connection, increase timeout to 15 seconds

**Problem:** Data quality validation failures
**Solution:** Review `DTqc001A-004A` logs, check data source status

## Roadmap

**Future Enhancements:**
- [ ] Local data caching (PostgreSQL + Parquet)
- [ ] Historical bootstrap CLI tool
- [ ] Additional data providers (backup sources)
- [ ] Real-time streaming (WebSocket feeds)
- [ ] Data quality dashboard