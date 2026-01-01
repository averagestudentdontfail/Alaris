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
| `Alaris.Core` | Core | Core mathematical primitives with documented accuracy bounds. |
| `Alaris.Double` | Double Boundary | Negative rate American option pricing engine. |
| `Alaris.Strategy` | Strategy | Trading strategy implementation, including core logic, pricing, and risk management. |
| `Alaris.Infrastructure` | Infrastructure | Data acquisition, event sourcing, and binary protocol serialization. Consolidates Data, Events, and Protocol. |
| `Alaris.Host` | Host | CLI, terminal UI, and LEAN algorithm integration. Consolidates Application and Algorithm. |
| `Alaris.Simulation` | Simulation | Monte Carlo simulations and output storage. |
| `Alaris.Governance` | Governance | Compliance documentation, standards, and project governance artifacts. |
| `Alaris.Test` | Test Suite | Unit, integration, and diagnostic tests. |
| `Alaris.Quantlib` | QuantLib | C# bindings and extensions for the QuantLib library. |
| `Alaris.Lean` | LEAN | QuantConnect LEAN engine (external dependency). |

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
| `CR` | Core | Core mathematical primitives and shared utilities. |
| `DB` | Double Boundary | Components related to the `Alaris.Double` project. |
| `ST` | Strategy | Components related to the `Alaris.Strategy` project. |
| `DT` | Data | Components related to the `Alaris.Data` project. |
| `EV` | Events | Components related to the `Alaris.Events` project. |
| `PL` | Protocol | Binary protocol schemas and serialization. |
| `TS` | Test Suite | Components related to testing and validation. |
| `CM` | Common | Shared utilities and infrastructure. |

### 4.3 Category Codes

#### 4.3.1 Core (CR)
| Code | Category | Description |
|------|----------|-------------|
| `EN` | Engine | Pricing engines (FD, unified, QD+). |
| `AP` | Approximation | Quasi-analytic boundary approximation. |
| `SL` | Solver | Numerical solvers (Kim integral, two-stage). |
| `EX` | Expiry | Near-expiry numerical stability. |
| `RE` | Regime | Rate regime classification. |
| `MF` | Math Functions | Core mathematical utilities (BS, IV, CDF). |
| `TS` | Term Structure | Yield curves and volatility surfaces. |
| `TM` | Time | Date, period, day counter types. |
| `OP` | Options | Option types, payoffs, exercise. |
| `VT` | Vectorized | SIMD/AVX2 vectorized operations. |

#### 4.3.2 Double Boundary (DB) - DEPRECATED
> [!WARNING]
> The `Alaris.Double` project has been deprecated. Components have been migrated to `Alaris.Core/Pricing/` with `CR*` naming convention.

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
| `SD` | Signal Detection | Statistical signal detection and hypothesis testing. |
| `QT` | Queue Theory | Queue-theoretic position and capacity management. |
| `CL` | Calendar | Trading calendars and time utilities. |
| `MG` | Maturity Guard | Maturity-based entry/exit filtering. |
| `KF` | Kalman Filter | Kalman-filtered estimation. |
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

#### 4.3.5 Test Suite (TS)
| Code | Category | Description |
|------|----------|-------------|
| `UN` | Unit | Component-level unit tests with formal derivation. |
| `IN` | Integration | End-to-end integration tests. |
| `DG` | Diagnostic | Mathematical constraint validation tests. |
| `BM` | Benchmark | Performance and accuracy benchmark tests. |

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

### 5.1 Alaris.Core

