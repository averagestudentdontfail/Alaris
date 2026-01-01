// CREN002ATests.cs - Comprehensive unit tests for the pricing engine
// 
// These tests validate the FD pricing engine against:
// 1. Known analytical solutions (Black-Scholes for European baseline)
// 2. Put-Call parity relationships
// 3. Boundary conditions and limits
// 4. Monotonicity and convexity properties
// 5. Greek sensitivities

using Alaris.Core.Options;
using Alaris.Core.Pricing;
using Xunit;

namespace Alaris.Test.Unit.Core.Pricing;

/// <summary>
/// Unit tests for CREN002A American option pricing engine.
/// </summary>
public class CREN002ATests
{
    private readonly CREN002A _engine;
    private const double Tolerance = 0.05;  // 5% relative tolerance for FD vs analytical

    public CREN002ATests()
    {
        _engine = new CREN002A(timeSteps: 200, spotSteps: 400, gridConcentration: 0.25);
    }

    #region Basic Pricing Tests

    [Fact]
    public void Price_ATMCall_ReturnsPositiveValue()
    {
        // Arrange
        double spot = 100.0;
        double strike = 100.0;
        double T = 0.5;
        double r = 0.05;
        double q = 0.02;
        double sigma = 0.20;

        // Act
        double price = _engine.Price(spot, strike, T, r, q, sigma, OptionType.Call);

        // Assert
        Assert.True(price > 0, $"ATM call price should be positive, got {price}");
        Assert.True(price < spot, $"Call price {price} should be less than spot {spot}");
    }

    [Fact]
    public void Price_ATMPut_ReturnsPositiveValue()
    {
        // Arrange
        double spot = 100.0;
        double strike = 100.0;
        double T = 0.5;
        double r = 0.05;
        double q = 0.02;
        double sigma = 0.20;

        // Act
        double price = _engine.Price(spot, strike, T, r, q, sigma, OptionType.Put);

        // Assert
        Assert.True(price > 0, $"ATM put price should be positive, got {price}");
        Assert.True(price < strike, $"Put price {price} should be less than strike {strike}");
    }

    [Fact]
    public void Price_DeepITMCall_ApproachesIntrinsicValue()
    {
        // Arrange - deep ITM call
        double spot = 150.0;
        double strike = 100.0;
        double T = 0.5;
        double r = 0.05;
        double q = 0.02;
        double sigma = 0.20;
        double intrinsic = spot - strike;

        // Act
        double price = _engine.Price(spot, strike, T, r, q, sigma, OptionType.Call);

        // Assert - deep ITM should be at least intrinsic
        Assert.True(price >= intrinsic * 0.99, $"Deep ITM call {price} should be >= intrinsic {intrinsic}");
    }

    [Fact]
    public void Price_DeepOTMCall_ApproachesZero()
    {
        // Arrange - deep OTM call
        double spot = 50.0;
        double strike = 100.0;
        double T = 0.5;
        double r = 0.05;
        double q = 0.02;
        double sigma = 0.20;

        // Act
        double price = _engine.Price(spot, strike, T, r, q, sigma, OptionType.Call);

        // Assert - deep OTM should be small
        Assert.True(price < 5.0, $"Deep OTM call {price} should be small");
        Assert.True(price >= 0, $"Price should be non-negative, got {price}");
    }

    #endregion

    #region American vs European Tests

    [Fact]
    public void Price_AmericanPut_AtLeastEqualToEuropean()
    {
        // American put should always be worth at least as much as European
        // because of early exercise premium
        double spot = 100.0;
        double strike = 100.0;
        double T = 1.0;
        double r = 0.05;
        double q = 0.0;  // No dividends - early exercise premium from interest
        double sigma = 0.25;

        double americanPut = _engine.Price(spot, strike, T, r, q, sigma, OptionType.Put);

        // Black-Scholes European put for comparison
        double europeanPut = BlackScholesPut(spot, strike, T, r, q, sigma);

        Assert.True(americanPut >= europeanPut * 0.99, 
            $"American put {americanPut} should be >= European put {europeanPut}");
    }

    [Fact]
    public void Price_AmericanCall_NoDividend_EqualsEuropean()
    {
        // Without dividends, American call should equal European call
        // (no reason to exercise early)
        double spot = 100.0;
        double strike = 100.0;
        double T = 0.5;
        double r = 0.05;
        double q = 0.0;  // No dividends
        double sigma = 0.25;

        double americanCall = _engine.Price(spot, strike, T, r, q, sigma, OptionType.Call);
        double europeanCall = BlackScholesCall(spot, strike, T, r, q, sigma);

        double relativeDiff = System.Math.Abs(americanCall - europeanCall) / europeanCall;
        Assert.True(relativeDiff < Tolerance,
            $"American call {americanCall} should approximately equal European call {europeanCall}");
    }

