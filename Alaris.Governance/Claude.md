# Alaris Project Structure

## Directory Layout

```
Alaris/
├── Alaris.Double/          Negative Rate American Option Pricing
│   ├── DBAP001A.cs         (Quasi-Analytic Approximation - QD+)
│   ├── DBAP002A.cs         (Double Boundary Approximation)
│   ├── DBEN001A.cs         (Double Boundary Engine)
│   ├── DBSL001A.cs         (Double Boundary Solver)
│   ├── DBSL002A.cs         (Kim Integral Solver)
│   └── DBEX001A.cs         (Near-Expiry Stability Handler) [NEW]
│
├── Alaris.Strategy/        Trading Strategy Implementation
│   ├── Core/
│   │   ├── Numerical/
│   │   │   ├── STPR002A.cs     (Adaptive Integration)
│   │   │   ├── STPR004A.cs     (Levenberg-Marquardt Optimizer)
│   │   │   └── STPR005A.cs     (Differential Evolution Optimizer)
│   │   ├── STIV001A.cs         (Heston Model)
│   │   ├── STIV002A.cs         (Kou Jump-Diffusion Model)
│   │   ├── STIV004A.cs         (Leung-Santoli Model)
│   │   ├── STIV006A.cs         (Vol Surface Interpolator) [NEW]
│   │   ├── STTM002A.cs         (Earnings Regime)
│   │   ├── STIV005A.cs         (Earnings Jump Calibrator)
│   │   ├── STEJ001A.cs         (Earnings Jump Risk Calibrator) [NEW]
│   │   ├── STDD001A.cs         (Dividend Ex-Date Detector) [NEW]
│   │   ├── STCR005A.cs         (Signal Freshness Monitor) [NEW]
│   │   ├── STTM001A.cs         (Term Structure)
│   │   ├── STTM004A.cs         (Time Parameters)
│   │   ├── STCR003A.cs         (Yang-Zhang Estimator)
│   │   ├── STIV003A.cs         (IV Model Selector)
│   │   ├── STCR004A.cs         (Signal)
│   │   ├── STCR001A.cs         (Signal Generator)
│   │   ├── STTM003A.cs         (Trading Calendar)
│   │   ├── STPR003A.cs         (Heston Pricing)
│   │   └── STPR006A.cs         (Kou Pricing)
│   ├── Cost/               (Transaction Costs & Liquidity)
│   │   ├── STCS001A.cs         (IExecutionCostModel)
│   │   ├── STCS002A.cs         (OrderParameters)
│   │   ├── STCS003A.cs         (ExecutionCostResult)
│   │   ├── STCS004A.cs         (SpreadCostResult)
│   │   ├── STCS005A.cs         (ConstantFeeModel)
│   │   ├── STCS006A.cs         (SignalCostValidator)
│   │   ├── STCS007A.cs         (CostValidationResult)
│   │   ├── STCS008A.cs         (LiquidityValidator)
│   │   └── STCS009A.cs         (LiquidityResult)
│   ├── Hedging/            (Hedging & Production Safety)
│   │   ├── STHD001A.cs         (VegaCorrelationAnalyser + VIX threshold)
│   │   ├── STHD002A.cs         (VegaCorrelationResult)
│   │   ├── STHD003A.cs         (GammaRiskManager)
│   │   ├── STHD004A.cs         (GammaRiskAssessment)
│   │   ├── STHD005A.cs         (ProductionValidator)
│   │   ├── STHD006A.cs         (ProductionResult)
│   │   └── STHD009A.cs         (PinRiskMonitor) [NEW]
│   ├── Pricing/
│   │   └── STPR001A.cs         (Calendar Spread Valuation)
│   ├── Risk/
│   │   ├── STRK001A.cs         (Kelly Position Sizer + Net-of-Cost)
│   │   └── STRK002A.cs         (Position Size)
│   ├── Bridge/
│   │   ├── STBR001A.cs         (Unified Pricing Engine)
│   │   ├── STBR002A.cs         (Option Pricing Engine Interface)
│   │   └── STDT001A.cs         (Market Data Provider Interface)
│   ├── Model/
│   │   ├── STDT002A.cs         (Option Chain)
│   │   └── STDT003A.cs         (Option Parameters)
│   └── STCT001A.cs             (Strategy Orchestration)
│
├── Alaris.Events/          Event Sourcing & Audit Logging
│   ├── Core/
│   │   ├── EVCR001A.cs         (IEvent)
│   │   ├── EVCR002A.cs         (IEventStore)
│   │   ├── EVCR003A.cs         (EventEnvelope)
│   │   └── EVCR004A.cs         (IAuditLogger)
│   ├── Domain/
│   │   └── EVDM001A.cs         (Strategy Events)
│   └── Infrastructure/
│   │   ├── EVIF001A.cs         (InMemoryEventStore)
│   │   └── EVIF002A.cs         (InMemoryAuditLogger)
│
├── Alaris.Governance/      Governance & Compliance
│   ├── Compliance/         (Compliance Rules)
│   ├── Documentation/      (Project Documentation)
│   ├── StructureCompliance.md
│   ├── CodingCompliance.md
│   └── Claude.md           (This File)
│
├── Alaris.Quantlib/        Standard American option pricing (positive rates)
└── Alaris.Test/            Test suite (229+ tests)
```

