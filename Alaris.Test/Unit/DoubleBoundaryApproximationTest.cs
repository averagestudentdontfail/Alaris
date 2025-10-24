using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for DoubleBoundaryApproximation (QD+ algorithm).
/// Tests boundary calculations and option valuation for negative rate regimes.
/// </summary>
public class DoubleBoundaryApproximationTests
{
    [Fact]
    public void DoubleBoundaryApproximation_CalculatesCallBoundary()
    {
        // Arrange: ATM call option with q < r < 0
        var approximation = new DoubleBoundaryApproximation(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,          // r = -1%
            dividendYield: -0.02, // q = -2%
            volatility: 0.20,
            isCall: true
        );

        // Act
        var result = approximation.CalculateBoundaries();

        // Assert
        result.UpperBoundary.Should().BeGreaterThan(100.0, 
            "upper boundary should be above strike for call options");
        result.LowerBoundary.Should().BeLessThan(result.UpperBoundary,
            "lower boundary should be below upper boundary");
        result.IsValid.Should().BeTrue();
        result.BoundariesCross.Should().BeFalse();
    }

    [Fact]
    public void DoubleBoundaryApproximation_CalculatesPutBoundary()
    {
        // Arrange: ATM put option with q < r < 0
        var approximation = new DoubleBoundaryApproximation(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,          // r = -1%
            dividendYield: -0.02, // q = -2%
            volatility: 0.20,
            isCall: false
        );

        // Act
        var result = approximation.CalculateBoundaries();

        // Assert
        result.LowerBoundary.Should().BeLessThan(100.0,
            "lower boundary should be below strike for put options");
        result.UpperBoundary.Should().BeGreaterThan(result.LowerBoundary,
            "upper boundary should be above lower boundary");
        result.IsValid.Should().BeTrue();
        result.BoundariesCross.Should().BeFalse();
    }

    [Fact]
    public void DoubleBoundaryApproximation_CalculatesPositiveValue()
    {
        // Arrange: ATM option with 1 year to maturity
        var approximation = new DoubleBoundaryApproximation(
            spot: 100.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: true
        );

        // Act
        double value = approximation.ApproximateValue();

        // Assert
        value.Should().BeGreaterThan(0.0,
            "ATM option with time value should have positive price");
        value.Should().BeLessThan(100.0,
            "option value should be less than spot price");
    }

    [Fact]
    public void DoubleBoundaryApproximation_HandlesDeepInTheMoney()
    {
        // Arrange: Deep ITM call (spot >> strike)
        var approximation = new DoubleBoundaryApproximation(
            spot: 150.0,
            strike: 100.0,
            maturity: 1.0,
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: true
        );

        // Act
        double value = approximation.ApproximateValue();

        // Assert
        value.Should().BeGreaterThan(50.0,
            "deep ITM option should have value close to intrinsic");
    }

    [Fact]
    public void DoubleBoundaryApproximation_ValidatesParameters()
    {
        // Act & Assert: Invalid spot price
        Action act1 = () => new DoubleBoundaryApproximation(
            -100.0, 100.0, 1.0, -0.01, -0.02, 0.20, true);
        act1.Should().Throw<System.ArgumentException>()
            .WithMessage("*spot*");

        // Act & Assert: Invalid strike
        Action act2 = () => new DoubleBoundaryApproximation(
            100.0, -100.0, 1.0, -0.01, -0.02, 0.20, true);
        act2.Should().Throw<System.ArgumentException>()
            .WithMessage("*strike*");

        // Act & Assert: Invalid maturity
        Action act3 = () => new DoubleBoundaryApproximation(
            100.0, 100.0, -1.0, -0.01, -0.02, 0.20, true);
        act3.Should().Throw<System.ArgumentException>()
            .WithMessage("*maturity*");

        // Act & Assert: Invalid volatility
        Action act4 = () => new DoubleBoundaryApproximation(
            100.0, 100.0, 1.0, -0.01, -0.02, -0.20, true);
        act4.Should().Throw<System.ArgumentException>()
            .WithMessage("*volatility*");
    }

    [Fact]
    public void DoubleBoundaryApproximation_MatchesHealyTable2()
    {
        // Arrange: Test case from Healy (2021) Table 2
        // Put option: K=100, T=10, σ=8%, r=-0.5%, q=-1%
        var approximation = new DoubleBoundaryApproximation(
            spot: 100.0,
            strike: 100.0,
            maturity: 10.0,
            rate: -0.005,
            dividendYield: -0.01,
            volatility: 0.08,
            isCall: false
        );

        // Act
        var result = approximation.CalculateBoundaries();

        // Assert: Should match Healy's QD+ results within tolerance
        // Expected: Upper ≈ 69.62, Lower ≈ 58.72
        result.UpperBoundary.Should().BeInRange(68.0, 72.0,
            "upper boundary should match Healy (2021) Table 2");
        result.LowerBoundary.Should().BeInRange(57.0, 61.0,
            "lower boundary should match Healy (2021) Table 2");
    }

    [Fact]
    public void DoubleBoundaryApproximation_ConsistentWithEuropean()
    {
        // Arrange: Very short maturity (should approach European)
        var approximation = new DoubleBoundaryApproximation(
            spot: 100.0,
            strike: 100.0,
            maturity: 0.01,  // 0.01 years ≈ 3.65 days
            rate: -0.01,
            dividendYield: -0.02,
            volatility: 0.20,
            isCall: true
        );

        // Act
        double americanValue = approximation.ApproximateValue();

        // Calculate European value for comparison
        double europeanValue = CalculateEuropeanCall(
            100.0, 100.0, 0.01, -0.01, -0.02, 0.20);

        // Assert: American should be close to European for very short maturity
        americanValue.Should().BeApproximately(europeanValue, 0.5,
            "American value should approach European with short maturity");
    }

    private double CalculateEuropeanCall(double S, double K, double T, 
        double r, double q, double sigma)
    {
        double d1 = (System.Math.Log(S / K) + (r - q + 0.5 * sigma * sigma) * T) 
                    / (sigma * System.Math.Sqrt(T));
        double d2 = d1 - sigma * System.Math.Sqrt(T);
        
        double Nd1 = 0.5 * (1.0 + Erf(d1 / System.Math.Sqrt(2.0)));
        double Nd2 = 0.5 * (1.0 + Erf(d2 / System.Math.Sqrt(2.0)));
        
        return S * System.Math.Exp(-q * T) * Nd1 
             - K * System.Math.Exp(-r * T) * Nd2;
    }

    private double Erf(double x)
    {
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;
        
        int sign = x < 0 ? -1 : 1;
        x = System.Math.Abs(x);
        
        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t 
                   * System.Math.Exp(-x * x);
        
        return sign * y;
    }
}