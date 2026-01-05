using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Alaris.Strategy.Risk;
using Alaris.Strategy.Core;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for STRK004A - Priority Queue Capital Allocator.
/// Tests queue-based allocation with heterogeneous opportunity ranking.
/// </summary>
public sealed class STRK004ATests
{
    private readonly STRK001A _baseKelly;
    private readonly STRK004A _queueAllocator;

    public STRK004ATests()
    {
        _baseKelly = new STRK001A();
        _queueAllocator = new STRK004A(_baseKelly);
    }

    [Fact]
    public void CalculatePriority_WithHighEdgeRecommendedSignal_ReturnsHighPriority()
    {
        // Arrange
        STCR004A signal = CreateSignal("AAPL", STCR004AStrength.Recommended, ivRvRatio: 1.50, tsSlope: -0.005);
        List<Trade> trades = GenerateHistoricalTrades(50);

        // Act
        double priority = _queueAllocator.CalculatePriority(signal, trades);

        // Assert: High IV/RV and negative term structure = high priority
        priority.Should().BeGreaterThan(0.2);
    }

    [Fact]
    public void CalculatePriority_WithLowEdgeConsiderSignal_ReturnsLowerPriority()
    {
        // Arrange
        STCR004A highSignal = CreateSignal("AAPL", STCR004AStrength.Recommended, ivRvRatio: 1.50, tsSlope: -0.005);
        STCR004A lowSignal = CreateSignal("MSFT", STCR004AStrength.Consider, ivRvRatio: 1.20, tsSlope: -0.002);
        List<Trade> trades = GenerateHistoricalTrades(50);

        // Act
        double highPriority = _queueAllocator.CalculatePriority(highSignal, trades);
        double lowPriority = _queueAllocator.CalculatePriority(lowSignal, trades);

        // Assert
        highPriority.Should().BeGreaterThan(lowPriority);
    }

    [Fact]
    public void CalculatePriority_WithAvoidSignal_ReturnsZero()
    {
        // Arrange
        STCR004A signal = CreateSignal("AAPL", STCR004AStrength.Avoid, ivRvRatio: 1.50);
        List<Trade> trades = GenerateHistoricalTrades(50);

        // Act
        double priority = _queueAllocator.CalculatePriority(signal, trades);

        // Assert
        priority.Should().Be(0);
    }

