# Mathematical Foundations

*From First Principles to Volatility Premium Capture*

> "Mathematics is the language with which God has written the universe."
> — Galileo Galilei

## Overview

This document develops the mathematical theory underlying Alaris. We proceed from elementary concepts through option pricing theory to the advanced numerical methods employed in practice. Each section builds upon the previous; the reader who works through the material sequentially will acquire a complete understanding of the computational machinery.

Prerequisites: familiarity with calculus, basic probability theory, and elementary statistics. Prior exposure to financial mathematics is helpful yet not essential; the relevant concepts are developed from first principles.

---

## Part I: Probability and Random Variables

### 1.1 The Nature of Randomness

Financial markets generate sequences of prices that appear random. Tomorrow's closing price cannot be predicted with certainty from today's information. This unpredictability is the foundation of both risk and opportunity.

A **random variable** $X$ is a function that assigns numerical values to the outcomes of a random process. The closing price of a stock tomorrow is a random variable; its value depends on events yet to occur.

The **probability distribution** of $X$ describes the likelihood of each possible value. For continuous random variables such as stock prices, the distribution is characterised by a probability density function $f(x)$ such that:

$$
P(a \leq X \leq b) = \int_a^b f(x) \, dx
$$

The **expected value** (mean) of $X$ is:

$$
E[X] = \int_{-\infty}^{\infty} x \cdot f(x) \, dx
$$

The **variance** measures the dispersion of $X$ around its mean:

$$
\text{Var}(X) = E[(X - E[X])^2] = E[X^2] - (E[X])^2
$$

The **standard deviation** is the square root of variance:

$$
\sigma_X = \sqrt{\text{Var}(X)}
$$

### 1.2 The Normal Distribution

The normal (Gaussian) distribution plays a central role in financial modelling. A random variable $X$ is normally distributed with mean $\mu$ and variance $\sigma^2$ if its density is:

$$
f(x) = \frac{1}{\sigma\sqrt{2\pi}} \exp\left(-\frac{(x-\mu)^2}{2\sigma^2}\right)
$$

We write $X \sim N(\mu, \sigma^2)$.

The **standard normal distribution** has $\mu = 0$ and $\sigma = 1$. Its cumulative distribution function is denoted $\Phi(z)$:

$$
\Phi(z) = P(Z \leq z) = \frac{1}{\sqrt{2\pi}} \int_{-\infty}^{z} e^{-t^2/2} \, dt
$$

The function $\Phi$ appears throughout option pricing; it gives the probability that a standard normal variable falls below a given threshold.

### 1.3 Log-Normal Distribution

Stock prices cannot be negative. The log-normal distribution respects this constraint: if $\ln(S)$ is normally distributed, then $S$ is log-normally distributed and necessarily positive.

If $S$ is log-normal with parameters $\mu$ and $\sigma$, then:

$$
E[S] = e^{\mu + \sigma^2/2}
$$

$$
\text{Var}(S) = e^{2\mu + \sigma^2}(e^{\sigma^2} - 1)
$$

The Black-Scholes model assumes that stock prices follow geometric Brownian motion, which implies that future prices are log-normally distributed.

---

## Part II: Stochastic Processes

### 2.1 Brownian Motion

**Brownian motion** (also called a Wiener process) is a continuous-time stochastic process $W_t$ with the following properties:

1. $W_0 = 0$
2. $W_t$ has independent increments: $W_t - W_s$ is independent of $W_u$ for $u \leq s < t$
3. $W_t - W_s \sim N(0, t-s)$ for $s < t$
4. $W_t$ has continuous paths

Brownian motion is the mathematical formalisation of random walk in continuous time. It captures the unpredictable, perpetual fluctuation observed in market prices.

### 2.2 Geometric Brownian Motion

Stock prices are modelled by **geometric Brownian motion** (GBM):

$$
dS_t = \mu S_t \, dt + \sigma S_t \, dW_t
$$