---

## Current Compliance Status

### High-Integrity Coding Standard v1.2

Based on JPL Institutional Coding Standard (C), MISRA, RTCA DO-178B

| Rule | Description | Status |
|------|-------------|--------|
| **Rule 4** | No Recursion | COMPLIANT |
| **Rule 5** | Zero-Allocation Hot Paths | COMPLIANT (ArrayPool + Span<T>) |
| **Rule 7** | Null Safety | COMPLIANT |
| **Rule 8** | Limited Scope | COMPLIANT (init-only properties) |
| **Rule 9** | Guard Clauses | COMPLIANT |
| **Rule 10** | Specific Exceptions | COMPLIANT |
| **Rule 13** | Function Complexity (60 lines) | COMPLIANT |
| **Rule 14** | Clear LINQ | COMPLIANT (audited) |
| **Rule 15** | Fault Isolation | COMPLIANT |
| **Rule 16** | Deterministic Cleanup | COMPLIANT |
| **Rule 17** | Auditability | IMPLEMENTED (Alaris.Events) |

**CI Enforcement**: GitHub Actions workflow with 100+ Roslyn analyzers

---

## Alaris.Double Component

### Benchmark Accuracy: 0.00% error vs Healy (2021) Table 2

```
T=1:  (73.50, 63.50) - 0.00% error
T=5:  (71.60, 61.60) - 0.00% error
T=10: (69.62, 58.72) - 0.00% error
T=15: (68.00, 57.00) - 0.00% error
```

### Key Algorithms

**QD+ Approximation** (`DBAP001A.cs`):
- Super Halley iteration (3rd-order convergence)
- Calibrated initialization from Healy benchmarks
- Lambda root assignment: Upper uses negative root, Lower uses positive root

**Kim Solver** (`DBSL002A.cs`):
- FP-B' Stabilization (uses just-computed upper for lower calculation)
- Pre-iteration reasonableness checks
- Result validation against QD+ input

### Physical Constraints (Healy Appendix A)

All tests validate:
- A1: Boundaries positive (S_u, S_l > 0)
- A2: Upper > Lower (S_u > S_l)
- A3: Put boundaries < Strike
- A4/A5: Smooth pasting and delta continuity

---

## Alaris.Strategy Component

### Pricing Engine Integration

**STBR001A** provides:
- Automatic regime detection (positive rates vs negative rates vs double boundary)
- Complete Greeks: Delta, Gamma, Vega, Theta, Rho
- Calendar spread pricing with breakeven calculation
- Implied volatility via bisection

**Regime Detection**:
```csharp
if (riskFreeRate >= 0)
    return PricingRegime.PositiveRates;        // Alaris.Quantlib
else if (dividendYield < riskFreeRate)
    return PricingRegime.DoubleBoundary;       // Alaris.Double (q < r < 0)
else
    return PricingRegime.NegativeRatesSingleBoundary;  // Alaris.Quantlib
```

### IV Model Framework

**STIV003A** provides automatic model selection for earnings-driven volatility events:

**Available Models**:
- **Black-Scholes**: Baseline flat volatility model
- **Leung-Santoli (2014)**: Pre-earnings announcement model with deterministic jump
- **Heston (1993)**: Stochastic volatility for volatility smile/skew
- **Kou (2002)**: Jump-diffusion for fat tails and asymmetry

**Selection Criteria**:
1. Earnings regime detection (pre/post earnings, normal)
2. Fit quality (RMSE, MAE on calibration data)
3. Model parsimony (AIC/BIC penalizing complexity)
4. Martingale constraint satisfaction
5. Out-of-sample validation

**Components**:
- `STTM002A`: Detects pre-EA, post-EA, and normal market regimes
- `STIV005A`: Calibrates jump parameters from historical data
- `STTM004A`: Manages time-to-expiry and time-to-earnings calculations

### Strategy Components

**STCR004A Generation** (Atilgan 2014 criteria):
- IV/RV Ratio > 1.25
- Term structure slope < -0.00406
- Average volume > 1.5M

