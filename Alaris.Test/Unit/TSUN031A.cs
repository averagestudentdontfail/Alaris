// =============================================================================
// TSUN031A.cs - Unit Tests for STBR003A (Cached QuantLib Infrastructure)
// Component ID: TSUN031A
// =============================================================================
//
// Coverage:
// - STBR003A: QuantLib infrastructure cache for Greek calculations
//
// Test Categories:
// 1. Pricing accuracy vs uncached implementation
// 2. Greek calculation correctness
// 3. Cache invalidation on parameter changes
// 4. Disposal and resource cleanup
//
// References:
// - Alaris.Governance/Coding.md Rule 5 (Zero-Allocation Hot Paths)
// - Alaris.Governance/Coding.md Rule 16 (Deterministic Cleanup)
// =============================================================================

using Xunit;
using FluentAssertions;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Model;

namespace Alaris.Test.Unit;

/// <summary>
/// TSUN031A: Unit tests for STBR003A (Cached QuantLib Infrastructure).
/// Validates pricing accuracy and Greek calculation correctness.
/// </summary>
public class TSUN031A : IDisposable
{
    private readonly STBR003A _sut;
    private bool _disposed;
    
    public TSUN031A()
    {
        _sut = new STBR003A();
    }
    
    #region Pricing Tests
    
    /// <summary>
    /// Verifies that cached infrastructure produces non-zero prices for standard ATM options.
    /// </summary>
    [Fact]
    public void Price_StandardATMOption_ReturnsPositiveValue()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Set up ATM option parameters
        // ═══════════════════════════════════════════════════════════
        var parameters = CreateStandardParameters();
        
        // ═══════════════════════════════════════════════════════════
        // ACT: Price the option
        // ═══════════════════════════════════════════════════════════
        double price = _sut.Price(parameters);
        
