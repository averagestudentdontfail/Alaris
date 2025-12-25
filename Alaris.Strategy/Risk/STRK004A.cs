// STRK004A.cs - priority queue capital allocator

using Alaris.Core.HotPath;
using Alaris.Strategy.Core;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Risk;

/// <summary>
/// Implements priority queue capital allocation for heterogeneous opportunities.
/// </summary>

public sealed class STRK004A
{
    private readonly STRK001A _baseKelly;
    private readonly ILogger<STRK004A>? _logger;

    /// <summary>
    /// Maximum total portfolio allocation across all positions.
    /// </summary>
    private const double MaxTotalAllocation = 0.60;

    /// <summary>
    /// Minimum priority improvement required to displace existing position.
    /// Prevents excessive rebalancing from marginal improvements.
    /// </summary>
    private const double MinPriorityImprovementRatio = 1.20;

    /// <summary>
    /// Fractional Kelly multiplier for safety (25% of full Kelly).
    /// </summary>
    private const double FractionalKellyMultiplier = 0.25;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, double, int, Exception?> LogAllocationDecision =
        LoggerMessage.Define<string, double, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogAllocationDecision)),
            "Queue allocation for {Symbol}: priority={Priority:F4}, rank={Rank}");

    private static readonly Action<ILogger, string, string, double, double, Exception?> LogDisplacementDecision =
        LoggerMessage.Define<string, string, double, double>(
            LogLevel.Information,
            new EventId(2, nameof(LogDisplacementDecision)),
            "Displacement analysis: {Candidate} (priority={CandidatePriority:F4}) vs {Incumbent} (priority={IncumbentPriority:F4})");

    private static readonly Action<ILogger, int, double, Exception?> LogQueueState =
        LoggerMessage.Define<int, double>(
            LogLevel.Debug,
            new EventId(3, nameof(LogQueueState)),
            "Queue state: {PositionCount} positions, {TotalAllocation:P2} allocated");

    /// <summary>
    /// Initialises a new instance of the priority queue allocator.
    /// </summary>
    /// <param name="baseKelly">Base Kelly calculator for individual position sizing.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when baseKelly is null.</exception>
    public STRK004A(STRK001A baseKelly, ILogger<STRK004A>? logger = null)
    {
        _baseKelly = baseKelly ?? throw new ArgumentNullException(nameof(baseKelly));
        _logger = logger;
    }

    /// <summary>
    /// Evaluates a candidate opportunity against the current portfolio queue.
    /// </summary>
    /// <param name="portfolioValue">Total portfolio value.</param>
    /// <param name="candidate">Candidate trading signal to evaluate.</param>
    /// <param name="candidateSpreadCost">Cost of the candidate calendar spread.</param>
    /// <param name="openPositions">Currently open positions with their priorities.</param>
    /// <param name="historicalTrades">Historical trades for Kelly calculation.</param>
    /// <returns>Allocation decision with sizing and any displacement recommendations.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when portfolioValue is non-positive.</exception>
    public QueueAllocationResult Allocate(
        double portfolioValue,
        STCR004A candidate,
        double candidateSpreadCost,
        IReadOnlyList<OpenPosition> openPositions,
        IReadOnlyList<Trade> historicalTrades)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(openPositions);
        ArgumentNullException.ThrowIfNull(historicalTrades);

        if (portfolioValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(portfolioValue),
                "Portfolio value must be positive");
        }

        if (candidateSpreadCost <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateSpreadCost),
                "Spread cost must be positive");
        }

        // Step 1: Calculate priority score for candidate
        double candidatePriority = CalculatePriority(candidate, historicalTrades);

        // Step 2: Calculate base Kelly allocation for candidate
        STRK002A basePosition = _baseKelly.CalculateFromHistory(
            portfolioValue, historicalTrades, candidateSpreadCost, candidate);

        if (basePosition.Contracts == 0 || candidatePriority <= 0)
        {
            return QueueAllocationResult.Reject(candidate.Symbol,
                "Insufficient edge for queue entry");
        }

        // Step 3: Build priority queue including open positions
        List<QueueEntry> queue = BuildPriorityQueue(openPositions, candidate, candidatePriority, basePosition.AllocationPercent);

        // Step 4: Calculate current total allocation
        double currentAllocation = openPositions.Sum(p => p.AllocationPercent);

        SafeLog(() => LogQueueState(_logger!, openPositions.Count, currentAllocation, null));

        // Step 5: Determine candidate's rank in queue
        int candidateRank = queue.FindIndex(p => p.Symbol == candidate.Symbol);

        SafeLog(() => LogAllocationDecision(_logger!, candidate.Symbol, candidatePriority, candidateRank + 1, null));

        // Step 6: Calculate allocation based on queue position
        return CalculateQueueBasedAllocation(
            portfolioValue,
            candidate,
            candidateSpreadCost,
            candidatePriority,
            candidateRank,
            queue,
            currentAllocation,
            basePosition);
    }

    /// <summary>
    /// Calculates the priority score for an opportunity.
    /// </summary>
    
    /// <param name="signal">Trading signal containing opportunity metrics.</param>
    /// <param name="historicalTrades">Historical trades for win rate estimation.</param>
    /// <returns>Priority score (higher is better).</returns>
    public double CalculatePriority(STCR004A signal, IReadOnlyList<Trade> historicalTrades)
    {
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(historicalTrades);

        // Base edge from IV/RV ratio (1.25 threshold = 25% edge minimum)
        double edge = Math.Max(0, signal.IVRVRatio - 1.0);

        // Signal strength multiplier
        double strengthMultiplier = signal.Strength switch
        {
            STCR004AStrength.Recommended => 1.0,
            STCR004AStrength.Consider => 0.6,
            STCR004AStrength.Avoid => 0.0,
            _ => 0.0
        };

        // Term structure contribution (more negative slope = higher priority)
        // Normalised: -0.00406 threshold maps to ~1.0 bonus at threshold
        double tsBonus = Math.Max(0, -signal.STTM001ASlope / 0.00406);
        tsBonus = Math.Min(tsBonus, 2.0); // Cap at 2x

        // Expected IV crush contribution (from L&S model if available)
        double ivCrushBonus = signal.IsLeungSantoliCalibrated
            ? Math.Min(signal.IVCrushRatio, 0.5) // Cap contribution
            : 0.0;

        // Combine factors into priority score
        // Priority = Edge × Quality × (1 + TermStructure + IVCrush)
        double priority = edge * strengthMultiplier * (1.0 + tsBonus + ivCrushBonus);

        // Apply variance penalty if historical data available
        if (historicalTrades.Count >= 20)
        {
            double winRate = historicalTrades.Count(t => t.ProfitLoss > 0) / (double)historicalTrades.Count;
            double variancePenalty = CalculateVariancePenalty(winRate);
            priority *= variancePenalty;
        }

        return priority;
    }

    /// <summary>
    /// Calculates variance penalty based on historical win rate uncertainty.
    /// </summary>
    
    private static double CalculateVariancePenalty(double winRate)
    {
        // Bernoulli variance maximised at p=0.5
        double variance = winRate * (1 - winRate);
        const double penaltyScale = 2.0;
        return 1.0 / (1.0 + (penaltyScale * Math.Sqrt(variance)));
    }

    /// <summary>
    /// Builds the priority queue including open positions and candidate.
    /// </summary>
    private static List<QueueEntry> BuildPriorityQueue(
        IReadOnlyList<OpenPosition> openPositions,
        STCR004A candidate,
        double candidatePriority,
        double candidateAllocation)
    {
        List<QueueEntry> queue = new List<QueueEntry>(openPositions.Count + 1);

        // Add existing positions
        foreach (OpenPosition pos in openPositions)
        {
            queue.Add(new QueueEntry(
                pos.Symbol,
                pos.Priority,
                pos.AllocationPercent,
                IsOpen: true));
        }

        // Add candidate
        queue.Add(new QueueEntry(
            candidate.Symbol,
            candidatePriority,
            candidateAllocation,
            IsOpen: false));

        // Sort by priority descending (highest priority first)
        queue.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        return queue;
    }

    /// <summary>
    /// Calculates allocation based on queue position and available capital.
    /// </summary>
    private QueueAllocationResult CalculateQueueBasedAllocation(
        double portfolioValue,
        STCR004A candidate,
        double spreadCost,
        double candidatePriority,
        int candidateRank,
        List<QueueEntry> queue,
        double currentAllocation,
        STRK002A basePosition)
    {
        // Calculate cumulative allocation to higher-priority positions
        double cumulativeAllocation = 0;
        for (int i = 0; i < candidateRank; i++)
        {
            cumulativeAllocation += queue[i].AllocationPercent;
        }

        // Check if there's room for this candidate
        double remainingCapacity = MaxTotalAllocation - cumulativeAllocation;

        if (remainingCapacity <= 0)
        {
            // Check if displacement is warranted
            return EvaluateDisplacement(
                candidate, candidatePriority, queue, currentAllocation, portfolioValue, spreadCost, basePosition);
        }

        // Calculate adjusted allocation (capped by remaining capacity and base Kelly)
        double adjustedAllocation = Math.Min(
            basePosition.AllocationPercent * FractionalKellyMultiplier,
            remainingCapacity);

        // Convert to contracts
        double dollarAllocation = portfolioValue * adjustedAllocation;
        int contracts = (int)Math.Floor(dollarAllocation / (spreadCost * 100.0));

        if (contracts <= 0)
        {
            return QueueAllocationResult.Reject(candidate.Symbol,
                "Insufficient remaining capacity after higher-priority positions");
        }

        var sizing = new STRK002A
        {
            Contracts = contracts,
            AllocationPercent = adjustedAllocation,
            DollarAllocation = dollarAllocation,
            MaxLossPerContract = basePosition.MaxLossPerContract,
            TotalRisk = contracts * basePosition.MaxLossPerContract,
            ExpectedProfitPerContract = basePosition.ExpectedProfitPerContract,
            KellyFraction = basePosition.KellyFraction
        };

        return new QueueAllocationResult
        {
            Symbol = candidate.Symbol,
            Decision = AllocationDecision.Accept,
            Sizing = sizing,
            QueueRank = candidateRank + 1,
            Priority = candidatePriority,
            Rationale = $"Accepted at rank {candidateRank + 1}/{queue.Count}, " +
                       $"allocation {adjustedAllocation:P2} of {remainingCapacity:P2} remaining"
        };
    }

    /// <summary>
    /// Evaluates whether the candidate should displace a lower-priority position.
    /// </summary>
    private QueueAllocationResult EvaluateDisplacement(
        STCR004A candidate,
        double candidatePriority,
        List<QueueEntry> queue,
        double currentAllocation,
        double portfolioValue,
        double spreadCost,
        STRK002A basePosition)
    {
        // Find lowest-priority open position - ZERO ALLOC (struct)
        QueueEntry lowestOpen = default;
        bool foundOpen = false;
        double lowestPriority = double.MaxValue;
        
        for (int i = 0; i < queue.Count; i++)
        {
            QueueEntry entry = queue[i];
            if (entry.IsOpen && entry.Priority < lowestPriority)
            {
                lowestOpen = entry;
                lowestPriority = entry.Priority;
                foundOpen = true;
            }
        }

        if (!foundOpen)
        {
            return QueueAllocationResult.Reject(candidate.Symbol,
                "No open positions available for displacement analysis");
        }

        SafeLog(() => LogDisplacementDecision(_logger!,
            candidate.Symbol, lowestOpen.Symbol,
            candidatePriority, lowestOpen.Priority, null));

        // Check if candidate priority exceeds threshold for displacement
        double requiredPriority = lowestOpen.Priority * MinPriorityImprovementRatio;

        if (candidatePriority < requiredPriority)
        {
            return QueueAllocationResult.Reject(candidate.Symbol,
                $"Priority {candidatePriority:F4} below displacement threshold " +
                $"{requiredPriority:F4} (requires {MinPriorityImprovementRatio:P0} improvement)");
        }

        // Calculate allocation using displaced position's freed capital
        double freedAllocation = lowestOpen.AllocationPercent;
        double adjustedAllocation = Math.Min(
            basePosition.AllocationPercent * FractionalKellyMultiplier,
            freedAllocation);

        double dollarAllocation = portfolioValue * adjustedAllocation;
        int contracts = (int)Math.Floor(dollarAllocation / (spreadCost * 100.0));

        var sizing = new STRK002A
        {
            Contracts = contracts,
            AllocationPercent = adjustedAllocation,
            DollarAllocation = dollarAllocation,
            MaxLossPerContract = basePosition.MaxLossPerContract,
            TotalRisk = contracts * basePosition.MaxLossPerContract,
            ExpectedProfitPerContract = basePosition.ExpectedProfitPerContract,
            KellyFraction = basePosition.KellyFraction
        };

        return new QueueAllocationResult
        {
            Symbol = candidate.Symbol,
            Decision = AllocationDecision.DisplaceExisting,
            Sizing = sizing,
            QueueRank = queue.FindIndex(q => q.Symbol == candidate.Symbol) + 1,
            Priority = candidatePriority,
            DisplacedSymbol = lowestOpen.Symbol,
            DisplacedPriority = lowestOpen.Priority,
            Rationale = $"Displaces {lowestOpen.Symbol} " +
                       $"(priority improvement: {candidatePriority / lowestOpen.Priority:P0})"
        };
    }

    /// <summary>
    /// Rebalances the entire portfolio to optimal queue allocation.
    /// </summary>
    
    /// <param name="portfolioValue">Total portfolio value.</param>
    /// <param name="openPositions">Currently open positions.</param>
    /// <param name="historicalTrades">Historical trades for priority calculation.</param>
    /// <returns>List of rebalancing recommendations.</returns>
    public IReadOnlyList<RebalanceRecommendation> RebalancePortfolio(
        double portfolioValue,
        IReadOnlyList<OpenPosition> openPositions,
        IReadOnlyList<Trade> historicalTrades)
    {
        ArgumentNullException.ThrowIfNull(openPositions);
        ArgumentNullException.ThrowIfNull(historicalTrades);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(portfolioValue);

        List<RebalanceRecommendation> recommendations = new List<RebalanceRecommendation>();

        // Sort positions by priority
        List<OpenPosition> sortedPositions = openPositions
            .OrderByDescending(p => p.Priority)
            .ToList();

        double cumulativeAllocation = 0;
        double targetAllocation = MaxTotalAllocation;

        foreach (OpenPosition position in sortedPositions)
        {
            double idealAllocation = position.KellyFraction * FractionalKellyMultiplier;
            double availableAllocation = Math.Max(0, targetAllocation - cumulativeAllocation);
            double optimalAllocation = Math.Min(idealAllocation, availableAllocation);

            if (Math.Abs(position.AllocationPercent - optimalAllocation) > 0.005) // 0.5% threshold
            {
                recommendations.Add(new RebalanceRecommendation
                {
                    Symbol = position.Symbol,
                    CurrentAllocation = position.AllocationPercent,
                    OptimalAllocation = optimalAllocation,
                    Priority = position.Priority,
                    Action = optimalAllocation > position.AllocationPercent
                        ? RebalanceAction.Increase
                        : RebalanceAction.Decrease,
                    Rationale = $"Priority rank warrants {optimalAllocation:P2} vs current {position.AllocationPercent:P2}"
                });
            }

            cumulativeAllocation += optimalAllocation;
        }

        return recommendations;
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

#pragma warning disable CA1031
        try { logAction(); }
        catch (Exception) { /* Swallow logging exceptions */ }
#pragma warning restore CA1031
    }
}

