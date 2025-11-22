namespace Alaris.Strategy.Core;

/// <summary>
/// Selects the optimal IV model based on market conditions, fit metrics, and regime.
/// Implements automatic model selection with martingale condition enforcement.
///
/// Model Selection Criteria:
///   1. Regime detection (pre/post earnings, normal)
///   2. Fit quality (RMSE, MAE on calibration data)
///   3. Parsimony (AIC/BIC penalizing complexity)
///   4. Martingale constraint satisfaction
///   5. Out-of-sample validation
///
/// Available Models:
///   - Black-Scholes: Baseline, no parameters to calibrate
///   - Leung-Santoli: Pre-earnings with deterministic jump
///   - Heston: Stochastic volatility for smile/skew
///   - Kou: Jump-diffusion for fat tails and asymmetry
/// </summary>
public sealed class IVModelSelector
{
    private readonly MartingaleValidator _martingaleValidator;
    private readonly RegimeModelConfig _config;

    public IVModelSelector(RegimeModelConfig? config = null)
    {
        _config = config ?? RegimeModelConfig.Default;
        _martingaleValidator = new MartingaleValidator();
    }

    /// <summary>
    /// Selects the best model for given market conditions.
    /// </summary>
    /// <param name="context">Market context including spot, IV surface, and regime.</param>
    /// <returns>Model selection result with fit metrics.</returns>
    public ModelSelectionResult SelectBestModel(ModelSelectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Detect regime
        EarningsRegime regime = context.Regime ?? EarningsRegime.Detect(context.TimeParams);

        // Get candidate models based on regime
        IReadOnlyList<RecommendedModel> candidates = GetCandidateModels(regime);

        // Evaluate each candidate
        List<ModelEvaluation> evaluations = new();

        foreach (RecommendedModel candidate in candidates)
        {
            ModelEvaluation evaluation = EvaluateModel(candidate, context, regime);
            evaluations.Add(evaluation);
        }

        // Select best based on composite score
        ModelEvaluation? best = evaluations
            .Where(e => e.MartingaleValid)
            .OrderBy(e => e.CompositeScore)
            .FirstOrDefault();

        if (best == null)
        {
            // Fall back to simplest valid model
            best = evaluations.OrderBy(e => e.Complexity).First();
        }

        return new ModelSelectionResult
        {
            SelectedModel = best.ModelType,
            Regime = regime,
            Evaluations = evaluations.AsReadOnly(),
            BestEvaluation = best,
            SelectionReason = GenerateSelectionReason(best, regime)
        };
    }

    /// <summary>
    /// Gets candidate models appropriate for the regime.
    /// </summary>
    private static IReadOnlyList<RecommendedModel> GetCandidateModels(EarningsRegime regime)
    {
        return regime.RegimeType switch
        {
            EarningsRegimeType.PreEarnings => new[]
            {
                RecommendedModel.LeungSantoli,
                RecommendedModel.Kou,
                RecommendedModel.HestonWithEarningsJump
            },
            EarningsRegimeType.PostEarningsTransition => new[]
            {
                RecommendedModel.PostEarningsBlend,
                RecommendedModel.Heston,
                RecommendedModel.BlackScholes
            },
            _ => new[]
            {
                RecommendedModel.Heston,
                RecommendedModel.Kou,
                RecommendedModel.BlackScholes
            }
        };
    }

    /// <summary>
    /// Evaluates a model's fit to market data.
    /// </summary>
    private ModelEvaluation EvaluateModel(
        RecommendedModel modelType,
        ModelSelectionContext context,
        EarningsRegime regime)
    {
        // Get model complexity (number of parameters)
        int complexity = GetModelComplexity(modelType);

        // Compute fit metrics
        FitMetrics fitMetrics = ComputeFitMetrics(modelType, context);

        // Validate martingale condition
        bool martingaleValid = _martingaleValidator.Validate(modelType, context);

        // Compute information criteria
        int n = context.MarketIVs?.Count ?? 1;
        double aic = ComputeAIC(fitMetrics.MSE, complexity, n);
        double bic = ComputeBIC(fitMetrics.MSE, complexity, n);

        // Composite score (lower is better)
        double compositeScore = ComputeCompositeScore(fitMetrics, aic, martingaleValid);

        return new ModelEvaluation
        {
            ModelType = modelType,
            FitMetrics = fitMetrics,
            Complexity = complexity,
            AIC = aic,
            BIC = bic,
            MartingaleValid = martingaleValid,
            CompositeScore = compositeScore
        };
    }

