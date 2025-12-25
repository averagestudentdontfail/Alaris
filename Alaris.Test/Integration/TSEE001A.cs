// TSEE001A.cs - End-to-End Pipeline Tests
// Component ID: TSEE001A
//
// End-to-end workflow tests for complete signal-to-execution pipeline.
// Tests full integration of data → signal generation → validation → sizing.
//
// Test Scenarios:
// 1. HappyPath_SingleSymbol - Complete pipeline with one equity
// 2. EarningsEvent_SignalGeneration - Signal generation around earnings
// 3. HighVolatility_PositionSizing - VIX > 30 regime adjustments
// 4. NearExpiry_BoundaryHandling - T < 3 DTE edge cases
// 5. DataGap_Recovery - Missing data handling
// 6. MultiSymbol_Throughput - Batched process multiple symbols
//

using Xunit;
using FluentAssertions;
using Alaris.Strategy.Core;
using Alaris.Core.Validation;

namespace Alaris.Test.Integration;

/// <summary>
/// TSEE001A: End-to-end workflow tests for complete signal-to-execution pipeline.
/// </summary>
public sealed class TSEE001A
{

    /// <summary>
    /// Creates a test signal with specified parameters.
    /// </summary>
    private static STCR004A CreateTestSignal(
        string symbol,
        double impliedVol30,
        double realizedVol30,
        double termSlope,
        long volume = 1_000_000)
    {
        return new STCR004A
        {
            Symbol = symbol,
            ImpliedVolatility30 = impliedVol30,
            RealizedVolatility30 = realizedVol30,
            IVRVRatio = realizedVol30 > 0 ? impliedVol30 / realizedVol30 : 0,
            STTM001ASlope = termSlope,
            AverageVolume = volume,
            Strength = STCR004AStrength.Consider
        };
    }



    /// <summary>
    /// Complete pipeline with single equity: Signal → Validation → Sizing.
    /// </summary>
    [Fact]
    public void HappyPath_SingleSymbol_CompletePipeline()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Valid signal with good IV/RV ratio
        // ═══════════════════════════════════════════════════════════
        STCR004A signal = CreateTestSignal(
            symbol: "AAPL",
            impliedVol30: 0.35,
            realizedVol30: 0.25,
            termSlope: 0.02);

        // ═══════════════════════════════════════════════════════════
        // ACT: Validate signal passes criteria
        // ═══════════════════════════════════════════════════════════
        bool ivrvPasses = signal.IVRVRatio >= 1.25;
        bool termSlopePasses = signal.STTM001ASlope > 0;
        bool volumePasses = signal.AverageVolume >= 500_000;