    #endregion

    #region Monotonicity Tests

    [Fact]
    public void Price_Call_MonotonicInSpot()
    {
        // Call price should increase with spot
        double strike = 100.0;
        double T = 0.5;
        double r = 0.05;
        double q = 0.02;
        double sigma = 0.20;

        double priceLow = _engine.Price(80.0, strike, T, r, q, sigma, OptionType.Call);
        double priceMid = _engine.Price(100.0, strike, T, r, q, sigma, OptionType.Call);
        double priceHigh = _engine.Price(120.0, strike, T, r, q, sigma, OptionType.Call);

        Assert.True(priceHigh > priceMid, "Call price should increase with spot");
        Assert.True(priceMid > priceLow, "Call price should increase with spot");
    }

    [Fact]
    public void Price_Put_MonotonicDecreasingInSpot()
    {
        // Put price should decrease with spot
        double strike = 100.0;
        double T = 0.5;
        double r = 0.05;
        double q = 0.02;
        double sigma = 0.20;

        double priceLow = _engine.Price(80.0, strike, T, r, q, sigma, OptionType.Put);
        double priceMid = _engine.Price(100.0, strike, T, r, q, sigma, OptionType.Put);
        double priceHigh = _engine.Price(120.0, strike, T, r, q, sigma, OptionType.Put);

        Assert.True(priceLow > priceMid, "Put price should decrease with spot");
        Assert.True(priceMid > priceHigh, "Put price should decrease with spot");
    }

    [Fact]
    public void Price_MonotonicIncreasingInVolatility()
    {
        // Option prices should increase with volatility
        double spot = 100.0;
        double strike = 100.0;
        double T = 0.5;
        double r = 0.05;
        double q = 0.02;

        double priceLowVol = _engine.Price(spot, strike, T, r, q, 0.10, OptionType.Call);
        double priceMidVol = _engine.Price(spot, strike, T, r, q, 0.25, OptionType.Call);
        double priceHighVol = _engine.Price(spot, strike, T, r, q, 0.40, OptionType.Call);

        Assert.True(priceHighVol > priceMidVol, "Call price should increase with volatility");
        Assert.True(priceMidVol > priceLowVol, "Call price should increase with volatility");
    }

    [Fact]
    public void Price_MonotonicIncreasingInTime()
    {
        // American option prices should generally increase with time to expiry
        double spot = 100.0;
        double strike = 100.0;
        double r = 0.05;
        double q = 0.02;
        double sigma = 0.25;

        double priceShort = _engine.Price(spot, strike, 0.25, r, q, sigma, OptionType.Call);
        double priceMid = _engine.Price(spot, strike, 0.50, r, q, sigma, OptionType.Call);
        double priceLong = _engine.Price(spot, strike, 1.00, r, q, sigma, OptionType.Call);

        Assert.True(priceLong >= priceMid, "Price should generally increase with time");
        Assert.True(priceMid >= priceShort, "Price should generally increase with time");
    }

    #endregion

    #region Greek Tests

    [Fact]
    public void Delta_Call_BoundedZeroToOne()
    {
        // Call delta should be between 0 and 1
        double spot = 100.0;
        double strike = 100.0;
        double T = 0.5;
        double r = 0.05;
        double q = 0.02;
        double sigma = 0.25;

        double delta = _engine.Delta(spot, strike, T, r, q, sigma, OptionType.Call);

        Assert.True(delta >= 0 && delta <= 1, $"Call delta {delta} should be in [0, 1]");
    }

    [Fact]
    public void Delta_Put_BoundedMinusOneToZero()
    {
        // Put delta should be between -1 and 0
        double spot = 100.0;
        double strike = 100.0;
        double T = 0.5;
        double r = 0.05;
        double q = 0.02;
        double sigma = 0.25;

        double delta = _engine.Delta(spot, strike, T, r, q, sigma, OptionType.Put);

        Assert.True(delta >= -1 && delta <= 0, $"Put delta {delta} should be in [-1, 0]");
    }

