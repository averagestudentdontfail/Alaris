# Alaris Specification

*Formal Definition of the Trading System*

**Version:** 1.0  
**Status:** Normative

---

## 1. Introduction

This document provides the formal specification of the Alaris trading system. It defines the signal generation criteria, pricing engine requirements, risk management rules, and operational constraints.

The specification is intended to be:

- **Complete:** Every behavioural aspect of the system is defined.
- **Precise:** Definitions are unambiguous.
- **Implementable:** The specification can be directly translated to code.
- **Testable:** Each requirement admits verification.

---

## 2. System Overview

### 2.1 Purpose

Alaris is a systematic trading system for capturing volatility risk premium around corporate earnings announcements. The system identifies opportunities where implied volatility is elevated relative to historical realised volatility, constructs hedged calendar spread positions, and manages risk through defined limits and circuit breakers.

### 2.2 Scope

The system encompasses:

1. **Data acquisition:** Market data feeds, earnings calendar, option chains.
2. **Volatility estimation:** Yang-Zhang realised volatility calculation.
3. **Signal generation:** IV/RV ratio, term structure, and liquidity criteria.
4. **Position construction:** Calendar spread leg selection and sizing.
5. **Risk management:** Position limits, circuit breakers, exposure monitoring.
6. **Execution:** Order generation and broker integration.

### 2.3 Excluded from Scope

The following are explicitly outside scope:

- Intraday trading strategies
- Delta-neutral market making
- Proprietary information signals
- Cryptocurrency or forex instruments

---

## 3. Data Requirements

### 3.1 Market Data

| Data Element | Source | Frequency | Retention |
|--------------|--------|-----------|-----------|
| Equity OHLCV | Polygon.io | End of day | 2 years minimum |
| Option quotes | Polygon.io | Real-time or EOD | Current day |
| Option chains | Polygon.io | On demand | Current snapshot |
| Risk-free rate | Treasury yields | Daily | 1 year |

### 3.2 Event Data

| Data Element | Source | Frequency |
|--------------|--------|-----------|
| Earnings dates | Earnings calendar provider | Daily refresh |
| Earnings timing | BMO/AMC indicator | Daily refresh |
| Dividend dates | Provider dependent | Daily refresh |

### 3.3 Data Validation

All external data MUST be validated before use.

**Price validation:**
- Bid price MUST be less than or equal to ask price
- Mid price MUST be positive
- Price change from previous close MUST NOT exceed 50% without flag

**Option validation:**
- Implied volatility MUST be positive
- Implied volatility MUST NOT exceed 500%
- Put-call parity violations MUST NOT exceed 2% of mid

**Volume validation:**
- Volume MUST be non-negative
- Zero volume with non-zero open interest is valid
- Negative volume MUST be rejected

---

## 4. Volatility Estimation

### 4.1 Yang-Zhang Estimator

The system MUST implement the Yang-Zhang volatility estimator as specified below.

**Inputs:**
- OHLC prices for $n$ trading days: $(O_i, H_i, L_i, C_i)$ for $i = 1, \ldots, n$
- Previous close $C_0$

**Computation:**

Step 1. Compute overnight returns:
$$
o_i = \ln(O_i / C_{i-1})
$$

Step 2. Compute close-to-open returns:
$$
c_i = \ln(C_i / O_i)
$$

Step 3. Compute overnight variance:
$$
\sigma_o^2 = \frac{1}{n-1} \sum_{i=1}^{n} (o_i - \bar{o})^2
$$

Step 4. Compute open-to-close variance:
$$
\sigma_c^2 = \frac{1}{n-1} \sum_{i=1}^{n} (c_i - \bar{c})^2
$$

Step 5. Compute Rogers-Satchell component:
$$
\sigma_{RS}^2 = \frac{1}{n} \sum_{i=1}^{n} \left[ (\ln H_i - \ln O_i)(\ln H_i - \ln C_i) + (\ln L_i - \ln O_i)(\ln L_i - \ln C_i) \right]
$$

Step 6. Compute weighting factor:
$$
k = \frac{0.34}{1.34 + \frac{n+1}{n-1}}
$$

