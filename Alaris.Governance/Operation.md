# Alaris Operational Governance

**Document ID:** ALARIS-OPS-001  
**Version:** 1.0  
**Date:** December 2025  
**Status:** Active  

---

## 1. Scope

This document establishes operational procedures, monitoring requirements, risk controls, and incident response protocols for the Alaris quantitative trading system in production environments.

---

## 2. System Overview

### 2.1 Component Dependencies

| Component | Purpose | Uptime SLA | Recovery Time |
|-----------|---------|------------|---------------|
| Polygon API | Historical + daily data | 99.5% | 15 minutes |
| FMP Earnings | Earnings calendar | 99.0% | 1 hour |
| IBKR Gateway | Order execution | 99.9% | 5 minutes |
| Alaris.Data | Data validation | 99.9% | 1 minute |
| Lean Engine | Strategy orchestration | 99.9% | 2 minutes |
| Alaris.Strategy | Signal generation | 99.9% | 1 minute |

---

## 3. Deployment Procedures

### 3.1 Pre-Deployment Checklist

**Environment Preparation:**
- [ ] VPS/cloud instance provisioned (minimum: 4 CPU, 8GB RAM, 100GB SSD)
- [ ] .NET 9.0 runtime installed
- [ ] PostgreSQL 16+ installed (for Lean data storage)
- [ ] IBKR Gateway installed and configured
- [ ] Network firewall rules configured (ports: 4001 for IB Gateway)

**Credentials & API Keys:**
- [ ] Polygon API key obtained and validated
- [ ] FMP API key obtained (free tier)
- [ ] IBKR paper trading account created ($500+ equity)
- [ ] IBKR API permissions enabled
- [ ] All credentials stored in secure key vault (not in code)

**Data Bootstrap:**
- [ ] Historical data downloaded (2 years options chains)
- [ ] Earnings calendar populated (next 3 months)
- [ ] Data quality validation passed (DTqc001A-004A)
- [ ] Realized volatility baseline calculated

**Testing:**
- [ ] All unit tests passing (173+ tests)
- [ ] Integration tests passing
- [ ] Paper trading validated (minimum 2 weeks)
- [ ] Performance benchmarks met (<100ms per signal evaluation)

### 3.2 Deployment Steps

**Day 1: Infrastructure Setup**
```bash
# 1. Deploy application
git clone https://github.com/your-org/alaris.git
cd alaris
dotnet restore
dotnet build --configuration Release

# 2. Configure environment
export POLYGON_API_KEY="your_key_here"
export FMP_API_KEY="your_key_here"
export IB_HOST="127.0.0.1"
export IB_PORT="4001"
export IB_CLIENT_ID="1"

# 3. Initialize database
dotnet ef database update --project Alaris.Lean

# 4. Bootstrap historical data
dotnet run --project Alaris.Data.Bootstrap -- \
  --start-date 2023-01-01 \
  --end-date 2025-01-01 \
  --symbols AAPL,MSFT,GOOGL

# 5. Validate data quality
dotnet test Alaris.Test --filter Category=DataQuality
```

**Day 2-14: Paper Trading**
```bash
# Start paper trading mode
dotnet run --project Alaris.Lean -- \
  --mode paper \
  --start-cash 100000 \
  --log-level Information
```

**Day 15+: Live Trading (Gradual Scale-Up)**
```bash
# Week 1: $10,000 capital
# Week 2: $25,000 capital
# Week 3: $50,000 capital
# Week 4+: Full capital allocation
```

### 3.3 Post-Deployment Validation

Within 24 hours of deployment, verify:
- [ ] Polygon data updates occurring (daily 5:00 PM ET)
- [ ] Earnings calendar refreshing (daily 6:00 AM ET)
- [ ] IBKR connection stable (ping every 60 seconds)
- [ ] Strategy signals generating correctly
- [ ] All metrics publishing to monitoring dashboard
- [ ] Alert notifications functioning

---

