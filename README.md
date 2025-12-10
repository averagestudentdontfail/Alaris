# Alaris

Alaris is a quantitative trading system designed to capture the implied volatility premium that manifests around corporate earnings announcements. The system implements calendar spread strategies on American-style equity options, exploiting the documented phenomenon that implied volatility systematically exceeds realised volatility in the days preceding earnings releases.

## Theoretical Foundation

The pricing methodology derives from first principles, beginning with the risk-neutral stochastic differential equation governing asset prices and developing the free boundary formulation for American option pricing. The system extends this framework to accommodate negative interest rate environments, where the optimal exercise region exhibits double boundary behaviour. Trading signals are expressed as mathematically precise predicates derived from academic literature on earnings volatility, with position sizing governed by fractional Kelly criterion to balance growth optimisation against drawdown risk.

## Architecture

The system is organised into domain-specific components. The pricing engine in `Alaris.Double` implements spectral collocation methods for solving the free boundary integral equations, validated against published benchmark values. The strategy layer in `Alaris.Strategy` encodes the signal generation logic and position management rules. Market data flows through `Alaris.Data`, which interfaces with external providers and maintains the historical databases required for volatility estimation. The backtesting infrastructure integrates with the LEAN algorithmic trading engine via `Alaris.Lean`, enabling systematic validation of strategy performance across historical earnings cycles.

## Fault Monitoring

Every decision rule in the system corresponds to an evaluable predicate, ensuring deterministic behaviour under normal operating conditions. The fault detection framework monitors data quality, model validity, execution risk, and position risk through a system of inequalities and logical predicates. Circuit breakers trigger automatic position reduction or system halt when monitored quantities exceed specified thresholds, protecting against both market dislocations and data feed failures.

## Documentation

The formal mathematical specification is maintained in `Alaris.Governance/Documentation`, providing rigorous derivations of all computational methods employed by the system. This specification serves as the authoritative reference for implementation correctness and documents the academic provenance of the trading signals.