Step 7. Combine:
$$
\sigma_{YZ}^2 = \sigma_o^2 + k \cdot \sigma_c^2 + (1 - k) \cdot \sigma_{RS}^2
$$

Step 8. Annualise:
$$
\sigma_{annual} = \sigma_{YZ} \cdot \sqrt{252}
$$

**Constraints:**
- Minimum observations: $n \geq 20$
- If $n < 20$, return error; do not estimate

### 4.2 Lookback Window

The standard lookback window is 30 trading days. This parameter MUST be configurable.

---

## 5. Signal Generation

### 5.1 Signal Criteria

A trading signal requires evaluation of three criteria:

**Criterion 1: IV/RV Ratio**

$$
\text{IVRV} = \frac{\sigma_I^{30}}{\sigma_{YZ}^{30}}
$$

Where:
- $\sigma_I^{30}$ is 30-day at-the-money implied volatility
- $\sigma_{YZ}^{30}$ is 30-day Yang-Zhang realised volatility

Threshold: IVRV $\geq 1.25$

**Criterion 2: Term Structure**

$$
\text{TS} = \sigma_I^{back} - \sigma_I^{front}
$$

Where:
- $\sigma_I^{front}$ is front-month ATM implied volatility
- $\sigma_I^{back}$ is back-month ATM implied volatility

Condition: TS $< 0$ (inverted)

**Criterion 3: Liquidity**

Two sub-conditions:
1. Average daily volume (ADV) $\geq$ 100,000 contracts
2. Bid-ask spread $\leq$ 5% of mid price

### 5.2 Signal Classification

| Criteria Satisfied | Classification |
|--------------------|----------------|
| 3 of 3 | Recommended |
| 2 of 3 | Consider |
| 0 or 1 of 3 | Avoid |

### 5.3 Earnings Timing Filter

Signals MUST only be generated when:
- Days to earnings: $3 \leq d \leq 14$

Signals MUST NOT be generated:
- On the earnings announcement day
- With fewer than 3 days to earnings (insufficient time for position)
- With more than 14 days to earnings (premium not yet elevated)

---

## 6. Position Construction

### 6.1 Calendar Spread Structure

A calendar spread consists of:
- **Short leg:** Sell one front-month option
- **Long leg:** Buy one back-month option
- **Same strike:** ATM or nearest to ATM

### 6.2 Expiration Selection

**Front-month expiration:** First standard expiration on or after the earnings date.

**Back-month expiration:** Next standard monthly expiration after front-month.

Weekly expirations SHOULD be avoided unless monthly is unavailable.

### 6.3 Strike Selection

The strike MUST be the listed strike closest to the current underlying price.

If two strikes are equidistant, select the lower strike for puts and the higher strike for calls.

### 6.4 Option Type Selection

For calendar spreads on equities:
- Use calls if underlying is above 50-day moving average
- Use puts if underlying is below 50-day moving average

This heuristic is advisory; either option type is valid.

---

## 7. Position Sizing

### 7.1 Kelly Criterion

The theoretical Kelly fraction is:

$$
f^* = \frac{p \cdot b - q}{b}
$$

Where:
- $p$ = historical win rate
- $q = 1 - p$ = historical loss rate
- $b$ = average win / average loss ratio

### 7.2 Fractional Kelly

The system uses fractional Kelly sizing:

| Signal Classification | Kelly Fraction |
|-----------------------|----------------|
| Recommended | 2% of full Kelly |
| Consider | 1% of full Kelly |

### 7.3 Position Size Calculation

$$
\text{Position Size} = \text{Kelly Fraction} \times \text{Portfolio Value} \times \text{Signal Multiplier}
$$

$$
\text{Number of Spreads} = \left\lfloor \frac{\text{Position Size}}{\text{Spread Cost}} \right\rfloor
$$

### 7.4 Position Limits

| Limit Type | Maximum | Enforcement |
|------------|---------|-------------|
| Single position | 2% of portfolio | Hard limit |
| Single underlying | 3% of portfolio | Hard limit |
| Sector exposure | 10% of portfolio | Soft warning at 8% |
| Strategy exposure | 20% of portfolio | Hard limit |
| Daily new positions | 5 | Hard limit |

