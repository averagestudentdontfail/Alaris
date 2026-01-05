# Practical Guide

*From Signal to Execution*

> "In theory, there is no difference between theory and practice. In practice, there is."
> — Attributed to Yogi Berra

## Preface

This document bridges the gap between theory and operation. It covers practical patterns for signal interpretation, position management, risk control, and troubleshooting.

The reader is assumed to have read the [Philosophy](philosophy.md) and [Foundations](foundations.md) documents, to understand options basics (calls, puts, spreads), and to be familiar with command-line interfaces.

This is not a tutorial for beginners in options trading; it is a guide for practitioners who understand the theory and wish to operate Alaris effectively.

---

## Part I: Signal Interpretation

### 1.1 Understanding Signal Output

When Alaris scans for opportunities, it produces structured output:

```
═══════════════════════════════════════════════════════════════════════════════
                            SIGNAL REPORT: AAPL
═══════════════════════════════════════════════════════════════════════════════

VOLATILITY ANALYSIS
───────────────────────────────────────────────────────────────────────────────
  30-day Implied Volatility:    34.2%
  30-day Realised Volatility:   26.1%  (Yang-Zhang)
  IV/RV Ratio:                  1.31   [PASS: ≥1.25]

TERM STRUCTURE
───────────────────────────────────────────────────────────────────────────────
  Front-month IV (Feb 21):      38.7%
  Back-month IV (Mar 21):       32.4%
  Spread:                       -6.3 points [PASS: Inverted]

LIQUIDITY
───────────────────────────────────────────────────────────────────────────────
  Average Daily Volume:         2,847,000 contracts
  ATM Bid-Ask Spread:          $0.02 (0.4%)
  Liquidity Score:             Excellent [PASS]

EARNINGS CONTEXT
───────────────────────────────────────────────────────────────────────────────
  Earnings Date:               Feb 21, 2025 (After Close)
  Days to Earnings:            6
  Historical Surprise:         +2.1% average move

═══════════════════════════════════════════════════════════════════════════════
                         RECOMMENDATION: TRADE
═══════════════════════════════════════════════════════════════════════════════
```

### 1.2 Signal Criteria in Detail

**IV/RV Ratio ≥ 1.25**

The IV/RV ratio measures how much the market's expected volatility exceeds recent realised volatility. A ratio of 1.25 means the market expects 25 per cent more volatility than history suggests.

The threshold of 1.25 is empirically derived. Lower thresholds capture more trades with lower average premium; higher thresholds miss opportunities. The value 1.25 balances capture rate against premium quality.

Edge cases:
- Ratio of 1.24: Fails the threshold. Do not round up.
- Ratio above 2.00: Unusually high. Check for corporate actions, M&A rumours, or data errors.
- Ratio below 1.00: Implied volatility is below realised. Rare; usually indicates regime change or data issues.

**Term Structure Inversion**

Normal term structure slopes upward: longer-dated options have higher implied volatility. Before earnings, near-term options often become more expensive than far-dated ones; the structure inverts.

Measurement:
$$
\text{Term Structure} = \text{IV}_{back} - \text{IV}_{front}
$$

Inverted when Term Structure < 0.

Inversion matters because calendar spreads sell front-month and buy back-month. Inverted structure means selling expensive volatility and buying cheaper volatility.

Edge cases:
- Flat structure (±1 point): Marginal inversion. Consider with caution.
- Steep inversion (−10 points or more): Strong signal; verify no unusual events.
- Normal structure (positive): Avoid calendar spreads; consider alternative strategies.

**Liquidity Adequacy**

Liquidity determines execution quality. Poor liquidity means wide spreads, slippage, and difficulty exiting positions.

| ADV (contracts) | Bid-Ask (%) | Assessment |
|-----------------|-------------|------------|
| > 1,000,000 | < 1% | Excellent |
| > 100,000 | < 2% | Adequate |
| > 10,000 | < 5% | Marginal |
| < 10,000 | > 5% | Avoid |

Position sizing rule: never exceed one per cent of average daily volume in a single order.

