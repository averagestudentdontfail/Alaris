namespace Alaris.Strategy.Core;

/// <summary>
/// Kou model parameters.
/// </summary>
public sealed class KouParameters
{
    /// <summary>
    /// Diffusion volatility (sigma). Must be positive.
    /// </summary>
    public double Sigma { get; init; }

    /// <summary>
    /// Jump intensity (lambda) - expected number of jumps per year. Must be non-negative.
    /// </summary>
    public double Lambda { get; init; }

    /// <summary>
    /// Probability of an upward jump (p). Must be in [0, 1].
    /// </summary>
    public double P { get; init; }

    /// <summary>
    /// Rate parameter for upward jumps (eta1). Must be > 1 for finite expectation.
    /// Mean upward jump = 1/eta1.
    /// </summary>
    public double Eta1 { get; init; }

    /// <summary>
    /// Rate parameter for downward jumps (eta2). Must be > 0.
    /// Mean downward jump = 1/eta2.
    /// </summary>
    public double Eta2 { get; init; }

    /// <summary>
    /// Risk-free rate.
    /// </summary>
    public double RiskFreeRate { get; init; }

    /// <summary>
    /// Dividend yield.
    /// </summary>
    public double DividendYield { get; init; }

    /// <summary>
    /// Validates parameters.
    /// </summary>
    public ValidationResult Validate()
    {
        List<string> errors = new();

        if (Sigma <= 0)
        {
            errors.Add("Sigma (diffusion volatility) must be positive.");
        }

        if (Lambda < 0)
        {
            errors.Add("Lambda (jump intensity) must be non-negative.");
        }

        if (P < 0 || P > 1)
        {
            errors.Add("P (probability of upward jump) must be in [0, 1].");
        }

        if (Eta1 <= 1)
        {
            errors.Add("Eta1 must be > 1 for finite mean upward jump.");
        }

        if (Eta2 <= 0)
        {
            errors.Add("Eta2 must be positive.");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Computes kappa = E[V-1], the expected percentage jump size.
    /// This is needed for the martingale condition.
    /// </summary>
    public double ComputeKappa()
    {
        if (Lambda == 0)
        {
            return 0;
        }

        // kappa = p * eta1/(eta1-1) + (1-p) * eta2/(eta2+1) - 1
        double upwardContribution = P * Eta1 / (Eta1 - 1);
        double downwardContribution = (1 - P) * Eta2 / (Eta2 + 1);
        return upwardContribution + downwardContribution - 1;
    }

    /// <summary>
    /// Computes the risk-neutral drift adjustment for martingale condition.
    /// </summary>
    public double ComputeMartingaleDrift()
    {
        return RiskFreeRate - DividendYield - (Lambda * ComputeKappa());
    }

    /// <summary>
    /// Default parameters calibrated to typical equity index behavior.
    /// </summary>
    public static KouParameters DefaultEquity => new()
    {
        Sigma = 0.20,      // 20% diffusion volatility
        Lambda = 3.0,      // ~3 jumps per year
        P = 0.4,           // 40% upward, 60% downward (negative skew)
        Eta1 = 10.0,       // Mean upward jump ~10%
        Eta2 = 5.0,        // Mean downward jump ~20%
        RiskFreeRate = 0.05,
        DividendYield = 0.02
    };
}

/// <summary>
/// Implements the Kou (2002) double-exponential jump-diffusion model for implied volatility.
///
/// The model extends Black-Scholes with Poisson jumps where jump sizes follow a
/// double-exponential (asymmetric Laplace) distribution, capturing:
///   1. Asymmetric jump sizes (upward vs downward moves)
///   2. Leptokurtic returns (fat tails)
///   3. Implied volatility skew
///
/// Stock price dynamics under risk-neutral measure Q:
///     dS/S = (r - d - lambda*kappa)dt + sigma*dW + d(sum(V_i - 1))
///
/// Where:
///     sigma  = diffusion volatility
///     lambda = jump intensity (jumps per year)
///     V_i    = jump multiplier with double-exponential distribution
///     kappa  = E[V-1] = p*eta1/(eta1-1) + (1-p)*eta2/(eta2+1) - 1
///
/// The (r - d - lambda*kappa) drift ensures the martingale condition.
///
/// Reference: "A Jump-Diffusion Model for Option Pricing"
/// S.G. Kou (2002), Management Science
/// </summary>
public sealed class KouModel
{
    private readonly KouParameters _params;

    public KouModel(KouParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate().ThrowIfInvalid();
        _params = parameters;
    }

    /// <summary>
    /// Computes the theoretical implied volatility at a given strike and time to expiry.
    /// Uses moment-matching approximation for computational efficiency.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="timeToExpiry">Time to expiry in years.</param>
    /// <returns>Approximate implied volatility.</returns>
    public double ComputeTheoreticalIV(double spot, double strike, double timeToExpiry)
    {
        ValidateInputs(spot, strike, timeToExpiry);

        // Log-moneyness
        double k = Math.Log(strike / spot);

        // Total variance from diffusion
        double diffusionVariance = _params.Sigma * _params.Sigma * timeToExpiry;

        // Jump contribution to variance
        double jumpVariance = ComputeJumpVariance(timeToExpiry);

        // Total variance
        double totalVariance = diffusionVariance + jumpVariance;

        // Skew adjustment based on moneyness
        double skewAdjustment = ComputeSkewAdjustment(k, timeToExpiry);

        // Approximate IV
        double approximateIV = Math.Sqrt(totalVariance / timeToExpiry) + skewAdjustment;

        return Math.Max(approximateIV, 0.001);
    }

    /// <summary>
    /// Computes variance contribution from jumps.
    /// </summary>
    private double ComputeJumpVariance(double timeToExpiry)
    {
        if (_params.Lambda == 0)
        {
            return 0;
        }

        // Second moment of log-jump: E[Y^2] where Y = log(V)
        // For double-exponential: E[Y^2] = 2*p/eta1^2 + 2*(1-p)/eta2^2
        double secondMoment = (2 * _params.P / (_params.Eta1 * _params.Eta1)) +
                              (2 * (1 - _params.P) / (_params.Eta2 * _params.Eta2));

        // First moment: E[Y] = p/eta1 - (1-p)/eta2
        double firstMoment = (_params.P / _params.Eta1) - ((1 - _params.P) / _params.Eta2);

        // Variance of single jump
        double jumpVariance = secondMoment - (firstMoment * firstMoment);

        // Total jump variance = lambda * t * Var(Y)
        return _params.Lambda * timeToExpiry * jumpVariance;
    }

    /// <summary>
    /// Computes skew adjustment based on moneyness.
    /// Captures the asymmetric jump distribution's effect on IV smile.
    /// </summary>
    private double ComputeSkewAdjustment(double logMoneyness, double timeToExpiry)
    {
        if (_params.Lambda == 0)
        {
            return 0;
        }

        // Skewness coefficient from jump distribution
        // Negative skew when downward jumps are larger/more frequent
        double skewness = ComputeJumpSkewness();

        // Adjust IV based on moneyness and skewness
        // OTM puts (k < 0) have higher IV when skewness is negative
        double sqrtT = Math.Sqrt(timeToExpiry);
        return -skewness * logMoneyness / (6 * sqrtT) * (_params.Lambda / 10);
    }

    /// <summary>
    /// Computes skewness of the jump distribution.
    /// </summary>
    private double ComputeJumpSkewness()
    {
        // Third moment contribution
        double thirdMoment = (6 * _params.P / Math.Pow(_params.Eta1, 3)) -
                             (6 * (1 - _params.P) / Math.Pow(_params.Eta2, 3));

        // Normalize by variance^(3/2)
        double variance = (2 * _params.P / (_params.Eta1 * _params.Eta1)) +
                          (2 * (1 - _params.P) / (_params.Eta2 * _params.Eta2));

        if (variance <= 0)
        {
            return 0;
        }

        return thirdMoment / Math.Pow(variance, 1.5);
    }

    /// <summary>
    /// Computes the IV term structure for a given moneyness level.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="dtePoints">Array of days-to-expiry points.</param>
    /// <returns>Array of (DTE, TheoreticalIV) tuples.</returns>
    public (int DTE, double TheoreticalIV)[] ComputeTermStructure(
        double spot,
        double strike,
        int[] dtePoints)
    {
        ArgumentNullException.ThrowIfNull(dtePoints);

        var result = new (int DTE, double TheoreticalIV)[dtePoints.Length];

        for (int i = 0; i < dtePoints.Length; i++)
        {
            int dte = dtePoints[i];
            if (dte <= 0)
            {
                result[i] = (dte, _params.Sigma);
                continue;
            }

            double timeToExpiry = dte / 252.0;
            result[i] = (dte, ComputeTheoreticalIV(spot, strike, timeToExpiry));
        }

        return result;
    }

    /// <summary>
    /// Computes the IV smile across strikes at a given expiry.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strikes">Array of strike prices.</param>
    /// <param name="timeToExpiry">Time to expiry in years.</param>
    /// <returns>Array of (Strike, TheoreticalIV) tuples.</returns>
    public (double Strike, double TheoreticalIV)[] ComputeSmile(
        double spot,
        double[] strikes,
        double timeToExpiry)
    {
        ArgumentNullException.ThrowIfNull(strikes);

        var result = new (double Strike, double TheoreticalIV)[strikes.Length];

        for (int i = 0; i < strikes.Length; i++)
        {
            result[i] = (strikes[i], ComputeTheoreticalIV(spot, strikes[i], timeToExpiry));
        }

        return result;
    }

    /// <summary>
    /// Calibrates Kou parameters from market IV surface.
    /// Uses least-squares fitting with martingale constraint.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="marketData">Market IV observations (strike, dte, iv).</param>
    /// <param name="riskFreeRate">Risk-free rate.</param>
    /// <param name="dividendYield">Dividend yield.</param>
    /// <returns>Calibrated parameters.</returns>
    public static KouParameters Calibrate(
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

        // Grid search over parameter space
        // In production, use more sophisticated optimization (Levenberg-Marquardt, differential evolution)
        double bestError = double.MaxValue;
        KouParameters? bestParams = null;

        // Parameter grid
        double[] sigmas = { 0.15, 0.20, 0.25, 0.30 };
        double[] lambdas = { 1.0, 3.0, 5.0, 10.0 };
        double[] ps = { 0.3, 0.4, 0.5 };
        double[] eta1s = { 5.0, 10.0, 20.0 };
        double[] eta2s = { 3.0, 5.0, 10.0 };

        foreach (double sigma in sigmas)
        {
            foreach (double lambda in lambdas)
            {
                foreach (double p in ps)
                {
                    foreach (double eta1 in eta1s)
                    {
                        foreach (double eta2 in eta2s)
                        {
                            KouParameters candidateParams = new()
                            {
                                Sigma = sigma,
                                Lambda = lambda,
                                P = p,
                                Eta1 = eta1,
                                Eta2 = eta2,
                                RiskFreeRate = riskFreeRate,
                                DividendYield = dividendYield
                            };

                            if (!candidateParams.Validate().IsValid)
                            {
                                continue;
                            }

                            KouModel model = new(candidateParams);
                            double error = ComputeCalibrationError(model, spot, marketData);

                            if (error < bestError)
                            {
                                bestError = error;
                                bestParams = candidateParams;
                            }
                        }
                    }
                }
            }
        }

        return bestParams ?? KouParameters.DefaultEquity;
    }

    private static double ComputeCalibrationError(
        KouModel model,
        double spot,
        IReadOnlyList<(double Strike, int DTE, double MarketIV)> marketData)
    {
        double totalError = 0;

        foreach ((double strike, int dte, double marketIV) in marketData)
        {
            double timeToExpiry = dte / 252.0;
            double modelIV = model.ComputeTheoreticalIV(spot, strike, timeToExpiry);
            double error = modelIV - marketIV;
            totalError += error * error;
        }

        return totalError / marketData.Count; // MSE
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
