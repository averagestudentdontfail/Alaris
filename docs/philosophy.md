# Philosophy of Alaris

*On Markets, Uncertainty, and the Nature of Edge*

> "In the short run, the market is a voting machine but in the long run, it is a weighing machine."
> — Benjamin Graham

## Preface

This document explores the philosophical foundations of Alaris. Reading it is not required for operating the system; however, it may illuminate the reasoning behind design decisions and the assumptions that underlie the strategy.

Every trading system embeds a worldview. Assumptions about markets, risk, causation, and human behaviour shape the code as surely as the mathematics. Most trading software conceals these assumptions beneath layers of abstraction. Alaris makes its philosophy explicit, rendering it available for scrutiny and refinement.

---

## Part I: The Nature of Markets

### What Is a Market?

A market is a coordination mechanism. It aggregates dispersed information, reconciles competing valuations, and produces prices that guide the allocation of capital. The prices that emerge reflect the collective beliefs of participants about the future; they are forecasts made manifest in currency.

Markets exhibit several fundamental properties:

**Reflexivity.** Participants observe prices, form beliefs, act on those beliefs, and thereby change the prices they observe. The map influences the territory. George Soros articulated this principle: market prices do not passively reflect reality but actively shape it through the feedback loop of perception and action.

**Uncertainty.** The future is genuinely unknown. Unlike games of chance with well-defined probability distributions, markets involve Knightian uncertainty; the relevant probability spaces cannot be specified in advance. New information arrives unpredictably; regimes shift; correlations that held for decades dissolve overnight.

**Competition.** Markets are adversarial environments. Every profitable trade has a counterparty who, in expectation, loses. Persistent profitability requires an edge that survives the scrutiny of competitors seeking to eliminate it.

**Friction.** Real markets involve transaction costs, information asymmetries, regulatory constraints, and liquidity limitations. These frictions create inefficiencies that can be exploited, yet they also impose costs that erode theoretical profits.

### The Efficient Market Hypothesis

Eugene Fama's efficient market hypothesis (EMH) holds that prices fully reflect all available information. In its strong form, the EMH implies that no trading strategy can consistently outperform the market on a risk-adjusted basis.

The EMH is useful as an idealisation. It establishes a null hypothesis against which claims of edge must be tested. It warns against overconfidence in pattern recognition. It explains why most active managers underperform passive indices after fees.

The EMH is incomplete as a description of reality. Markets exhibit persistent anomalies: momentum, value, low volatility, and, most relevant to Alaris, the volatility risk premium. These anomalies may represent compensation for risk, behavioural biases, or structural features of market microstructure. They persist because exploiting them involves costs, risks, or constraints that limit arbitrage.

Alaris operates in the space between perfect efficiency and exploitable inefficiency. It targets a premium that is well-documented, economically rational, and structurally persistent.

---

## Part II: The Volatility Risk Premium

### Definition

The volatility risk premium (VRP) is the difference between implied volatility and subsequently realised volatility:

$$
\text{VRP}(t, T) = \sigma_I(t, T) - \sigma_R(t, T)
$$

Where $\sigma_I(t, T)$ is the implied volatility at time $t$ for options expiring at $T$, and $\sigma_R(t, T)$ is the volatility realised over the period $[t, T]$.

The VRP is positive on average. Implied volatility systematically exceeds realised volatility approximately eighty per cent of the time for equity index options. This regularity has been documented across markets, asset classes, and decades.

### Why Does the Premium Exist?

The VRP exists because it should exist. It represents compensation for bearing uncertainty, and compensation for risk is the foundation of financial returns.

**Risk transfer.** Option buyers seek protection against adverse price movements. They are willing to pay more than actuarially fair value for this insurance, particularly when the insured event is salient, imminent, or catastrophic. Option sellers provide this insurance and receive the premium as compensation for accepting the associated risk.

**Demand asymmetry.** The demand for protection exceeds the supply of risk-bearing capacity. Institutional investors, constrained by mandates, regulations, and risk committees, cannot freely sell volatility. Retail investors, lacking sophistication, do not participate. The remaining sellers can extract a premium for their willingness to bear risk.

**Behavioural factors.** Human psychology amplifies the premium. The availability heuristic causes overweighting of vivid, easily imagined disasters. Loss aversion makes potential losses loom larger than equivalent gains. These cognitive biases increase the demand for protection and the price buyers are willing to pay.

### Earnings Amplification

The VRP intensifies around corporate earnings announcements. In the days preceding earnings, implied volatility rises as traders anticipate the binary outcome of the announcement. After the announcement, implied volatility collapses as uncertainty resolves. This pattern creates a predictable premium in time-limited options.

The earnings VRP represents a concentration of uncertainty into a defined window. Traders are uncertain about the earnings figure, guidance, market reaction, and knock-on effects. They bid up protection against adverse outcomes. Sellers of this protection, by bearing the risk through the announcement, earn the premium when volatility collapses.

---

## Part III: The Philosophy of Risk