### 1.3 Signal Strength Levels

**Recommended (3/3 criteria)**

All conditions are met. This is the highest-confidence signal. Proceed with standard position sizing.

**Consider (2/3 criteria)**

Two conditions are met. Common scenarios:
- High IV/RV and inverted structure with marginal liquidity: reduce size.
- High IV/RV and adequate liquidity with flat structure: alternative structures may work.
- Inverted structure and adequate liquidity with modest IV/RV: lower expected premium.

Reduce position size to fifty per cent of standard for Consider signals.

**Avoid (0-1/3 criteria)**

Insufficient criteria met. Do not trade.

Exception: research mode may justify tracking these opportunities without execution.

---

## Part II: Position Construction

### 2.1 The Standard Calendar Spread

**Structure:**
- SELL 1 front-month ATM option (call or put)
- BUY 1 back-month ATM option (same type, same strike)

**Strike Selection:**

ATM strikes maximise gamma exposure and premium decay. Use the strike closest to current underlying price.

```
Current AAPL: $185.42
Available strikes: 182.5, 185, 187.5
Selected strike: 185 (closest to current)
```

**Expiration Selection:**

Front month: first expiration after the earnings announcement.
Back month: next monthly expiration (typically four to five weeks out).

```
Earnings: Feb 21 (After Close)
Front expiration: Feb 21 (captures earnings IV crush)
Back expiration: Mar 21 (one month out)
```

### 2.2 Position Entry Workflow

```
1. VERIFY signal is current (generated within last 30 minutes)
2. CHECK underlying price has not moved significantly (>2%) since signal
3. CONFIRM earnings date unchanged
4. CALCULATE position size (see Part IV)
5. DETERMINE limit price:
   - Natural price = Ask(back) − Bid(front)
   - Limit = Natural − $0.05 (attempt improvement)
6. SUBMIT limit order for calendar spread
7. WAIT for fill (up to 2 minutes)
8. IF not filled:
   - Adjust to natural price
   - WAIT 1 minute
   - IF still not filled, cancel and reassess
9. RECORD position in position log
```

### 2.3 Alternative Structures

**Double Calendar (Strangle Calendar)**

When uncertain about direction, use two calendar spreads:
- Call calendar at strike above current price
- Put calendar at strike below current price

This widens the profitable range yet increases cost.

**Diagonal Spread**

When you have a directional view:
- SELL front-month at one strike
- BUY back-month at different strike

Example (bullish diagonal):
- SELL Feb 185 Call
- BUY Mar 190 Call

This adds directional exposure to the calendar structure.

**Iron Calendar**

Combines put and call calendars at same strikes:
- SELL Feb 185 Put + BUY Mar 185 Put
- SELL Feb 185 Call + BUY Mar 185 Call

This doubles theta collection yet also doubles risk and capital requirement.

---

## Part III: Position Management

### 3.1 Pre-Earnings Monitoring

**Daily Checks:**
```bash
./alaris position monitor --portfolio live
```

Output:
```
POSITION MONITOR
═══════════════════════════════════════════════════════════════════════════════
Symbol  Structure        Entry    Current   P&L     Days   Status
───────────────────────────────────────────────────────────────────────────────
AAPL    Cal Feb/Mar 185  $6.20    $6.85    +10.5%   3      On Track
NVDA    Cal Feb/Mar 925  $24.50   $22.10   -9.8%    2      Warning: Price Move
META    Cal Feb/Mar 510  $18.30   $19.20   +4.9%    5      On Track
═══════════════════════════════════════════════════════════════════════════════
```

**Warning Conditions:**

| Condition | Trigger | Action |
|-----------|---------|--------|
| Large underlying move | >5% from entry | Evaluate delta hedge or early exit |
| IV collapse pre-earnings | Front IV drops >20% | Potential early profit; consider closing |
| IV spike | Front IV rises >30% | Hold; increased terminal profit potential |
| Liquidity deterioration | Spread doubles | Prepare for wider exit spreads |

### 3.2 The Earnings Event

