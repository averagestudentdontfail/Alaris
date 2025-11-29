// =============================================================================
// STHD005A.cs - Production Signal Validator
// Component: STHD005A | Category: Hedging | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.2
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

using Alaris.Strategy.Core;
using Alaris.Strategy.Cost;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Hedging;

/// <summary>
/// Orchestrates all pre-trade validations for production deployment.
/// </summary>
/// <remarks>
/// <para>
/// This component integrates the four key validation components:
/// 1. Execution cost validation (signal survives transaction costs)
/// 2. Vega correlation analysis (front/back tenor independence)
/// 3. Liquidity validation (defined risk assumption holds)
/// 4. Gamma risk assessment (initial position parameters acceptable)
/// </para>
/// <para>
/// A signal is production-ready only when ALL validations pass.
/// </para>
/// </remarks>
public sealed class STHD005A
{
    private readonly STCS006A _costValidator;
    private readonly STHD001A _vegaAnalyser;
    private readonly STCS008A _liquidityValidator;
    private readonly STHD003A _gammaManager;
    private readonly ILogger<STHD005A>? _logger;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, bool, int, int, Exception?> LogValidationComplete =
        LoggerMessage.Define<string, bool, int, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogValidationComplete)),
            "Production validation for {Symbol}: {Passed} ({PassedChecks}/{TotalChecks} checks)");

    private static readonly Action<ILogger, string, string, Exception?> LogValidationError =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(2, nameof(LogValidationError)),
            "Validation error for {Symbol}: {Error}");

    /// <summary>
    /// Initialises a new instance of the production signal validator.
    /// </summary>
    /// <param name="costValidator">The execution cost validator.</param>
    /// <param name="vegaAnalyser">The vega correlation analyser.</param>
    /// <param name="liquidityValidator">The liquidity validator.</param>
    /// <param name="gammaManager">The gamma risk manager.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any required validator is null.
    /// </exception>
    public STHD005A(
        STCS006A costValidator,
        STHD001A vegaAnalyser,
        STCS008A liquidityValidator,
        STHD003A gammaManager,
        ILogger<STHD005A>? logger = null)
    {
        _costValidator = costValidator ?? throw new ArgumentNullException(nameof(costValidator));
        _vegaAnalyser = vegaAnalyser ?? throw new ArgumentNullException(nameof(vegaAnalyser));
        _liquidityValidator = liquidityValidator ?? throw new ArgumentNullException(nameof(liquidityValidator));
        _gammaManager = gammaManager ?? throw new ArgumentNullException(nameof(gammaManager));
        _logger = logger;
    }

    /// <summary>
    /// Validates a trading signal for production deployment.
    /// </summary>
    /// <param name="signal">The base trading signal.</param>
    /// <param name="frontLegParams">Front-month option parameters.</param>
    /// <param name="backLegParams">Back-month option parameters.</param>
    /// <param name="frontIVHistory">Historical front-month IV changes.</param>
    /// <param name="backIVHistory">Historical back-month IV changes.</param>
    /// <param name="backMonthVolume">Back-month average daily volume.</param>
    /// <param name="backMonthOpenInterest">Back-month open interest.</param>
    /// <param name="spotPrice">Current underlying price.</param>
    /// <param name="strikePrice">Calendar spread strike.</param>
    /// <param name="spreadGreeks">Current spread Greeks.</param>
    /// <param name="daysToEarnings">Days until earnings.</param>
    /// <returns>Complete production validation result.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any required parameter is null.
    /// </exception>
    public STHD006A Validate(
        Signal signal,
        STCS002A frontLegParams,
        STCS002A backLegParams,
        IReadOnlyList<double> frontIVHistory,
        IReadOnlyList<double> backIVHistory,
        int backMonthVolume,
        int backMonthOpenInterest,
        double spotPrice,
        double strikePrice,
        SpreadGreeks spreadGreeks,
        int daysToEarnings)
    {
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(frontLegParams);
        ArgumentNullException.ThrowIfNull(backLegParams);
        ArgumentNullException.ThrowIfNull(frontIVHistory);
        ArgumentNullException.ThrowIfNull(backIVHistory);
        ArgumentNullException.ThrowIfNull(spreadGreeks);

        var checks = new List<ValidationCheck>();

        try
        {
            // 1. Execution cost validation
            STCS007A costResult = _costValidator.Validate(signal, frontLegParams, backLegParams);
            checks.Add(new ValidationCheck
            {
                Name = "Execution Cost Survival",
                Passed = costResult.OverallPass,
                Detail = costResult.Summary
            });

            // 2. Vega correlation analysis
            STHD002A vegaResult = _vegaAnalyser.AnalyseFromLevels(signal.Symbol, frontIVHistory, backIVHistory);
            checks.Add(new ValidationCheck
            {
                Name = "Vega Independence",
                Passed = vegaResult.PassesFilter,
                Detail = vegaResult.Summary
            });

            // 3. Liquidity validation
            STCS009A liquidityResult = _liquidityValidator.Validate(
                signal.Symbol,
                frontLegParams.Contracts,
                backMonthVolume,
                backMonthOpenInterest);
            checks.Add(new ValidationCheck
            {
                Name = "Liquidity Assurance",
                Passed = liquidityResult.DefinedRiskAssured,
                Detail = liquidityResult.Summary
            });

            // 4. Gamma risk assessment
            STHD004A gammaResult = _gammaManager.Evaluate(
                signal.Symbol,
                spreadGreeks.Delta,
                spreadGreeks.Gamma,
                spreadGreeks.Vega,
                spreadGreeks.Theta,
                spotPrice,
                strikePrice,
                daysToEarnings);
            checks.Add(new ValidationCheck
            {
                Name = "Gamma Risk",
                Passed = gammaResult.RecommendedAction == RehedgeAction.Hold,
                Detail = gammaResult.Summary
            });

            // Determine overall result
            int passedCount = checks.Count(c => c.Passed);
            bool overallPass = checks.All(c => c.Passed);

            // Determine recommended contracts (may be adjusted by liquidity)
            int recommendedContracts = liquidityResult.RecommendedContracts;

            // Compute adjusted debit
            double adjustedDebit = costResult.SpreadCost.ExecutionDebit;

            var result = new STHD006A
            {
                BaseSignal = signal,
                Checks = checks,
                OverallPass = overallPass,
                AdjustedDebit = adjustedDebit,
                RecommendedContracts = recommendedContracts,
                ProductionReady = overallPass && signal.Strength == SignalStrength.Recommended,
                CostValidation = costResult,
                VegaCorrelation = vegaResult,
                LiquidityValidation = liquidityResult,
                GammaAssessment = gammaResult
            };

            SafeLog(() => LogValidationComplete(
                _logger!,
                signal.Symbol,
                overallPass,
                passedCount,
                checks.Count,
                null));

            return result;
        }
        catch (InvalidOperationException ex)
        {
            SafeLog(() => LogValidationError(_logger!, signal.Symbol, ex.Message, ex));

            // Return failed result with error information
            checks.Add(new ValidationCheck
            {
                Name = "Validation Error",
                Passed = false,
                Detail = ex.Message
            });

            return new STHD006A
            {
                BaseSignal = signal,
                Checks = checks,
                OverallPass = false,
                AdjustedDebit = 0,
                RecommendedContracts = 0,
                ProductionReady = false,
                CostValidation = null,
                VegaCorrelation = null,
                LiquidityValidation = null,
                GammaAssessment = null
            };
        }
    }

    /// <summary>
    /// Safely executes logging operation with fault isolation (Rule 15).
    /// </summary>
    private void SafeLog(Action logAction)
    {
        if (_logger == null)
        {
            return;
        }

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            logAction();
        }
        catch (Exception)
        {
            // Swallow logging exceptions (Rule 15: Fault Isolation)
        }
#pragma warning restore CA1031
    }
}

/// <summary>
/// Represents a single validation check result.
/// </summary>
public sealed record ValidationCheck
{
    /// <summary>
    /// Gets the name of the validation check.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets whether the check passed.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Gets the detailed result description.
    /// </summary>
    public required string Detail { get; init; }
}

/// <summary>
/// Represents the Greeks of a calendar spread position.
/// </summary>
public sealed record SpreadGreeks
{
    /// <summary>
    /// Gets the spread delta per contract.
    /// </summary>
    public required double Delta { get; init; }

    /// <summary>
    /// Gets the spread gamma per contract.
    /// </summary>
    public required double Gamma { get; init; }

    /// <summary>
    /// Gets the spread vega per contract.
    /// </summary>
    public required double Vega { get; init; }

    /// <summary>
    /// Gets the spread theta per contract.
    /// </summary>
    public required double Theta { get; init; }
}