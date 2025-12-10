// =============================================================================
// STKF001A.cs - Kalman-Filtered Yang-Zhang Volatility Estimator
// Component: STKF001A | Category: Core | Variant: A (Primary)
// =============================================================================
// Reference: Kalman (1960), Yang-Zhang (2000), Alaris Phase 3 Specification
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Core;

/// <summary>
/// Implements a Kalman filter for volatility estimation using Yang-Zhang measurements.
/// </summary>
/// <remarks>
/// <para>
/// This component provides filtered volatility estimates with uncertainty quantification
/// by treating the Yang-Zhang estimator as a noisy measurement of true latent volatility.
/// </para>
/// <para>
/// State-space model:
/// State: x = [σ, dσ/dt]ᵀ (volatility and its rate of change)
/// Transition: x_{k+1} = F × x_k + w_k where w_k ~ N(0, Q)
/// Measurement: z_k = H × x_k + v_k where v_k ~ N(0, R_k)
/// </para>
/// <para>
/// The filter provides optimal point estimates minimising mean squared error
/// for Gaussian noise, with adaptively computed measurement noise based on
/// Yang-Zhang estimator efficiency.
/// </para>
/// </remarks>
public sealed class STKF001A
{
    private readonly ILogger<STKF001A>? _logger;
    private readonly KalmanParameters _params;

    // State estimate: [σ, dσ/dt]
    private double _sigma;      // Filtered volatility estimate
    private double _sigmaDot;   // Volatility rate of change

    // Error covariance matrix P (2×2 symmetric, stored as 3 elements)
    private double _p11;  // Var(σ)
    private double _p12;  // Cov(σ, dσ/dt)
    private double _p22;  // Var(dσ/dt)

    // Filter state
    private bool _isInitialised;
    private double _lastYangZhang;
    private double _lastKalmanGain;
    private double _lastInnovation;
    private int _updateCount;

    // LoggerMessage delegates
    private static readonly Action<ILogger, double, double, double, Exception?> LogFilterUpdate =
        LoggerMessage.Define<double, double, double>(
            LogLevel.Debug,
            new EventId(1, nameof(LogFilterUpdate)),
            "Kalman update: σ={Sigma:P2}, K={Gain:F4}, innovation={Innovation:F4}");

    private static readonly Action<ILogger, double, double, Exception?> LogFilterReset =
        LoggerMessage.Define<double, double>(
            LogLevel.Information,
            new EventId(2, nameof(LogFilterReset)),
            "Kalman filter reset: σ₀={InitialSigma:P2}, P₀={InitialVariance:E2}");

    /// <summary>
    /// Initialises a new Kalman-filtered volatility estimator.
    /// </summary>
    /// <param name="parameters">Optional filter parameters; uses defaults if null.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public STKF001A(KalmanParameters? parameters = null, ILogger<STKF001A>? logger = null)
    {
        _params = parameters ?? KalmanParameters.Default;
        _logger = logger;
        Reset(0.20, 0.01); // Default: 20% volatility, 1% initial uncertainty
    }

    /// <summary>
    /// Gets the current filtered volatility estimate.
    /// </summary>
    public double Volatility => _sigma;

    /// <summary>
    /// Gets the current volatility drift estimate.
    /// </summary>
    public double VolatilityDrift => _sigmaDot;

    /// <summary>
    /// Gets the current estimation variance (uncertainty squared).
    /// </summary>
    public double Variance => _p11;

    /// <summary>
    /// Gets the current standard error of the volatility estimate.
    /// </summary>
    public double StandardError => Math.Sqrt(Math.Max(0, _p11));

    /// <summary>
    /// Gets whether the filter has been initialised.
    /// </summary>
    public bool IsInitialised => _isInitialised;

    /// <summary>
    /// Gets the number of filter updates performed.
    /// </summary>
    public int UpdateCount => _updateCount;

    /// <summary>
    /// Resets the filter to initial conditions.
    /// </summary>
    /// <param name="initialVolatility">Initial volatility estimate.</param>
    /// <param name="initialUncertainty">Initial standard error of the estimate.</param>
    public void Reset(double initialVolatility, double initialUncertainty)
    {
        _sigma = initialVolatility;
        _sigmaDot = 0.0; // Assume no initial drift

        // Initialise covariance matrix
        double variance = initialUncertainty * initialUncertainty;
        _p11 = variance;
        _p12 = 0.0;
        _p22 = _params.QSigmaDot; // Prior uncertainty on drift

        _isInitialised = true;
        _updateCount = 0;
        _lastYangZhang = initialVolatility;
        _lastKalmanGain = 0.0;
        _lastInnovation = 0.0;

        SafeLog(() => LogFilterReset(_logger!, initialVolatility, variance, null));
    }

