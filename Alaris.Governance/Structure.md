--- START OF FILE Structure.md ---

# Alaris Structural Compliance Standard

**Document ID:** ALARIS-STD-STRUC-001
**Version:** 1.2
**Date:** December 2025
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
| `Alaris.Data` | Data | Data acquisition, validation, and integration layer. |
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
| `DT` | Data | Components related to the `Alaris.Data` project. |
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
| `DT` | Data | Market data structures and providers (Legacy/Strategy-specific). |
| `TM` | Time | Time management, calendars, and term structures. |
| `CT` | Control | Strategy control and orchestration. |
| `CS` | Cost | Transaction cost models, execution cost validation, liquidity analysis. |
| `HD` | Hedging | Hedging analysis, vega correlation, gamma risk management. |
| `UN` | Universe | Universe selection models. |

#### 4.3.3 Data (DT)
| Code | Category | Description |
|------|----------|-------------|
| `PR` | Provider | Data providers (API clients, scrapers). |
| `MD` | Model | Data models and DTOs. |
| `BR` | Bridge | Data bridges and adapters. |
| `QC` | Quality | Data quality validators. |

#### 4.3.4 Events (EV)
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

#### 5.2.1 Core & Pricing
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `STCR001A` | `SignalGenerator` | Signal Generation | Atilgan (2014) |
| `STIV001A` | `HestonModel` | Stochastic Volatility | Heston (1993) |
| `STIV002A` | `KouModel` | Jump Diffusion | Kou (2002) |
| `STPR004A` | `LevenbergMarquardtOptimizer` | Optimization | MathNet.Numerics |
| `STRK001A` | `KellyPositionSizer` | Position Sizing | Kelly Criterion |

#### 5.2.2 Cost Subdirectory (Alaris.Strategy/Cost/)
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `STCS001A` | `IExecutionCostModel` | Execution cost model interface | QuantConnect FeeModel |
| `STCS002A` | `OrderParameters` | Order parameters for cost calc | IBKR Fee Structure |
| `STCS003A` | `ExecutionCostResult` | Single leg execution cost result | - |
| `STCS004A` | `SpreadCostResult` | Calendar spread cost result | - |
| `STCS005A` | `ConstantFeeModel` | Brokerage-agnostic constant fees | QC ConstantFeeModel |
| `STCS006A` | `SignalCostValidator` | Validates signal survives costs | Atilgan (2014) § IV/RV |
| `STCS007A` | `CostValidationResult` | Cost validation result | - |
| `STCS008A` | `LiquidityValidator` | Position size vs liquidity | Institutional Practice |
| `STCS009A` | `LiquidityResult` | Liquidity validation result | - |

#### 5.2.3 Hedging Subdirectory (Alaris.Strategy/Hedging/)
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `STHD001A` | `VegaCorrelationAnalyser` | Front/back IV correlation analysis | MathNet.Numerics |
| `STHD002A` | `VegaCorrelationResult` | Vega correlation result | - |
| `STHD003A` | `GammaRiskManager` | Gamma/delta monitoring & rehedge | Options Risk Management |
| `STHD004A` | `GammaRiskAssessment` | Gamma risk assessment result | - |
| `STHD005A` | `ProductionValidator` | Orchestrates all pre-trade checks | - |
| `STHD006A` | `ProductionResult` | Complete production validation | - |

### 5.3 Alaris.Data
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `DTpr001A` | `PolygonApiClient` | Polygon.io API client | - |
| `DTpr002A` | `InteractiveBrokersSnapshotProvider` | Execution quote provider | IBKR API |
| `DTpr003A` | Market data provider interface | Interface for market data providers | - |
| `DTpr004A` | Earnings calendar interface | Interface for earnings calendar providers | - |
| `DTpr005A` | Risk-free rate interface | Interface for risk-free rate providers | - |
| `DTmd001A` | `MarketDataSnapshot` | Market data model | - |
| `DTmd002A` | `CalendarSpreadQuote` | Calendar spread model | - |
| `DTmd003A` | `CorporateAction` | Corporate action data model | - |
| `DTca001A` | Corporate actions provider interface | Interface for corporate actions data | - |
| `DTea001A` | `FinancialModelingPrepProvider` | FMP earnings calendar | - |
| `DTrf001A` | `TreasuryDirectRateProvider` | Treasury rate provider | US Treasury |
| `DTqc001A` | Data quality validators | Price, IV, Volume/OI, Earnings validators | - |
| `DTqc002A` | Data quality validator interface | Interface for data quality validators | - |
| `DTbr001A` | `AlarisDataBridge` | Unified data bridge | - |

### 5.4 Alaris.Application
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `APap001A` | `APap001A` | Application entry point | Spectre.Console |
| `APcm001A` | `APcm001A` | Run command (alaris run) | - |
| `APcm002A` | `APcm002A` | Config command (alaris config) | - |
| `APcm003A` | `APcm003A` | Data command (alaris data) | - |

## 6. Compliance
All changes to the codebase must adhere to this standard. Non-compliant components will be rejected during code review.