// Supporting Types

/// <summary>
/// Represents an entry in the priority queue.
/// </summary>
/// <param name="Symbol">Underlying symbol.</param>
/// <param name="Priority">Calculated priority score.</param>
/// <param name="AllocationPercent">Current or proposed allocation.</param>
/// <param name="IsOpen">Whether this is an existing open position.</param>
internal readonly record struct QueueEntry(
    string Symbol,
    double Priority,
    double AllocationPercent,
    bool IsOpen);

/// <summary>
/// Represents a currently open position for queue allocation.
/// </summary>
public sealed record OpenPosition
{
    /// <summary>Underlying symbol.</summary>
    public required string Symbol { get; init; }

    /// <summary>Priority score when position was opened.</summary>
    public required double Priority { get; init; }

    /// <summary>Current allocation percentage.</summary>
    public required double AllocationPercent { get; init; }

    /// <summary>Kelly fraction for this position.</summary>
    public required double KellyFraction { get; init; }

    /// <summary>Entry date.</summary>
    public required DateTime EntryDate { get; init; }

    /// <summary>Days to earnings announcement.</summary>
    public required int DaysToEarnings { get; init; }
}

/// <summary>
/// Result of queue-based allocation decision.
/// </summary>
public sealed record QueueAllocationResult
{
    /// <summary>Symbol evaluated.</summary>
    public required string Symbol { get; init; }