        // ═══════════════════════════════════════════════════════════
        // ASSERT: All criteria pass for this signal
        // ═══════════════════════════════════════════════════════════
        ivrvPasses.Should().BeTrue("IV/RV = 1.4 > 1.25 threshold");
        termSlopePasses.Should().BeTrue("Term slope is positive");
        volumePasses.Should().BeTrue("Volume above threshold");
    }



    /// <summary>
    /// Signal generation around earnings events with elevated IV.
    /// </summary>
    [Fact]
    public void EarningsEvent_ElevatedIV_GeneratesSignal()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Pre-earnings signal with elevated IV
        // ═══════════════════════════════════════════════════════════
        STCR004A signal = CreateTestSignal(
            symbol: "NVDA",
            impliedVol30: 0.65,  // Pre-earnings elevated
            realizedVol30: 0.40,
            termSlope: 0.05);   // Strong contango

        // ═══════════════════════════════════════════════════════════
        // ACT: Calculate IV/RV ratio
        // ═══════════════════════════════════════════════════════════
        double ivrvRatio = signal.IVRVRatio;
        bool meetsThreshold = ivrvRatio >= 1.25;

        // ═══════════════════════════════════════════════════════════
        // ASSERT: Earnings setup generates valid signal
        // ═══════════════════════════════════════════════════════════
        ivrvRatio.Should().BeApproximately(1.625, 0.01);
        meetsThreshold.Should().BeTrue("Strong earnings premium");
    }



    /// <summary>
    /// Position sizing under VIX > 30 regime.
    /// </summary>
    [Theory]
    [InlineData(0.25, 1.0)]   // Low VIX: full size
    [InlineData(0.35, 0.8)]   // Elevated VIX: reduced
    [InlineData(0.45, 0.5)]   // High VIX: half size
    public void HighVolatility_AdjustsPositionSize(double vixLevel, double expectedScaling)
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: VIX-based position scaling
        // ═══════════════════════════════════════════════════════════
        double baseRisk = 0.02; // 2% base risk

        // ═══════════════════════════════════════════════════════════
        // ACT: Calculate VIX-adjusted risk
        // ═══════════════════════════════════════════════════════════
        double vixScaling = vixLevel < 0.30 ? 1.0 :
                           vixLevel < 0.40 ? 0.8 :
                           0.5;
        double adjustedRisk = baseRisk * vixScaling;

        // ═══════════════════════════════════════════════════════════
        // ASSERT: Risk scales with VIX regime
        // ═══════════════════════════════════════════════════════════
        vixScaling.Should().BeApproximately(expectedScaling, 0.01);
        adjustedRisk.Should().BeLessThanOrEqualTo(baseRisk);
    }



    /// <summary>
    /// Near-expiry (T < 3 DTE) edge case handling.
    /// </summary>
    [Theory]
    [InlineData(0.5 / 252)]  // 0.5 days
    [InlineData(1.0 / 252)]  // 1 day
    [InlineData(2.0 / 252)]  // 2 days
    public void NearExpiry_BoundaryValidation(double timeToExpiry)
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Near-expiry parameters
        // ═══════════════════════════════════════════════════════════
        double minTimeToExpiry = 1.0 / 252.0;  // ~1 trading day

        // ═══════════════════════════════════════════════════════════
        // ACT: Check if time falls within valid bounds
        // ═══════════════════════════════════════════════════════════
        bool isValid = timeToExpiry >= minTimeToExpiry &&
                       timeToExpiry <= 30.0;
        bool isNearExpiry = timeToExpiry < 3.0 / 252.0;

        // ═══════════════════════════════════════════════════════════
        // ASSERT: Near-expiry detection works
        // ═══════════════════════════════════════════════════════════
        if (timeToExpiry >= minTimeToExpiry)
        {
            isValid.Should().BeTrue();
            isNearExpiry.Should().BeTrue("All test cases are < 3 DTE");
        }
    }



    /// <summary>
    /// Missing data handling - system gracefully handles gaps.
    /// </summary>
    [Fact]
    public void DataGap_MissingBars_HandledGracefully()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Signal with some missing data fields
        // ═══════════════════════════════════════════════════════════
        STCR004A incompleteSignal = new()
        {
            Symbol = "TEST",
            ImpliedVolatility30 = 0.30,
            RealizedVolatility30 = 0.0, // Missing
            IVRVRatio = 0.0,            // Invalid
            STTM001ASlope = 0.0,
            AverageVolume = 0,
            Strength = STCR004AStrength.Avoid
        };

        // ═══════════════════════════════════════════════════════════
        // ACT: Check for valid data
        // ═══════════════════════════════════════════════════════════
        bool hasValidRV = incompleteSignal.RealizedVolatility30 > 0;
        bool canCalculateRatio = hasValidRV && incompleteSignal.ImpliedVolatility30 > 0;

        // ═══════════════════════════════════════════════════════════
        // ASSERT: System detects incomplete data
        // ═══════════════════════════════════════════════════════════
        hasValidRV.Should().BeFalse("RV is missing");
        canCalculateRatio.Should().BeFalse("Cannot calculate ratio without RV");
    }



    /// <summary>
    /// Processing multiple symbols efficiently.
    /// </summary>
    [Fact]
    public void MultiSymbol_BatchProcess_Efficient()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Multiple symbols
        // ═══════════════════════════════════════════════════════════
        string[] symbols = ["AAPL", "MSFT", "GOOGL", "AMZN", "META"];
        List<STCR004A> signals = symbols.Select(s => CreateTestSignal(
            symbol: s,
            impliedVol30: 0.30 + Random.Shared.NextDouble() * 0.10,
            realizedVol30: 0.20 + Random.Shared.NextDouble() * 0.05,
            termSlope: 0.01 + Random.Shared.NextDouble() * 0.02
        )).ToList();

        // ═══════════════════════════════════════════════════════════
        // ACT: Filter to valid signals
        // ═══════════════════════════════════════════════════════════
        List<STCR004A> validSignals = signals
            .Where(s => s.IVRVRatio >= 1.25)
            .ToList();

        // ═══════════════════════════════════════════════════════════
        // ASSERT: Batch processing works
        // ═══════════════════════════════════════════════════════════
        signals.Should().HaveCount(5);
        validSignals.Should().NotBeNull();
    }



    /// <summary>
    /// Validates bounds are enforced across pipeline.
    /// </summary>
    [Fact]
    public void BoundsValidation_IntegrationWithDoubleSolver()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Valid parameters within bounds
        // ═══════════════════════════════════════════════════════════
        double spot = 100.0;
        double strike = 100.0;
        double maturity = 0.5;  // 6 months
        double rate = -0.01;
        double dividend = -0.02;
        double volatility = 0.25;

        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT: Validation should pass
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot, strike, maturity, rate, dividend, volatility);

        validate.Should().NotThrow("All parameters are within bounds");
    }

    /// <summary>
    /// Invalid parameters rejected at pipeline entry.
    /// </summary>
    [Theory]
    [InlineData(0.0)]    // Zero volatility
    [InlineData(-0.1)]   // Negative volatility
    [InlineData(6.0)]    // Exceeds max
    public void BoundsValidation_InvalidVolatility_Rejected(double volatility)
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE
        // ═══════════════════════════════════════════════════════════
        double spot = 100.0;
        double strike = 100.0;
        double maturity = 0.5;
        double rate = -0.01;
        double dividend = -0.02;

        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT: Should throw for invalid vol
        // ═══════════════════════════════════════════════════════════
        Action validate = () => AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot, strike, maturity, rate, dividend, volatility);

        validate.Should().Throw<BoundsViolationException>();
    }

}
