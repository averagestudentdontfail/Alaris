// STCS008A.cs - liquidity validator

using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Cost;

/// <summary>
/// Validates that position sizing is appropriate given instrument liquidity.
/// </summary>

public sealed class STCS008A
{
    private readonly ILogger<STCS008A>? _logger;
    private readonly double _maxPositionToVolumeRatio;
    private readonly double _maxPositionToOpenInterestRatio;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, int, double, double, Exception?> LogValidationResult =
        LoggerMessage.Define<string, int, double, double>(
            LogLevel.Information,
            new EventId(1, nameof(LogValidationResult)),
            "Liquidity validation for {Symbol}: {Contracts} contracts, volume ratio={VolumeRatio:P2}, OI ratio={OIRatio:P2}");

    private static readonly Action<ILogger, string, int, int, Exception?> LogSizeRecommendation =
        LoggerMessage.Define<string, int, int>(
            LogLevel.Warning,
            new EventId(2, nameof(LogSizeRecommendation)),
            "Position size exceeds liquidity for {Symbol}: requested {Requested}, recommended {Recommended}");

    /// <summary>
    /// Default maximum position-to-volume ratio.
    /// </summary>
    
    public const double DefaultMaxVolumeRatio = 0.01;

    /// <summary>
    /// Default maximum position-to-open-interest ratio.
    /// </summary>
    
    public const double DefaultMaxOpenInterestRatio = 0.02;

    /// <summary>
    /// Initialises a new instance of the liquidity validator.
    /// </summary>
    /// <param name="maxPositionToVolumeRatio">
    /// Maximum acceptable position-to-volume ratio. Default: 1%.
    /// </param>
    /// <param name="maxPositionToOpenInterestRatio">
    /// Maximum acceptable position-to-open-interest ratio. Default: 2%.
    /// </param>
    /// <param name="logger">Optional logger instance.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when either ratio is not in the range (0, 1].
    /// </exception>
    public STCS008A(
        double maxPositionToVolumeRatio = DefaultMaxVolumeRatio,
        double maxPositionToOpenInterestRatio = DefaultMaxOpenInterestRatio,
        ILogger<STCS008A>? logger = null)
    {
        if (maxPositionToVolumeRatio <= 0 || maxPositionToVolumeRatio > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPositionToVolumeRatio),
                maxPositionToVolumeRatio,
                "Volume ratio must be in range (0, 1].");
        }

        if (maxPositionToOpenInterestRatio <= 0 || maxPositionToOpenInterestRatio > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPositionToOpenInterestRatio),
                maxPositionToOpenInterestRatio,
                "Open interest ratio must be in range (0, 1].");
        }

        _maxPositionToVolumeRatio = maxPositionToVolumeRatio;
        _maxPositionToOpenInterestRatio = maxPositionToOpenInterestRatio;
        _logger = logger;
    }

    /// <summary>
    /// Validates that the proposed position size can be absorbed by the market.
    /// </summary>
    /// <param name="symbol">The underlying symbol.</param>
    /// <param name="proposedContracts">The proposed number of contracts.</param>
    /// <param name="backMonthVolume">Back-month option average daily volume.</param>
    /// <param name="backMonthOpenInterest">Back-month option open interest.</param>
    /// <returns>Liquidity validation result with recommendations.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when contracts, volume, or open interest is non-positive.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when symbol is null or empty.
    /// </exception>
    public STCS009A Validate(
        string symbol,
        int proposedContracts,
        int backMonthVolume,
        int backMonthOpenInterest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        if (proposedContracts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(proposedContracts),
                proposedContracts,
                "Proposed contracts must be positive.");
        }

        if (backMonthVolume <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(backMonthVolume),
                backMonthVolume,
                "Back month volume must be positive.");
        }

        if (backMonthOpenInterest <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(backMonthOpenInterest),
                backMonthOpenInterest,
                "Back month open interest must be positive.");
        }

        // Compute ratios
        double volumeRatio = (double)proposedContracts / backMonthVolume;
        double openInterestRatio = (double)proposedContracts / backMonthOpenInterest;

        // Evaluate against thresholds
        bool passesVolumeFilter = volumeRatio <= _maxPositionToVolumeRatio;
        bool passesOpenInterestFilter = openInterestRatio <= _maxPositionToOpenInterestRatio;
        bool definedRiskAssured = passesVolumeFilter && passesOpenInterestFilter;

        // Compute recommended size if thresholds exceeded
        int recommendedContracts = proposedContracts;
        if (!definedRiskAssured)
        {
            int maxByVolume = (int)Math.Floor(backMonthVolume * _maxPositionToVolumeRatio);
            int maxByOpenInterest = (int)Math.Floor(backMonthOpenInterest * _maxPositionToOpenInterestRatio);
            recommendedContracts = Math.Max(1, Math.Min(maxByVolume, maxByOpenInterest));
        }

        STCS009A result = new STCS009A
        {
            Symbol = symbol,
            RequestedContracts = proposedContracts,
            RecommendedContracts = recommendedContracts,
            BackMonthVolume = backMonthVolume,
            BackMonthOpenInterest = backMonthOpenInterest,
            VolumeRatio = volumeRatio,
            OpenInterestRatio = openInterestRatio,
            VolumeThreshold = _maxPositionToVolumeRatio,
            OpenInterestThreshold = _maxPositionToOpenInterestRatio,
            PassesVolumeFilter = passesVolumeFilter,
            PassesOpenInterestFilter = passesOpenInterestFilter,
            DefinedRiskAssured = definedRiskAssured
        };

        SafeLog(() => LogValidationResult(
            _logger!,
            symbol,
            proposedContracts,
            volumeRatio,
            openInterestRatio,
            null));

        if (!definedRiskAssured)
        {
            SafeLog(() => LogSizeRecommendation(
                _logger!,
                symbol,
                proposedContracts,
                recommendedContracts,
                null));
        }

        return result;
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