## 4. Monitoring & Alerting

### 4.1 System Health Metrics

**Real-Time Monitoring (every 60 seconds):**

| Metric | Normal Range | Warning Threshold | Critical Threshold |
|--------|--------------|-------------------|-------------------|
| API response time (Polygon) | <500ms | >2s | >5s |
| API response time (FMP) | <1s | >3s | >10s |
| IBKR Gateway connection | Connected | Disconnected >30s | Disconnected >2min |
| Data validation pass rate | >98% | <95% | <90% |
| Memory usage | <4GB | >6GB | >7GB |
| CPU usage | <40% | >70% | >90% |

**Daily Monitoring (at market close):**

| Metric | Normal Range | Investigation Trigger |
|--------|--------------|----------------------|
| Signals generated | 0-10 per day | >20 per day |
| Data quality failures | 0-2 per day | >5 per day |
| Unfilled orders | <10% | >25% |
| Execution slippage | <1% | >3% |
| Position P&L deviation | ±5% of expected | >10% deviation |

### 4.2 Trading Metrics

**Strategy Performance (weekly review):**

| Metric | Target | Action if Below Target |
|--------|--------|------------------------|
| Atilgan signal precision | >60% | Review term structure thresholds |
| Fill rate (within 1 hour) | >80% | Widen limit price buffers |
| Average execution cost | <2% of spread | Review snapshot timing |
| IV crush capture rate | >50% | Validate Leung-Santoli model |
| Production validation pass | >70% | Review validator thresholds |

### 4.3 Alert Configuration

**Critical Alerts (immediate action required):**
- IBKR Gateway disconnected >2 minutes
- Data feed outage >15 minutes
- System exception in production
- Position loss >5% in single day
- Margin call received
- Order execution failure >3 consecutive times

**Warning Alerts (review within 1 hour):**
- Data quality validation failure rate >5%
- API rate limit exceeded
- Unusual signal volume (>15 per day)
- Memory usage >6GB
- Execution slippage >2%

**Informational Alerts (daily digest):**
- Daily performance summary
- Data quality report
- Unfilled orders summary
- System resource usage

### 4.4 Monitoring Implementation

**Recommended Stack:**
- **Application Metrics**: Prometheus + Grafana
- **Log Aggregation**: Seq or ELK Stack
- **Alerting**: PagerDuty or custom email/SMS
- **Uptime Monitoring**: Pingdom or UptimeRobot

**Logging Standards:**
```csharp
// All components must log using structured logging
_logger.LogInformation(
    "Signal generated for {Symbol}: {Signal} | IV/RV={IvRvRatio:F3}",
    symbol,
    signal.Strength,
    signal.IvRvRatio
);

// Include correlation IDs for tracing
using (_logger.BeginScope("TradeId={TradeId}", tradeId))
{
    // All log entries will include TradeId
}
```

---

## 5. Risk Controls & Circuit Breakers

### 5.1 Position Limits

**Per-Symbol Limits:**
| Parameter | Limit | Enforcement |
|-----------|-------|-------------|
| Maximum contracts per spread | 10 | Hard limit (pre-trade) |
| Maximum notional per position | $25,000 | Hard limit (pre-trade) |
| Maximum loss per position | $5,000 | Monitor (post-trade) |
| Maximum daily loss per symbol | $10,000 | Circuit breaker |

**Portfolio Limits:**
| Parameter | Limit | Enforcement |
|-----------|-------|-------------|
| Maximum total positions | 5 concurrent | Hard limit (pre-trade) |
| Maximum portfolio allocation | 80% of capital | Hard limit (pre-trade) |
| Maximum daily loss (portfolio) | $15,000 | Circuit breaker |
| Maximum weekly loss (portfolio) | $30,000 | Circuit breaker |
| Minimum cash reserve | 20% of capital | Hard limit (pre-trade) |

### 5.2 Circuit Breakers

**Automatic Trading Halt Conditions:**