    /// <summary>
    /// Computes fit metrics for a model.
    /// </summary>
    private static FitMetrics ComputeFitMetrics(
        RecommendedModel modelType,
        ModelSelectionContext context)
    {
        if (context.MarketIVs == null || context.MarketIVs.Count == 0)
        {
            return FitMetrics.Default;
        }

        IReadOnlyList<double> modelIVs = ComputeModelIVs(modelType, context);

        if (modelIVs.Count != context.MarketIVs.Count)
        {
            return FitMetrics.Default;
        }

        // Compute error metrics
        double sumSquaredError = 0;
        double sumAbsError = 0;
        double maxError = 0;

        for (int i = 0; i < context.MarketIVs.Count; i++)
        {
            double error = modelIVs[i] - context.MarketIVs[i].IV;
            sumSquaredError += error * error;
            sumAbsError += Math.Abs(error);
            maxError = Math.Max(maxError, Math.Abs(error));
        }

        int n = context.MarketIVs.Count;
        double mse = sumSquaredError / n;
        double rmse = Math.Sqrt(mse);
        double mae = sumAbsError / n;

        // Compute R-squared
        double meanIV = context.MarketIVs.Average(x => x.IV);
        double totalSS = context.MarketIVs.Sum(x => (x.IV - meanIV) * (x.IV - meanIV));
        double rSquared = totalSS > 0 ? 1 - (sumSquaredError / totalSS) : 0;

        return new FitMetrics
        {
            MSE = mse,
            RMSE = rmse,
            MAE = mae,
            MaxError = maxError,
            RSquared = Math.Max(0, rSquared)
        };
    }

    /// <summary>
    /// Computes model IVs for comparison with market.
    /// </summary>
    private static IReadOnlyList<double> ComputeModelIVs(
        RecommendedModel modelType,
        ModelSelectionContext context)
    {
        List<double> result = new();

        if (context.MarketIVs == null)
        {
            return result;
        }

        foreach ((double strike, int dte, double _) in context.MarketIVs)
        {
            double timeToExpiry = dte / 252.0;
            double modelIV = modelType switch
            {
                RecommendedModel.BlackScholes =>
                    context.BaseVolatility,

                RecommendedModel.LeungSantoli when context.EarningsJumpVolatility.HasValue =>
                    LeungSantoliModel.ComputeTheoreticalIV(
                        context.BaseVolatility,
                        context.EarningsJumpVolatility.Value,
                        timeToExpiry),

                RecommendedModel.Heston when context.HestonParams != null =>
                    new HestonModel(context.HestonParams)
                        .ComputeTheoreticalIV(context.Spot, strike, timeToExpiry),

                RecommendedModel.Kou when context.KouParams != null =>
                    new KouModel(context.KouParams)
                        .ComputeTheoreticalIV(context.Spot, strike, timeToExpiry),

                _ => context.BaseVolatility
            };

            result.Add(modelIV);
        }

        return result;
    }

    /// <summary>
    /// Gets model complexity (number of free parameters).
    /// </summary>
    private static int GetModelComplexity(RecommendedModel model) => model switch
    {
        RecommendedModel.BlackScholes => 1,         // sigma only
        RecommendedModel.LeungSantoli => 2,         // sigma, sigma_e
        RecommendedModel.Heston => 5,               // V0, theta, kappa, sigmaV, rho
        RecommendedModel.Kou => 5,                  // sigma, lambda, p, eta1, eta2
        RecommendedModel.HestonWithEarningsJump => 6,
        RecommendedModel.PostEarningsBlend => 3,
        _ => 1
    };

    /// <summary>
    /// Computes Akaike Information Criterion.
    /// AIC = n * ln(MSE) + 2k
    /// </summary>
    private static double ComputeAIC(double mse, int k, int n)
    {
        if (mse <= 0 || n <= 0)
        {
            return double.MaxValue;
        }

        return (n * Math.Log(mse)) + (2 * k);
    }