#### 5.1.1 Pricing Subdirectory (Alaris.Core/Pricing/)
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `CREN001A` | `CREN001A` | Base Crank-Nicolson FD pricing engine | Wilmott (2006) |
| `CREN002A` | `CREN002A` | Enhanced FD engine (ASINH grid, Gamma=0 BC) | Healy (2021) § 3 |
| `CREN003A` | `CREN003A` | Unified American option pricing engine | Integrates Spectral + FD |
| `CREN004A` | `CREN004A` | Spectral collocation American pricing engine | Andersen-Lake-Offengenden (2016) |
| `CRAP001A` | `CRAP001A` | QD+ boundary approximation (Super-Halley) | Healy (2021) § 4 |
| `CRSL001A` | `CRSL001A` | Two-stage solver (QD+ → Kim) | Healy (2021) § 5 |
| `CRSL002A` | `CRSL002A` | Kim integral equation solver (FP-B') | Kim (1990) |
| `CREX001A` | `CREX001A` | Near-expiry numerical stability handler | T→0 blending |
| `CRRE001A` | `CRRE001A` | Rate regime classifier (Standard/DoubleBoundary) | Healy (2021) § 2 |

#### 5.1.2 Math Subdirectory (Alaris.Core/Math/)
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `CRMF001A` | `CRMF001A` | BS pricing, IV (Householder(3) + Brent), Greeks | Black-Scholes (1973) |
| `CRMF002A` | `CRMF002A` | Characteristic equation solver (Super-Halley + Brent) | Healy (2021) |
| `CRCH001A` | `CRCH001A` | Chebyshev nodes, barycentric interpolation, Clenshaw | Trefethen (2000) |
| `CRGQ001A` | `CRGQ001A` | Gauss-Legendre, Gauss-Laguerre, Gauss-Hermite, Tanh-Sinh quadrature | Davis & Rabinowitz (1984) |

#### 5.1.3 Other Core Components
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `CRTM005A` | `CRTM005A` | AlarisDate struct (QuantLib-compatible) | ISO 8601 |
| `CRTM006A` | `CRTM006A` | Period struct | QuantLib Period |
| `CRTM007A` | `CRTM007A` | IDayCounter (Actual/365, 360, 30/360) | ISDA conventions |
| `CRTS001A` | `CRTS001A` | IYieldCurve, FlatForwardCurve | Hull (2018) |
| `CRTS002A` | `CRTS002A` | IVolatilitySurface, FlatVolatilitySurface | Gatheral (2006) |
| `CROP001A` | `CROP001A` | OptionType, PlainVanillaPayoff | Option types |
| `CROP002A` | `CROP002A` | AmericanExercise, VanillaOption | Exercise types |
| `CROP004A` | `CROP004A` | DividendSchedule (Spot/Escrowed) | Merton (1973) |

### 5.2 Alaris.Double (DEPRECATED)

> [!WARNING]
> These components have been migrated to `Alaris.Core/Pricing/` with `CR*` naming.
> The `Alaris.Double` project has been removed from the solution.

| Component Code | Migrated To | Description |
|----------------|-------------|-------------|
| `DBAP001A` | `CRAP001A` | QD+ Approximation |
| `DBSL001A` | `CRSL001A` | Two-stage solver |
| `DBSL002A` | `CRSL002A` | Kim integral solver |
| `DBEX001A` | `CREX001A` | Near-expiry handler |
| `DBEN001A` | Removed | QuantLib-dependent engine (deprecated) |
| `DBAP002A` | Removed | Superseded by CREN003A |

### 5.2 Alaris.Strategy

#### 5.2.1 Core & Pricing
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `STCR001A` | `SignalGenerator` | Signal Generation | Atilgan (2014) |
| `STIV001A` | `HestonModel` | Stochastic Volatility | Heston (1993) |
| `STIV002A` | `KouModel` | Jump Diffusion | Kou (2002) |
| `STPR004A` | `LevenbergMarquardtOptimizer` | Optimization | MathNet.Numerics |
| `STRK001A` | `KellyPositionSizer` | Position Sizing (+ net-of-cost) | Kelly Criterion |
| `STIV006A` | `VolSurfaceInterpolator` | Sticky-delta skew interpolation | Gatheral (2006) |
| `STEJ001A` | `EarningsJumpCalibrator` | Normal-Laplace mixture | Dubinsky & Johannes (2006) |
| `STDD001A` | `DividendExDateDetector` | Early exercise risk | Merton (1973) |
| `STCR005A` | `SignalFreshnessMonitor` | Exponential decay freshness | Signal staleness detection |
| `STKF001A` | `STKF001A` | Kalman-filtered volatility estimation | Kalman (1960), Yang-Zhang (2000) |

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

#### 5.2.4 Detection Subdirectory (Alaris.Strategy/Detection/)
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `STSD001A` | `STSD001A` | Neyman-Pearson signal detection | Neyman-Pearson (1933) |

#### 5.2.5 Risk Subdirectory (Alaris.Strategy/Risk/)
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `STRK001A` | `KellyPositionSizer` | Position sizing (Kelly) | Kelly Criterion |
| `STQT001A` | `STQT001A` | Queue-theoretic position management | Little (1961), M/G/1 Queue |
| `STMG001A` | `MaturityGuard` | Entry/exit filtering by maturity | Near-expiry safety |

#### 5.2.6 Calendar Subdirectory (Alaris.Strategy/Calendar/)
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `STCL001A` | `TradingCalendar` | NYSE trading calendar | QuantLib UnitedStates |

#### 5.2.3 Hedging Subdirectory (Alaris.Strategy/Hedging/)
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `STHD001A` | `VegaCorrelationAnalyser` | Front/back IV correlation (+ VIX threshold) | MathNet.Numerics |
| `STHD002A` | `VegaCorrelationResult` | Vega correlation result | - |
| `STHD003A` | `GammaRiskManager` | Gamma/delta monitoring & rehedge | Options Risk Management |
| `STHD004A` | `GammaRiskAssessment` | Gamma risk assessment result | - |
| `STHD005A` | `ProductionValidator` | Orchestrates all pre-trade checks | - |
| `STHD006A` | `ProductionResult` | Complete production validation | - |
| `STHD009A` | `PinRiskMonitor` | Near-expiry pin risk detection | Gamma explosion at strike |
| `STHD007A` | `GapRiskAnalyser` | Overnight gap risk analysis | Options Risk Management |
| `STHD007B` | `STHD007B` | Rule-based exit monitor with stall detection | Alaris Phase 3 Specification |

#### 5.2.7 Core Subdirectory (Phase 3)
| Component Code | Class Name | Description | Reference |
|----------------|------------|-------------|-----------|
| `STKF001A` | `KalmanVolatilityEstimator` | Kalman-filtered volatility | Kalman (1960), Yang-Zhang |

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
| `DTea001B` | `SecEdgarProvider` | SEC EDGAR 8-K filings | SEC.gov |
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

### 5.5 Alaris.Test

#### 5.5.1 Unit Tests (TSUN)
| Component Code | File Name | Description | Coverage |
|----------------|-----------|-------------|----------|
| `TSUN001A-022A` | `TSUN001A-022A.cs` | Core functionality tests | Alaris.Double, Alaris.Strategy |
| `TSUN023A` | `TSUN023A.cs` | Near-expiry handler tests | DBEX001A intrinsic values, blending |
| `TSUN024A` | `TSUN024A.cs` | IV calculator tests | STIV001A-003A Heston/Kou invariants |
| `TSUN026A` | `TSUN026A.cs` | Cost model tests | STCS001A-006A fee additivity |
| `TSUN027A` | `TSUN027A.cs` | Risk/Maturity tests | STRK001A/002A Kelly, STMG001A |
| `TSUN028A` | `TSUN028A.cs` | Simulation/Algo tests | SMSM001A/STLN001A constants |
| `TSUN029A` | `TSUN029A.cs` | Data model tests | DTmd001A, DTpr003A-005A interfaces |

#### 5.5.2 Integration Tests (TSIN)
| Component Code | File Name | Description | Coverage |
|----------------|-----------|-------------|----------|
| `TSIN001A-003A` | `TSIN001A-003A.cs` | Core integration tests | Data pipeline, strategy flow |
| `TSIN004A` | `TSIN004A.cs` | Events infrastructure tests | EVIF001A/EVIF002A event store, audit |
| `TSIN005A` | `TSIN005A.cs` | Session management tests | APsv001A/APmd001A CRUD operations |

#### 5.5.3 Other Test Categories
| Component Code | File Name | Description | Coverage |
|----------------|-----------|-------------|----------|
| `TSDG001A-002A` | `TSDG001A-002A.cs` | Diagnostic tests | Mathematical constraint validation |
| `TSBM001A` | `TSBM001A.cs` | Benchmark tests | Performance vs Healy (2021) |

**Test Statistics** (2026-01-01):
- Total test cases: 1001
- Test lines: ~15,000
- Production:Test ratio: 2.0:1
- QuantLib dependency: ELIMINATED


## 6. Compliance
All changes to the codebase must adhere to this standard. Non-compliant components will be rejected during code review.