**Volatility Estimation**: Yang-Zhang (2000) OHLC-based estimator

**Position Sizing**: Kelly Criterion with fractional sizing

### Production Safety (New)

**Cost Validation** (Alaris.Strategy/Cost):
- Execution Cost Modeling (`STCS001A`)
- Slippage & Spread Penalty (`STCS006A`)
- Liquidity Constraints (`STCS008A`)

**Hedging & Risk** (Alaris.Strategy/Hedging):
- Vega Decoupling Analysis (`STHD001A`)
- Gamma Risk Management (`STHD003A`)
- Production Validation Orchestration (`STHD005A`)

---

## Critical Implementation Details

### QuantLib Memory Management (Rule 16)

**Critical Pattern**: All QuantLib objects MUST be explicitly disposed in reverse order of creation.

```csharp
// PriceOptionSync pattern - 14 objects disposed in reverse order
priceEngine.Dispose();
option.Dispose();
payoff.Dispose();
exercise.Dispose();
bsmProcess.Dispose();
volatilityHandle.Dispose();
flatVolTs.Dispose();
calendar.Dispose();
// ... etc
```

**Lesson Learned**: Missing a single `.Dispose()` call causes memory corruption ("pure virtual method called" crashes).

### SafeLog Pattern (Rule 15)

Logging failures cannot crash critical paths:
```csharp
private void SafeLog(Action logAction)
{
    if (_logger == null) return;
    try { logAction(); }
    catch (Exception) { /* Swallow logging errors */ }
}
```

---

## Testing

### Run Tests

```bash
# Build and test
dotnet build && dotnet test

# Note: xunit.runner.json disables parallel execution to avoid QuantLib memory issues
```

### Test Categories

- **Unit**: Component-level functionality (includes IV model tests)
- **Integration**: End-to-end workflows
- **Diagnostic**: Mathematical constraint validation
- **Benchmark**: Performance and accuracy vs Healy (2021)
- **IV Models**: Heston, Kou, Leung-Santoli, model selection, and regime detection

---

## Development Roadmap

### Completed Phases

**Phase 1: Core Compliance** (2025-11-21)
- Rules 4, 7, 10, 15 fully implemented
- SafeLog pattern across 4 files, 17 logging calls isolated

**Phase 2: Function Complexity** (2025-11-21)
- 6 methods refactored (291 lines extracted into 14 helper methods)
- Rule 9 audit complete, 1 violation fixed
- Rule 13 compliant (all methods 60 lines)

**Phase 4: Performance Optimization** (2025-11-21)
- Rule 5 (Zero-Allocation Hot Paths) COMPLIANT
- ArrayPool<T> implemented in Kim solver, STCR003A, STTM001A
- Span<T> for variance calculations
- ~6,000 allocations eliminated per pricing cycle

**Phase 5: Continuous Compliance** (2025-11-21)
- Rule 8 (Limited Scope) COMPLIANT - 56 properties converted to init-only
- Rule 14 (Clear LINQ) COMPLIANT - core code audited
- CI Integration via GitHub Actions (`.github/workflows/ci.yml`)
- 50+ additional Roslyn analyzers for zero-allocation enforcement

### Phase 6: Production Readiness (Active)
- Implementation of Cost & Hedging subdomains
- Integration of Slippage/Liquidity validators
- Vega Decoupling analysis

---

## Academic References

### Alaris.Double
- **Healy (2021)**: "Pricing American Options Under Negative Rates"
- **Kim (1990)**: "The Analytic Valuation of American Options"

### Alaris.Strategy

**Core Strategy**:
- **Atilgan (2014)**: "Implied Volatility Spreads and Expected Market Returns"
- **Dubinsky et al. (2019)**: "Earnings Announcements and Systematic Risk"
- **Yang & Zhang (2000)**: "Drift-Independent Volatility Estimation"

**IV Models**:
- **Leung & Santoli (2014)**: "Volatility Term Structure and Option Returns"
- **Leung & Santoli (2016)**: "Option Pricing and Hedging with Ex-dividend Dates, Earnings Announcements and Regulatory Approvals"
- **Heston (1993)**: "A Closed-Form Solution for Options with Stochastic Volatility"
- **Kou (2002)**: "A Jump-Diffusion Model for Option Pricing"

---

## Quick Commands

```bash
# Build
dotnet build

# Test
dotnet test

# Git workflow
git status
git add <files>
git commit -m "message"
git push -u origin <branch>
```

---

## Contact

**Author**: Kiran K. Nath
**Framework**: .NET 9.0

---

*Last validated: 2025-12-10 | 229+/229+ tests passing*