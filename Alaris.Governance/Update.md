# Alaris Development Log

**Current Version:** 2.0  
**Date:** 2026-01-01  

---

## Active Development

### Spectral Collocation Engine (CREN004A) — Complete

**Status:** Production Ready

Implemented spectral collocation American option pricing engine based on Andersen-Lake-Offengenden (2016).

**Key Metrics:**
- Performance: 2.8x faster than FD
- Test coverage: 112 tests
- RMSE vs FD: 0.35

**Error Profile (Characterized):**
| Region | Deviation | Direction |
|--------|-----------|-----------|
| ATM | ±0.01 | Neutral |
| ITM (K≥110) | +0.2 to +1.0 | Spectral higher |

**Known Limitations:**
- σ > 100%: NaN (fallback to FD)
- Validated for: Earnings vol calendar spreads

---

## Future Development

### End-to-End Integration Tests

**Priority:** Medium  
**Effort:** 5-7 days

Create `TSIN006A` testing full backtest pipeline with mock data.

<details>
<summary>Details</summary>

Test scenarios:
- `HappyPath_SingleSymbol` - Full pipeline
- `EarningsEvent_SignalGeneration` - STCR001A → STLN001A
- `HighVolatility_PositionSizing` - STRK001A, STMG001A
- `NearExpiry_BoundaryHandling` - Near-expiry blending

</details>

---

### Proposed Improvements (Deferred)

#### Volatility-Dependent Threshold

**Assessment:** Not recommended

The current fixed threshold (T < 3 DTE) is adequate for the earnings strategy use case. A σ-dependent threshold adds complexity without material benefit because:
1. Earnings vol strategies rarely encounter extreme low-vol scenarios
2. The spectral engine handles near-expiry robustly already
3. Implementation cost outweighs marginal improvement

#### C¹ Blending (Cubic Hermite)

**Assessment:** Not recommended

The current linear blending (C⁰) is sufficient because:
1. The transition region is small (2-3 trading days)
2. Greeks in this region are dominated by intrinsic anyway
3. Gamma spikes near expiry are expected behavior, not a bug
4. No observed production issues from current approach

---

## Completed Development

<details>
<summary><strong>Codebase Cleanup (2026-01-01)</strong></summary>

- Removed `Alaris.Double/` (6 files)
- Removed `Alaris.Quantlib/`
- Migrated `SMSM001A`, `STBR001A` to CREN003A
- Cleaned stale project references
- 853 tests passing

</details>

<details>
<summary><strong>Greek Calculation Optimization (2025-12-25)</strong></summary>

- Created `STBR003A.cs` with cached QuantLib infrastructure
- 91% allocation reduction (165 → 15 per option)

</details>

<details>
<summary><strong>Test Coverage Expansion (2025-12-11)</strong></summary>

- Added 314 test cases (8 new files)
- Improved ratio from 3.2:1 to 2.1:1
- All tests use in-language mocking

</details>

<details>
<summary><strong>Trading Calendar (2025-12-11)</strong></summary>

- Created `STCL001A.cs` NYSE calendar
- Replaced 15 hardcoded `dte/252.0` calls

</details>

<details>
<summary><strong>Compliance Phases 1-2 (2025-11-21)</strong></summary>

- Rule 13 (Complexity): 6 methods refactored
- Rule 9 (Guard Clauses): Fixed in DBSL001A
- Rule 10 (Exceptions): 5 violations fixed

</details>

---

**Last Updated:** 2026-01-01
