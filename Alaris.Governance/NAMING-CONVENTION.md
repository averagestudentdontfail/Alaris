# Alaris Structured Component Naming Convention

**Version 1.0 | November 2025**

## Overview

This document establishes a hierarchical significant-digit coding system for the Alaris quantitative finance system, inspired by industrial standards such as ISO 81346 and RDS-PP.

## Code Structure

```
[Domain][Category][Sequence][Variant]
   ↓        ↓        ↓        ↓
  DB       AP       001       A
```

| Segment   | Format           | Description                        |
|-----------|------------------|------------------------------------|
| Domain    | 2 letters (AA-ZZ)| System domain identifier           |
| Category  | 2 letters (AA-ZZ)| Functional category within domain  |
| Sequence  | 3 digits (001-999)| Component sequence number         |
| Variant   | 1 letter (A-Z)   | Version or variant indicator       |

---

## Domain Codes

| Code | Domain          | Description                                      |
|------|-----------------|--------------------------------------------------|
| DB   | Double Boundary | Negative rate American option pricing            |
| ST   | Strategy        | Trading strategy components                      |
| EV   | Events          | Event sourcing and audit logging                 |
| TS   | Test Suite      | Validation and testing                           |
| CM   | Common          | Shared utilities and infrastructure              |

---

## Category Codes

### Double Boundary Domain (DB)

| Code | Category      | Components                                       |
|------|---------------|--------------------------------------------------|
| AP   | Approximation | CA505A, CA504A |
| SL   | Solver        | CA502A, CA503A    |
| EN   | Engine        | CA501A, PricingEngine              |
| MD   | Model         | BoundaryModel, ExerciseRegion                    |
| RS   | Result        | SolverResult, BoundaryResult                     |

### Strategy Domain (ST)

| Code | Category | Components                                          |
|------|----------|-----------------------------------------------------|
| CR   | Core     | CA111A, CA106AAnalyzer, CA108AEstimator |
| IV   | IV Models| CA101A, CA102A, CA109A              |
| PR   | Pricing  | CA321A, CA201A, CA204A  |
| RK   | Risk     | CA401A, PositionSize                    |
| BR   | Bridge   | CA301A, PricingRegime                 |
| DT   | Data     | CA303A, CA311AData                |
| TM   | Time     | CA107A, CA104A                      |
| CT   | Control  | Control (strategy orchestration)                    |

### Events Domain (EV)

| Code | Category       | Components                    |
|------|----------------|-------------------------------|
| CR   | Core           | IEvent, IEventStore, EventEnvelope |
| DM   | Domain         | Strategy domain events        |
| IF   | Infrastructure | InMemoryEventStore            |

### Test Suite Domain (TS)

| Code | Category    | Description                                |
|------|-------------|--------------------------------------------|
| UT   | Unit        | Component-level functionality tests        |
| IT   | Integration | End-to-end workflow tests                  |
| DG   | Diagnostic  | Mathematical constraint validation         |
| BM   | Benchmark   | Performance and accuracy tests vs Healy (2021) |

---

## Variant Codes

| Code | Meaning                | Usage                                |
|------|------------------------|--------------------------------------|
| A    | Primary implementation | Production-ready, validated          |
| B    | Alternative            | Different algorithm or approach      |
| X    | Experimental           | Under development, not validated     |
| D    | Deprecated             | Superseded, retained for compatibility |
| T    | Test/Mock              | Test fixture or mock implementation  |

---

## Component Registry

### Alaris.Double

| Code       | Component                  | Academic Reference    |
|------------|----------------------------|-----------------------|
| DBAP001A   | CA505A        | Healy (2021) § 4      |
| DBAP002A   | CA504A| Healy (2021) § 5      |
| DBSL001A   | CA502A       | Healy (2021) § 5.3    |
| DBSL002A   | CA503A    | Kim (1990)            |
| DBEN001A   | CA501A       | Healy (2021)          |
| DBRS001A   | SolverResult               | —                     |

### Alaris.Strategy

| Code       | Component                | Academic Reference      |
|------------|--------------------------|-------------------------|
| STCR001A   | CA111A          | Atilgan (2014)          |
| STCR002A   | CA106AAnalyzer    | Leung & Santoli (2014)  |
| STCR003A   | CA108AEstimator       | Yang & Zhang (2000)     |
| STIV001A   | CA101A              | Heston (1993)           |
| STIV002A   | CA102A                 | Kou (2002)              |
| STIV003A   | CA109A          | —                       |
| STPR001A   | CA321A           | —                       |
| STPR002A   | CA201A      | MathNet.Numerics        |
| STPR003A   | CA204A            | Heston (1993)           |
| STRK001A   | CA401A       | Kelly Criterion         |
| STBR001A   | CA301A     | —                       |
| STCT001A   | Control                  | —                       |

---

## Usage Examples

```
DBAP001A = DB (Double Boundary) + AP (Approximation) + 001 + A (Primary)
         → CA505A (primary implementation)

STIV002A = ST (Strategy) + IV (IV Models) + 002 + A (Primary)
         → CA102A (Kou jump-diffusion model)

TSBM001A = TS (Test Suite) + BM (Benchmark) + 001 + A (Primary)
         → Healy benchmark validation tests
```

---

## Files to Remove

The following temporary diagnostic files should be removed:

- `TestProbabilities/` — Temporary probability calculation diagnostic program
- Any `HestonDiagnostic` files — Temporary diagnostic utilities

---

## Academic References

- **Healy (2021)**: "Pricing American Options Under Negative Rates"
- **Kim (1990)**: "The Analytic Valuation of American Options"
- **Atilgan (2014)**: "Implied Volatility Spreads and Expected Market Returns"
- **Heston (1993)**: "A Closed-Form Solution for Options with Stochastic Volatility"
- **Kou (2002)**: "A Jump-Diffusion Model for Option Pricing"
- **Yang & Zhang (2000)**: "Drift-Independent Volatility Estimation"
- **Leung & Santoli (2014, 2016)**: Volatility term structure and earnings announcements

---

*Document Version: 1.0 | Last Updated: November 2025*