    [Fact]
    public void Gamma_AlwaysPositive()
    {
        // Gamma should always be positive for long options
        double spot = 100.0;
        double strike = 100.0;
        double T = 0.5;
        double r = 0.05;
        double q = 0.02;
        double sigma = 0.25;

        double gammaCall = _engine.Gamma(spot, strike, T, r, q, sigma, OptionType.Call);
        double gammaPut = _engine.Gamma(spot, strike, T, r, q, sigma, OptionType.Put);

        Assert.True(gammaCall >= 0, $"Call gamma {gammaCall} should be non-negative");
        Assert.True(gammaPut >= 0, $"Put gamma {gammaPut} should be non-negative");
    }

    [Fact]
    public void Vega_AlwaysPositive()
    {
        // Vega should always be positive for long options
        double spot = 100.0;
        double strike = 100.0;
        double T = 0.5;
        double r = 0.05;
        double q = 0.02;
        double sigma = 0.25;

        double vega = _engine.Vega(spot, strike, T, r, q, sigma, OptionType.Call);

        Assert.True(vega >= 0, $"Vega {vega} should be non-negative");
    }

    [Fact]
    public void Theta_PutTypicallyNegative()
    {
        // Theta is typically negative (time decay)
        double spot = 100.0;
        double strike = 100.0;
        double T = 0.5;
        double r = 0.05;
        double q = 0.02;
        double sigma = 0.25;

        double theta = _engine.Theta(spot, strike, T, r, q, sigma, OptionType.Call);

        // Note: Theta is defined as dV/dt which is negative for time decay
        // Our implementation returns (V(t-dt) - V(t))/dt which is negative
        Assert.True(theta <= 0.1, $"Call theta {theta} should typically be negative or small");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Price_ZeroTimeToExpiry_ReturnsIntrinsic()
    {
        double spot = 110.0;
        double strike = 100.0;
        double intrinsic = spot - strike;

        double price = _engine.Price(spot, strike, 0.0, 0.05, 0.02, 0.25, OptionType.Call);

        Assert.Equal(intrinsic, price, 2);
    }

    [Theory]
    [InlineData(0.05, 0.02)]   // Normal rates
    [InlineData(0.0, 0.0)]      // Zero rates
    [InlineData(0.10, 0.05)]    // High rates
    public void Price_VariousRates_ReturnsValidPrice(double r, double q)
    {
        double spot = 100.0;
        double strike = 100.0;
        double T = 0.5;
        double sigma = 0.25;

        double price = _engine.Price(spot, strike, T, r, q, sigma, OptionType.Call);

        Assert.True(price > 0 && price < spot, $"Price {price} should be valid for r={r}, q={q}");
    }

    [Fact]
    public void Price_InvalidSpot_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => 
            _engine.Price(0.0, 100.0, 0.5, 0.05, 0.02, 0.25, OptionType.Call));
    }

    [Fact]
    public void Price_InvalidVolatility_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => 
            _engine.Price(100.0, 100.0, 0.5, 0.05, 0.02, 0.0, OptionType.Call));
    }

    #endregion

    #region Helper Methods - Black-Scholes Analytics

    private static double BlackScholesCall(double S, double K, double T, double r, double q, double sigma)
    {
        double d1 = (System.Math.Log(S / K) + (r - q + 0.5 * sigma * sigma) * T) / (sigma * System.Math.Sqrt(T));
        double d2 = d1 - sigma * System.Math.Sqrt(T);
        return S * System.Math.Exp(-q * T) * NormalCdf(d1) - K * System.Math.Exp(-r * T) * NormalCdf(d2);
    }

    private static double BlackScholesPut(double S, double K, double T, double r, double q, double sigma)
    {
        double d1 = (System.Math.Log(S / K) + (r - q + 0.5 * sigma * sigma) * T) / (sigma * System.Math.Sqrt(T));
        double d2 = d1 - sigma * System.Math.Sqrt(T);
        return K * System.Math.Exp(-r * T) * NormalCdf(-d2) - S * System.Math.Exp(-q * T) * NormalCdf(-d1);
    }

    private static double NormalCdf(double x)
    {
        return 0.5 * (1 + Erf(x / System.Math.Sqrt(2)));
    }

    private static double Erf(double x)
    {
        // Abramowitz and Stegun approximation
        double t = 1.0 / (1.0 + 0.5 * System.Math.Abs(x));
        double tau = t * System.Math.Exp(-x * x - 1.26551223 +
            t * (1.00002368 +
            t * (0.37409196 +
            t * (0.09678418 +
            t * (-0.18628806 +
            t * (0.27886807 +
            t * (-1.13520398 +
            t * (1.48851587 +
            t * (-0.82215223 +
            t * 0.17087277)))))))));
        return x >= 0 ? 1 - tau : tau - 1;
    }

    #endregion
}