        // ═══════════════════════════════════════════════════════════
        // ASSERT: Option price must be positive and reasonable
        // ═══════════════════════════════════════════════════════════
        price.Should().BeGreaterThan(0.0, "ATM option should have positive value");
        price.Should().BeLessThan(parameters.Strike, "put price cannot exceed strike");
    }
    
    /// <summary>
    /// Verifies that repricing with same parameters returns consistent results.
    /// </summary>
    [Fact]
    public void Price_SameParameters_ReturnsConsistentResult()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Standard parameters
        // ═══════════════════════════════════════════════════════════
        var parameters = CreateStandardParameters();
        
        // ═══════════════════════════════════════════════════════════
        // ACT: Price multiple times with same parameters
        // ═══════════════════════════════════════════════════════════
        double price1 = _sut.Price(parameters);
        double price2 = _sut.Price(parameters);
        double price3 = _sut.Price(parameters);
        
        // ═══════════════════════════════════════════════════════════
        // ASSERT: All prices should be identical
        // ═══════════════════════════════════════════════════════════
        price2.Should().BeApproximately(price1, 1e-10, "cached pricing should be deterministic");
        price3.Should().BeApproximately(price1, 1e-10, "cached pricing should be deterministic");
    }
    
    /// <summary>
    /// Verifies that changing spot price updates the price correctly.
    /// </summary>
    [Fact]
    public void Price_SpotPriceIncrease_UpdatesPriceCorrectly()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Standard put option
        // ═══════════════════════════════════════════════════════════
        var parameters = CreateStandardParameters();
        double originalPrice = _sut.Price(parameters);
        
        // ═══════════════════════════════════════════════════════════
        // ACT: Increase spot price (put should decrease)
        // ═══════════════════════════════════════════════════════════
        parameters.UnderlyingPrice = 110.0; // Higher spot
        double newPrice = _sut.Price(parameters);
        
        // ═══════════════════════════════════════════════════════════
        // ASSERT: Put value decreases as spot increases (delta < 0)
        // ═══════════════════════════════════════════════════════════
        newPrice.Should().BeLessThan(originalPrice, 
            "put option value should decrease as spot increases");
    }
    
    #endregion
    
    #region Greek Tests
    
    /// <summary>
    /// Verifies that Delta for ATM put is approximately -0.5.
    /// </summary>
    [Fact]
    public void CalculateDelta_ATMPut_ApproximatelyNegativeHalf()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: ATM option
        // ═══════════════════════════════════════════════════════════
        var parameters = CreateStandardParameters();
        
        // ═══════════════════════════════════════════════════════════
        // ACT: Calculate delta
        // ═══════════════════════════════════════════════════════════
        double delta = _sut.CalculateDelta(parameters);
        
        // ═══════════════════════════════════════════════════════════
        // ASSERT: ATM put delta ≈ -0.5 (theoretical)
        // ═══════════════════════════════════════════════════════════
        delta.Should().BeInRange(-0.7, -0.3, 
            "ATM put delta should be approximately -0.5");
    }
    
    /// <summary>
    /// Verifies that Gamma is positive for all options.
    /// </summary>
    [Fact]
    public void CalculateGamma_StandardPut_IsPositive()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Standard option
        // ═══════════════════════════════════════════════════════════
        var parameters = CreateStandardParameters();
        
        // ═══════════════════════════════════════════════════════════
        // ACT: Calculate gamma
        // ═══════════════════════════════════════════════════════════
        double gamma = _sut.CalculateGamma(parameters);
        
        // ═══════════════════════════════════════════════════════════
        // ASSERT: Gamma must be positive (convexity)
        // ═══════════════════════════════════════════════════════════
        gamma.Should().BeGreaterThan(0.0, 
            "invariant: gamma ≥ 0 for all options (convexity)");
    }
    
    /// <summary>
    /// Verifies that Vega is positive for all options.
    /// </summary>
    [Fact]
    public void CalculateVega_StandardPut_IsPositive()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Standard option
        // ═══════════════════════════════════════════════════════════
        var parameters = CreateStandardParameters();
        
        // ═══════════════════════════════════════════════════════════
        // ACT: Calculate vega
        // ═══════════════════════════════════════════════════════════
        double vega = _sut.CalculateVega(parameters);
        
        // ═══════════════════════════════════════════════════════════
        // ASSERT: Vega must be positive (long options benefit from vol)
        // ═══════════════════════════════════════════════════════════
        vega.Should().BeGreaterThan(0.0, 
            "invariant: vega ≥ 0 for all options");
    }
    
    /// <summary>
    /// Verifies that Theta is negative for standard options (time decay).
    /// </summary>
    [Fact]
    public void CalculateTheta_StandardPut_IsNegative()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Standard option with sufficient time value
        // ═══════════════════════════════════════════════════════════
        var parameters = CreateStandardParameters();
        
        // ═══════════════════════════════════════════════════════════
        // ACT: Calculate theta
        // ═══════════════════════════════════════════════════════════
        double theta = _sut.CalculateTheta(parameters);
        
        // ═══════════════════════════════════════════════════════════
        // ASSERT: Theta is negative (time decay)
        // ═══════════════════════════════════════════════════════════
        theta.Should().BeLessThan(0.0, 
            "standard options experience time decay (theta < 0)");
    }
    
    /// <summary>
    /// Verifies that CalculateAllGreeks returns consistent values with individual calculations.
    /// </summary>
    [Fact]
    public void CalculateAllGreeks_StandardOption_MatchesIndividualCalculations()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Standard parameters and separate cache instance
        // ═══════════════════════════════════════════════════════════
        var parameters = CreateStandardParameters();
        using var separateCache = new STBR003A();
        
        // ═══════════════════════════════════════════════════════════
        // ACT: Calculate all Greeks and individual Greeks
        // ═══════════════════════════════════════════════════════════
        var allGreeks = _sut.CalculateAllGreeks(parameters);
        
        double individualDelta = separateCache.CalculateDelta(CreateStandardParameters());
        double individualGamma = separateCache.CalculateGamma(CreateStandardParameters());
        double individualVega = separateCache.CalculateVega(CreateStandardParameters());
        double individualTheta = separateCache.CalculateTheta(CreateStandardParameters());
        double individualRho = separateCache.CalculateRho(CreateStandardParameters());
        
        // ═══════════════════════════════════════════════════════════
        // ASSERT: Bulk calculation matches individual calculations
        // ═══════════════════════════════════════════════════════════
        allGreeks.Delta.Should().BeApproximately(individualDelta, 1e-6, "delta should match");
        allGreeks.Gamma.Should().BeApproximately(individualGamma, 1e-6, "gamma should match");
        allGreeks.Vega.Should().BeApproximately(individualVega, 1e-6, "vega should match");
        allGreeks.Theta.Should().BeApproximately(individualTheta, 1e-6, "theta should match");
        allGreeks.Rho.Should().BeApproximately(individualRho, 1e-6, "rho should match");
    }
    
    #endregion
    
    #region Guard Clause Tests
    
    /// <summary>
    /// Verifies that null parameters throw ArgumentNullException.
    /// </summary>
    [Fact]
    public void Price_NullParameters_ThrowsArgumentNullException()
    {
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT: Null should throw
        // ═══════════════════════════════════════════════════════════
        Action act = () => _sut.Price(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("parameters");
    }
    
    /// <summary>
    /// Verifies disposal prevents further pricing.
    /// </summary>
    [Fact]
    public void Price_AfterDispose_ThrowsObjectDisposedException()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Dispose the cache
        // ═══════════════════════════════════════════════════════════
        using var cache = new STBR003A();
        cache.Dispose();
        
        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT: Pricing after dispose should throw
        // ═══════════════════════════════════════════════════════════
        var parameters = CreateStandardParameters();
        Action act = () => cache.Price(parameters);
        act.Should().Throw<ObjectDisposedException>();
    }
    
    #endregion
    
    #region Helpers
    
    private static STDT003As CreateStandardParameters()
    {
        return new STDT003As
        {
            UnderlyingPrice = 100.0,
            Strike = 100.0,
            Expiry = new Date(31, Month.March, 2025),
            ImpliedVolatility = 0.20,
            RiskFreeRate = 0.05,
            DividendYield = 0.02,
            OptionType = Option.Type.Put,
            ValuationDate = new Date(25, Month.December, 2024)
        };
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }
        
        if (disposing)
        {
            _sut.Dispose();
        }
        
        _disposed = true;
    }
    
    #endregion
}