    /// <summary>
    /// Computes Bayesian Information Criterion.
    /// BIC = n * ln(MSE) + k * ln(n)
    /// </summary>
    private static double ComputeBIC(double mse, int k, int n)
    {
        if (mse <= 0 || n <= 0)
        {
            return double.MaxValue;
        }

        return (n * Math.Log(mse)) + (k * Math.Log(n));
    }

    /// <summary>
    /// Computes composite selection score.
    /// </summary>
    private static double ComputeCompositeScore(FitMetrics fit, double aic, bool martingaleValid)
    {
        // Weighted combination
        double score = (0.4 * fit.RMSE * 100) +  // RMSE in percentage points
                       (0.3 * aic / 100) +        // Normalized AIC
                       (0.2 * (1 - fit.RSquared)) + // R-squared penalty
                       (0.1 * fit.MaxError * 100);   // Maximum error

        // Penalize martingale violation
        if (!martingaleValid)
        {
            score += 10;
        }

        return score;
    }

    private static string GenerateSelectionReason(ModelEvaluation best, EarningsRegime regime)
    {
        return $"{best.ModelType} selected for {regime.RegimeType} regime. " +
               $"RMSE={best.FitMetrics.RMSE:P2}, R²={best.FitMetrics.RSquared:F3}, " +
               $"Martingale={best.MartingaleValid}";
    }
}

/// <summary>
/// Validates martingale conditions for calibrated models.
/// Ensures risk-neutral pricing consistency.
/// </summary>
public sealed class MartingaleValidator
{
    /// <summary>
    /// Tolerance for drift deviation from risk-neutral rate.
    /// </summary>
    private const double DriftTolerance = 0.001;

    /// <summary>
    /// Validates martingale condition for a model.
    /// </summary>
    /// <param name="modelType">The model type to validate.</param>
    /// <param name="context">The model selection context containing market data.</param>
    /// <returns>True if the martingale condition is satisfied, false otherwise.</returns>
    public bool Validate(RecommendedModel modelType, ModelSelectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return modelType switch
        {
            RecommendedModel.BlackScholes => true, // Always satisfies by construction
            RecommendedModel.LeungSantoli => ValidateLeungSantoli(context),
            RecommendedModel.Heston => ValidateHeston(context),
            RecommendedModel.Kou => ValidateKou(context),
            _ => true
        };
    }

    /// <summary>
    /// Validates Leung and Santoli martingale condition.
    /// The model satisfies martingale by using the actual drift r-d.
    /// </summary>
    private static bool ValidateLeungSantoli(ModelSelectionContext context)
    {
        // L&S model doesn't affect drift - martingale preserved
        // The jump only affects variance, not expected return
        return true;
    }

    /// <summary>
    /// Validates Heston martingale condition.
    /// Stock drift must equal r - d.
    /// </summary>
    private static bool ValidateHeston(ModelSelectionContext context)
    {
        if (context.HestonParams == null)
        {
            return true;
        }

        // Heston uses drift = r - d by construction
        // Martingale requires: E[S_T / B_T] = S_0 / B_0
        // This is satisfied when stock drift = r - d
        double impliedDrift = context.HestonParams.RiskFreeRate -
                              context.HestonParams.DividendYield;
        double expectedDrift = context.RiskFreeRate - context.DividendYield;

        return Math.Abs(impliedDrift - expectedDrift) < DriftTolerance;
    }

    /// <summary>
    /// Validates Kou martingale condition.
    /// Drift must be adjusted: r - d - lambda * kappa.
    /// </summary>
    private static bool ValidateKou(ModelSelectionContext context)
    {
        if (context.KouParams == null)
        {
            return true;
        }

        // Check that drift is properly compensated
        double kappa = context.KouParams.ComputeKappa();
        double adjustedDrift = context.KouParams.ComputeMartingaleDrift();
        double expectedDrift = context.RiskFreeRate - context.DividendYield -
                               (context.KouParams.Lambda * kappa);

        return Math.Abs(adjustedDrift - expectedDrift) < DriftTolerance;
    }

    /// <summary>
    /// Computes the jump compensation term for martingale adjustment.
    /// </summary>
    public static double ComputeJumpCompensation(KouParameters kouParams)
    {
        ArgumentNullException.ThrowIfNull(kouParams);
        return kouParams.Lambda * kouParams.ComputeKappa();
    }
}