**The Day Before:**
1. Verify position details (strikes, expirations)
2. Set alerts for after-hours and pre-market moves
3. Review historical earnings moves for the ticker
4. Confirm no competing corporate events

**The Announcement:**
1. Note the time of release (before open or after close)
2. Do not trade during the announcement
3. Wait for initial volatility to settle (fifteen to thirty minutes)

**The Morning After (for after-close announcements):**
```bash
./alaris position evaluate --symbol AAPL
```

Output:
```
POST-EARNINGS EVALUATION: AAPL
═══════════════════════════════════════════════════════════════════════════════
Underlying Move:      +3.2% ($185.42 → $191.35)
Front-month IV:       38.7% → 24.1% (−37.7% collapse)
Back-month IV:        32.4% → 28.9% (−10.8% decline)
Position P&L:         +$2.85 per spread (+46.0%)
Recommendation:       CLOSE at market open
═══════════════════════════════════════════════════════════════════════════════
```

### 3.3 Exit Strategies

**Strategy 1: Full Exit at Open**

Most common approach. Exit the entire position at market open after earnings.

Advantages: captures IV crush; avoids gamma risk from continued holding.
Disadvantages: may leave money on table if position has not reached maximum profit.

**Strategy 2: Staged Exit**

Exit half at open; hold remainder for continued theta decay.

When to use: position is profitable yet underlying has not reached strike.

**Strategy 3: Roll to Next Cycle**

If another earnings opportunity exists, roll the back-month to become the new front-month.

When to use: back-to-back earnings quarters with continuing elevated IV.

**Strategy 4: Conversion**

If underlying moves significantly, convert to directional position:
- Close the losing calendar
- Hold the profitable side
- Add directional exposure if thesis is strong

### 3.4 Position Exit Workflow

```
1. EVALUATE position post-earnings
2. DETERMINE exit strategy based on:
   - P&L status
   - Time remaining to front expiration
   - Underlying location relative to strike
3. CALCULATE exit price:
   - Natural price = Bid(back) − Ask(front)
   - Limit = Natural + $0.05 (be patient)
4. SUBMIT closing order
5. IF not filled in 5 minutes:
   - Adjust to natural
   - IF still not filled, hit bid/ask
6. RECORD exit in position log
7. UPDATE performance metrics
```

---

## Part IV: Risk Management

### 4.1 Position Sizing

**The Kelly Framework**

Alaris uses fractional Kelly sizing:

$$
\text{Position Size} = \text{Kelly Fraction} \times \text{Portfolio Value} \times \text{Signal Multiplier}
$$

Where:
- Kelly Fraction = 0.02 (two per cent of full Kelly)
- Signal Multiplier = 1.0 (Recommended) or 0.5 (Consider)

**Example Calculation:**

```
Portfolio Value:     $100,000
Signal:              Recommended
Spread Cost:         $6.20
Maximum Risk:        $6.20 per spread

Position Size = 0.02 × $100,000 × 1.0 = $2,000
Number of Spreads = $2,000 / $6.20 = 322 spreads

Round down to: 300 spreads (conservative)
Total Position: 300 × $6.20 = $1,860
Maximum Loss: $1,860 (1.86% of portfolio)
```

**Concentration Limits**

| Limit Type | Maximum | Rationale |
|------------|---------|-----------|
| Single position | 2% of portfolio | Limits single-name risk |
| Sector exposure | 10% of portfolio | Limits sector correlation |
| Weekly earnings | 5% of portfolio | Limits event clustering |
| Total calendar exposure | 20% of portfolio | Limits strategy risk |

### 4.2 Circuit Breakers

Alaris implements automatic circuit breakers:

**Daily Loss Limit:**
- Threshold: −2% of portfolio in single day
- Action: Halt new position entry; allow existing position management
- Reset: Next trading day

**Weekly Loss Limit:**
- Threshold: −5% of portfolio in rolling five days
- Action: Halt all trading; require manual override
- Reset: After 24-hour cooling period and manual review

**Volatility Spike:**
- Threshold: VIX > 35 or +10 points single day
- Action: Reduce new position sizes by 50%
- Reset: When VIX returns below 30

