# Alaris Structural Compliance Standard

**Version:** 2.0  
**Date:** January 2026  

---

## 1. Scope
Mandatory structural and naming conventions for the Alaris quantitative finance system.

## 2. Directory Structure

| Directory | Domain | Description |
|-----------|--------|-------------|
| `Alaris.Core` | Core | Mathematical primitives, pricing engines, spectral methods |
| `Alaris.Strategy` | Strategy | Trading strategy, signal generation, risk management |
| `Alaris.Infrastructure` | Infrastructure | Data acquisition, events, protocol serialization |
| `Alaris.Host` | Host | CLI, TUI, LEAN integration |
| `Alaris.Simulation` | Simulation | Monte Carlo simulations |
| `Alaris.Governance` | Governance | Compliance documentation |
| `Alaris.Test` | Test Suite | Unit, integration, benchmark tests |
| `Alaris.Lean` | LEAN | QuantConnect engine (external) |

## 3. Component Naming Convention

**Format:** `[Domain][Category][Sequence][Variant]`

**Example:** `CREN004A`
- **Domain:** `CR` (Core)
- **Category:** `EN` (Engine)
- **Sequence:** `004`
- **Variant:** `A` (Primary)

### 3.1 Domain Codes
| Code | Domain |
|------|--------|
| `CR` | Core |
| `ST` | Strategy |
| `DT` | Data |
| `EV` | Events |
| `PL` | Protocol |
| `AP` | Application |
| `TS` | Test Suite |

### 3.2 Variant Codes
| Code | Meaning |
|------|---------|
| `A` | Primary |
| `B` | Alternative |
| `X` | Experimental |

---

## 4. Component Registry

### 4.1 Alaris.Core

#### Pricing (Alaris.Core/Pricing/)
| Component | Description | Reference |
|-----------|-------------|-----------|
| `CREN001A` | Base Crank-Nicolson FD engine | Wilmott (2006) |
| `CREN002A` | Enhanced FD (ASINH grid) | Healy (2021) |
| `CREN003A` | Unified American pricing (default) | Spectral + FD |
| `CREN004A` | Spectral collocation engine | Andersen-Lake (2016) |
| `CRAP001A` | QD+ boundary approximation | Healy (2021) |

#### Math (Alaris.Core/Math/)
| Component | Description | Reference |
|-----------|-------------|-----------|
| `CRMF001A` | BS pricing, IV, Greeks | Black-Scholes (1973) |
| `CRCH001A` | Chebyshev nodes, interpolation | Trefethen (2000) |
| `CRGQ001A` | Gauss quadrature (Legendre, Laguerre) | Davis (1984) |

### 4.2 Alaris.Strategy

#### Core
| Component | Description |
|-----------|-------------|
| `STCR001A` | Signal generation (Atilgan) |
| `STRK001A` | Kelly position sizing |
| `STMG001A` | Maturity guard |
| `STBR001A` | Pricing bridge (uses CREN003A) |

#### Risk/Hedging
| Component | Description |
|-----------|-------------|
| `STHD003A` | Gamma risk manager |
| `STHD007B` | Rule-based exit monitor |
| `STHD009A` | Pin risk monitor |
| `STKF001A` | Kalman-filtered volatility |

### 4.3 Alaris.Infrastructure

#### Protocol (Alaris.Infrastructure/Protocol/Workflow/)
| Component | Description | Reference |
|-----------|-------------|-----------|
| `PLWF001A` | FSM engine for workflow routing | Hopcroft et al. (2006) |
| `PLWF002A` | Backtest workflow definition | DFA formalism |
| `PLWF003A` | Trading workflow definition | DFA formalism |
| `PLBF001A` | Buffer pool manager | Zero-allocation patterns |

#### SBE Schemas (Alaris.Infrastructure/Protocol/Schemas/)
| Schema | Description |
|--------|-------------|
| `Workflow.xml` | FSM state transition protocol |
| `MarketData.xml` | Market data message formats |
| `Session.xml` | Backtest session management |
| `Events.xml` | Domain event serialization |

### 4.4 Alaris.Test

| Category | Prefix | Coverage |
|----------|--------|----------|
| Unit | `TSUN` | Component-level |
| Integration | `TSIN` | Pipeline |
| Benchmark | `TSBM` | Performance |
| Diagnostic | `TSDG` | Constraints |

**Statistics (2026-01-02):**
- Total tests: 916 (was 853)
- FSM tests: 29 (new)
- Spectral engine tests: 112
- All passing 

---

## 5. Spectral Engine (CREN004A)

### Performance
| Metric | Spectral | FD | Ratio |
|--------|----------|------|-------|
| Throughput | 2,834/sec | 1,006/sec | 2.8x |

### Known Limitations
| Condition | Impact |
|-----------|--------|
| σ > 100% | NaN (use FD fallback) |
| Deep ITM (K≥120) | +1.0 deviation max |

### Validation
- 112 tests covering correctness, invariants, concurrency, stress
- RMSE vs FD: 0.35 (concentrated near exercise boundary)
- Directional bias: Spectral higher for ITM (conservative for short premium)
- Use-case validated: Earnings vol calendar spreads

---

## 6. Compliance
All changes must adhere to this standard. Non-compliant components rejected during review.
