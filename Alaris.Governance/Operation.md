# Alaris Operational Runbook

**Version:** 2.0  
**Date:** January 2026  

---

## 1. Quick Reference

### Key Commands
```bash
# Run backtest
alaris backtest run --symbol AAPL --start 2024-01-01 --end 2024-12-31

# Bootstrap data
alaris data bootstrap --symbol AAPL --days 365

# System health
alaris system health

# Run tests
dotnet test Alaris.Test
```

### Critical Thresholds
| Metric | Warning | Critical |
|--------|---------|----------|
| API latency | >2s | >5s |
| Data validation | <95% | <90% |
| Memory | >6GB | >7GB |

---

## 2. Deployment

### Prerequisites
- .NET 10.0+ runtime
- IBKR Gateway (port 4001)
- API keys: `POLYGON_API_KEY`, `FMP_API_KEY`

### Startup Sequence
```bash
dotnet restore && dotnet build --configuration Release
export POLYGON_API_KEY="..."
dotnet run --project Alaris.Host
```

---

## 3. Monitoring

### Automated Checks (Built into CLI)
| Check | Frequency | Action on Failure |
|-------|-----------|-------------------|
| IBKR connection | 60s | Alert + retry |
| Data freshness | 15min | Use cached |
| Position reconciliation | Daily | Manual review |

### Alert Escalation
- **P0 (Capital at risk)**: Immediate halt, manual intervention
- **P1 (Degraded)**: Auto-retry 15 minutes
- **P2 (Warning)**: Daily digest

---

## 4. Risk Controls

### Position Limits
| Parameter | Limit |
|-----------|-------|
| Max contracts per spread | 10 |
| Max notional per position | $25,000 |
| Max daily loss (portfolio) | $15,000 |
| Cash reserve | 20% minimum |

### Circuit Breakers
| Trigger | Action |
|---------|--------|
| Daily loss > $15K | Halt new positions |
| VIX > 40 | Increase cost buffers 50% |
| 5+ execution failures | Halt, investigate |

---

## 5. Incident Response

### Common Issues
| Issue | Resolution |
|-------|------------|
| IBKR disconnection | Restart Gateway, re-auth |
| API rate limit | Wait 15min, reduce frequency |
| Data validation failures | Check source, widen thresholds |

### System Restart
```bash
# Graceful shutdown
dotnet Alaris.Host.dll --shutdown-graceful

# Clear cache and restart
rm -rf /tmp/alaris-cache/*
dotnet run --project Alaris.Host
```

---

## 6. Backup & Recovery

| Backup | Schedule | Retention |
|--------|----------|-----------|
| Database | Daily 2AM | 90 days |
| Config | On change | 1 year |
| Trade history | Daily | 5 years |

**RTO:** 4 hours | **RPO:** 24 hours

---

## 7. Pre-Trade Validation

All orders validated by:
- `STCS006A` - Cost survival (IV/RV > 1.20 post-cost)
- `STHD003A` - Gamma risk (|Δ| < 0.10)
- `STCS008A` - Liquidity (volume ratio < 5%)

---

## 8. Data Quality

| Validator | Check |
|-----------|-------|
| `DTqc001A` | Spot ±10% of previous close |
| `DTqc002A` | Put-call parity within 2% |
| `DTqc003A` | Volume within 10× average |
| `DTqc004A` | Earnings date from 2+ sources |

---

**Last Updated:** 2026-01-01