### Risk and Volatility Are Distinct

Modern finance conflates risk with volatility. The Capital Asset Pricing Model, the Sharpe ratio, and value-at-risk all treat volatility as the measure of risk. This conflation is a category error.

**Volatility** is a statistical property: the dispersion of returns around their mean. It is symmetric; upside deviations and downside deviations receive equal weight. A stock that rises unpredictably exhibits high volatility regardless of whether the surprises are favourable.

**Risk** is the probability and magnitude of permanent capital impairment. It is asymmetric; losses matter more than gains because capital lost cannot compound. Risk is the probability that you will not achieve your financial objectives; it encompasses not only volatility but also leverage, liquidity, concentration, and catastrophic loss.

Alaris distinguishes between volatility exposure (the magnitude of short-term price fluctuations) and risk exposure (the probability of unrecoverable loss). Calendar spreads exhibit high volatility exposure yet limited risk exposure; the maximum loss is the net debit paid, and this maximum is known at entry.

### The Kelly Criterion

The Kelly criterion prescribes the optimal bet size for long-run geometric growth:

$$
f^* = \frac{p \cdot b - q}{b}
$$

Where $p$ is the probability of winning, $q = 1 - p$ is the probability of losing, and $b$ is the win/loss ratio.

Full Kelly maximises expected geometric growth; however, it tolerates extreme drawdowns along the path. A single adverse run can devastate a portfolio before long-run expectations manifest. The Kelly optimal bettor experiences severe volatility in wealth even when the underlying edge is genuine.

Alaris uses fractional Kelly: two per cent of the Kelly-optimal fraction for recommended signals, one per cent for consider signals. This conservatism sacrifices expected return for dramatically reduced probability of ruin. The philosophy is direct: survival precedes returns.

### Ergodicity

Traditional expected value calculations assume ergodicity: that time averages equal ensemble averages. For a single trader with one portfolio, this assumption fails.

Consider a bet with fifty per cent probability of sixty per cent gain and fifty per cent probability of fifty per cent loss. The ensemble average is positive five per cent expected return. The time average after two rounds is $0.6 \times 0.5 \times 0.6 \times 0.5 = 0.09$, or a ninety-one per cent loss.

The lesson is fundamental: the expectation across many parallel trials differs from the experience of a single trial extended through time. A strategy can have positive expected value yet lead to ruin with certainty if sizing is wrong.

Alaris is designed for non-ergodic reality. Position sizing, diversification across signals, and circuit breakers all address the gap between ensemble expectations and lived portfolio experience.

---

## Part IV: The Philosophy of Uncertainty

### Known and Unknown

Donald Rumsfeld's epistemological framework applies directly to trading:

**Known knowns.** The current market state: prices, volumes, implied volatilities, earnings dates. These are observable and form the inputs to Alaris.

**Known unknowns.** Uncertain yet measurable quantities. Implied volatility represents the market's estimate of unknown future volatility. Alaris trades on the relationship between this estimate and historical patterns.

**Unknown unknowns.** Genuinely unexpected events. Flash crashes, regulatory changes, global pandemics, black swans. Alaris cannot predict these events; it can limit exposure through position sizing, hedging, and circuit breakers.

The philosophy of uncertainty counsels humility. Backtests reveal what would have happened; they do not guarantee what will happen. Historical patterns may persist, shift, or invert. The prudent approach assumes that the future will surprise us and positions accordingly.

### The Limits of Prediction

Alaris does not claim to predict stock prices. It claims to identify situations where implied volatility is likely elevated relative to subsequent realised volatility. This is a weaker yet more defensible claim.

The signal criteria (IV/RV ratio, term structure inversion, adequate liquidity) identify preconditions for premium capture. They do not predict the magnitude of the premium, the direction of the underlying, or the timing of convergence. They establish that conditions are favourable for a specific trade structure.

This epistemic modesty shapes the system design. Signals are binary (recommended, consider, avoid) rather than continuous. Position sizing is coarse-grained. The philosophy is: acknowledge what you do not know and act decisively on what you do.

### Precision and Illusion

Financial models produce outputs to many decimal places. This precision is illusory.

The true price of an option depends on unknown future volatility, unknown future interest rates, unknown early exercise behaviour, and unknown liquidity conditions. Model prices are estimates derived from assumptions about unknowable futures. The fourth decimal place of an option price is noise masquerading as signal.

Alaris embraces acknowledged uncertainty over false precision. Signals are discrete categories. Position sizes are rounded to practical quantities. The philosophy is: uncertainty honestly reported is more valuable than precision falsely claimed.

---

## Part V: The Philosophy of Design

### Simplicity

The Alaris signal is simple: IV/RV ratio, term structure, liquidity. Three conditions, binary thresholds.

This simplicity is deliberate. Simple models are robust to regime change; they depend on few parameters that could shift. Simple signals are explicable to stakeholders; they can be understood, questioned, and improved. Simple code has fewer bugs; each line of code is a liability as well as an asset.