1. **Daily Loss Circuit Breaker**
   - Trigger: Portfolio loss exceeds $15,000 in single day
   - Action: Immediately halt all new positions
   - Recovery: Manual review + approval required
   - Logging: `EVAD001A` (Audit logger)

2. **Execution Failure Circuit Breaker**
   - Trigger: >5 consecutive order execution failures
   - Action: Halt trading, investigate IBKR connectivity
   - Recovery: Automatic retry after 15 minutes if connectivity restored

3. **Data Quality Circuit Breaker**
   - Trigger: >10% data validation failure rate
   - Action: Halt signal generation, use cached data only
   - Recovery: Automatic when validation pass rate >95%

4. **Volatility Spike Circuit Breaker**
   - Trigger: VIX >40 or symbol RV >2× normal
   - Action: Increase execution cost buffers by 50%
   - Recovery: Automatic when conditions normalize

5. **Market-Wide Circuit Breaker**
   - Trigger: NYSE/NASDAQ trading halt
   - Action: Cancel all pending orders, close IB connection
   - Recovery: Manual review after market resumes

### 5.3 Pre-Trade Compliance Checks

Every order must pass these checks before submission:

**Component:** `STCS006A` (Execution Cost Survival)
```csharp
// Verify post-cost IV/RV ratio still meets threshold
if (postCostIvRvRatio < 1.20m)
    return ValidationResult.Reject("Execution cost too high");
```

**Component:** `STHD003A` (Gamma Risk)
```csharp
// Verify delta-neutral positioning
if (Math.Abs(position.Delta) > 0.10m)
    return ValidationResult.Reject("Excessive directional exposure");
```

**Component:** `STHD001A` (Vega Independence)
```csharp
// Verify front/back month vega correlation acceptable
if (vegaCorrelation > 0.70m)
    return ValidationResult.Reject("Insufficient vega hedging");
```

**Component:** `STCS008A` (Liquidity Assurance)
```csharp
// Verify order size within liquidity bounds
if (volumeRatio > 0.05m || oiRatio > 0.02m)
    return ValidationResult.Reject("Insufficient liquidity");
```

### 5.4 Post-Trade Monitoring

**Position Health Checks (every 15 minutes):**
- Unrealized P&L vs expected
- Delta drift from target
- IV surface changes
- Time decay progression

**Daily Risk Report (at market close):**
- Portfolio Greeks (delta, gamma, vega, theta)
- Position concentration by symbol/sector
- Cash utilization and margin headroom
- Open position summary

---

## 6. Data Quality Standards

### 6.1 Data Validation Rules

**Price Reasonableness (`DTqc001A`):**
```
Checks:
✓ Spot price within ±10% of previous close
✓ Option bid > 0 and bid < ask
✓ Option ask < intrinsic value + $10
✓ No stale timestamps (>1 hour old)

Failure action: Reject data point, log warning
```

**IV Arbitrage Detection (`DTqc002A`):**
```
Checks:
✓ Put-call parity holds within 2%
✓ Calendar spread IV differences reasonable
✓ No butterfly arbitrage opportunities >$0.50

Failure action: Flag for review, use alternative pricing
```

**Volume/OI Validation (`DTqc003A`):**
```
Checks:
✓ Volume > 0 for liquid options (OI > 100)
✓ Volume within 10× of 30-day average
✓ OI change consistent with volume

Failure action: Mark as illiquid, increase execution cost buffer
```

**Earnings Date Verification (`DTqc004A`):**
```
Checks:
✓ Earnings date confirmed from 2+ sources
✓ Date within next 90 days
✓ No conflicting dates in recent history

Failure action: Reject signal, manual verification required
```

### 6.2 Data Freshness Requirements

| Data Type | Maximum Age | Refresh Frequency | Stale Data Action |
|-----------|-------------|-------------------|-------------------|
| Spot prices | 15 minutes | Real-time stream | Use last known good |
| Options chains | 24 hours | Daily at 5 PM ET | Skip signal generation |
| Earnings dates | 7 days | Daily at 6 AM ET | Manual verification |
| Risk-free rates | 7 days | Daily at 8 AM ET | Use previous day |
| Historical OHLCV | 48 hours | Daily at 5 PM ET | Skip RV calculation |