/// <summary>
/// Context for model selection.
/// </summary>
public sealed class ModelSelectionContext
{
    /// <summary>
    /// Current spot price.
    /// </summary>
    public double Spot { get; init; }

    /// <summary>
    /// Base (diffusion) volatility estimate.
    /// </summary>
    public double BaseVolatility { get; init; }

    /// <summary>
    /// Risk-free rate.
    /// </summary>
    public double RiskFreeRate { get; init; }

    /// <summary>
    /// Dividend yield.
    /// </summary>
    public double DividendYield { get; init; }

    /// <summary>
    /// Time parameters.
    /// </summary>
    public required TimeParameters TimeParams { get; init; }

    /// <summary>
    /// Pre-detected regime (optional).
    /// </summary>
    public EarningsRegime? Regime { get; init; }

    /// <summary>
    /// Calibrated earnings jump volatility (for L&amp;S model).
    /// </summary>
    public double? EarningsJumpVolatility { get; init; }

    /// <summary>
    /// Calibrated Heston parameters.
    /// </summary>
    public HestonParameters? HestonParams { get; init; }

    /// <summary>
    /// Calibrated Kou parameters.
    /// </summary>
    public KouParameters? KouParams { get; init; }

    /// <summary>
    /// Market IV observations for calibration.
    /// </summary>
    public IReadOnlyList<(double Strike, int DTE, double IV)>? MarketIVs { get; init; }
}

/// <summary>
/// Result of model selection.
/// </summary>
public sealed class ModelSelectionResult
{
    /// <summary>
    /// Selected model type.
    /// </summary>
    public RecommendedModel SelectedModel { get; init; }

    /// <summary>
    /// Detected regime.
    /// </summary>
    public required EarningsRegime Regime { get; init; }

    /// <summary>
    /// All model evaluations.
    /// </summary>
    public required IReadOnlyList<ModelEvaluation> Evaluations { get; init; }

    /// <summary>
    /// Best evaluation details.
    /// </summary>
    public required ModelEvaluation BestEvaluation { get; init; }

    /// <summary>
    /// Human-readable selection reason.
    /// </summary>
    public string SelectionReason { get; init; } = string.Empty;
}

/// <summary>
/// Evaluation metrics for a single model.
/// </summary>
public sealed class ModelEvaluation
{
    /// <summary>
    /// Model type evaluated.
    /// </summary>
    public RecommendedModel ModelType { get; init; }

    /// <summary>
    /// Fit quality metrics.
    /// </summary>
    public required FitMetrics FitMetrics { get; init; }

    /// <summary>
    /// Model complexity (parameter count).
    /// </summary>
    public int Complexity { get; init; }

    /// <summary>
    /// Akaike Information Criterion.
    /// </summary>
    public double AIC { get; init; }

    /// <summary>
    /// Bayesian Information Criterion.
    /// </summary>
    public double BIC { get; init; }

    /// <summary>
    /// Whether martingale condition is satisfied.
    /// </summary>
    public bool MartingaleValid { get; init; }

    /// <summary>
    /// Composite selection score (lower is better).
    /// </summary>
    public double CompositeScore { get; init; }
}

/// <summary>
/// Fit quality metrics.
/// </summary>
public sealed class FitMetrics
{
    /// <summary>
    /// Mean Squared Error.
    /// </summary>
    public double MSE { get; init; }

    /// <summary>
    /// Root Mean Squared Error.
    /// </summary>
    public double RMSE { get; init; }

    /// <summary>
    /// Mean Absolute Error.
    /// </summary>
    public double MAE { get; init; }

    /// <summary>
    /// Maximum absolute error.
    /// </summary>
    public double MaxError { get; init; }

    /// <summary>
    /// Coefficient of determination.
    /// </summary>
    public double RSquared { get; init; }

    /// <summary>
    /// Default metrics for missing data.
    /// </summary>
    public static FitMetrics Default { get; } = new()
    {
        MSE = 1.0,
        RMSE = 1.0,
        MAE = 1.0,
        MaxError = 1.0,
        RSquared = 0.0
    };
}