Where:
- $S_t$ is the stock price at time $t$
- $\mu$ is the drift (expected return per unit time)
- $\sigma$ is the volatility (standard deviation of returns per unit time)
- $W_t$ is standard Brownian motion

The solution to this stochastic differential equation is:

$$
S_T = S_0 \exp\left[\left(\mu - \frac{\sigma^2}{2}\right)T + \sigma W_T\right]
$$

This formula shows that future stock prices are log-normally distributed. The parameter $\sigma$ determines the dispersion of future prices; it is the volatility that Alaris estimates and trades.

### 2.3 Itô's Lemma

For a function $f(S_t, t)$ where $S_t$ follows GBM, Itô's lemma gives:

$$
df = \left(\frac{\partial f}{\partial t} + \mu S \frac{\partial f}{\partial S} + \frac{1}{2}\sigma^2 S^2 \frac{\partial^2 f}{\partial S^2}\right) dt + \sigma S \frac{\partial f}{\partial S} \, dW
$$

This formula is the chain rule for stochastic calculus. The extra term involving the second derivative arises because Brownian motion has non-zero quadratic variation: $(dW)^2 = dt$.

Itô's lemma is the foundation for deriving the Black-Scholes equation and all its extensions.

---

## Part III: Option Pricing Theory

### 3.1 Option Contracts

An **option** is a derivative contract that gives the holder the right, but not the obligation, to buy or sell an underlying asset at a specified price on or before a specified date.

**Call option:** The right to buy the underlying at strike price $K$.
**Put option:** The right to sell the underlying at strike price $K$.

**European options** can be exercised only at expiration.
**American options** can be exercised at any time before or at expiration.

At expiration $T$, the payoff of a European call is:

$$
C_T = \max(S_T - K, 0) = (S_T - K)^+
$$

The payoff of a European put is:

$$
P_T = \max(K - S_T, 0) = (K - S_T)^+
$$

### 3.2 Risk-Neutral Valuation

The fundamental insight of modern option pricing is **risk-neutral valuation**: option prices equal the discounted expected payoff under the risk-neutral probability measure $\mathbb{Q}$.

Under $\mathbb{Q}$, the stock price evolves as:

$$
dS_t = r S_t \, dt + \sigma S_t \, dW_t^{\mathbb{Q}}
$$

Where $r$ is the risk-free rate. The drift is replaced by the risk-free rate; the volatility remains unchanged.

The price of a European derivative with payoff $V_T$ at time $T$ is:

$$
V_0 = e^{-rT} E^{\mathbb{Q}}[V_T]
$$

This formula transforms the pricing problem into an expectation calculation under a modified probability measure.

### 3.3 The Black-Scholes Formula

For a European call option on a non-dividend-paying stock, the Black-Scholes formula gives:

$$
C = S_0 \Phi(d_1) - K e^{-rT} \Phi(d_2)
$$

Where:

$$
d_1 = \frac{\ln(S_0/K) + (r + \sigma^2/2)T}{\sigma\sqrt{T}}
$$

$$
d_2 = d_1 - \sigma\sqrt{T}
$$

For a European put:

$$
P = K e^{-rT} \Phi(-d_2) - S_0 \Phi(-d_1)
$$

The Black-Scholes formula provides closed-form prices for European options. It depends on five inputs: spot price $S_0$, strike $K$, time to expiration $T$, risk-free rate $r$, and volatility $\sigma$.

### 3.4 Put-Call Parity

European puts and calls with the same strike and expiration satisfy:

$$
C - P = S_0 - K e^{-rT}
$$

This relationship follows from arbitrage arguments. If put-call parity is violated, a riskless profit can be extracted by trading the mispriced options against the correctly priced ones.

---

## Part IV: Implied Volatility

### 4.1 The Inverse Problem

Volatility $\sigma$ is the only unobservable input to the Black-Scholes formula. Given an observed market price $V_{obs}$, **implied volatility** $\sigma_I$ is the value that makes the model price equal to the market price:

