# Alaris Structural Compliance Standard

**Document ID:** ALARIS-STD-STRUC-001
**Version:** 1.0
**Date:** November 2025
**Status:** Active

---

## 1. Scope
This standard establishes the mandatory structural and naming conventions for the Alaris quantitative finance system. It applies to all source code, documentation, and artifacts within the Alaris repository. Adherence to this standard is required for compliance with the Alaris Governance Framework.

## 2. References
- **ISO 81346**: Industrial systems, installations and equipment and industrial products — Structuring principles and reference designations.
- **RDS-PP**: Reference Designation System for Power Plants.

## 3. Directory Structure
The Alaris system follows a strict domain-driven directory structure. All components must be located within their designated domain projects.

### 3.1 Domain Projects
| Directory | Domain | Description |
|-----------|--------|-------------|
| `Alaris.Double` | Double Boundary | Negative rate American option pricing engine. |
| `Alaris.Strategy` | Strategy | Trading strategy implementation, including core logic, pricing, and risk management. |
| `Alaris.Events` | Events | Event sourcing, audit logging, and domain event definitions. |
| `Alaris.Governance` | Governance | Compliance documentation, standards, and project governance artifacts. |
| `Alaris.Test` | Test Suite | Unit, integration, and diagnostic tests. |
| `Alaris.Quantlib` | QuantLib | C# bindings and extensions for the QuantLib library. |

## 4. Component Naming Convention
Alaris utilizes a hierarchical significant-digit coding system for component naming, ensuring uniqueness and traceability.

### 4.1 Format
The naming format consists of four segments:
`[Domain][Category][Sequence][Variant]`

**Example:** `DBAP001A`
- **Domain**: `DB` (Double Boundary)
- **Category**: `AP` (Approximation)
- **Sequence**: `001` (First component)
- **Variant**: `A` (Primary implementation)

### 4.2 Domain Codes
| Code | Domain | Description |
|------|--------|-------------|
| `DB` | Double Boundary | Components related to the `Alaris.Double` project. |
| `ST` | Strategy | Components related to the `Alaris.Strategy` project. |
| `EV` | Events | Components related to the `Alaris.Events` project. |
| `TS` | Test Suite | Components related to testing and validation. |
| `CM` | Common | Shared utilities and infrastructure. |

### 4.3 Category Codes

#### 4.3.1 Double Boundary (DB)
| Code | Category | Description |
|------|----------|-------------|
| `AP` | Approximation | Analytic and quasi-analytic approximation methods. |
| `SL` | Solver | Numerical solvers and root-finding algorithms. |
| `EN` | Engine | Pricing engines and orchestration logic. |
| `MD` | Model | Mathematical models and boundary definitions. |
| `RS` | Result | Calculation results and data structures. |

#### 4.3.2 Strategy (ST)
| Code | Category | Description |
|------|----------|-------------|
| `CR` | Core | Core strategy logic, signal generation, and analysis. |
| `IV` | IV Models | Implied volatility models (Heston, Kou, etc.). |
| `PR` | Pricing | Option pricing models and numerical methods. |
| `RK` | Risk | Risk management and position sizing. |
| `BR` | Bridge | Interfaces and bridges to other domains (e.g., QuantLib). |
| `DT` | Data | Market data structures and providers. |
| `TM` | Time | Time management, calendars, and term structures. |
| `CT` | Control | Strategy control and orchestration. |

#### 4.3.3 Events (EV)
| Code | Category | Description |
|------|----------|-------------|
| `CR` | Core | Core event interfaces and base classes. |
| `DM` | Domain | Domain-specific event definitions. |
| `IF` | Infrastructure | Infrastructure implementations (stores, loggers). |

### 4.4 Variant Codes
| Code | Meaning | Usage |
|------|---------|-------|
| `A` | Primary | Production-ready, validated implementation. |
| `B` | Alternative | Alternative algorithm or approach. |
| `X` | Experimental | Under development, not validated. |
| `D` | Deprecated | Superseded, retained for compatibility. |
| `T` | Test/Mock | Test fixture or mock implementation. |

## 5. Component Registry
The following table lists the primary components and their academic references.

### 5.1 Alaris.Double
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `DBAP001A` | `QuasiAnalyticApproximation` | QD+ Approximation | Healy (2021) § 4 |
| `DBAP002A` | `DoubleBoundaryApproximation` | Boundary Approximation | Healy (2021) § 5 |
| `DBSL001A` | `DoubleBoundarySolver` | Boundary Solver | Healy (2021) § 5.3 |
| `DBSL002A` | `DoubleBoundaryKimSolver` | Integral Solver | Kim (1990) |
| `DBEN001A` | `DoubleBoundaryEngine` | Pricing Engine | Healy (2021) |

### 5.2 Alaris.Strategy
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `STCR001A` | `SignalGenerator` | Signal Generation | Atilgan (2014) |
| `STIV001A` | `HestonModel` | Stochastic Volatility | Heston (1993) |
| `STIV002A` | `KouModel` | Jump Diffusion | Kou (2002) |
| `STPR004A` | `LevenbergMarquardtOptimizer` | Optimization | MathNet.Numerics |
| `STRK001A` | `KellyPositionSizer` | Position Sizing | Kelly Criterion |

## 6. Compliance
All changes to the codebase must adhere to this standard. Non-compliant components will be rejected during code review.