**Correlation Alarm:**
- Threshold: >70% of positions moving same direction
- Action: Alert for review; no automatic action
- Reset: Manual acknowledgement

### 4.3 Hedging Strategies

**Delta Neutralisation**

If underlying moves significantly, delta accumulates:

```
Position: 100 AAPL calendars at 185 strike
Underlying: Moved from 185 to 195
Estimated Delta: +0.25 per spread = +25 deltas total

Hedge: SHORT 25 shares AAPL
```

When to hedge: accumulated delta exceeds 0.5 per spread.

**VIX Hedge**

For large calendar portfolios, consider VIX call protection:

```
Calendar Portfolio: $50,000 notional
VIX Hedge: Buy VIX calls = 5% of portfolio notional
Structure: 2-month VIX calls, 20% OTM
```

This provides protection against systemic volatility spikes that would hurt all calendar positions.

---

## Part V: Backtesting

### 5.1 Running Backtests

**Basic Backtest:**
```bash
./alaris backtest run \
  --start 2020-01-01 \
  --end 2024-12-31 \
  --universe sp500 \
  --capital 100000
```

**Output:**
```
BACKTEST RESULTS: 2020-01-01 to 2024-12-31
═══════════════════════════════════════════════════════════════════════════════

PERFORMANCE SUMMARY
───────────────────────────────────────────────────────────────────────────────
  Total Return:            +47.3%
  Annualised Return:       +8.1%
  Sharpe Ratio:            1.24
  Sortino Ratio:           1.87
  Maximum Drawdown:        -12.4%
  Win Rate:                64.2%
  Average Win:             +18.3%
  Average Loss:            -9.1%
  Profit Factor:           1.82

TRADE STATISTICS
───────────────────────────────────────────────────────────────────────────────
  Total Trades:            847
  Recommended Signals:     512 (60.4%)
  Consider Signals:        335 (39.6%)
  Average Hold Period:     7.2 days

═══════════════════════════════════════════════════════════════════════════════
```

### 5.2 Backtest Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `--start` | Start date (YYYY-MM-DD) | Required |
| `--end` | End date (YYYY-MM-DD) | Today |
| `--universe` | Stock universe | sp500 |
| `--capital` | Starting capital | 100000 |
| `--sizing` | Position sizing method | kelly |
| `--slippage` | Assumed slippage per leg | 0.01 |
| `--commission` | Commission per contract | 0.65 |

### 5.3 Analysing Backtest Results

**Regime Analysis:**
```bash
./alaris backtest analyze --by-regime
```

Shows performance across VIX regimes:
- Low VIX (<15): Normal conditions
- Medium VIX (15-25): Elevated uncertainty
- High VIX (>25): Crisis conditions

**Sector Analysis:**
```bash
./alaris backtest analyze --by-sector
```

Shows performance by GICS sector. Identifies if certain sectors consistently underperform.

**Seasonality Analysis:**
```bash
./alaris backtest analyze --seasonality
```

Shows performance by month. Earnings cluster in certain months; identifies seasonal patterns.

### 5.4 Walk-Forward Optimisation

To avoid overfitting, use walk-forward analysis:

```bash
./alaris backtest walkforward \
  --start 2018-01-01 \
  --end 2024-12-31 \
  --train-months 24 \
  --test-months 6 \
  --parameter iv_threshold \
  --range 1.15:1.35:0.05
```

This trains on 24-month windows, tests on subsequent six months, and walks forward through the period. Reports out-of-sample performance for each parameter value.

---

## Part VI: Live Trading

### 6.1 Pre-Launch Checklist

Before enabling live trading:

- [ ] Paper traded for minimum 30 days
- [ ] Reviewed all backtest metrics
- [ ] Configured API credentials
- [ ] Set appropriate position limits
- [ ] Verified circuit breaker thresholds
- [ ] Tested order submission (single contract)
- [ ] Confirmed market data quality
- [ ] Established monitoring procedures
- [ ] Documented escalation procedures
- [ ] Obtained any required approvals

