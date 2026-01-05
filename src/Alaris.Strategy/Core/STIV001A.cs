// STIV001A.cs - heston model parameters.

using System.Numerics;
using Alaris.Strategy.Calendar;
using Alaris.Strategy.Core.Numerical;

namespace Alaris.Strategy.Core;

/// <summary>
/// Heston model parameters.
/// </summary>
public sealed class HestonParameters
{
    /// <summary>
    /// Current instantaneous variance (V_0). Must be positive.
    /// </summary>
    public double V0 { get; init; }

    /// <summary>
    /// Long-run variance (theta). Must be positive.
    /// </summary>
    public double Theta { get; init; }

    /// <summary>
    /// Mean reversion speed (kappa). Must be positive.
    /// </summary>
    public double Kappa { get; init; }

    /// <summary>
    /// Volatility of volatility (sigma_v). Must be positive.
    /// </summary>
    public double SigmaV { get; init; }

    /// <summary>
    /// Correlation between stock and variance (rho). Must be in [-1, 1].
    /// Typically negative for equities (leverage effect).
    /// </summary>
    public double Rho { get; init; }

    /// <summary>
    /// Risk-free rate.
    /// </summary>
    public double RiskFreeRate { get; init; }

    /// <summary>
    /// Dividend yield.
    /// </summary>
    public double DividendYield { get; init; }