    /// <summary>
    /// Updates the filter with a new Yang-Zhang measurement.
    /// </summary>
    /// <param name="yangZhangEstimate">The raw Yang-Zhang volatility estimate.</param>
    /// <param name="sampleSize">Number of bars used in Yang-Zhang calculation.</param>
    /// <returns>The filtered volatility estimate with uncertainty.</returns>
    public KalmanVolatilityEstimate Update(double yangZhangEstimate, int sampleSize = 30)
    {
        if (!_isInitialised)
        {
            Reset(yangZhangEstimate, 0.02);
        }

        //=====================================================================
        // PREDICT STEP (Time Update)
        //=====================================================================

        // State prediction: x̂_{k|k-1} = F × x̂_{k-1|k-1}
        double sigmaPred = _sigma + (_params.DeltaT * _sigmaDot);
        double sigmaDotPred = _params.Phi * _sigmaDot;

        // Covariance prediction: P_{k|k-1} = F × P_{k-1|k-1} × Fᵀ + Q
        double p11Pred = _p11 + (2 * _params.DeltaT * _p12)
                        + (_params.DeltaT * _params.DeltaT * _p22) + _params.QSigma;
        double p12Pred = _params.Phi * (_p12 + (_params.DeltaT * _p22));
        double p22Pred = (_params.Phi * _params.Phi * _p22) + _params.QSigmaDot;

        //=====================================================================
        // UPDATE STEP (Measurement Update)
        //=====================================================================

        // Measurement noise: R_k = Var(ŝ_YZ) ≈ σ²/(2n×η)
        double measurementNoise = ComputeMeasurementNoise(yangZhangEstimate, sampleSize);

        // Innovation: y_k = z_k - H × x̂_{k|k-1}
        double innovation = yangZhangEstimate - sigmaPred;

        // Innovation covariance: S_k = H × P_{k|k-1} × Hᵀ + R_k
        // For H = [1, 0], this simplifies to S = P[1,1] + R
        double innovationCov = p11Pred + measurementNoise;

        // Kalman gain: K_k = P_{k|k-1} × Hᵀ × S_k⁻¹
        double k1 = p11Pred / innovationCov;
        double k2 = p12Pred / innovationCov;

        // State update: x̂_{k|k} = x̂_{k|k-1} + K_k × y_k
        _sigma = sigmaPred + (k1 * innovation);
        _sigmaDot = sigmaDotPred + (k2 * innovation);

        // Covariance update (Joseph form for numerical stability):
        // P_{k|k} = (I - K×H) × P_{k|k-1} × (I - K×H)ᵀ + K × R × Kᵀ
        // For scalar measurement, simplifies considerably
        double oneMinusK1 = 1.0 - k1;
        _p11 = (oneMinusK1 * oneMinusK1 * p11Pred) + (k1 * k1 * measurementNoise);
        _p12 = (oneMinusK1 * p12Pred) - (k1 * k2 * p11Pred) + (k2 * oneMinusK1 * p12Pred);
        _p22 = p22Pred - (k2 * p12Pred) - (k2 * (p12Pred - (k2 * p22Pred)));

        // Ensure positive semi-definiteness
        _p11 = Math.Max(_p11, 1e-10);
        _p22 = Math.Max(_p22, 1e-10);

        // Store diagnostics
        _lastYangZhang = yangZhangEstimate;
        _lastKalmanGain = k1;
        _lastInnovation = innovation;
        _updateCount++;

        SafeLog(() => LogFilterUpdate(_logger!, _sigma, k1, innovation, null));

        return new KalmanVolatilityEstimate(
            Volatility: _sigma,
            VolatilityDrift: _sigmaDot,
            Variance: _p11,
            StandardError: Math.Sqrt(Math.Max(0, _p11)),
            YangZhangRaw: yangZhangEstimate,
            KalmanGain: k1,
            Innovation: innovation,
            MeasurementNoise: measurementNoise);
    }

    /// <summary>
    /// Performs a predict-only step (handles missing data).
    /// </summary>
    /// <returns>The predicted estimate (no measurement update).</returns>
    public KalmanVolatilityEstimate SkipMeasurement()
    {
        if (!_isInitialised)
        {
            throw new InvalidOperationException("Filter not initialised");
        }

        // State prediction only
        _sigma = _sigma + (_params.DeltaT * _sigmaDot);
        _sigmaDot = _params.Phi * _sigmaDot;

        // Covariance prediction (uncertainty grows)
        _p11 = _p11 + (2 * _params.DeltaT * _p12)
               + (_params.DeltaT * _params.DeltaT * _p22) + _params.QSigma;
        _p12 = _params.Phi * (_p12 + (_params.DeltaT * _p22));
        _p22 = (_params.Phi * _params.Phi * _p22) + _params.QSigmaDot;

        return new KalmanVolatilityEstimate(
            Volatility: _sigma,
            VolatilityDrift: _sigmaDot,
            Variance: _p11,
            StandardError: Math.Sqrt(Math.Max(0, _p11)),
            YangZhangRaw: double.NaN, // No measurement
            KalmanGain: 0.0,
            Innovation: double.NaN,
            MeasurementNoise: double.NaN);
    }

