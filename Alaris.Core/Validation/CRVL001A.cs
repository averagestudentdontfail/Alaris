// =============================================================================
// CRVL001A.cs - Core Validation Types
// Component: CR (Core) | Category: VL (Validation) | Variant: A (Primary)
// =============================================================================
// Provides standardized validation results and exception types for numerical
// algorithms with documented bounds.
// =============================================================================

namespace Alaris.Core.Validation;

/// <summary>
/// Represents the result of a bounded numerical algorithm.
/// All algorithms must return this type to enable deterministic execution monitoring.
/// </summary>
/// <remarks>
/// Design rationale:
/// - Immutable record for thread-safety
/// - Bounded iteration count for determinism
/// - Explicit convergence/failure states
/// </remarks>
public sealed record NumericalResult<T>
{
    /// <summary>
    /// The computed value.
    /// </summary>
    public required T Value { get; init; }

    /// <summary>
    /// Whether the algorithm converged within bounds.
    /// </summary>
    public required bool Converged { get; init; }

    /// <summary>
    /// Number of iterations performed.
    /// </summary>
    public required int Iterations { get; init; }

    /// <summary>
    /// Final error estimate (algorithm-specific).
    /// </summary>
    public double Error { get; init; }

    /// <summary>
    /// Convergence status code.
    /// </summary>
    public ConvergenceStatus Status { get; init; } = ConvergenceStatus.Unknown;
}

/// <summary>
/// Factory methods for creating NumericalResult instances.
/// </summary>
public static class NumericalResult
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static NumericalResult<T> Success<T>(T value, int iterations, double error = 0)
        => new()
        {
            Value = value,
            Converged = true,
            Iterations = iterations,
            Error = error,
            Status = ConvergenceStatus.Converged
        };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static NumericalResult<T> Failure<T>(T fallbackValue, int iterations, ConvergenceStatus status)
        => new()
        {
            Value = fallbackValue,
            Converged = false,
            Iterations = iterations,
            Error = double.NaN,
            Status = status
        };
}

/// <summary>
/// Convergence status for numerical algorithms.
/// </summary>
public enum ConvergenceStatus
{
    /// <summary>Unknown or not yet computed.</summary>
    Unknown = 0,

    /// <summary>Algorithm converged within tolerance.</summary>
    Converged = 1,

    /// <summary>Maximum iterations reached without convergence.</summary>
    MaxIterationsReached = 2,

    /// <summary>Derivative too small for Newton-type methods.</summary>
    DerivativeTooSmall = 3,

    /// <summary>Input parameters outside valid bounds.</summary>
    BoundsViolation = 4,

    /// <summary>Numerical instability detected.</summary>
    NumericalInstability = 5,

    /// <summary>Algorithm switched to fallback method.</summary>
    FallbackUsed = 6
}

/// <summary>
/// Exception thrown when input parameters violate algorithm bounds.
/// </summary>
/// <remarks>
/// Bounds:
/// - Volatility: σ ∈ [0.001, 5.0]
/// - Time to expiry: τ ∈ [1/252, 30] years
/// - Spot/Strike: S, K > 0
/// - Moneyness: |ln(K/S)| ≤ 3
/// </remarks>
public sealed class BoundsViolationException : ArgumentException
{
    /// <summary>
    /// The actual value that violated bounds.
    /// </summary>
    public double ActualValue { get; }

    /// <summary>
    /// The minimum allowed value.
    /// </summary>
    public double MinBound { get; }

    /// <summary>
    /// The maximum allowed value.
    /// </summary>
    public double MaxBound { get; }

    /// <summary>Default constructor.</summary>
    public BoundsViolationException() : base() { }

    /// <summary>Constructor with message.</summary>
    public BoundsViolationException(string message) : base(message) { }

