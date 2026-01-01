// CREN003ATests.cs - First-principles tests for unified pricing engine
// Component ID: CREN003ATests
//
// Tests key mathematical invariants:
// - American ≥ European (early exercise premium is non-negative)
// - Intrinsic floor (American ≥ max(0, payoff))
// - Greek bounds (Delta ∈ [-1,1], Gamma ≥ 0, Vega ≥ 0)
// - Regime classification accuracy

using Alaris.Core.Math;
using Alaris.Core.Options;
using Alaris.Core.Pricing;
using Xunit;

namespace Alaris.Test.Unit.Core.Pricing;

/// <summary>
/// First-principles tests for unified American option pricing engine.
/// </summary>
public class CREN003ATests
{
    private readonly CREN003A _engine;
    private const double Tolerance = 1e-6;

    public CREN003ATests()
    {
        _engine = new CREN003A(100, 200); // 100 time steps, 200 spot steps
    }

    #region American ≥ European Invariant

    [Theory]
    [InlineData(100, 100, 0.25, 0.30, 0.05, 0.02)]  // ATM
    [InlineData(100, 90, 0.5, 0.25, 0.03, 0.01)]    // ITM call / OTM put
    [InlineData(100, 110, 0.5, 0.25, 0.03, 0.01)]   // OTM call / ITM put
    [InlineData(120, 100, 0.25, 0.35, 0.04, 0.015)] // Deep ITM call
    [InlineData(80, 100, 0.25, 0.35, 0.04, 0.015)]  // Deep ITM put
    public void Price_AmericanGreaterThanOrEqualEuropean(
        double spot, double strike, double tau, double sigma, double r, double q)
    {
        // American call
        double americanCall = _engine.Price(spot, strike, tau, r, q, sigma, OptionType.Call);
        double europeanCall = CRMF001A.BSPrice(spot, strike, tau, sigma, r, q, isCall: true);
        Assert.True(americanCall >= europeanCall - Tolerance, 
            $"American call {americanCall:F6} should be >= European {europeanCall:F6}");

        // American put
        double americanPut = _engine.Price(spot, strike, tau, r, q, sigma, OptionType.Put);
        double europeanPut = CRMF001A.BSPrice(spot, strike, tau, sigma, r, q, isCall: false);
        Assert.True(americanPut >= europeanPut - Tolerance, 
            $"American put {americanPut:F6} should be >= European {europeanPut:F6}");
    }

    #endregion

    #region Intrinsic Value Floor

    [Theory]
    [InlineData(150, 100, 0.25, 0.30, 0.05, 0.02)]  // Deep ITM call
    [InlineData(50, 100, 0.25, 0.30, 0.05, 0.02)]   // Deep ITM put
    [InlineData(105, 100, 0.1, 0.20, 0.03, 0.01)]   // Slightly ITM call
    [InlineData(95, 100, 0.1, 0.20, 0.03, 0.01)]    // Slightly ITM put
    public void Price_AtLeastIntrinsicValue(
        double spot, double strike, double tau, double sigma, double r, double q)
    {
        // Call intrinsic = max(0, S - K)
        double callIntrinsic = System.Math.Max(0, spot - strike);
        double americanCall = _engine.Price(spot, strike, tau, r, q, sigma, OptionType.Call);
        Assert.True(americanCall >= callIntrinsic - Tolerance, 
            $"American call {americanCall:F6} should be >= intrinsic {callIntrinsic:F6}");

        // Put intrinsic = max(0, K - S)
        double putIntrinsic = System.Math.Max(0, strike - spot);
        double americanPut = _engine.Price(spot, strike, tau, r, q, sigma, OptionType.Put);
        Assert.True(americanPut >= putIntrinsic - Tolerance, 
            $"American put {americanPut:F6} should be >= intrinsic {putIntrinsic:F6}");
    }

    #endregion

    #region Greek Bounds

    [Theory]
    [InlineData(100, 100, 0.25, 0.30, 0.05, 0.02)]  // ATM
    [InlineData(100, 90, 0.5, 0.25, 0.03, 0.01)]   // ITM call
    [InlineData(100, 110, 0.5, 0.25, 0.03, 0.01)]  // OTM call
    public void Delta_InValidRange(
        double spot, double strike, double tau, double sigma, double r, double q)
    {
        double callDelta = _engine.Delta(spot, strike, tau, r, q, sigma, OptionType.Call);
        double putDelta = _engine.Delta(spot, strike, tau, r, q, sigma, OptionType.Put);

        // Call delta ∈ [0, 1]
        Assert.True(callDelta >= -Tolerance && callDelta <= 1.0 + Tolerance,
            $"Call delta {callDelta:F6} should be in [0, 1]");

        // Put delta ∈ [-1, 0]
        Assert.True(putDelta >= -1.0 - Tolerance && putDelta <= Tolerance,
            $"Put delta {putDelta:F6} should be in [-1, 0]");
    }