### 6.3 Data Retention Policy

**Hot Storage (PostgreSQL):**
- Options chains: 90 days
- Trade history: 2 years
- Signal history: 1 year
- System logs: 30 days

**Warm Storage (Parquet files):**
- Historical options chains: 2 years
- OHLCV bars: 5 years
- Archived trade data: 5 years

**Cold Storage (S3/Glacier):**
- All historical data: Indefinite
- Compliance records: 7 years (regulatory requirement)

---

## 7. Incident Response

### 7.1 Severity Levels

| Severity | Definition | Response Time | Escalation |
|----------|------------|---------------|------------|
| **P0 - Critical** | Trading halted, capital at risk | Immediate | CTO + Trading desk |
| **P1 - High** | Degraded performance, orders failing | <15 minutes | Lead developer |
| **P2 - Medium** | Data quality issues, non-critical bugs | <1 hour | On-call engineer |
| **P3 - Low** | Minor issues, monitoring alerts | <4 hours | Regular sprint |

### 7.2 Incident Response Workflow

**Detection:**
1. Alert triggered via monitoring system
2. On-call engineer paged (P0/P1) or notified (P2/P3)
3. Incident ticket created automatically

**Triage:**
1. Assess severity and impact
2. Determine if trading halt required
3. Begin incident log (timestamp all actions)

**Mitigation:**
1. **P0**: Immediately halt trading, liquidate positions if capital at risk
2. **P1**: Implement temporary workaround, escalate if needed
3. **P2/P3**: Schedule fix in next sprint

**Resolution:**
1. Root cause analysis completed
2. Fix implemented and tested
3. Post-incident review scheduled
4. Documentation updated

**Communication:**
1. Status updates every 30 minutes (P0), 2 hours (P1)
2. Post-mortem report within 48 hours
3. Lessons learned shared with team

### 7.3 Common Incident Scenarios

**Scenario 1: IBKR Gateway Disconnection**
```
Symptoms: Order execution failures, connection errors
Impact: Cannot place or monitor orders
Actions:
1. Check IB Gateway process status
2. Restart IB Gateway if needed
3. Re-authenticate if credentials expired
4. Verify network connectivity
5. Switch to backup IB connection if available

Recovery Time: 2-5 minutes
Post-Incident: Review connection stability logs
```

**Scenario 2: Polygon API Rate Limit**
```
Symptoms: HTTP 429 errors, data retrieval failures
Impact: Cannot refresh market data
Actions:
1. Check current API usage vs limit
2. Implement exponential backoff
3. Use cached data temporarily
4. Consider upgrading Polygon tier

Recovery Time: 15 minutes (rate limit reset)
Post-Incident: Optimize API call patterns
```

**Scenario 3: Data Quality Validation Failures**
```
Symptoms: >5% of data points failing validation
Impact: Signal generation impaired
Actions:
1. Identify which validator is failing (DTqc001A-004A)
2. Investigate data source (Polygon vs IBKR)
3. Use alternative data source if available
4. Widen validation thresholds temporarily

Recovery Time: 30 minutes
Post-Incident: Adjust validation rules if false positives
```

**Scenario 4: Unexpected Loss**
```
Symptoms: Position loss exceeds expected max drawdown
Impact: Capital erosion
Actions:
1. Immediately halt new position entries
2. Review open positions for mishedging
3. Calculate Greeks and verify risk exposure
4. Consider early exit if position thesis invalidated
5. Document market conditions and strategy performance

Recovery Time: Manual review (1-2 hours)
Post-Incident: Strategy parameter recalibration
```

### 7.4 Runbook: System Restart

**When to use:** After P0/P1 incidents, planned maintenance, or infrastructure changes