---

## 8. Risk Management

### 8.1 Circuit Breakers

**Daily Loss Limit:**
- Threshold: -2% of portfolio NAV
- Action: Halt new position entry
- Reset: Next trading day

**Weekly Loss Limit:**
- Threshold: -5% of portfolio NAV (rolling 5 days)
- Action: Halt all trading; require manual override
- Reset: 24-hour cooling period plus manual review

**Volatility Spike:**
- Threshold: VIX > 35 or VIX change > +10 points intraday
- Action: Reduce new position sizes by 50%
- Reset: VIX < 30 for full session

### 8.2 Position Monitoring

Positions MUST be monitored for:

| Metric | Warning Threshold | Action |
|--------|-------------------|--------|
| Underlying move | > 5% from entry | Alert; evaluate delta hedge |
| Front IV collapse | > 20% pre-earnings | Alert; consider early close |
| Time to expiration | < 1 day post-earnings | Close position |
| Liquidity deterioration | Spread > 2x entry | Alert; widen exit limits |

### 8.3 Maximum Loss

The maximum loss on a calendar spread is the net debit paid. This MUST be computed at entry and recorded.

---

## 9. Pricing Engine

### 9.1 European Options

European options MUST be priced using the Black-Scholes formula:

**Call:**
$$
C = S \Phi(d_1) - K e^{-rT} \Phi(d_2)
$$

**Put:**
$$
P = K e^{-rT} \Phi(-d_2) - S \Phi(-d_1)
$$

Where:
$$
d_1 = \frac{\ln(S/K) + (r + \sigma^2/2)T}{\sigma\sqrt{T}}, \quad d_2 = d_1 - \sigma\sqrt{T}
$$

### 9.2 American Options

American options MUST be priced using the spectral collocation method.

**Requirements:**
- Chebyshev polynomial order: $\geq 12$
- Convergence tolerance: $10^{-8}$
- Validation: RMSE $< 1$ cent versus Andersen benchmarks

### 9.3 Implied Volatility

Implied volatility MUST be computed using Newton-Raphson iteration.

**Requirements:**
- Initial guess: Brenner-Subrahmanyam approximation
- Convergence tolerance: $10^{-8}$
- Maximum iterations: 50
- Failure handling: Return NaN; do not use fallback values

### 9.4 Greeks

The system MUST compute the following Greeks:

| Greek | Method | Precision |
|-------|--------|-----------|
| Delta | Analytic (European) or finite difference | $10^{-4}$ |
| Gamma | Analytic (European) or finite difference | $10^{-4}$ |
| Theta | Analytic (European) or finite difference | $10^{-4}$ |
| Vega | Analytic (European) or finite difference | $10^{-4}$ |

---

## 10. Execution

### 10.1 Order Types

| Order Type | Usage |
|------------|-------|
| Limit order | Default for all entries and exits |
| Market order | Emergency exits only |
| Spread order | Preferred for calendar spread execution |

### 10.2 Order Workflow

1. Generate trade recommendation
2. Validate against position limits
3. Check circuit breaker status
4. Compute limit price (natural minus improvement)
5. Submit order
6. Monitor for fill (timeout: 2 minutes)
7. If not filled, adjust to natural price
8. If still not filled (timeout: 1 minute), cancel and reassess

### 10.3 Execution Constraints

- Maximum order size: 1% of average daily volume
- Minimum price improvement attempt: $0.05
- Maximum slippage from mid: 2%
- Absolute slippage per spread MUST be capped in dollars; percent checks MAY be bypassed when debit basis is below the configured minimum.
- Execution cost percent MUST use a minimum capital basis; absolute execution cost per spread MUST be capped in dollars.

---

## 11. Logging and Audit

### 11.1 Required Log Events

| Event | Log Level | Required Fields |
|-------|-----------|-----------------|
| Signal generated | INFO | Symbol, criteria values, classification |
| Order submitted | INFO | Order ID, symbol, legs, price, quantity |
| Order filled | INFO | Order ID, fill price, fill quantity, timestamp |
| Order rejected | WARNING | Order ID, rejection reason |
| Circuit breaker triggered | ERROR | Breaker type, trigger value, threshold |
| Validation failure | ERROR | Field, value, reason |