$$
V_{BS}(S, K, r, T, \sigma_I) = V_{obs}
$$

This is an inverse problem: finding the input that produces the observed output.

### 4.2 Newton-Raphson Solution

The Black-Scholes price is monotonically increasing in volatility, guaranteeing a unique solution. Newton-Raphson iteration solves efficiently:

$$
\sigma_{n+1} = \sigma_n - \frac{V_{BS}(\sigma_n) - V_{obs}}{\nu(\sigma_n)}
$$

Where $\nu = \partial V / \partial \sigma$ is **vega**:

$$
\nu = S_0 \sqrt{T} \, \phi(d_1)
$$

With $\phi$ the standard normal density.

Convergence is typically achieved in three to five iterations for precision of $10^{-8}$.

### 4.3 Initial Guess: Brenner-Subrahmanyam

For at-the-money options, Brenner and Subrahmanyam (1988) provide an approximation:

$$
\sigma_0 \approx \sqrt{\frac{2\pi}{T}} \cdot \frac{V_{obs}}{S_0}
$$

This serves as an excellent initial guess, reducing the number of iterations required.

### 4.4 The Volatility Surface

Implied volatility varies with strike $K$ and expiration $T$, forming a surface $\sigma_I(K, T)$.

**The smile.** For fixed $T$, $\sigma_I(K)$ often exhibits a "smile" shape: higher implied volatility for options far from at-the-money than for those near it.

**The skew.** For equity options, the smile is typically asymmetric: implied volatility is higher for low strikes (out-of-the-money puts) than for high strikes (out-of-the-money calls). This reflects demand for downside protection.

**Term structure.** For fixed moneyness, $\sigma_I(T)$ varies with expiration. Inverted term structure (short-dated volatility exceeds long-dated) indicates elevated near-term uncertainty.

---

## Part V: Volatility Estimation

### 5.1 The Estimation Problem

Volatility is latent; it cannot be observed directly. We observe prices and infer volatility from their behaviour.

**Given:** A sequence of prices $\{P_0, P_1, \ldots, P_n\}$ over $n$ periods.
**Find:** An estimate $\hat{\sigma}$ of the annualised volatility.

### 5.2 Close-to-Close Estimator

The simplest approach uses logarithmic returns:

$$
r_i = \ln\left(\frac{P_i}{P_{i-1}}\right)
$$

The close-to-close variance estimator is:

$$
\hat{\sigma}_{CC}^2 = \frac{252}{n-1} \sum_{i=1}^{n} (r_i - \bar{r})^2
$$

Where 252 is the number of trading days per year.

**Efficiency:** The close-to-close estimator has efficiency 1.0 (baseline).

### 5.3 Parkinson Estimator

Parkinson (1980) demonstrated that incorporating intraday high and low prices improves efficiency:

$$
\hat{\sigma}_{P}^2 = \frac{252}{4n\ln(2)} \sum_{i=1}^{n} (\ln H_i - \ln L_i)^2
$$

Where $H_i$ and $L_i$ are the high and low prices on day $i$.

**Efficiency:** Approximately 5.2 times the close-to-close estimator in the absence of drift.

### 5.4 Rogers-Satchell Estimator

Rogers and Satchell (1991) developed a drift-independent estimator:

$$
\hat{\sigma}_{RS}^2 = \frac{252}{n} \sum_{i=1}^{n} \left[ (\ln H_i - \ln O_i)(\ln H_i - \ln C_i) + (\ln L_i - \ln O_i)(\ln L_i - \ln C_i) \right]
$$

Where $O_i$ and $C_i$ are the open and close prices.

**Efficiency:** Approximately 6.0 times the close-to-close estimator, robust to non-zero drift.

### 5.5 Yang-Zhang Estimator

Yang and Zhang (2000) combined overnight and intraday information:

