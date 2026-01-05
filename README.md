# Alaris

> *Alaris*: from Latin *ālāris*, "of the wing." A trading system designed for flight through uncertain markets.

Alaris is a quantitative trading system that captures the volatility risk premium around corporate earnings announcements. The system implements calendar spread strategies on American-style equity options, exploiting the documented phenomenon that implied volatility systematically exceeds realised volatility in the days preceding earnings releases.

## The Volatility Risk Premium

The volatility risk premium (VRP) is the difference between market-implied volatility and subsequently realised volatility:

$$
\text{VRP}(t) = \sigma_I(t) - \sigma_R(t)
$$

This premium is persistently positive. Implied volatility exceeds realised volatility approximately eighty per cent of the time for equity index options. The premium represents compensation for bearing uncertainty; it exists because it should exist.

For securities approaching earnings, the premium exhibits a predictable pattern: implied volatility rises in the five to seven trading days before the announcement, then collapses sharply afterward. Alaris captures this premium by selling short-dated options rich in inflated implied volatility while hedging with longer-dated options that retain their value through the announcement.

## Signal Generation

Trading signals are generated when three conditions are satisfied simultaneously:

**IV/RV Ratio.** The 30-day implied volatility must exceed 30-day realised volatility by at least 25 per cent:

$$
\frac{\sigma_I^{30}}{\sigma_R^{30}} \geq 1.25
$$

**Term Structure.** The implied volatility term structure must be inverted, indicating elevated near-term uncertainty relative to longer tenors.

**Liquidity.** Adequate trading volume must exist to execute positions without excessive market impact.

| Criteria Met | Recommendation |
|--------------|----------------|
| 3 of 3 | Recommended |
| 2 of 3 | Consider |
| 0-1 of 3 | Avoid |

## Realised Volatility Estimation

Alaris employs the Yang-Zhang estimator, which achieves approximately eight times the statistical efficiency of close-to-close estimation by incorporating overnight gaps and intraday price ranges:

$$
\sigma_{YZ}^2 = \sigma_o^2 + k\sigma_c^2 + (1-k)\sigma_{RS}^2
$$

Where $\sigma_o^2$ captures overnight variance, $\sigma_c^2$ captures open-to-close variance, and $\sigma_{RS}^2$ is the Rogers-Satchell component incorporating high and low prices.

## American Option Pricing

The pricing engine solves the free boundary problem for American options using spectral collocation methods. The American option value decomposes into European value plus early exercise premium:

$$
V(\tau, s) = v(\tau, s) + \mathcal{P}(\tau, s)
$$

The implementation handles negative interest rate environments, where optimal exercise exhibits double-boundary behaviour.

## Position Sizing

Capital allocation follows fractional Kelly:

- **Recommended signals:** 2% of Kelly-optimal fraction
- **Consider signals:** 1% of Kelly-optimal fraction

This conservatism sacrifices expected return for dramatically reduced probability of ruin.

---

## Project Structure

```
Alaris/
├── docs/                    # Comprehensive documentation
│   ├── philosophy.md        # Design philosophy and worldview
│   ├── foundations.md       # Mathematical theory from first principles
│   ├── specification.md     # Formal system requirements
│   ├── guide.md             # Practical operation guide
│   ├── standard.md          # Coding conventions
│   └── architecture.md      # System architecture and naming conventions
│
├── src/                     # Source code (Alaris components)
│   ├── Alaris.Core/         # Domain layer: pricing, Greeks, volatility
│   ├── Alaris.Strategy/     # Application layer: signals, risk management
│   ├── Alaris.Algorithm/    # Application layer: LEAN integration
│   ├── Alaris.Simulation/   # Application layer: backtesting
│   ├── Alaris.Infrastructure/# Infrastructure: data feeds, persistence
│   ├── Alaris.Host/         # Presentation: CLI, TUI
│   ├── Alaris.Library/      # Native bindings
│   └── Alaris.Test/         # Test suite
│
├── lib/                     # External dependencies
│   └── Alaris.Lean/         # QuantConnect LEAN engine (submodule)
│
├── ses/                     # Runtime session data (gitignored)
│   └── {TYPE}-{DATE}-{TIME}/# Session directories (e.g., BT-20250105-143022)
│
├── Alaris.sln               # Solution file
└── Directory.Build.props    # Centralised build configuration
```

### Naming Convention

Components use structured codes for traceability: `[Domain][Category][Sequence][Variant]`

Example: `CREN004A` = Core (CR) + Engine (EN) + Sequence 004 + Primary (A)

See [Architecture](docs/architecture.md) for the complete naming specification.

## Documentation

The documentation is designed to be read in order, building from philosophical foundations through mathematical theory to practical operation:

1. **[Philosophy](docs/philosophy.md)** — explores the worldview underlying the system: markets, risk, uncertainty, and design principles.

2. **[Foundations](docs/foundations.md)** — develops the mathematical theory from probability and stochastic processes through option pricing and volatility estimation.

3. **[Specification](docs/specification.md)** — provides formal definitions of signal criteria, position sizing rules, risk limits, and configuration parameters.

4. **[Guide](docs/guide.md)** — covers practical patterns for signal interpretation, position management, backtesting, and troubleshooting.

5. **[Standard](docs/standard.md)** — codifies conventions for high-integrity trading system development.

6. **[Architecture](docs/architecture.md)** — describes the structural design and component responsibilities.

## Building

Prerequisites:
- .NET 10.0 SDK
- QuantLib native libraries (for American option pricing)

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test
```

## Usage

```bash
# Scan for signals
./alaris signal scan --universe sp500

# Evaluate specific ticker
./alaris analyze AAPL

# Run backtest
./alaris backtest run --start 2020-01-01 --end 2024-12-31

# Start live trading (paper mode)
./alaris live start --paper
```

## Configuration

Configuration is loaded from:
1. `appsettings.json` (base configuration)
2. `appsettings.{Environment}.json` (environment overrides)
3. Environment variables (secrets)

Key configuration parameters are documented in the [Specification](docs/specification.md).

## Licence

Copyright © 2024-2025 Kiran K. Nath. All rights reserved.

## Acknowledgements

- Yang and Zhang (2000) for the minimum-variance volatility estimator
- Andersen, Lake, and Offengenden (2015) for American option pricing benchmarks
- QuantConnect for the LEAN algorithmic trading engine
- The Polygon.io team for market data infrastructure