### 6.2 Starting Live Mode

**Paper Trading (Recommended First):**
```bash
./alaris live start --paper --capital 100000
```

**Live Trading:**
```bash
./alaris live start --capital 100000
```

### 6.3 Daily Operations

**Morning Routine (Pre-Market):**
```
06:30  Review overnight news for position holdings
06:45  Check pre-market prices for significant moves
07:00  Review today's earnings calendar
07:15  Verify system health
       ./alaris system health
07:30  Review any pending signals
       ./alaris signal pending
```

**Market Hours:**
```
09:30  Execute any pending morning exits
09:45  Process new signals
       ./alaris signal scan --execute
10:00  Verify order fills
       ./alaris orders status
...    Monitor positions throughout day
15:45  Final signal scan for next-day entries
16:00  End-of-day position reconciliation
       ./alaris position reconcile
```

**Evening Routine:**
```
16:30  Review day's P&L
       ./alaris report daily
17:00  Check after-hours earnings announcements
17:30  Update trading journal with observations
```

### 6.4 System Health Monitoring

```bash
./alaris system health
```

Output:
```
SYSTEM HEALTH
═══════════════════════════════════════════════════════════════════════════════
Component              Status    Latency    Last Update
───────────────────────────────────────────────────────────────────────────────
Market Data Feed       ● OK      12ms       2025-02-14 10:32:45
Options Chain API      ● OK      45ms       2025-02-14 10:32:42
Earnings Calendar      ● OK      --         2025-02-14 06:00:00
Broker Connection      ● OK      23ms       2025-02-14 10:32:45
Pricing Engine         ● OK      <1ms       --
Risk Engine            ● OK      <1ms       --
Database               ● OK      3ms        2025-02-14 10:32:45
═══════════════════════════════════════════════════════════════════════════════
Overall Status: OPERATIONAL
═══════════════════════════════════════════════════════════════════════════════
```

---

## Part VII: Troubleshooting

### 7.1 Common Issues

**Issue: Signal shows stale data**

Symptoms: prices have not updated; timestamps are old.

Diagnosis:
```bash
./alaris diagnostic data --symbol AAPL
```

Resolution:
1. Check API key validity
2. Verify network connectivity
3. Check for API rate limiting
4. Restart data feed service

**Issue: IV calculation returns unreasonable values**

Symptoms: IV > 200% or IV < 5% for liquid options.

Diagnosis:
```bash
./alaris diagnostic iv --symbol AAPL --expiry 2025-02-21 --strike 185
```

Resolution:
1. Verify option price is reasonable
2. Check for corporate actions affecting price
3. Verify interest rate input
4. Check for dividend adjustments

**Issue: Order not filling**

Symptoms: limit order sits unfilled despite fair price.

Diagnosis:
```bash
./alaris orders diagnose --order-id 12345
```

Resolution:
1. Check if market is open
2. Verify order price versus current market
3. Check for trading halts
4. Verify sufficient buying power
5. Check order routing settings

**Issue: Position reconciliation mismatch**

Symptoms: Alaris shows different position than broker.

Diagnosis:
```bash
./alaris position reconcile --verbose
```

Resolution:
1. Compare trade history
2. Check for partial fills not recorded
3. Verify corporate action adjustments
4. Manual position adjustment if needed

### 7.2 Diagnostic Commands

```bash
# Full system diagnostic
./alaris diagnostic all

# Volatility surface diagnostic
./alaris diagnostic volatility AAPL --output surface.html

# Pricing accuracy check
./alaris diagnostic pricing --benchmark

# Order flow analysis
./alaris diagnostic orders --last 24h

# Performance profiling
./alaris diagnostic performance --component pricing
```

### 7.3 Log Analysis

Logs are stored in `~/.alaris/logs/`:

```
alaris.log          # Main application log
data.log            # Market data events
orders.log          # Order lifecycle events
positions.log       # Position changes
errors.log          # Error events only
```

Common log patterns to watch:

```bash
# Recent errors
grep ERROR ~/.alaris/logs/alaris.log | tail -20

# Order failures
grep "ORDER_REJECTED" ~/.alaris/logs/orders.log

# Data gaps
grep "STALE_DATA" ~/.alaris/logs/data.log

# Circuit breaker triggers
grep "CIRCUIT_BREAKER" ~/.alaris/logs/alaris.log
```

---

## Part VIII: Performance Analysis

### 8.1 Key Metrics

**Return Metrics:**
- **Total Return:** Cumulative percentage gain or loss.
- **Annualised Return:** Geometric average annual return.
- **Risk-Adjusted Return:** Return per unit of risk (Sharpe).

**Risk Metrics:**
- **Maximum Drawdown:** Largest peak-to-trough decline.
- **Volatility:** Standard deviation of returns.
- **Value at Risk (VaR):** 95th percentile daily loss.

**Trade Metrics:**
- **Win Rate:** Percentage of profitable trades.
- **Profit Factor:** Gross profit divided by gross loss.
- **Average Win/Loss Ratio:** Mean profit divided by mean loss.

### 8.2 Generating Reports

```bash
# Daily report
./alaris report daily --date 2025-02-14

# Weekly summary
./alaris report weekly --week 2025-W07

# Monthly performance
./alaris report monthly --month 2025-02

# Annual review
./alaris report annual --year 2024
```

### 8.3 Performance Attribution

Understanding why performance occurred is as important as measuring what occurred.

```bash
./alaris report attribution --period 2024-Q4
```

Output:
```
PERFORMANCE ATTRIBUTION: 2024-Q4
═══════════════════════════════════════════════════════════════════════════════

TOTAL RETURN: +5.2%

FACTOR ATTRIBUTION
───────────────────────────────────────────────────────────────────────────────
  Theta Decay:           +4.1%   (primary driver)
  IV Collapse:           +2.3%   (earnings events)
  Gamma P&L:             -0.8%   (underlying moves)
  Vega P&L:              -0.2%   (term structure changes)
  Transaction Costs:     -0.2%   (commissions + slippage)
───────────────────────────────────────────────────────────────────────────────
  Net:                   +5.2%

SIGNAL ATTRIBUTION
───────────────────────────────────────────────────────────────────────────────
  Recommended Signals:   +4.1%   (72 trades, 68% win rate)
  Consider Signals:      +1.1%   (31 trades, 58% win rate)

═══════════════════════════════════════════════════════════════════════════════
```

---

## Appendix A: Quick Reference

### Command Cheat Sheet

```bash
# Signals
./alaris signal scan                    # Scan for opportunities
./alaris signal scan --symbol AAPL      # Single ticker
./alaris signal pending                 # View pending signals

# Positions
./alaris position list                  # Current positions
./alaris position monitor               # Live monitoring
./alaris position close AAPL            # Close specific position

# Orders
./alaris orders status                  # Order status
./alaris orders cancel --all            # Cancel all orders

# Analysis
./alaris analyze AAPL                   # Ticker analysis
./alaris analyze AAPL --chart           # With chart output

# Backtesting
./alaris backtest run --quick           # Quick backtest (1 year)
./alaris backtest run --full            # Full backtest (5 years)

# System
./alaris system health                  # Health check
./alaris system status                  # Detailed status
./alaris system restart                 # Restart services

# Reports
./alaris report daily                   # Today's report
./alaris report pnl                     # P&L summary
```

### Glossary

| Term | Definition |
|------|------------|
| **ATM** | At-the-money; strike nearest current price |
| **IV** | Implied volatility; market's expected volatility |
| **RV** | Realised volatility; historical actual volatility |
| **VRP** | Volatility risk premium; IV minus RV |
| **Theta** | Time decay; daily P&L from time passage |
| **Gamma** | Convexity; sensitivity of delta to price |
| **Vega** | Volatility sensitivity; P&L per 1% IV change |
| **Calendar** | Spread selling near-dated, buying far-dated |
| **IV Crush** | Rapid IV decline after earnings announcement |
| **Term Structure** | IV variation across expirations |

---

*End of Guide*