$$
\hat{\sigma}_{YZ}^2 = \hat{\sigma}_o^2 + k\hat{\sigma}_c^2 + (1-k)\hat{\sigma}_{RS}^2
$$

Where:

**Overnight variance:**
$$
\hat{\sigma}_o^2 = \frac{252}{n-1} \sum_{i=1}^{n} (\ln O_i - \ln C_{i-1} - \bar{o})^2
$$

**Open-to-close variance:**
$$
\hat{\sigma}_c^2 = \frac{252}{n-1} \sum_{i=1}^{n} (\ln C_i - \ln O_i - \bar{c})^2
$$

**Optimal weighting:**
$$
k = \frac{0.34}{1.34 + \frac{n+1}{n-1}}
$$

**Efficiency:** Approximately 8.0 times the close-to-close estimator; minimum variance among OHLC estimators.

Alaris uses the Yang-Zhang estimator with a 30-day lookback window.

---

## Part VI: The Greeks

### 6.1 First-Order Sensitivities

The **Greeks** measure how option prices respond to changes in inputs.

**Delta ($\Delta$):** Sensitivity to underlying price.
$$
\Delta = \frac{\partial V}{\partial S}
$$
For a call: $\Delta_C = \Phi(d_1)$. For a put: $\Delta_P = \Phi(d_1) - 1$.

**Vega ($\nu$):** Sensitivity to volatility.
$$
\nu = \frac{\partial V}{\partial \sigma} = S_0 \sqrt{T} \, \phi(d_1)
$$

**Theta ($\Theta$):** Time decay.
$$
\Theta = \frac{\partial V}{\partial t}
$$

**Rho ($\rho$):** Sensitivity to interest rate.
$$
\rho = \frac{\partial V}{\partial r}
$$

### 6.2 Second-Order Sensitivities

**Gamma ($\Gamma$):** Convexity in underlying price.
$$
\Gamma = \frac{\partial^2 V}{\partial S^2} = \frac{\phi(d_1)}{S_0 \sigma \sqrt{T}}
$$

**Vanna:** Cross-sensitivity of delta to volatility.
$$
\text{Vanna} = \frac{\partial^2 V}{\partial S \partial \sigma}
$$

**Volga (Vomma):** Convexity in volatility.
$$
\text{Volga} = \frac{\partial^2 V}{\partial \sigma^2}
$$

### 6.3 Calendar Spread Greeks

A calendar spread sells the front-month option and buys the back-month option at the same strike. The net Greeks are:

$$
\Delta_{net} = \Delta_{back} - \Delta_{front} \approx 0 \text{ (near ATM)}
$$

$$
\Gamma_{net} = \Gamma_{back} - \Gamma_{front} < 0 \text{ (short gamma)}
$$

$$
\Theta_{net} = \Theta_{back} - \Theta_{front} > 0 \text{ (positive theta)}
$$

$$
\nu_{net} = \nu_{back} - \nu_{front} > 0 \text{ (long vega)}
$$

Calendar spreads profit from time decay (positive theta) and volatility collapse (long vega when IV decreases after establishment).

---

## Part VII: American Option Pricing

### 7.1 The Early Exercise Premium

American options can be exercised before expiration. This feature adds value; an American option is worth at least as much as the corresponding European option.

The **early exercise premium** is:

$$
\mathcal{P}(S, t) = V_{American}(S, t) - V_{European}(S, t)
$$

For American puts on non-dividend-paying stocks, early exercise may be optimal when the stock price is sufficiently low; receiving the strike price immediately has value due to the time value of money.

### 7.2 The Free Boundary Problem

The American option price satisfies a partial differential equation with a free boundary:

$$
\frac{\partial V}{\partial t} + \frac{1}{2}\sigma^2 S^2 \frac{\partial^2 V}{\partial S^2} + rS\frac{\partial V}{\partial S} - rV = 0 \quad \text{for } S > B(t)
$$

