# Alaris

Alaris is a quantitative trading system designed to capture the implied volatility premium that manifests around corporate earnings announcements. The system implements calendar spread strategies on American-style equity options, exploiting the documented phenomenon that implied volatility systematically exceeds realised volatility in the days preceding earnings releases.

## The Volatility Risk Premium

The theoretical foundation rests on the volatility risk premium, defined as the difference between market-implied volatility and subsequently realised volatility. For securities approaching earnings announcements, this premium exhibits a predictable pattern: implied volatility rises in the five to seven trading days before the announcement, then collapses sharply afterward. The system captures this premium by selling short-dated options rich in inflated implied volatility while hedging with longer-dated options that retain their value through the announcement.

The volatility risk premium at time $t$ is:

$$
\text{VRP}(t) = \sigma_I(t) - \sigma_R(t)
$$

where $\sigma_I$ denotes implied volatility and $\sigma_R$ denotes realised volatility. For securities with earnings at time $T_E$, the expected VRP is positive during the pre-announcement window $[T_E - \Delta, T_E)$.

## Signal Generation

Trading signals are generated when three conditions are satisfied simultaneously. The IV/RV ratio criterion requires that 30-day implied volatility exceed 30-day realised volatility by at least 25 percent:

$$
\frac{\sigma_I^{30}}{\sigma_R^{30}} \geq 1.25
$$

The term structure criterion requires a sufficiently negative slope in the implied volatility curve, indicating elevated short-dated volatility relative to longer tenors. The liquidity criterion ensures adequate trading volume to execute positions without excessive market impact. When all three criteria are met, the signal is classified as "Recommended"; when exactly two are met, as "Consider"; otherwise, the opportunity is avoided.

## Realised Volatility Estimation

The system employs the Yang-Zhang estimator for realised volatility, which achieves approximately eight times the statistical efficiency of simple close-to-close estimation by incorporating overnight gaps and intraday price ranges:

$$
\sigma_{YZ}^2 = \sigma_o^2 + k\sigma_c^2 + (1-k)\sigma_{RS}^2
$$

where $\sigma_o^2$ captures overnight variance, $\sigma_c^2$ captures open-to-close variance, and $\sigma_{RS}^2$ is the Rogers-Satchell component incorporating high and low prices. This efficiency gain is essential for reliable signal generation on the 30-day lookback window.

## American Option Pricing

The pricing engine solves the free boundary problem for American options, which cannot be valued with closed-form expressions due to the early exercise feature. The system implements spectral collocation methods for the integral equation formulation, decomposing the American option value into European value plus early exercise premium:

$$
V(\tau, s) = v(\tau, s) + \mathcal{P}(\tau, s)
$$

The implementation extends to negative interest rate environments, where the optimal exercise region exhibits double boundary behaviour—a phenomenon requiring specialised numerical treatment validated against published benchmark values.

## Position Sizing

Capital allocation follows the fractional Kelly criterion, which balances the mathematical optimum for long-run geometric growth against the practical need to limit drawdowns. The system applies a conservative fraction of the Kelly-optimal bet size, with the multiplier depending on signal strength: 2% of Kelly for "Recommended" signals and 1% for "Consider" signals.

## Fault Monitoring

Every decision rule corresponds to an evaluable predicate, ensuring deterministic behaviour under normal operating conditions. The fault detection framework monitors data quality through validation checks on price reasonableness, IV arbitrage, and volume consistency. Model validity faults trigger when the pricing assumptions may be compromised. Execution risk faults detect when transaction costs would erode expected returns below acceptable thresholds. Position risk faults monitor delta drift, gamma exposure, and moneyness deviation from the at-the-money target. Circuit breakers automatically halt trading when daily losses exceed specified thresholds or when data feed failures compromise decision quality.

## Architecture

The system is organised into domain-specific components. `Alaris.Double` implements the spectral collocation pricing engine for American options under both standard and double-boundary regimes. `Alaris.Strategy` encodes the signal generation logic and position management rules. `Alaris.Data` interfaces with Polygon for historical and real-time market data and with FMP for earnings calendar information. `Alaris.Lean` integrates with the QuantConnect LEAN engine for backtesting and live execution through Interactive Brokers. `Alaris.Governance` maintains the formal mathematical specification and operational procedures.

## Documentation

The formal mathematical specification is maintained in `Alaris.Governance/Documentation`, providing rigorous derivations of all computational methods employed by the system. This specification serves as the authoritative reference for implementation correctness and documents the academic provenance of the trading signals.
