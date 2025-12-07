# Alaris Production Readiness Roadmap

> **Last Updated:** 2025-12-07  
> **Status:** In Development

This document outlines the remaining work required to make the Alaris Trading System production-ready for live deployment.

---

## Quick Status Summary

| Category | Status | Blocking Production? |
|----------|--------|---------------------|
| Universe Generation | ✅ Complete | No |
| Market Data (Polygon) | ✅ Complete | No |
| Earnings Calendar | ❌ Needs Work | **Yes** |
| IBKR Integration | ⚠️ Partial | Yes (for live) |
| Backtesting | ⚠️ Partial | Yes |
| Configuration | ✅ Complete | No |

---

## 1. Earnings Calendar Provider (Critical)

### Problem
The current earnings calendar provider (`DTea001A.cs` - FinancialModelingPrepProvider) uses FMP's `/v3/earning_calendar` endpoint which is now **deprecated as a legacy endpoint** requiring paid subscription.

### Solution: SEC EDGAR 8-K Filings (Free)

The SEC provides **free APIs** at `data.sec.gov` with:
- **No API key required** (just User-Agent header with contact info)
- **Real-time updates** (JSON updated as filings are disseminated)
- **Rate limit:** 10 requests/second
- **Coverage:** All US public companies since 2001

**8-K Form Item 2.02** ("Results of Operations and Financial Condition") is filed within 4 business days of earnings announcements.

### Implementation Plan

#### Phase 1: SEC EDGAR Provider (`DTea001B.cs`)
Create new earnings provider that:
1. Queries SEC EDGAR for 8-K filings by company CIK
2. Filters for Item 2.02 filings (earnings announcements)
3. Extracts filing date as earnings announcement date
4. Caches results to minimize API calls

**SEC API Endpoints:**
```
GET https://data.sec.gov/submissions/CIK{10-digit-cik}.json
Returns: Company filings including form types and dates

GET https://efts.sec.gov/LATEST/search-index?q="item 2.02"&form=8-K&ciks={cik}
Returns: Full-text search results for Item 2.02 filings
```

#### Phase 2: CIK Mapping Service
Map stock tickers to SEC CIK numbers:
- Download SEC's ticker-to-CIK mapping file
- Cache locally for fast lookups
- Update periodically (CIK mappings rarely change)

**Mapping Endpoint:**
```
GET https://www.sec.gov/files/company_tickers.json
Returns: {"0":{"cik_str":"320193","ticker":"AAPL","title":"Apple Inc."}, ...}
```

#### Phase 3: CLI Integration
Add `alaris earnings` commands:
```bash
alaris earnings generate --from 20241001 --to 20241015  # Pre-generate for backtest
alaris earnings lookup AAPL                              # Lookup single ticker
alaris earnings sync                                     # Sync latest filings
```

#### Phase 4: Real-Time Support (for Live Trading)
For live/paper trading, poll SEC EDGAR for new 8-K filings:
- Query recent filings endpoint periodically
- Filter for Item 2.02 forms
- Update internal calendar cache

### Development Effort
| Component | Effort |
|-----------|--------|
| DTea001B (SEC Provider) | 3-4 hours |
| CIK Mapping Service | 1-2 hours |
| CLI Commands | 2-3 hours |
| Real-Time Polling | 2-3 hours |
| **Total** | **8-12 hours** |

---

## 2. Interactive Brokers Integration (Required for Live)

### Current Status
- IBKR brokerage project cloned into `Alaris.Lean/Brokerages/InteractiveBrokers/`
- Launcher references IBKR project (✅ builds successfully)
- Snapshot provider (`DTib005A.cs`) has **commented-out EWrapper** integration

### Issues to Resolve

#### 2.1 IB Gateway Connection
The `InteractiveBrokersSnapshotProvider` currently simulates responses:
```csharp
// Line 150-173: SimulateResponse() is used instead of real IB data
```

**Action Required:**
- Uncomment and complete IbWrapper implementation (lines 345-383)
- Add proper IBApi package reference
- Implement real TWS/IB Gateway connection

#### 2.2 Configuration
`appsettings.local.jsonc` has IB credentials but they may not be wired correctly:
```jsonc
"InteractiveBrokers": {
    "Account": "DUE407919",
    "Username": "sunnyisday",
    ...
}
```

**Action Required:**
- Verify IB credentials are loaded in `STLN001A.BuildConfiguration()`
- Add IB Gateway host/port configuration (currently hardcoded to 127.0.0.1:4002)

### Development Time
- Complete IBKR snapshot provider: ~4-6 hours
- Live trading verification: ~2-4 hours

---

## 3. Backtest Date Range Configuration