With boundary conditions:
- $V(B(t), t) = K - B(t)$ (value matching for puts)
- $\frac{\partial V}{\partial S}(B(t), t) = -1$ (smooth pasting)
- $V(S, T) = \max(K - S, 0)$ (terminal condition)

The **exercise boundary** $B(t)$ is part of the solution; it must be determined simultaneously with the option price. This coupling renders the problem analytically intractable.

### 7.3 Integral Equation Formulation

Kim (1990) and Carr, Jarrow, Myneni (1992) showed that the American put can be expressed as:

$$
P(S, t) = p(S, t) + \mathcal{P}(S, t)
$$

Where $p$ is the European put and the early exercise premium is:

$$
\mathcal{P}(S, t) = \int_t^T rK e^{-r(\tau-t)} \Phi(-d_2(S, B(\tau), \tau-t)) \, d\tau - \int_t^T q S e^{-q(\tau-t)} \Phi(-d_1(S, B(\tau), \tau-t)) \, d\tau
$$

This formulation reduces the problem to finding the exercise boundary $B(t)$.

---

## Part VIII: Spectral Collocation Methods

### 8.1 The Discretisation Challenge

Finite difference methods discretise the PDE domain uniformly. Accuracy improves slowly with grid refinement, typically as $O(h^2)$ where $h$ is the grid spacing. Achieving high precision requires many grid points and correspondingly long computation times.

Spectral methods use global basis functions, achieving exponential convergence for smooth solutions. The same accuracy is achieved with far fewer points.

### 8.2 Chebyshev Polynomials

The Chebyshev polynomials $T_n(x)$ are defined on $[-1, 1]$:

$$
T_n(x) = \cos(n \arccos(x))
$$

They satisfy an orthogonality relation:

$$
\int_{-1}^{1} \frac{T_m(x) T_n(x)}{\sqrt{1-x^2}} dx =
\begin{cases}
\pi & m = n = 0 \\
\pi/2 & m = n \neq 0 \\
0 & m \neq n
\end{cases}
$$

Chebyshev polynomials are optimal for polynomial interpolation; they minimise the maximum interpolation error.

### 8.3 Collocation Points

The Chebyshev-Gauss-Lobatto points are:

$$
x_j = \cos\left(\frac{\pi j}{N}\right), \quad j = 0, 1, \ldots, N
$$

These points cluster near the boundaries of the interval. For option pricing, this clustering provides high resolution near the exercise boundary, precisely where accuracy is most critical.

### 8.4 Spectral Solution

Represent the exercise boundary as a Chebyshev expansion:

$$
B(\tau) \approx \sum_{k=0}^{N} b_k T_k\left(\frac{2\tau - T}{T}\right)
$$

The integral equation becomes a system of nonlinear algebraic equations at collocation points. Newton's method solves the system, with the Jacobian computed analytically for efficiency.

Alaris implements a 16th-order Chebyshev expansion with Clenshaw-Curtis quadrature. Performance characteristics:
- Throughput: approximately 2,800 prices per second (single-threaded)
- Accuracy: RMSE 0.35 cents versus benchmark values
- Speed: 2.8 times faster than finite difference methods at equal accuracy

---

## Part IX: Negative Interest Rates

### 9.1 The Double Boundary Problem

When interest rates are negative ($r < 0$), American put behaviour changes qualitatively.

For $r < 0$:
- Holding cash loses value (negative carry)
- Early exercise of puts may be optimal even when deep out-of-the-money
- A second exercise boundary emerges for low stock prices

The option holder faces competing incentives: exercise early to receive cash (which loses value over time) versus wait for better intrinsic value (but cash received earlier is worth more in present value terms).

### 9.2 Mathematical Formulation

The double boundary condition:

$$
P(S,t) = K - S \quad \text{for } S \leq B_L(t) \text{ or } S \geq B_U(t)
$$

Where $B_L$ is the lower boundary (deep OTM early exercise) and $B_U$ is the upper boundary (standard early exercise).

### 9.3 Validation