    /// <summary>
    /// Validates parameters including Feller condition.
    /// </summary>
    public ValidationResult Validate()
    {
        List<string> errors = new();

        if (V0 <= 0)
        {
            errors.Add("V0 (initial variance) must be positive.");
        }

        if (Theta <= 0)
        {
            errors.Add("Theta (long-run variance) must be positive.");
        }

        if (Kappa <= 0)
        {
            errors.Add("Kappa (mean reversion speed) must be positive.");
        }

        if (SigmaV <= 0)
        {
            errors.Add("SigmaV (vol-of-vol) must be positive.");
        }

        if (Rho < -1 || Rho > 1)
        {
            errors.Add("Rho (correlation) must be in [-1, 1].");
        }

        // Check Feller condition for variance positivity
        if (!SatisfiesFellerCondition())
        {
            errors.Add($"Feller condition violated: 2*kappa*theta ({(2 * Kappa * Theta):F4}) must be > sigma_v^2 ({(SigmaV * SigmaV):F4}).");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Checks if Feller condition is satisfied (ensures variance stays positive).
    /// </summary>
    public bool SatisfiesFellerCondition() =>
        (2 * Kappa * Theta) > (SigmaV * SigmaV);

    /// <summary>
    /// Computes the expected variance at time t.
    /// E[V_t] = theta + (V0 - theta) * exp(-kappa * t)
    /// </summary>
    public double ExpectedVariance(double t) =>
        Theta + ((V0 - Theta) * Math.Exp(-Kappa * t));

    /// <summary>
    /// Computes the variance of variance at time t.
    /// </summary>
    public double VarianceOfVariance(double t)
    {
        double expKt = Math.Exp(-Kappa * t);
        return (V0 * SigmaV * SigmaV / Kappa * expKt * (1 - expKt)) +
               (Theta * SigmaV * SigmaV / (2 * Kappa) * (1 - expKt) * (1 - expKt));
    }

    /// <summary>
    /// Default parameters calibrated to typical equity behavior.
    /// </summary>
    public static HestonParameters DefaultEquity => new()
    {
        V0 = 0.04,         // Initial variance (20% vol)
        Theta = 0.04,      // Long-run variance (20% vol)
        Kappa = 2.0,       // Mean reversion speed
        SigmaV = 0.3,      // Vol-of-vol
        Rho = -0.7,        // Negative correlation (leverage effect)
        RiskFreeRate = 0.05,
        DividendYield = 0.02
    };

    /// <summary>
    /// Parameters for high volatility regime.
    /// </summary>
    public static HestonParameters HighVolRegime => new()
    {
        V0 = 0.09,         // Initial variance (30% vol)
        Theta = 0.0625,    // Long-run variance (25% vol)
        Kappa = 1.5,       // Slower reversion in crisis
        SigmaV = 0.5,      // Higher vol-of-vol
        Rho = -0.8,        // Stronger leverage
        RiskFreeRate = 0.03,
        DividendYield = 0.01
    };
}

/// <summary>
/// Implements the Heston (1993) stochastic volatility model for implied volatility.
/// Stock price dynamics under risk-neutral measure Q:
///     dS/S = (r - d)dt + sqrt(V)*dW_S
///     dV   = kappa*(theta - V)dt + sigma_v*sqrt(V)*dW_V
///     corr(dW_S, dW_V) = rho
/// Where:
///     V       = instantaneous variance
///     theta   = long-run variance (mean reversion level)
///     kappa   = mean reversion speed
///     sigma_v = volatility of volatility (vol-of-vol)
///     rho     = correlation between stock and variance
/// The model captures:
///   1. Volatility clustering (mean reversion)
///   2. Leverage effect (negative correlation)
///   3. Implied volatility smile/skew
/// Martingale condition: The drift (r-d) ensures discounted stock price is a martingale.
/// Feller condition: 2*kappa*theta > sigma_v^2 ensures variance stays positive.
/// Reference: "A Closed-Form Solution for Options with Stochastic Volatility with
/// Applications to Bond and Currency Options" S.L. Heston (1993), Review of Financial Studies
/// </summary>
public sealed class STIV001A
{
    private readonly HestonParameters _params;

    public STIV001A(HestonParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate().ThrowIfInvalid();
        _params = parameters;
    }

    /// <summary>
    /// Computes theoretical implied volatility using characteristic function approach.
    /// PRODUCTION VERSION: Uses full semi-analytical pricing with Fourier inversion
    /// and Newton-Raphson IV solving.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="timeToExpiry">Time to expiry in years.</param>
    /// <returns>Implied volatility from Heston model.</returns>
    public double ComputeTheoreticalIV(double spot, double strike, double timeToExpiry)
    {
        ValidateInputs(spot, strike, timeToExpiry);

        // Production implementation: Full characteristic function integration
        return STPR003A.ComputeImpliedVolatility(spot, strike, timeToExpiry, _params);
    }

    /// <summary>
    /// Computes theoretical implied volatility using moment-matching approximation.
    /// LEGACY VERSION: Fast approximation for educational/comparison purposes.
    /// For production use, prefer ComputeTheoreticalIV() which uses full pricing.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="timeToExpiry">Time to expiry in years.</param>
    /// <returns>Approximate implied volatility.</returns>
    public double ComputeTheoreticalIVApproximate(double spot, double strike, double timeToExpiry)
    {
        ValidateInputs(spot, strike, timeToExpiry);

        // Use moment-matching approximation for efficiency
        double logMoneyness = Math.Log(strike / spot);

        // Expected total variance
        double expectedVariance = ComputeExpectedIntegratedVariance(timeToExpiry);
        double baseIV = Math.Sqrt(expectedVariance / timeToExpiry);

        // Skew adjustment from correlation
        double skewAdjustment = ComputeSkewAdjustment(logMoneyness, timeToExpiry);

        // Smile curvature from vol-of-vol
        double smileAdjustment = ComputeSmileAdjustment(logMoneyness, timeToExpiry);

        double iv = baseIV + skewAdjustment + smileAdjustment;
        return Math.Max(iv, 0.001);
    }

    /// <summary>
    /// Computes expected integrated variance E[int_0^T V_s ds].
    /// </summary>
    private double ComputeExpectedIntegratedVariance(double t)
    {
        // E[int_0^T V_s ds] = theta*T + (V0 - theta)/kappa * (1 - exp(-kappa*T))
        double expKt = Math.Exp(-_params.Kappa * t);
        return (_params.Theta * t) +
               ((_params.V0 - _params.Theta) / _params.Kappa * (1 - expKt));
    }

    /// <summary>
    /// Computes skew adjustment based on correlation.
    /// </summary>
    private double ComputeSkewAdjustment(double logMoneyness, double timeToExpiry)
    {
        // First-order approximation of skew from rho
        double sqrtV = Math.Sqrt(_params.V0);
        return _params.Rho * _params.SigmaV * logMoneyness / (2 * sqrtV * timeToExpiry);
    }

    /// <summary>
    /// Computes smile curvature from vol-of-vol.
    /// </summary>
    private double ComputeSmileAdjustment(double logMoneyness, double timeToExpiry)
    {
        // Second-order approximation (smile curvature)
        double k2 = logMoneyness * logMoneyness;
        double sigmaV2 = _params.SigmaV * _params.SigmaV;
        return sigmaV2 * k2 / (24 * _params.V0 * timeToExpiry);
    }

    /// <summary>
    /// Computes the Heston characteristic function.
    /// This is the core of the semi-analytical pricing formula.
    /// </summary>
    /// <param name="u">Complex frequency parameter.</param>
    /// <param name="t">Time to maturity.</param>
    /// <returns>Characteristic function value.</returns>
    public Complex CharacteristicFunction(Complex u, double t)
    {
        Complex i = Complex.ImaginaryOne;

        double kappa = _params.Kappa;
        double theta = _params.Theta;
        double sigmaV = _params.SigmaV;
        double rho = _params.Rho;
        double v0 = _params.V0;
        double r = _params.RiskFreeRate;
        double d = _params.DividendYield;

        // Heston (1993) parameters
        Complex xi = kappa - (rho * sigmaV * i * u);
        Complex d_h = Complex.Sqrt(
            (xi * xi) + (sigmaV * sigmaV * ((i * u) + (u * u))));

        Complex g = (xi - d_h) / (xi + d_h);
        Complex exp_dt = Complex.Exp(-d_h * t);

        Complex C = ((r - d) * i * u * t) +
                    (kappa * theta / (sigmaV * sigmaV) *
                    (((xi - d_h) * t) - (2 * Complex.Log((1 - (g * exp_dt)) / (1 - g)))));

        Complex D = (xi - d_h) / (sigmaV * sigmaV) *
                    ((1 - exp_dt) / (1 - (g * exp_dt)));

        return Complex.Exp(C + (D * v0));
    }

    /// <summary>
    /// Computes the IV term structure using parallel computation.
    /// </summary>
    public (int DTE, double TheoreticalIV)[] ComputeSTTM001A(
        double spot,
        double strike,
        int[] dtePoints)
    {
        ArgumentNullException.ThrowIfNull(dtePoints);

        var result = new (int DTE, double TheoreticalIV)[dtePoints.Length];

        // Parallel computation for independent DTE points
        Parallel.For(0, dtePoints.Length, i =>
        {
            int dte = dtePoints[i];
            if (dte <= 0)
            {
                result[i] = (dte, Math.Sqrt(_params.V0));
                return;
            }

            double timeToExpiry = TradingCalendarDefaults.DteToYears(dte);
            result[i] = (dte, ComputeTheoreticalIV(spot, strike, timeToExpiry));
        });

        return result;
    }

    /// <summary>
    /// Computes the IV smile across strikes using parallel computation.
    /// </summary>
    public (double Strike, double TheoreticalIV)[] ComputeSmile(
        double spot,
        double[] strikes,
        double timeToExpiry)
    {
        ArgumentNullException.ThrowIfNull(strikes);

        var result = new (double Strike, double TheoreticalIV)[strikes.Length];

        // Parallel computation for independent strikes
        Parallel.For(0, strikes.Length, i =>
        {
            result[i] = (strikes[i], ComputeTheoreticalIV(spot, strikes[i], timeToExpiry));
        });

        return result;
    }

    /// <summary>
    /// Calibrates Heston parameters from market IV surface using Levenberg-Marquardt optimization.
    /// PRODUCTION VERSION: Uses nonlinear least squares optimization for accurate parameter estimation.
    /// </summary>
    public static HestonParameters Calibrate(
        double spot,
        IReadOnlyList<(double Strike, int DTE, double MarketIV)> marketData,
        double riskFreeRate,
        double dividendYield)
    {
        ArgumentNullException.ThrowIfNull(marketData);

        if (marketData.Count < 5)
        {
            throw new ArgumentException("Need at least 5 market observations for calibration.", nameof(marketData));
        }

        // Use Levenberg-Marquardt for production-grade calibration
        var optimizer = new Numerical.STPR004A
        {
            MaxIterations = 200,
            ParameterTolerance = 1e-8,
            ObjectiveTolerance = 1e-8
        };

        // Parameter vector: [v0, theta, kappa, sigmaV, rho]
        // Initial guess from ATM market data or defaults
        double atmIV = marketData
            .Where(x => Math.Abs(x.Strike - spot) / spot < 0.05)
            .Select(x => x.MarketIV)
            .DefaultIfEmpty(0.25)
            .Average();

        double[] initialGuess =
        {
            atmIV * atmIV,  // v0
            atmIV * atmIV,  // theta
            2.0,            // kappa
            0.3,            // sigmaV
            -0.7            // rho
        };

        // Parameter bounds
        double[] lowerBounds = { 0.001, 0.001, 0.01, 0.01, -0.99 };
        double[] upperBounds = { 2.0, 2.0, 10.0, 2.0, 0.99 };

        // Residual function
        double[] Residuals(double[] x)
        {
            HestonParameters candidateParams = new()
            {
                V0 = x[0],
                Theta = x[1],
                Kappa = x[2],
                SigmaV = x[3],
                Rho = x[4],
                RiskFreeRate = riskFreeRate,
                DividendYield = dividendYield
            };

            // Validate Feller condition and basic constraints
            if (!candidateParams.Validate().IsValid)
            {
                // Return large residuals for invalid parameters
                return Enumerable.Repeat(100.0, marketData.Count).ToArray();
            }

            STIV001A model = new(candidateParams);
            double[] residuals = new double[marketData.Count];

            for (int i = 0; i < marketData.Count; i++)
            {
                (double strike, int dte, double marketIV) = marketData[i];
                double timeToExpiry = TradingCalendarDefaults.DteToYears(dte);

                try
                {
                    double modelIV = model.ComputeTheoreticalIV(spot, strike, timeToExpiry);
                    residuals[i] = modelIV - marketIV;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031
                {
                    // Pricing can fail for many reasons during calibration (e.g., invalid parameters,
                    // numerical instability, convergence issues). Penalize rather than failing the entire calibration.
                    residuals[i] = 10.0;
                }
            }

            return residuals;
        }

        // Run optimization
        OptimizationResult result = optimizer.Minimize(Residuals, initialGuess, lowerBounds, upperBounds);

        if (result.Converged)
        {
            return new HestonParameters
            {
                V0 = result.OptimalParameters[0],
                Theta = result.OptimalParameters[1],
                Kappa = result.OptimalParameters[2],
                SigmaV = result.OptimalParameters[3],
                Rho = result.OptimalParameters[4],
                RiskFreeRate = riskFreeRate,
                DividendYield = dividendYield
            };
        }

        // Fallback: Try grid search with coarse grid to find better initial guess
        return CalibrateGridSearch(spot, marketData, riskFreeRate, dividendYield);
    }

    /// <summary>
    /// Calibrates Heston parameters using parallel grid search.
    /// LEGACY VERSION: Used as fallback when LM optimization fails.
    /// For production, prefer Calibrate() which uses Levenberg-Marquardt.
    /// </summary>
    private static HestonParameters CalibrateGridSearch(
        double spot,
        IReadOnlyList<(double Strike, int DTE, double MarketIV)> marketData,
        double riskFreeRate,
        double dividendYield)
    {
        double bestError = double.MaxValue;
        HestonParameters? bestParams = null;
        object lockObj = new();

        // Parameter grid (coarse grid for demo)
        double[] v0s = { 0.02, 0.04, 0.06, 0.09 };
        double[] thetas = { 0.02, 0.04, 0.06 };
        double[] kappas = { 1.0, 2.0, 3.0, 5.0 };
        double[] sigmaVs = { 0.2, 0.3, 0.4, 0.5 };
        double[] rhos = { -0.9, -0.7, -0.5, -0.3 };

        // Flatten parameter combinations for parallel processing
        var combinations = from v0 in v0s
                          from theta in thetas
                          from kappa in kappas
                          from sigmaV in sigmaVs
                          from rho in rhos
                          select (v0, theta, kappa, sigmaV, rho);

        // Parallel grid search over all parameter combinations
        Parallel.ForEach(combinations, combination =>
        {
            var (v0, theta, kappa, sigmaV, rho) = combination;
            HestonParameters candidateParams = new()
            {
                V0 = v0,
                Theta = theta,
                Kappa = kappa,
                SigmaV = sigmaV,
                Rho = rho,
                RiskFreeRate = riskFreeRate,
                DividendYield = dividendYield
            };

            if (!candidateParams.Validate().IsValid)
            {
                return;
            }

            STIV001A model = new(candidateParams);
            double error = ComputeCalibrationError(model, spot, marketData);

            lock (lockObj)
            {
                if (error < bestError)
                {
                    bestError = error;
                    bestParams = candidateParams;
                }
            }
        });

        return bestParams ?? HestonParameters.DefaultEquity;
    }

    private static double ComputeCalibrationError(
        STIV001A model,
        double spot,
        IReadOnlyList<(double Strike, int DTE, double MarketIV)> marketData)
    {
        double totalError = 0;

        foreach ((double strike, int dte, double marketIV) in marketData)
        {
            double timeToExpiry = TradingCalendarDefaults.DteToYears(dte);
            double modelIV = model.ComputeTheoreticalIV(spot, strike, timeToExpiry);
            double error = modelIV - marketIV;
            totalError += error * error;
        }

        return totalError / marketData.Count;
    }

    private static void ValidateInputs(double spot, double strike, double timeToExpiry)
    {
        if (spot <= 0)
        {
            throw new ArgumentException("Spot price must be positive.", nameof(spot));
        }

        if (strike <= 0)
        {
            throw new ArgumentException("Strike price must be positive.", nameof(strike));
        }

        if (timeToExpiry <= 0)
        {
            throw new ArgumentException("Time to expiry must be positive.", nameof(timeToExpiry));
        }
    }
}