### Current Issue
The backtest runs from **2025-01-01 to 2025-12-06** (algorithm default), but:
- Universe data only exists for **2024-10-01 to 2024-10-15**
- STUN001B falls back to most recent file (20241015.csv) which is incorrect

### Solution
Add CLI arguments for backtest date range:
```bash
alaris run --mode backtest --from 20241001 --to 20241015
```

Or update algorithm to read dates from configuration:
```csharp
SetStartDate(configuration.GetValue<DateTime>("Backtest:StartDate"));
SetEndDate(configuration.GetValue<DateTime>("Backtest:EndDate"));
```

### Development Time: ~1-2 hours

---

## 4. Code Quality Items

### 4.1 Placeholder Implementations
| File | Issue | Priority |
|------|-------|----------|
| `DTib005A.cs` | SimulateResponse() instead of real IB data | High |
| `STUN001A.cs` | Unused (replaced by STUN001B) but still referenced | Low |
| `STUN001B.cs` | Has fallback to "top N stocks" when earnings fails | Medium |

### 4.2 Suppressed Warnings
The following analyzer warnings are suppressed project-wide and should be reviewed:
- `CA1031` - Catch generic Exception
- `CA2234` - URI vs string overloads
- `CA1307/CA1310` - String comparison culture

### 4.3 Test Coverage
- Unit tests: 210 passing
- Integration tests: Needed for IB connection
- End-to-end backtest: Blocked by earnings provider

---

## 5. Data Pipeline Summary

```
┌──────────────────────────────────────────────────────────────────┐
│                    ALARIS DATA PIPELINE                          │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────┐    ┌─────────────────┐                     │
│  │ alaris universe │───▶│ coarse/*.csv    │ ✅ Complete         │
│  │ generate        │    │ (3000 stocks/day)│                     │
│  └─────────────────┘    └─────────────────┘                     │
│           │                                                      │
│           ▼                                                      │
│  ┌─────────────────┐    ┌─────────────────┐                     │
│  │ alaris data     │───▶│ daily/*.csv     │ ✅ Complete         │
│  │ download        │    │ (per ticker)    │                     │
│  └─────────────────┘    └─────────────────┘                     │
│           │                                                      │
│           ▼                                                      │
│  ┌─────────────────┐    ┌─────────────────┐                     │
│  │ EARNINGS DATA   │───▶│ earnings/*.csv  │ ❌ NEEDED          │
│  │ (TODO)          │    │ or API runtime  │                     │
│  └─────────────────┘    └─────────────────┘                     │
│           │                                                      │
│           ▼                                                      │
│  ┌─────────────────────────────────────────┐                    │
│  │ STUN001B (Universe Selector)            │                    │
│  │ Reads universe + filters by earnings    │                    │
│  └─────────────────────────────────────────┘                    │
│           │                                                      │
│           ▼                                                      │
│  ┌─────────────────────────────────────────┐                    │
│  │ STLN001A (Algorithm)                    │                    │
│  │ Executes calendar spread strategy       │                    │
│  └─────────────────────────────────────────┘                    │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

---

## 6. Priority Action Items

### Immediate (Before Next Backtest)
1. **Implement Polygon/Benzinga earnings provider** (`DTea001B.cs`)
2. Update `STLN001A` to use new earnings provider
3. Add backtest date configuration
4. Test end-to-end backtest

### Short-term (Before Paper Trading)
5. Complete IBKR snapshot provider implementation
6. Test IB Gateway connection in paper mode
7. Verify order execution flow

### Before Live Deployment
8. Run extended paper trading period
9. Implement position monitoring dashboard
10. Add emergency stop/circuit breaker
11. Complete audit logging

---

## 7. CLI Commands Status

| Command | Status | Notes |
|---------|--------|-------|
| `alaris run --mode backtest` | ⚠️ Partial | Needs earnings + dates |
| `alaris run --mode paper` | ⚠️ Partial | Needs IBKR connection |
| `alaris run --mode live` | ❌ Not Ready | Needs full IBKR impl |
| `alaris data download` | ✅ Works | Polygon with rate limiting |
| `alaris universe generate` | ✅ Works | 3000 stocks/day |
| `alaris universe list` | ✅ Works | Shows generated files |
| `alaris config show` | ✅ Works | Configuration display |
| `alaris earnings generate` | ❌ Missing | Needs implementation |

---

## 8. Development Estimates

| Task | Effort | Blocking |
|------|--------|----------|
| Polygon earnings provider | 2-3 hours | Backtest |
| Backtest date configuration | 1-2 hours | Backtest |
| IBKR snapshot completion | 4-6 hours | Live trading |
| STUN001B earnings fallback removal | 1 hour | Code quality |
| Integration tests | 3-4 hours | Code quality |

**Total to production-ready backtest:** ~5-6 hours  
**Total to production-ready live:** ~15-20 hours