    /// <summary>Allocation decision.</summary>
    public required AllocationDecision Decision { get; init; }

    /// <summary>Position sizing (null if rejected).</summary>
    public STRK002A? Sizing { get; init; }

    /// <summary>Rank in priority queue.</summary>
    public int QueueRank { get; init; }

    /// <summary>Calculated priority score.</summary>
    public double Priority { get; init; }

    /// <summary>Symbol to displace (if Decision is DisplaceExisting).</summary>
    public string? DisplacedSymbol { get; init; }

    /// <summary>Priority of displaced position.</summary>
    public double DisplacedPriority { get; init; }

    /// <summary>Human-readable rationale.</summary>
    public required string Rationale { get; init; }

    /// <summary>Creates a rejection result.</summary>
    public static QueueAllocationResult Reject(string symbol, string reason) => new()
    {
        Symbol = symbol,
        Decision = AllocationDecision.Reject,
        Rationale = reason
    };
}

/// <summary>
/// Allocation decision types.
/// </summary>
public enum AllocationDecision
{
    /// <summary>Reject the opportunity - insufficient priority or capacity.</summary>
    Reject = 0,

    /// <summary>Accept the opportunity into the queue.</summary>
    Accept = 1,

    /// <summary>Accept by displacing a lower-priority position.</summary>
    DisplaceExisting = 2
}

/// <summary>
/// Recommendation for portfolio rebalancing.
/// </summary>
public sealed record RebalanceRecommendation
{
    /// <summary>Position symbol.</summary>
    public required string Symbol { get; init; }

    /// <summary>Current allocation percentage.</summary>
    public required double CurrentAllocation { get; init; }

    /// <summary>Optimal allocation based on queue position.</summary>
    public required double OptimalAllocation { get; init; }

    /// <summary>Current priority score.</summary>
    public required double Priority { get; init; }

    /// <summary>Recommended action.</summary>
    public required RebalanceAction Action { get; init; }

    /// <summary>Human-readable rationale.</summary>
    public required string Rationale { get; init; }
}

/// <summary>
/// Rebalancing actions.
/// </summary>
public enum RebalanceAction
{
    /// <summary>No change needed.</summary>
    Hold = 0,

    /// <summary>Increase allocation.</summary>
    Increase = 1,

    /// <summary>Decrease allocation.</summary>
    Decrease = 2,

    /// <summary>Close position entirely.</summary>
    Close = 3
}