### 11.2 Audit Trail

Every trade MUST record:
- Signal ID that generated the trade
- Input data provenance (timestamps, sources)
- Pricing inputs used
- Configuration hash at time of trade

---

## 12. Configuration

### 12.1 Required Configuration Parameters

| Parameter | Type | Default | Range |
|-----------|------|---------|-------|
| `ivrv_threshold` | decimal | 1.25 | [1.0, 3.0] |
| `min_days_to_earnings` | integer | 3 | [1, 10] |
| `max_days_to_earnings` | integer | 14 | [7, 30] |
| `kelly_fraction_recommended` | decimal | 0.02 | [0.005, 0.10] |
| `kelly_fraction_consider` | decimal | 0.01 | [0.005, 0.05] |
| `max_position_pct` | decimal | 0.02 | [0.01, 0.10] |
| `max_sector_pct` | decimal | 0.10 | [0.05, 0.30] |
| `daily_loss_limit` | decimal | 0.02 | [0.01, 0.10] |
| `weekly_loss_limit` | decimal | 0.05 | [0.02, 0.20] |
| `vol_lookback_days` | integer | 30 | [10, 60] |

Validation thresholds MUST be configured per run mode under `ForwardValidation` and `BackValidation` in `appsettings.jsonc`.
These settings include slippage percent, slippage dollars per spread, execution cost percent, execution cost dollars per spread,
minimum capital basis for percent calculations, and vega data policy.
Backtests MUST disable live fallback for options and earnings by setting
`Alaris:Backtest:RequireOptionChainCache` and `Alaris:Backtest:RequireEarningsCache` to true.

### 12.2 Configuration Validation

All configuration MUST be validated at startup. Invalid configuration MUST prevent system start.

---

## 13. Error Handling

### 13.1 Error Categories

| Category | Severity | Response |
|----------|----------|----------|
| Data error | Warning | Use fallback; log warning |
| Validation error | Error | Reject operation; log error |
| Execution error | Error | Cancel order; alert operator |
| System error | Critical | Halt affected component |
| Fatal error | Critical | Halt entire system |

### 13.2 Recovery Procedures

| Error Type | Recovery |
|------------|----------|
| Data feed disconnect | Reconnect with exponential backoff; halt trading after 5 minutes |
| Broker disconnect | Reconnect; reconcile positions; require manual confirmation |
| Pricing engine failure | Switch to fallback pricer; alert operator |
| Database failure | Halt trading; preserve in-memory state |

---

## 14. Performance Requirements

### 14.1 Latency

| Operation | Maximum Latency |
|-----------|-----------------|
| Signal evaluation (single symbol) | 100 ms |
| Volatility calculation (30 days) | 10 ms |
| Option pricing (single option) | 1 ms |
| Order submission | 500 ms |

### 14.2 Throughput

| Operation | Minimum Throughput |
|-----------|--------------------|
| Signal scans | 500 symbols per minute |
| Option pricing | 2,000 options per second |
| Volatility estimation | 10,000 calculations per second |

---

## Appendix A: Error Codes

| Code | Name | Description |
|------|------|-------------|
| E001 | InvalidPrice | Price failed validation |
| E002 | InsufficientData | Not enough historical data |
| E003 | StaleData | Data timestamp exceeds staleness threshold |
| E004 | CircuitBreakerOpen | Trading halted by circuit breaker |
| E005 | PositionLimitExceeded | Order would exceed position limit |
| E006 | OrderRejected | Broker rejected order |
| E007 | PricingFailure | Option pricing did not converge |
| E008 | ConfigurationInvalid | Configuration validation failed |

---

## Appendix B: Glossary

| Term | Definition |
|------|------------|
| ATM | At-the-money; strike nearest current price |
| BMO | Before market open |
| AMC | After market close |
| IV | Implied volatility |
| RV | Realised volatility |
| VRP | Volatility risk premium (IV minus RV) |
| OHLC | Open, high, low, close prices |
| NAV | Net asset value |
| ADV | Average daily volume |

---

*End of Specification*