Alaris detects and handles double-boundary cases automatically. Implementation is validated against Andersen, Lake, and Offengenden (2015) benchmark values.

---

## Part X: The Signal Framework

### 10.1 IV/RV Ratio

The primary signal is the ratio of implied to realised volatility:

$$
\text{IVRV}(t) = \frac{\sigma_I^{30}(t)}{\sigma_{YZ}^{30}(t)}
$$

Where the superscript 30 indicates 30-day tenor for implied volatility and 30-day lookback for realised volatility.

**Threshold:** IVRV $\geq 1.25$ indicates significantly elevated premium.

### 10.2 Term Structure Signal

Let $\sigma_I^{front}$ be front-month implied volatility and $\sigma_I^{back}$ be next-month:

$$
\text{TS} = \sigma_I^{back} - \sigma_I^{front}
$$

**Normal term structure:** TS $> 0$ (upward sloping).
**Inverted term structure:** TS $< 0$ (downward sloping).

Inverted term structure indicates elevated near-term uncertainty.

### 10.3 Signal Aggregation

Signals are aggregated into discrete recommendations:

| Criteria Met | Recommendation |
|--------------|----------------|
| 3 of 3 | Recommended |
| 2 of 3 | Consider |
| 0-1 of 3 | Avoid |

This discrete classification acknowledges the uncertainty in signal quality and prevents false precision.

---

## Appendix A: Notation Reference

| Symbol | Meaning |
|--------|---------|
| $S$ | Underlying (spot) price |
| $K$ | Strike price |
| $r$ | Risk-free interest rate |
| $q$ | Dividend yield |
| $T$ | Time to expiration |
| $\sigma$ | Volatility |
| $\sigma_I$ | Implied volatility |
| $\sigma_R$ | Realised volatility |
| $V$, $C$, $P$ | Option value, call value, put value |
| $B(t)$ | Exercise boundary |
| $\Phi$ | Standard normal CDF |
| $\phi$ | Standard normal PDF |
| $\Delta$, $\Gamma$, $\Theta$, $\nu$, $\rho$ | Greeks |

---

## Appendix B: Numerical Constants

| Constant | Value | Usage |
|----------|-------|-------|
| Trading days per year | 252 | Annualisation |
| IV/RV threshold | 1.25 | Signal generation |
| Yang-Zhang k | $\frac{0.34}{1.34 + (n+1)/(n-1)}$ | Variance weighting |
| Newton tolerance | $10^{-8}$ | IV solver convergence |
| Chebyshev order | 16 | Exercise boundary expansion |

---

## References

**Foundational Theory**

- Black, F. & Scholes, M. (1973). "The Pricing of Options and Corporate Liabilities." *Journal of Political Economy*.
- Merton, R. C. (1973). "Theory of Rational Option Pricing." *Bell Journal of Economics and Management Science*.

**Volatility Estimation**

- Parkinson, M. (1980). "The Extreme Value Method for Estimating the Variance of the Rate of Return." *Journal of Business*.
- Rogers, L.C.G. & Satchell, S.E. (1991). "Estimating Variance from High, Low and Closing Prices." *Annals of Applied Probability*.
- Yang, D. & Zhang, Q. (2000). "Drift Independent Volatility Estimation." *Journal of Business*.

**American Option Pricing**

- Kim, I.J. (1990). "The Analytic Valuation of American Options." *Review of Financial Studies*.
- Carr, P., Jarrow, R., & Myneni, R. (1992). "Alternative Characterizations of American Put Options." *Mathematical Finance*.

**Spectral Methods**

- Boyd, J.P. (2001). *Chebyshev and Fourier Spectral Methods*. Dover.
- Trefethen, L.N. (2000). *Spectral Methods in MATLAB*. SIAM.

**Negative Rates**

- Andersen, L., Lake, M., & Offengenden, D. (2015). "High Performance American Option Pricing." *SSRN*.

---

*End of Foundations*