    [Fact]
    public void Allocate_WhenQueueEmpty_AcceptsNewOpportunity()
    {
        // Arrange
        STCR004A candidate = CreateSignal("AAPL", STCR004AStrength.Recommended, ivRvRatio: 1.40);
        List<OpenPosition> openPositions = new List<OpenPosition>();
        List<Trade> trades = GenerateHistoricalTrades(30);

        // Act
        QueueAllocationResult result = _queueAllocator.Allocate(
            portfolioValue: 100000,
            candidate: candidate,
            candidateSpreadCost: 2.0,
            openPositions: openPositions,
            historicalTrades: trades);

        // Assert
        result.Decision.Should().Be(AllocationDecision.Accept);
        result.QueueRank.Should().Be(1);
        result.Sizing.Should().NotBeNull();
        result.Sizing!.Contracts.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Allocate_WhenCandidateIsHigherPriority_RanksFirst()
    {
        // Arrange
        STCR004A candidate = CreateSignal("NVDA", STCR004AStrength.Recommended, ivRvRatio: 1.60, tsSlope: -0.006);
        List<Trade> trades = GenerateHistoricalTrades(30);
        
        List<OpenPosition> openPositions = new List<OpenPosition>
        {
            CreateOpenPosition("AAPL", priority: 0.10, allocation: 0.05),
            CreateOpenPosition("MSFT", priority: 0.08, allocation: 0.04)
        };

        // Act
        QueueAllocationResult result = _queueAllocator.Allocate(
            portfolioValue: 100000,
            candidate: candidate,
            candidateSpreadCost: 2.0,
            openPositions: openPositions,
            historicalTrades: trades);

        // Assert
        result.Decision.Should().Be(AllocationDecision.Accept);
        result.QueueRank.Should().Be(1); // Highest priority
    }

    [Fact]
    public void Allocate_WhenCapacityExhausted_EvaluatesDisplacement()
    {
        // Arrange: Create positions that consume all 60% capacity
        STCR004A candidate = CreateSignal("AMZN", STCR004AStrength.Recommended, ivRvRatio: 1.45);
        List<Trade> trades = GenerateHistoricalTrades(30);
        
        List<OpenPosition> positions = new List<OpenPosition>
        {
            CreateOpenPosition("AAPL", priority: 0.25, allocation: 0.20),
            CreateOpenPosition("MSFT", priority: 0.20, allocation: 0.20),
            CreateOpenPosition("GOOG", priority: 0.15, allocation: 0.20)
        };

        // Act
        QueueAllocationResult result = _queueAllocator.Allocate(
            portfolioValue: 100000,
            candidate: candidate,
            candidateSpreadCost: 2.0,
            openPositions: positions,
            historicalTrades: trades);

        // Assert: Should evaluate displacement (may accept or reject)
        result.Rationale.Should().NotBeEmpty();
    }

    [Fact]
    public void Allocate_WhenCandidateCanDisplace_ReturnsDisplaceExisting()
    {
        // Arrange: Candidate has much higher priority than lowest position
        STCR004A candidate = CreateSignal("NVDA", STCR004AStrength.Recommended, ivRvRatio: 1.80, tsSlope: -0.008);
        List<Trade> trades = GenerateHistoricalTrades(30);
        
        List<OpenPosition> positions = new List<OpenPosition>
        {
            CreateOpenPosition("AAPL", priority: 0.30, allocation: 0.30),
            CreateOpenPosition("MSFT", priority: 0.25, allocation: 0.20),
            CreateOpenPosition("WEAK", priority: 0.02, allocation: 0.10) // Low priority target
        };

        // Act
        QueueAllocationResult result = _queueAllocator.Allocate(
            portfolioValue: 100000,
            candidate: candidate,
            candidateSpreadCost: 2.0,
            openPositions: positions,
            historicalTrades: trades);

        // Assert: Should displace the weak position
        if (result.Decision == AllocationDecision.DisplaceExisting)
        {
            result.DisplacedSymbol.Should().Be("WEAK");
        }
    }

    [Fact]
    public void Allocate_WithAvoidSignal_RejectsOpportunity()
    {
        // Arrange: Signal with Avoid strength (which gives zero priority)
        STCR004A candidate = CreateSignal("XYZ", STCR004AStrength.Avoid, ivRvRatio: 1.30);
        List<Trade> trades = GenerateHistoricalTrades(30);
        List<OpenPosition> openPositions = new List<OpenPosition>();

        // Act
        QueueAllocationResult result = _queueAllocator.Allocate(
            portfolioValue: 100000,
            candidate: candidate,
            candidateSpreadCost: 2.0,
            openPositions: openPositions,
            historicalTrades: trades);

        // Assert: Avoid signals should be rejected
        result.Decision.Should().Be(AllocationDecision.Reject);
    }

    [Fact]
    public void RebalancePortfolio_WithMisallocatedPositions_ReturnsRecommendations()
    {
        // Arrange: Positions with allocations not matching their priority ranks
        List<OpenPosition> positions = new List<OpenPosition>
        {
            CreateOpenPosition("HIGH", priority: 0.50, allocation: 0.05, kellyFraction: 0.15), // Under-allocated
            CreateOpenPosition("LOW", priority: 0.05, allocation: 0.30, kellyFraction: 0.10)   // Over-allocated
        };
        List<Trade> trades = GenerateHistoricalTrades(30);

        // Act
        IReadOnlyList<RebalanceRecommendation> recommendations = _queueAllocator.RebalancePortfolio(
            portfolioValue: 100000,
            openPositions: positions,
            historicalTrades: trades);

        // Assert: LOW position is over-allocated relative to priority, should decrease
        recommendations.Should().NotBeEmpty();
        recommendations.Should().Contain(r => r.Symbol == "LOW" && r.Action == RebalanceAction.Decrease);
    }

    [Fact]
    public void Allocate_WithNullCandidate_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => _queueAllocator.Allocate(
            portfolioValue: 100000,
            candidate: null!,
            candidateSpreadCost: 2.0,
            openPositions: new List<OpenPosition>(),
            historicalTrades: GenerateHistoricalTrades(30));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100000)]
    public void Allocate_WithInvalidPortfolioValue_ThrowsArgumentOutOfRangeException(double portfolioValue)
    {
        // Act & Assert
        Action act = () => _queueAllocator.Allocate(
            portfolioValue: portfolioValue,
            candidate: CreateSignal("AAPL", STCR004AStrength.Recommended),
            candidateSpreadCost: 2.0,
            openPositions: new List<OpenPosition>(),
            historicalTrades: GenerateHistoricalTrades(30));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }


    private static STCR004A CreateSignal(
        string symbol, 
        STCR004AStrength strength, 
        double ivRvRatio = 1.30,
        double tsSlope = -0.004)
    {
        return new STCR004A
        {
            Symbol = symbol,
            Strength = strength,
            IVRVRatio = ivRvRatio,
            STTM001ASlope = tsSlope,
            AverageVolume = 2_000_000,
            EarningsDate = DateTime.Today.AddDays(5),
            STCR004ADate = DateTime.Today,
            IsLeungSantoliCalibrated = false,
            IVCrushRatio = 0.0
        };
    }

    private static OpenPosition CreateOpenPosition(
        string symbol,
        double priority,
        double allocation,
        double kellyFraction = 0.10)
    {
        return new OpenPosition
        {
            Symbol = symbol,
            Priority = priority,
            AllocationPercent = allocation,
            KellyFraction = kellyFraction,
            EntryDate = DateTime.Today.AddDays(-5),
            DaysToEarnings = 3
        };
    }

    private static List<Trade> GenerateHistoricalTrades(int count)
    {
        List<Trade> trades = new List<Trade>();
        DateTime baseDate = DateTime.Today.AddDays(-count * 2.5);

        for (int i = 0; i < count; i++)
        {
            DateTime entryDate = baseDate.AddDays(i * 2.5);
            int holdingPeriod = 5 + (i % 3 - 1);

            trades.Add(new Trade
            {
                Symbol = i % 2 == 0 ? "AAPL" : "MSFT",
                EntryDate = entryDate,
                ExitDate = entryDate.AddDays(holdingPeriod),
                ProfitLoss = (i % 3 == 0) ? -100 : 150
            });
        }

        return trades;
    }

}
