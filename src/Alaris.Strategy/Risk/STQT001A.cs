// STQT001A.cs - queue-theoretic position management

using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Risk;

/// <summary>
/// Implements queue-theoretic analysis for position management.
/// </summary>

public sealed class STQT001A
{
    private readonly ILogger<STQT001A>? _logger;
    private readonly QueueParameters _params;

    // State tracking
    private double _estimatedArrivalRate;
    private double _estimatedServiceRate;
    private double _estimatedServiceCV;
    private double _currentVirtualTime;

    // LoggerMessage delegates
    private static readonly Action<ILogger, double, double, Exception?> LogBlockingProbability =
        LoggerMessage.Define<double, double>(
            LogLevel.Debug,
            new EventId(1, nameof(LogBlockingProbability)),
            "Blocking probability: P_K={Probability:P2} at utilisation ρ={Utilisation:F3}");

    private static readonly Action<ILogger, int, int, Exception?> LogCapacityRecommendation =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(2, nameof(LogCapacityRecommendation)),
            "Optimal capacity: K*={OptimalK} (current={CurrentK})");

    /// <summary>
    /// Initialises a new queue-theoretic position manager.
    /// </summary>
    public STQT001A(QueueParameters? parameters = null, ILogger<STQT001A>? logger = null)
    {
        _params = parameters ?? QueueParameters.Default;
        _logger = logger;
        _currentVirtualTime = 0;

        // Default estimates
        _estimatedArrivalRate = 0.5;  // 0.5 signals per day
        _estimatedServiceRate = 1.0 / 7.0;  // 7 day average holding
        _estimatedServiceCV = 0.5;
    }

    /// <summary>
    /// Computes the mean queue length using the Pollaczek-Khinchine formula.
    /// </summary>
    /// <param name="arrivalRate">Signal arrival rate λ (per day).</param>
    /// <param name="serviceRate">Position service rate μ = 1/E[S].</param>
    /// <param name="serviceCV">Coefficient of variation of service time.</param>
    /// <returns>Mean number of positions in system.</returns>
    
    public static double ComputeMeanQueueLength(double arrivalRate, double serviceRate, double serviceCV)
    {
        if (serviceRate <= 0)
        {
            return double.PositiveInfinity;
        }

        double rho = arrivalRate / serviceRate;

        if (rho >= 1.0)
        {
            return double.PositiveInfinity; // Unstable queue
        }

        double cSq = serviceCV * serviceCV;
        return rho + (rho * rho * (1.0 + cSq) / (2.0 * (1.0 - rho)));
    }

    /// <summary>
    /// Computes the mean waiting time (Little's Law derived).
    /// </summary>
    public static double ComputeMeanWaitingTime(double arrivalRate, double serviceRate, double serviceCV)
    {
        double meanQueueLength = ComputeMeanQueueLength(arrivalRate, serviceRate, serviceCV);
        return meanQueueLength / arrivalRate;
    }

    /// <summary>
    /// Computes blocking probability for M/M/1/K queue (exponential approximation).
    /// </summary>
    /// <param name="currentPositions">Current number of open positions.</param>
    /// <param name="maxCapacity">Maximum capacity K.</param>
    /// <param name="utilisation">System utilisation ρ = λ/μ.</param>
    /// <returns>Probability of blocking the next arrival.</returns>
    
    public double ComputeBlockingProbability(int currentPositions, int maxCapacity, double? utilisation = null)
    {
        double rho = utilisation ?? (_estimatedArrivalRate / _estimatedServiceRate);

        if (rho <= 0 || maxCapacity <= 0)
        {
            return 0.0;
        }

        // At capacity, blocking is certain
        if (currentPositions >= maxCapacity)
        {
            return 1.0;
        }

        if (Math.Abs(rho - 1.0) < 1e-10)
        {
            // Special case: ρ = 1
            return 1.0 / (maxCapacity + 1);
        }

        double rhoK = Math.Pow(rho, maxCapacity);
        double rhoK1 = rhoK * rho;

        double pk = (1.0 - rho) * rhoK / (1.0 - rhoK1);

        SafeLog(() => LogBlockingProbability(_logger!, pk, rho, null));

        return Math.Max(0, Math.Min(1, pk));
    }

    /// <summary>
    /// Computes the optimal capacity given cost parameters.
    /// </summary>
    /// <param name="arrivalRate">Expected signal arrival rate.</param>
    /// <param name="meanHoldingTime">Mean position holding time.</param>
    /// <param name="blockingCost">Cost per blocked signal.</param>
    /// <param name="holdingCost">Cost per position-day (capital cost).</param>
    /// <param name="maxSearch">Maximum capacity to consider.</param>
    /// <returns>Optimal capacity K*.</returns>
    
    public int ComputeOptimalCapacity(
        double arrivalRate,
        double meanHoldingTime,
        double blockingCost,
        double holdingCost,
        int maxSearch = 30)
    {
        double serviceRate = 1.0 / meanHoldingTime;
        double rho = arrivalRate / serviceRate;

        if (rho >= 1.0)
        {
            // Unstable - return max capacity
            return maxSearch;
        }

        double bestCost = double.PositiveInfinity;
        int optimalK = 5;

        for (int k = 1; k <= maxSearch; k++)
        {
            double pk = ComputeBlockingProbability(0, k, rho);
            double lk = ComputeMeanQueueLengthWithCapacity(rho, k);

            double cost = (blockingCost * arrivalRate * pk) + (holdingCost * lk);

            if (cost < bestCost)
            {
                bestCost = cost;
                optimalK = k;
            }
        }

        SafeLog(() => LogCapacityRecommendation(_logger!, optimalK, (int)(rho * 10), null));

        return optimalK;
    }

    /// <summary>
    /// Computes the Gittins-index proxy for a position.
    /// </summary>
    /// <param name="expectedProfit">Expected profit from position.</param>
    /// <param name="remainingDays">Days remaining until expected exit.</param>
    /// <param name="currentPnL">Current unrealised P&amp;L.</param>
    /// <returns>Priority score for position (higher = more valuable to keep).</returns>
    
    public static double ComputeGittinsProxy(double expectedProfit, double remainingDays, double currentPnL = 0)
    {
        if (remainingDays <= 0)
        {
            return double.NegativeInfinity; // Should be exited
        }

        // Adjusted expected value accounting for current P&L
        double adjustedExpected = expectedProfit + currentPnL;

        return adjustedExpected / remainingDays;
    }

    /// <summary>
    /// Selects the position to eject when at capacity and a new signal arrives.
    /// </summary>
    /// <param name="positions">Current positions with their priorities.</param>
    /// <param name="newSignalPriority">Priority of the incoming signal.</param>
    /// <returns>Index of position to eject, or -1 if new signal should be rejected.</returns>
    public static int SelectForEjection(IReadOnlyList<PositionPriority> positions, double newSignalPriority)
    {
        ArgumentNullException.ThrowIfNull(positions);

        if (positions.Count == 0)
        {
            return -1;
        }

        // Find position with minimum Gittins proxy
        int minIndex = 0;
        double minPriority = positions[0].Priority;

        for (int i = 1; i < positions.Count; i++)
        {
            if (positions[i].Priority < minPriority)
            {
                minPriority = positions[i].Priority;
                minIndex = i;
            }
        }

        // Eject only if new signal has higher priority
        if (newSignalPriority > minPriority)
        {
            return minIndex;
        }

        return -1; // Reject new signal instead
    }

    /// <summary>
    /// Computes the virtual finish time for WFQ scheduling.
    /// </summary>
    /// <param name="signalStrength">Signal strength (Recommended=3, Consider=1).</param>
    /// <param name="capitalRequired">Capital requirement (size).</param>
    /// <param name="arrivalTime">Signal arrival virtual time.</param>
    /// <returns>Virtual finish time for scheduling.</returns>
    
    public double ComputeVirtualFinishTime(int signalStrength, double capitalRequired, double? arrivalTime = null)
    {
        double weight = signalStrength switch
        {
            2 => 3.0,   // Recommended
            1 => 1.0,   // Consider
            _ => 0.5    // Avoid (shouldn't reach here normally)
        };

        double arrival = arrivalTime ?? _currentVirtualTime;
        double finishTime = Math.Max(arrival, _currentVirtualTime) + (capitalRequired / weight);

        return finishTime;
    }

    /// <summary>
    /// Advances the virtual time after processing a signal.
    /// </summary>
    /// <param name="finishTime">Virtual finish time of processed signal.</param>
    public void AdvanceVirtualTime(double finishTime)
    {
        _currentVirtualTime = Math.Max(_currentVirtualTime, finishTime);
    }

    /// <summary>
    /// Ranks signals by WFQ virtual finish time (ascending = higher priority).
    /// </summary>
    /// <param name="signals">Signals to rank.</param>
    /// <returns>Signals ordered by virtual finish time.</returns>
    public IEnumerable<SignalQueueEntry> RankByWFQ(IEnumerable<SignalQueueEntry> signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        List<SignalQueueEntry> ranked = new List<SignalQueueEntry>();
        foreach (SignalQueueEntry signal in signals)
        {
            SignalQueueEntry entry = signal with
            {
                VirtualFinishTime = ComputeVirtualFinishTime(signal.Strength, signal.CapitalRequired)
            };
            ranked.Add(entry);
        }

        ranked.Sort((left, right) => left.VirtualFinishTime.CompareTo(right.VirtualFinishTime));
        return ranked;
    }

    /// <summary>
    /// Updates arrival rate estimate using exponential smoothing.
    /// </summary>
    /// <param name="newArrivalCount">Number of arrivals in the observation period.</param>
    /// <param name="observationPeriod">Length of observation period in days.</param>
    public void UpdateArrivalRateEstimate(int newArrivalCount, double observationPeriod)
    {
        double observedRate = newArrivalCount / observationPeriod;
        _estimatedArrivalRate = (_params.SmoothingAlpha * observedRate)
                               + ((1 - _params.SmoothingAlpha) * _estimatedArrivalRate);
    }

    /// <summary>
    /// Updates service rate estimate from observed holding times.
    /// </summary>
    /// <param name="holdingTimes">Observed holding durations in days.</param>
    public void UpdateServiceRateEstimate(IReadOnlyList<double> holdingTimes)
    {
        ArgumentNullException.ThrowIfNull(holdingTimes);

        if (holdingTimes.Count == 0)
        {
            return;
        }

        double sum = 0.0;
        for (int i = 0; i < holdingTimes.Count; i++)
        {
            sum += holdingTimes[i];
        }

        double mean = sum / holdingTimes.Count;
        double varianceSum = 0.0;
        for (int i = 0; i < holdingTimes.Count; i++)
        {
            double diff = holdingTimes[i] - mean;
            varianceSum += diff * diff;
        }

        double variance = varianceSum / holdingTimes.Count;
        double stdDev = Math.Sqrt(variance);

        double observedRate = 1.0 / mean;
        double observedCV = stdDev / mean;

        _estimatedServiceRate = (_params.SmoothingAlpha * observedRate)
                               + ((1 - _params.SmoothingAlpha) * _estimatedServiceRate);
        _estimatedServiceCV = (_params.SmoothingAlpha * observedCV)
                             + ((1 - _params.SmoothingAlpha) * _estimatedServiceCV);
    }

    /// <summary>
    /// Gets current queue parameter estimates.
    /// </summary>
    public QueueEstimates GetCurrentEstimates()
    {
        double rho = _estimatedArrivalRate / _estimatedServiceRate;
        double meanQueue = ComputeMeanQueueLength(_estimatedArrivalRate, _estimatedServiceRate, _estimatedServiceCV);

        return new QueueEstimates(
            ArrivalRate: _estimatedArrivalRate,
            ServiceRate: _estimatedServiceRate,
            ServiceCV: _estimatedServiceCV,
            Utilisation: rho,
            MeanQueueLength: meanQueue);
    }

    private static double ComputeMeanQueueLengthWithCapacity(double rho, int capacity)
    {
        // Approximate mean queue length for M/M/1/K
        if (Math.Abs(rho - 1.0) < 1e-10)
        {
            return capacity / 2.0;
        }

        double rhoK1 = Math.Pow(rho, capacity + 1);
        double numerator = rho * (1 - ((capacity + 1) * Math.Pow(rho, capacity)) + (capacity * rhoK1));
        double denominator = (1 - rho) * (1 - rhoK1);

        return numerator / denominator;
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

}

/// <summary>
/// Queue management parameters.
/// </summary>
public readonly record struct QueueParameters(
    double SmoothingAlpha,
    double DefaultBlockingCost,
    double DefaultHoldingCost)
{
    /// <summary>Default parameters.</summary>
    public static QueueParameters Default => new QueueParameters(
        SmoothingAlpha: 0.1,
        DefaultBlockingCost: 100.0,   // $100 cost per missed opportunity
        DefaultHoldingCost: 1.0);     // $1 per position-day
}

/// <summary>
/// Current queue parameter estimates.
/// </summary>
public readonly record struct QueueEstimates(
    double ArrivalRate,
    double ServiceRate,
    double ServiceCV,
    double Utilisation,
    double MeanQueueLength);

/// <summary>
/// Represents a position with its computed priority.
/// </summary>
public readonly record struct PositionPriority(
    string Symbol,
    double ExpectedProfit,
    double RemainingDays,
    double CurrentPnL,
    double Priority);

/// <summary>
/// Signal entry in the scheduling queue.
/// </summary>
public readonly record struct SignalQueueEntry(
    string Symbol,
    int Strength,
    double CapitalRequired,
    double VirtualFinishTime,
    DateTime ArrivalTime);