Complex strategies may capture more edge in sample. They also fail more spectacularly out of sample. The pursuit of optimality often produces fragility. Alaris chooses robustness over optimality.

### Separation of Concerns

Alaris separates responsibilities:

**Core.** Pure mathematical functions: pricing, Greeks, volatility estimation. These are deterministic, testable, and free of side effects.

**Strategy.** Trading logic that depends on market structure: signal generation, position construction, risk management. Strategy consumes Core but can evolve independently.

**Infrastructure.** External interfaces: data feeds, persistence, messaging, broker connections. Infrastructure changes when vendors or APIs change; Core and Strategy remain stable.

This separation enables independent evolution. Pricing algorithms can be optimised without touching strategy logic. Data sources can be changed without rewriting mathematical functions. Each layer has a single reason to change.

### Explicitness

Alaris makes assumptions explicit:

Interest rate assumptions are configuration parameters, not hardcoded constants. Volatility window parameters are visible and documented. Risk thresholds are stated in configuration files, not buried in code.

When assumptions are implicit, they become surprises. Explicit assumptions can be questioned, tested, and modified. They form the basis for reasoned disagreement and iterative improvement.

---

## Part VI: The Philosophy of Integrity

### What Integrity Means

Alaris is designed as a high-integrity trading system. Integrity in this context means:

**Determinism.** Given the same inputs, the system produces the same outputs. No hidden state, no random behaviour without explicit seeding, no dependence on timing or ordering that could vary between runs.

**Traceability.** Every decision can be traced to its inputs. A signal can be decomposed into its constituent criteria. A price can be traced to its market data provenance. An order can be linked to the signal that generated it.

**Fault isolation.** Failures are contained. Data feed failures do not corrupt pricing. Pricing errors do not affect position sizing. Position sizing errors do not crash the system.

**Graceful degradation.** Under stress, the system reduces activity rather than making increasingly poor decisions. Circuit breakers halt trading before catastrophic losses. Missing data produces no signal rather than a default signal.

### The Cost of Integrity

High-integrity design has costs:

Development takes longer. Rigorous testing, fault handling, and validation require effort that could otherwise produce features. Performance may be reduced. Validation checks add latency; immutable data structures add memory overhead. Complexity increases. Error handling, logging, and fallback logic add lines of code.

These costs are worth paying. A trading system that operates correctly ninety-nine point nine per cent of the time yet fails catastrophically in the remaining fraction will eventually fail catastrophically. The rare case is not rare; it is inevitable given sufficient time.

### Trust and Verification

Alaris trusts its data sources yet verifies constantly:

Price reasonableness checks reject implausible quotes. Implied volatility arbitrage checks detect surfaces that violate no-arbitrage conditions. Volume consistency checks identify spurious liquidity signals.

When verification fails, the system does not guess. It raises a fault and awaits correction. The philosophy is: it is better to miss an opportunity than to trade on corrupted data.

---

## Conclusion

Alaris rests on philosophical commitments:

Markets are competitive, uncertain, and reflexive; persistent edge requires structural foundations rather than informational advantage. The volatility risk premium is such a foundation: compensation for bearing uncertainty that persists because it should persist.

Risk and volatility are distinct concepts requiring distinct treatment. Position sizing, informed by the Kelly criterion and tempered by fractional betting, balances expected return against probability of ruin.

Uncertainty is irreducible. Historical data informs yet does not guarantee. Epistemic humility counsels conservatism in claims, simplicity in models, and robustness in design.

Integrity has costs worth paying. Determinism, traceability, fault isolation, and graceful degradation prevent the rare failure that would otherwise be catastrophic.

Simplicity and robustness outweigh cleverness and optimality. Systems that survive are systems that can be understood, tested, and corrected.

Whether this philosophy proves profitable is an empirical question that only time can answer. The foundations are defensible, the mathematics sound, and the implementation rigorous.

*That is the philosophy of Alaris.*

---

## Further Reading

**Market Philosophy**

- Soros, G. (1987). *The Alchemy of Finance*. Simon & Schuster.
- Taleb, N. N. (2007). *The Black Swan*. Random House.
- Mandelbrot, B. (2004). *The (Mis)Behavior of Markets*. Basic Books.
- Peters, O. (2019). "The ergodicity problem in economics." *Nature Physics*.

**Risk and Position Sizing**

- Kelly, J. L. (1956). "A New Interpretation of Information Rate." *Bell System Technical Journal*.
- Thorp, E. O. (2017). *A Man for All Markets*. Random House.
- MacLean, L. C., Thorp, E. O., & Ziemba, W. T. (2011). *The Kelly Capital Growth Investment Criterion*. World Scientific.

**Volatility and Options**

- Sinclair, E. (2013). *Volatility Trading*. Wiley.
- Natenberg, S. (2014). *Option Volatility and Pricing*. McGraw-Hill.
- Carr, P., & Wu, L. (2009). "Variance Risk Premiums." *Review of Financial Studies*.