    /// <summary>Constructor with message and inner exception.</summary>
    public BoundsViolationException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Creates a bounds violation exception with full details.
    /// </summary>
    public BoundsViolationException(
        string parameterName,
        double actualValue,
        double minBound,
        double maxBound)
        : base($"Parameter '{parameterName}' = {actualValue} violates bounds [{minBound}, {maxBound}].", parameterName)
    {
        ActualValue = actualValue;
        MinBound = minBound;
        MaxBound = maxBound;
    }
}

/// <summary>
/// Algorithm bounds constants for validation.
/// </summary>
public static class AlgorithmBounds
{
    // ===== Volatility Bounds =====
    /// <summary>Minimum volatility: 0.1% annualized.</summary>
    public const double MinVolatility = 0.001;

    /// <summary>Maximum volatility: 500% annualized.</summary>
    public const double MaxVolatility = 5.0;

    // ===== Time Bounds =====
    /// <summary>Minimum time to expiry: 1 trading day.</summary>
    public const double MinTimeToExpiry = 1.0 / 252.0;

    /// <summary>Maximum time to expiry: 30 years.</summary>
    public const double MaxTimeToExpiry = 30.0;

    // ===== Price Bounds =====
    /// <summary>Minimum positive price.</summary>
    public const double MinPositivePrice = 1e-10;

    /// <summary>Maximum log-moneyness |ln(K/S)|.</summary>
    public const double MaxLogMoneyness = 3.0;

    // ===== Iteration Bounds =====
    /// <summary>Newton-Raphson max iterations.</summary>
    public const int NewtonMaxIterations = 50;

    /// <summary>Levenberg-Marquardt max iterations.</summary>
    public const int LMMaxIterations = 200;

    /// <summary>Bisection max iterations.</summary>
    public const int BisectionMaxIterations = 100;

    /// <summary>Super Halley max iterations.</summary>
    public const int SuperHalleyMaxIterations = 25;

    /// <summary>Gauss-Legendre max chunks.</summary>
    public const int IntegrationMaxChunks = 1000;

    // ===== Tolerance Bounds =====
    /// <summary>IV solver tolerance.</summary>
    public const double IVTolerance = 1e-8;

    /// <summary>Integration tolerance.</summary>
    public const double IntegrationTolerance = 1e-8;

    /// <summary>Root-finding tolerance.</summary>
    public const double RootFindingTolerance = 1e-10;

    /// <summary>Minimum vega for Newton IV solver.</summary>
    public const double MinVegaForNewton = 1e-15;

    // ===== Validation Methods =====

    /// <summary>Validates volatility is within bounds.</summary>
    public static void ValidateVolatility(double sigma, string paramName = "sigma")
    {
        if (sigma < MinVolatility || sigma > MaxVolatility || double.IsNaN(sigma))
        {
            throw new BoundsViolationException(paramName, sigma, MinVolatility, MaxVolatility);
        }
    }

    /// <summary>Validates time to expiry is within bounds.</summary>
    public static void ValidateTimeToExpiry(double tau, string paramName = "tau")
    {
        if (tau < MinTimeToExpiry || tau > MaxTimeToExpiry || double.IsNaN(tau))
        {
            throw new BoundsViolationException(paramName, tau, MinTimeToExpiry, MaxTimeToExpiry);
        }
    }

    /// <summary>Validates price is positive.</summary>
    public static void ValidatePositivePrice(double price, string paramName = "price")
    {
        if (price <= MinPositivePrice || double.IsNaN(price) || double.IsInfinity(price))
        {
            throw new BoundsViolationException(paramName, price, MinPositivePrice, double.MaxValue);
        }
    }

    /// <summary>Validates moneyness is within bounds.</summary>
    public static void ValidateMoneyness(double spot, double strike, string spotParamName = "spot")
    {
        double logMoneyness = System.Math.Abs(System.Math.Log(strike / spot));
        if (logMoneyness > MaxLogMoneyness)
        {
            throw new BoundsViolationException(
                spotParamName,
                logMoneyness,
                -MaxLogMoneyness,
                MaxLogMoneyness);
        }
    }
}