**Prerequisites:**
- [ ] All open positions documented
- [ ] IBKR Gateway running and authenticated
- [ ] Network connectivity verified
- [ ] Data sources accessible (Polygon, FMP)

**Steps:**
```bash
# 1. Graceful shutdown
cd /opt/alaris
dotnet Alaris.Lean.dll --shutdown-graceful

# 2. Verify clean exit
tail -n 50 /var/log/alaris/application.log

# 3. Clear temporary cache
rm -rf /tmp/alaris-cache/*

# 4. Start services
systemctl start postgresql
systemctl start alaris-lean

# 5. Verify startup
curl http://localhost:8080/health
# Expected: { "status": "healthy", "timestamp": "..." }

# 6. Check data feeds
dotnet run --project Alaris.Test -- \
  --filter Category=DataFeedHealth

# 7. Resume trading (if paper trading passed)
dotnet Alaris.Lean.dll --mode live
```

**Post-Restart Validation:**
- [ ] All services reporting healthy
- [ ] Data feeds updating
- [ ] Open positions reconciled with IBKR
- [ ] First signal generation successful

---

## 8. Backup & Recovery

### 8.1 Backup Schedule

**Daily Backups (automated):**
- PostgreSQL database (full backup at 2 AM ET)
- Configuration files
- Strategy parameters
- Trade history

**Weekly Backups (automated):**
- Complete system snapshot
- Historical data files (Parquet)
- Application binaries

**Monthly Backups (manual):**
- Compliance records archive
- Performance reports
- Incident logs

### 8.2 Disaster Recovery

**Recovery Time Objective (RTO):** 4 hours  
**Recovery Point Objective (RPO):** 24 hours

**Recovery Procedures:**

**Scenario: Database Corruption**
```bash
# 1. Stop application
systemctl stop alaris-lean

# 2. Restore from latest backup
pg_restore -d alaris /backups/alaris_YYYYMMDD.dump

# 3. Verify data integrity
psql -d alaris -f /scripts/verify_integrity.sql

# 4. Restart application
systemctl start alaris-lean
```

**Scenario: Complete System Failure**
```bash
# 1. Provision new instance
# 2. Deploy from version control
git clone https://github.com/your-org/alaris.git
dotnet restore && dotnet build

# 3. Restore database
pg_restore -d alaris /backups/latest/alaris.dump

# 4. Restore configuration
cp /backups/latest/config/* /opt/alaris/config/

# 5. Restart all services
./scripts/start-all-services.sh

# 6. Verify system health
./scripts/health-check.sh
```

### 8.3 Position Reconciliation

**After any service disruption:**

1. **Retrieve IBKR positions:**
```csharp
var ibPositions = await _ibClient.GetAllPositions();
```

2. **Compare with Alaris records:**
```csharp
var alarisPositions = await _portfolioManager.GetOpenPositions();
var discrepancies = ReconcilePositions(ibPositions, alarisPositions);
```

3. **Resolve discrepancies:**
- If IB has position not in Alaris → Import to Alaris
- If Alaris has position not in IB → Mark as closed
- If quantities differ → Use IB as source of truth

4. **Document all discrepancies:**
```csharp
foreach (var discrepancy in discrepancies)
{
    _auditLogger.LogWarning(
        "Position reconciliation discrepancy: {Symbol} | " +
        "IB Qty: {IbQty} | Alaris Qty: {AlarisQty}",
        discrepancy.Symbol,
        discrepancy.IbQuantity,
        discrepancy.AlarisQuantity
    );
}
```

---

## 9. Change Management

### 9.1 Code Deployment Process

**All production changes must follow this process:**

1. **Development:**
   - Feature branch from `main`
   - All tests passing locally
   - Code review by senior developer

2. **Testing:**
   - Merge to `staging` branch
   - Deploy to paper trading environment
   - Run for minimum 48 hours
   - Verify no regressions

3. **Approval:**
   - Trading performance reviewed
   - Risk metrics validated
   - Change approval documented