    /// <summary>
    /// Gets the current complete filter state.
    /// </summary>
    public KalmanVolatilityEstimate CurrentEstimate => new(
        Volatility: _sigma,
        VolatilityDrift: _sigmaDot,
        Variance: _p11,
        StandardError: Math.Sqrt(Math.Max(0, _p11)),
        YangZhangRaw: _lastYangZhang,
        KalmanGain: _lastKalmanGain,
        Innovation: _lastInnovation,
        MeasurementNoise: double.NaN);

    /// <summary>
    /// Computes 95% confidence interval for the volatility estimate.
    /// </summary>
    public (double Lower, double Upper) GetConfidenceInterval(double confidenceLevel = 0.95)
    {
        // For 95%: z = 1.96
        double z = confidenceLevel switch
        {
            0.99 => 2.576,
            0.95 => 1.96,
            0.90 => 1.645,
            _ => 1.96
        };

        double se = StandardError;
        return (_sigma - (z * se), _sigma + (z * se));
    }

    #region Private Methods

    private double ComputeMeasurementNoise(double measurement, int sampleSize)
    {
        // Yang-Zhang variance: Var(σ̂_YZ) ≈ σ²/(2n×η) where η ≈ 8
        const double yangZhangEfficiency = 8.0;

        double variance = measurement * measurement / (2.0 * sampleSize * yangZhangEfficiency);

        // Apply minimum to prevent numerical issues
        return Math.Max(variance, 1e-8);
    }

    private void SafeLog(Action logAction)
    {
        if (_logger == null)
        {
            return;
        }

#pragma warning disable CA1031
        try
        {
            logAction();
        }
        catch
        {
            /* Fault isolation per Rule 15 */
        }
#pragma warning restore CA1031
    }

    #endregion
}

/// <summary>
/// Kalman filter parameters for volatility estimation.
/// </summary>
/// <param name="DeltaT">Time step in days (typically 1.0).</param>
/// <param name="Phi">Mean-reversion parameter for volatility drift (typically 0.95).</param>
/// <param name="QSigma">Process noise variance for volatility shocks.</param>
/// <param name="QSigmaDot">Process noise variance for drift changes.</param>
public readonly record struct KalmanParameters(
    double DeltaT,
    double Phi,
    double QSigma,
    double QSigmaDot)
{
    /// <summary>
    /// Default parameters for daily equity volatility estimation.
    /// </summary>
    public static KalmanParameters Default => new(
        DeltaT: 1.0,                    // 1 day time step
        Phi: 0.95,                      // Slow drift mean-reversion
        QSigma: 0.0001,                 // 1% daily volatility shock std²
        QSigmaDot: 0.000025);           // 0.5% drift instability std²

    /// <summary>
    /// High-frequency parameters (intraday).
    /// </summary>
    public static KalmanParameters HighFrequency => new(
        DeltaT: 1.0 / 24.0,             // 1 hour time step
        Phi: 0.99,                       // Very slow drift decay
        QSigma: 0.00001,                // Smaller shocks per period
        QSigmaDot: 0.000001);

    /// <summary>
    /// Earnings-event parameters (higher uncertainty).
    /// </summary>
    public static KalmanParameters EarningsEvent => new(
        DeltaT: 1.0,
        Phi: 0.85,                       // Faster drift decay around events
        QSigma: 0.0004,                  // Higher volatility of volatility
        QSigmaDot: 0.0001);              // More dynamic drift
}

/// <summary>
/// Represents the Kalman-filtered volatility estimate.
/// </summary>
/// <param name="Volatility">Filtered volatility estimate.</param>
/// <param name="VolatilityDrift">Estimated rate of change dσ/dt.</param>
/// <param name="Variance">Estimation variance P[1,1].</param>
/// <param name="StandardError">Square root of variance.</param>
/// <param name="YangZhangRaw">Raw Yang-Zhang measurement.</param>
/// <param name="KalmanGain">Current Kalman gain (diagnostic).</param>
/// <param name="Innovation">z - Hx̂ innovation (diagnostic).</param>
/// <param name="MeasurementNoise">Estimated measurement noise R.</param>
public readonly record struct KalmanVolatilityEstimate(
    double Volatility,
    double VolatilityDrift,
    double Variance,
    double StandardError,
    double YangZhangRaw,
    double KalmanGain,
    double Innovation,
    double MeasurementNoise)
{
    /// <summary>
    /// Gets whether the raw measurement is within 2σ of the filtered estimate.
    /// </summary>
    public bool IsConsistent => Math.Abs(Innovation) <= 2 * StandardError;

    /// <summary>
    /// Gets the z-score of the innovation.
    /// </summary>
    public double InnovationZScore => StandardError > 0 ? Innovation / StandardError : 0;
}