    [Theory]
    [InlineData(100, 100, 0.25, 0.30, 0.05, 0.02)]
    [InlineData(100, 90, 0.5, 0.25, 0.03, 0.01)]
    [InlineData(100, 110, 0.5, 0.25, 0.03, 0.01)]
    public void Gamma_NonNegative(
        double spot, double strike, double tau, double sigma, double r, double q)
    {
        double callGamma = _engine.Gamma(spot, strike, tau, r, q, sigma, OptionType.Call);
        double putGamma = _engine.Gamma(spot, strike, tau, r, q, sigma, OptionType.Put);

        // Gamma ≥ 0 (convexity)
        Assert.True(callGamma >= -Tolerance, $"Call gamma {callGamma:F6} should be >= 0");
        Assert.True(putGamma >= -Tolerance, $"Put gamma {putGamma:F6} should be >= 0");
    }

    [Theory]
    [InlineData(100, 100, 0.25, 0.30, 0.05, 0.02)]
    [InlineData(100, 90, 0.5, 0.25, 0.03, 0.01)]
    [InlineData(100, 110, 0.5, 0.25, 0.03, 0.01)]
    public void Vega_NonNegative(
        double spot, double strike, double tau, double sigma, double r, double q)
    {
        double callVega = _engine.Vega(spot, strike, tau, r, q, sigma, OptionType.Call);
        double putVega = _engine.Vega(spot, strike, tau, r, q, sigma, OptionType.Put);

        // Vega ≥ 0 (more volatility = more value)
        Assert.True(callVega >= -Tolerance, $"Call vega {callVega:F6} should be >= 0");
        Assert.True(putVega >= -Tolerance, $"Put vega {putVega:F6} should be >= 0");
    }

    #endregion

    #region Regime Classification

    [Theory]
    [InlineData(0.05, 0.02, false)]  // Standard regime (r > 0)
    [InlineData(0.00, 0.02, false)]  // Standard regime (r = 0)
    [InlineData(-0.02, -0.01, false)] // Put: r < q < 0, Standard
    public void Classify_StandardRegime(double r, double q, bool isCall)
    {
        RateRegime regime = CRRE001A.Classify(r, q, isCall);
        Assert.Equal(RateRegime.Standard, regime);
    }

    [Theory]
    [InlineData(-0.01, -0.02, false)]  // Put: q < r < 0, DoubleBoundary
    [InlineData(0.02, 0.05, true)]     // Call: 0 < r < q, DoubleBoundary
    public void Classify_DoubleBoundaryRegime(double r, double q, bool isCall)
    {
        RateRegime regime = CRRE001A.Classify(r, q, isCall);
        Assert.Equal(RateRegime.DoubleBoundary, regime);
    }

    #endregion

    #region PriceWithDetails

    [Fact]
    public void PriceWithDetails_IncludesAllGreeks()
    {
        UnifiedPricingResult result = _engine.PriceWithDetails(
            spot: 100, strike: 100, timeToExpiry: 0.25, volatility: 0.30,
            riskFreeRate: 0.05, dividendYield: 0.02, optionType: OptionType.Call);

        // All Greeks should be computed
        Assert.True(result.Price > 0, "Price should be positive");
        Assert.True(result.Delta is >= 0 and <= 1, "Delta should be in [0,1]");
        Assert.True(result.Gamma >= 0, "Gamma should be non-negative");
        Assert.True(result.Vega >= 0, "Vega should be non-negative");
        Assert.NotEqual(0, result.Theta); // Theta can be positive or negative
        Assert.NotEqual(0, result.Rho); // Rho can be positive or negative

        // Metadata should be populated
        Assert.Equal(RateRegime.Standard, result.Regime);
        Assert.Equal(PricingMethod.FiniteDifference, result.Method);
        Assert.True(result.EarlyExercisePremium >= 0, "Early exercise premium should be non-negative");
    }

    [Fact]
    public void PriceWithDetails_NegativeRateRegime_ClassifiesCorrectly()
    {
        UnifiedPricingResult result = _engine.PriceWithDetails(
            spot: 100, strike: 100, timeToExpiry: 0.25, volatility: 0.30,
            riskFreeRate: -0.01, dividendYield: -0.02, optionType: OptionType.Put);

        // q < r < 0 for put is DoubleBoundary
        Assert.Equal(RateRegime.DoubleBoundary, result.Regime);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Price_NearExpiryOption_StillValid()
    {
        double tau = 1.0 / 252.0; // 1 trading day
        double americanCall = _engine.Price(100, 100, tau, 0.05, 0.02, 0.30, OptionType.Call);
        double americanPut = _engine.Price(100, 100, tau, 0.05, 0.02, 0.30, OptionType.Put);

        Assert.True(americanCall > 0, "Near-expiry call should have positive value");
        Assert.True(americanPut > 0, "Near-expiry put should have positive value");
    }

    [Fact]
    public void Price_DeepITM_ConvergesToIntrinsic()
    {
        double spot = 200;
        double strike = 100;
        double tau = 0.01; // Very short time to expiry
        
        double americanCall = _engine.Price(spot, strike, tau, 0.05, 0.02, 0.30, OptionType.Call);
        double intrinsic = spot - strike;

        // Deep ITM near expiry should be very close to intrinsic
        Assert.True(System.Math.Abs(americanCall - intrinsic) < 1.0,
            $"Deep ITM call {americanCall:F2} should be close to intrinsic {intrinsic:F2}");
    }

    #endregion
}