4. **Production Deployment:**
   - Deploy during off-market hours
   - Gradual rollout (10% → 50% → 100%)
   - Monitor closely for 24 hours

5. **Rollback Plan:**
   - Keep previous version available
   - Automated rollback if alerts triggered
   - Document rollback procedures

### 9.2 Configuration Changes

**Parameter tuning requires:**
- [ ] Backtest with new parameters (minimum 6 months)
- [ ] Paper trading validation (minimum 2 weeks)
- [ ] Documented justification (academic reference or empirical data)
- [ ] Version control of all parameter changes

**Examples of configuration changes:**
- Atilgan threshold adjustments (IV/RV ratio, term slope)
- Execution cost buffers
- Position sizing parameters (Kelly fraction)
- Validation thresholds (liquidity, vega correlation)

---

## 10. Compliance & Audit

### 10.1 Regulatory Requirements

**SEC Regulations (if registered):**
- Books and records retention (7 years)
- Trade confirmations and statements
- Best execution documentation
- Algorithm testing and monitoring

**FINRA Requirements (if applicable):**
- Algorithmic trading supervision
- Risk management controls
- Business continuity planning

### 10.2 Audit Trail

**Every trade must be traceable:**
```
Signal Generation → Production Validation → Execution Decision → 
Order Submission → Fill Confirmation → P&L Attribution
```

**Logged via:** `Alaris.Events` (EVAD001A, EVIF002A)

**Audit log retention:** 7 years (cold storage)

### 10.3 Internal Audit Schedule

**Daily:**
- Trade blotter review
- Position reconciliation
- P&L explanation

**Weekly:**
- Strategy performance review
- Risk metrics review
- Data quality summary

**Monthly:**
- Compliance review
- Incident log review
- System performance review

**Quarterly:**
- External audit (if required)
- Strategy recalibration
- Infrastructure review

---

## 11. Performance Optimization

### 11.1 System Performance Targets

| Metric | Target | Current | Action if Below Target |
|--------|--------|---------|------------------------|
| Signal evaluation time | <100ms | TBD | Profile and optimize hot paths |
| Order execution latency | <500ms | TBD | Review network path |
| Data refresh latency | <2s | TBD | Optimize API calls |
| Memory footprint | <4GB | TBD | Investigate memory leaks |
| CPU utilization | <40% | TBD | Review algorithm complexity |

### 11.2 Optimization Guidelines

**Hot Path Optimization:**
- Yang-Zhang RV calculation
- Term structure analysis
- Option pricing (Healy 2021 double boundary)
- Production validation checks

**Profiling Tools:**
- BenchmarkDotNet for microbenchmarks
- dotMemory for memory profiling
- dotTrace for CPU profiling

**Performance Rules (from High-Integrity Coding Standard):**
- Zero allocation in hot paths (Rule 5)
- ArrayPool for temporary buffers
- Span<T> for stack allocations
- Avoid LINQ in performance-critical sections

---

## 12. Contact Information

**On-Call Rotation:**
- Primary: [Lead Developer]
- Secondary: [Senior Engineer]
- Escalation: [CTO]

**Emergency Contacts:**
- IBKR Support: 1-877-442-2757
- Polygon Support: support@polygon.io
- Infrastructure Provider: [Provider Contact]

**Internal Escalation Path:**
```
P3 → On-call engineer
P2 → On-call engineer → Lead developer (if unresolved in 1 hour)
P1 → On-call engineer → Lead developer → CTO (if unresolved in 30 min)
P0 → Immediate: On-call + Lead + CTO
```

---

## 13. Document Maintenance

**Review Schedule:**
- Quarterly review of all procedures
- Update after any P0/P1 incident
- Annual comprehensive review

**Version Control:**
- All changes tracked in Git
- Change log maintained in this document
- Approval required for substantive changes

**Change Log:**

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-12-03 | 1.0 | Initial operational governance document | Claude Code |