# Alaris Update Log

Project changelog with completed items collapsed and active/future items detailed.

---

## Active Development

### Trading Calendar Integration ✅ (2025-12-11)

**Status**: Complete

Created NYSE trading calendar (`STCL001A`) and standardized time-to-expiry across all components.

**Files Changed**: STCL001A.cs, STMG001A.cs, STIV001A-005A.cs, STCR001A.cs, STHD009A.cs, TSUN022A.cs

---

## Future Development (Deferred)

### Near-Expiry Boundary Extrapolation

> [!NOTE]
> Optional enhancement for strategies other than the current earnings spread strategy.
> The current `DBEX001A` intrinsic fallback is sufficient for earnings plays.

**Problem**: As T → 0, QD+ asymptotic expansion degrades. Current approach uses intrinsic value fallback with σ√T-scaled boundaries, but this creates a discontinuity.

**Proposed Enhancements**:

#### 1. Volatility-Dependent Threshold

Replace fixed threshold with σ-dependent:

```
T_thresh = max(3/252, (σ_min / σ)² × 3/252)
```

where σ_min ≈ 0.10 (low-vol reference). High-vol positions can use QD+ closer to expiry.

#### 2. C¹ Blending with Cubic Hermite Spline

Replace linear blending with cubic Hermite for smooth derivative continuity:

```csharp
// Current (C⁰ only):
// result = α * qd + (1-α) * intrinsic

// Proposed (C¹):
double H0 = (1 + 2*t) * (1-t)² ;  // Hermite basis
double H1 = t² * (3 - 2*t);
result = H0 * intrinsic + H1 * qd + derivatives...
```

**Benefits**:
- Continuous delta (∂V/∂S) at handoff
- Continuous theta (∂V/∂t) at handoff
- Eliminates gamma spikes near threshold

#### 3. Greeks Validation Suite

New test class `TSUN023A` to verify:
- Delta continuity across T_thresh
- Theta continuity across T_thresh
- Gamma behaviour in blending zone
- Vega decay near expiry

**Estimated Effort**: 3-5 days

**Priority**: Low (only needed for non-earnings strategies)

---

## Completed Phases

<details>
<summary><strong>Phase 5: Trading Calendar (2025-12-11)</strong></summary>

- Created `STCL001A.cs` NYSE calendar using QuantLib
- Updated `STMG001A` with `ITradingCalendar` injection
- Replaced 15 occurrences of `dte/252.0` with `TradingCalendarDefaults.DteToYears()`
- Added `TSUN022A.cs` tests (10 tests)

</details>

<details>
<summary><strong>Phase 4: Near-Expiry Stability (2025-12-11)</strong></summary>

- Integrated `DBEX001A` into `DBSL001A.Solve()`
- Added σ√T scaling for near-expiry boundaries
- Created `STMG001A` maturity guard (entry/exit filters)

</details>

<details>
<summary><strong>Phase 3: First-Principles Tests (2025-12-10)</strong></summary>

- Created `TSUN021A.cs` with 33 mathematically-derived tests
- Validated boundary ordering, lambda roots, monotonicity, intrinsic floor
- Added extreme parameter and delta continuity tests

</details>

<details>
<summary><strong>Phase 2: Compliance Hardening (2025-11-21)</strong></summary>

- Rule 13 (Function Complexity): 6 methods refactored, 291 lines extracted
- Rule 9 (Guard Clauses): 1 violation fixed in `DBSL001A`
- Created 14 focused helper methods

</details>

<details>
<summary><strong>Phase 1: Compliance Foundation (2025-11-21)</strong></summary>

- Rule 4 (No Recursion): Verified compliant
- Rule 7 (Null Safety): Nullable enabled, zero suppressions
- Rule 10 (Specific Exceptions): 5 violations fixed
- Rule 15 (Fault Isolation): SafeLog pattern implemented

</details>

---

**Last Updated**: 2025-12-11
