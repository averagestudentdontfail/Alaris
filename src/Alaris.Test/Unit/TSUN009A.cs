using Xunit;
using FluentAssertions;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Model;
using Alaris.Strategy.Pricing;
using Alaris.Core.Time;
using Alaris.Core.Options;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for the STBR001A component.
/// Tests regime detection, pricing accuracy, and calendar spread calculations.
/// </summary>
public sealed class STBR001ATests : IDisposable
{
    private readonly STBR001A _engine;
    private readonly CRTM005A _valuationDate;
    private bool _disposed;

    public STBR001ATests()
    {
        _engine = new STBR001A();
        _valuationDate = new CRTM005A(15, CRTM005AMonth.January, 2024);
    }


    [Theory]
    [InlineData(0.05, 0.02, false, PricingRegime.PositiveRates)]  // Put, positive rates
    [InlineData(0.03, 0.01, true, PricingRegime.PositiveRates)]   // Call, positive rates
    [InlineData(0.00, 0.00, false, PricingRegime.PositiveRates)]  // Put, zero rates
    [InlineData(-0.005, -0.010, false, PricingRegime.DoubleBoundary)]  // Put, q < r < 0
    [InlineData(-0.010, -0.015, false, PricingRegime.DoubleBoundary)]  // Put, q < r < 0
    [InlineData(-0.005, -0.005, false, PricingRegime.NegativeRatesSingleBoundary)]  // Put, r < 0, q = r
    [InlineData(-0.005, 0.000, false, PricingRegime.NegativeRatesSingleBoundary)]   // Put, r < 0, q > r
    [InlineData(-0.005, -0.010, true, PricingRegime.NegativeRatesSingleBoundary)]   // Call, negative rates
    [InlineData(0.03, 0.05, true, PricingRegime.DoubleBoundary)]  // Call, 0 < r < q
    public void DetermineRegime_ReturnsCorrectRegime(double rate, double dividend, bool isCall, PricingRegime expected)
    {
        // Act
        PricingRegime regime = STBR001A.DetermineRegime(rate, dividend, isCall);

        // Assert
        regime.Should().Be(expected);
    }

    [Fact]
    public void DetermineRegime_PositiveRates_Standard()
    {
        // Arrange
        double r = 0.05;
        double q = 0.02;
        bool isCall = false; // Put option

        // Act
        PricingRegime regime = STBR001A.DetermineRegime(r, q, isCall);

        // Assert
        regime.Should().Be(PricingRegime.PositiveRates);
    }

    [Fact]
    public void DetermineRegime_NegativeRatesDoubleBoundary()
    {
        // Arrange: Healy (2021) parameters (put option)
        double r = -0.005;
        double q = -0.010;
        bool isCall = false; // Put option

        // Act
        PricingRegime regime = STBR001A.DetermineRegime(r, q, isCall);

        // Assert
        regime.Should().Be(PricingRegime.DoubleBoundary);
    }



    [Fact]
    public async Task PriceOption_PositiveRates_CallOption()
    {
        // Arrange
        STDT003A parameters = CreateStandardCallParameters();

        // Act
        OptionPricing result = await _engine.PriceOption(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Price.Should().BeGreaterThan(0);
        result.Delta.Should().BeInRange(0, 1); // Call delta is positive
        result.Gamma.Should().BeGreaterThan(0);
        result.Vega.Should().BeGreaterThan(0);
        result.Theta.Should().BeLessThan(0); // Time decay
        result.ImpliedVolatility.Should().Be(parameters.ImpliedVolatility);
    }

    [Fact]
    public async Task PriceOption_PositiveRates_PutOption()
    {
        // Arrange
        STDT003A parameters = CreateStandardPutParameters();

        // Act
        OptionPricing result = await _engine.PriceOption(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Price.Should().BeGreaterThan(0);
        result.Delta.Should().BeInRange(-1, 0); // Put delta is negative
        result.Gamma.Should().BeGreaterThan(0);
        result.Vega.Should().BeGreaterThan(0);
        result.Theta.Should().BeLessThan(0); // Time decay
    }

    [Fact]
    public async Task PriceOption_PositiveRates_ATM_HasHighestGamma()
    {
        // Arrange
        STDT003A parameters = CreateStandardCallParameters();
        parameters.Strike = parameters.UnderlyingPrice; // ATM

        // Act
        OptionPricing result = await _engine.PriceOption(parameters);

        // Assert
        result.Gamma.Should().BeGreaterThan(0.01); // ATM options have highest gamma
    }



    [Fact]
    public async Task PriceOption_NegativeRates_DoubleBoundary_Put()
    {
        // Arrange: Healy (2021) benchmark parameters
        STDT003A parameters = CreateHealyPutParameters();

        // Act
        OptionPricing result = await _engine.PriceOption(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Price.Should().BeGreaterThan(0);
        result.Delta.Should().BeInRange(-1, 0); // Put delta
        result.Gamma.Should().BeGreaterThan(0);
        result.Vega.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PriceOption_NegativeRates_DoubleBoundary_Call()
    {
        // Arrange: Negative rates with double boundary
        STDT003A parameters = CreateHealyCallParameters();

        // Act
        OptionPricing result = await _engine.PriceOption(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Price.Should().BeGreaterThan(0);
        result.Delta.Should().BeInRange(0, 1); // Call delta
    }

    [Fact]
    public async Task PriceOption_NegativeRates_ConsistentAcrossRegimes()
    {
        // Arrange: Test at rate boundary (r = 0)
        STDT003A paramsAtBoundary = CreateStandardCallParameters();
        paramsAtBoundary.RiskFreeRate = 0.0001; // Slightly positive

        STDT003A paramsSlightlyNegative = CreateStandardCallParameters();
        paramsSlightlyNegative.RiskFreeRate = -0.0001;
        paramsSlightlyNegative.DividendYield = 0.02; // q > r, so single boundary

        // Act
        OptionPricing resultPositive = await _engine.PriceOption(paramsAtBoundary);
        OptionPricing resultNegative = await _engine.PriceOption(paramsSlightlyNegative);

        // Assert: Prices should be very close at regime boundary
        double priceDifference = System.Math.Abs(resultPositive.Price - resultNegative.Price);
        priceDifference.Should().BeLessThan(0.5); // Within $0.50
    }



    [Fact]
    public async Task PriceSTPR001A_PositiveRates_ValidSpread()
    {
        // Arrange
        STPR001AParameters parameters = CreateSTPR001AParameters();

        // Act
        STPR001APricing result = await _engine.PriceSTPR001A(parameters);

        // Assert
        result.Should().NotBeNull();
        result.SpreadCost.Should().BeGreaterThan(0); // Calendar spreads are debit spreads
        result.BackOption.Price.Should().BeGreaterThan(result.FrontOption.Price); // Back > Front
        result.SpreadVega.Should().BeGreaterThan(0); // Long vega
        result.MaxLoss.Should().Be(result.SpreadCost); // Max loss is debit paid
        result.MaxProfit.Should().BeGreaterThan(result.MaxLoss); // Profit potential exists
    }

    [Fact]
    public async Task PriceSTPR001A_PositiveRates_CorrectGreeks()
    {
        // Arrange
        STPR001AParameters parameters = CreateSTPR001AParameters();

        // Act
        STPR001APricing result = await _engine.PriceSTPR001A(parameters);

        // Assert
        result.SpreadDelta.Should().BeInRange(-0.2, 0.2); // Near-neutral delta
        result.SpreadGamma.Should().BeLessThan(0); // Negative gamma
        result.SpreadVega.Should().BeGreaterThan(0); // Positive vega
        result.SpreadTheta.Should().BeGreaterThan(0); // Positive theta (benefits from time decay)
    }

    [Fact]
    public async Task PriceSTPR001A_NegativeRates_ValidSpread()
    {
        // Arrange
        STPR001AParameters parameters = CreateSTPR001AParameters();
        parameters.RiskFreeRate = -0.005;
        parameters.DividendYield = -0.010; // Double boundary regime

        // Act
        STPR001APricing result = await _engine.PriceSTPR001A(parameters);

        // Assert
        result.Should().NotBeNull();
        result.SpreadCost.Should().BeGreaterThan(0);
        result.BackOption.Price.Should().BeGreaterThan(result.FrontOption.Price);
        result.SpreadVega.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PriceSTPR001A_Validation_Works()
    {
        // Arrange
        STPR001AParameters parameters = CreateSTPR001AParameters();

        // Act
        STPR001APricing result = await _engine.PriceSTPR001A(parameters);

        // Assert: Should not throw when validating
        result.Invoking(r => r.Validate()).Should().NotThrow();
    }



    [Fact]
    public async Task CalculateImpliedVolatility_ConvergesCorrectly()
    {
        // Arrange
        STDT003A parameters = CreateStandardCallParameters();
        double targetIV = 0.25;
        parameters.ImpliedVolatility = targetIV;

        // Price the option to get target price
        OptionPricing pricing = await _engine.PriceOption(parameters);
        double targetPrice = pricing.Price;

        // Remove IV from parameters
        parameters.ImpliedVolatility = 0.0;

        // Act
        double calculatedIV = await _engine.CalculateImpliedVolatility(targetPrice, parameters);

        // Assert
        calculatedIV.Should().BeApproximately(targetIV, 0.01); // Within 1% vol
    }

    [Fact]
    public async Task CalculateImpliedVolatility_InvalidPrice_Throws()
    {
        // Arrange
        STDT003A parameters = CreateStandardCallParameters();
        double invalidPrice = -1.0;

        // Act & Assert
        await _engine.Invoking(e => e.CalculateImpliedVolatility(invalidPrice, parameters))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CalculateImpliedVolatility_ZeroPrice_Throws()
    {
        // Arrange
        STDT003A parameters = CreateStandardCallParameters();
        double zeroPrice = 0.0;

        // Act & Assert
        await _engine.Invoking(e => e.CalculateImpliedVolatility(zeroPrice, parameters))
            .Should().ThrowAsync<ArgumentException>();
    }



    [Fact]
    public async Task PriceOption_NullParameters_Throws()
    {
        // Act & Assert
        await _engine.Invoking(e => e.PriceOption(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PriceOption_InvalidParameters_Throws()
    {
        // Arrange
        STDT003A parameters = CreateStandardCallParameters();
        parameters.UnderlyingPrice = -100; // Invalid

        // Act & Assert
        await _engine.Invoking(e => e.PriceOption(parameters))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PriceSTPR001A_NullParameters_Throws()
    {
        // Act & Assert
        await _engine.Invoking(e => e.PriceSTPR001A(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PriceSTPR001A_FrontAfterBack_Throws()
    {
        // Arrange
        STPR001AParameters parameters = CreateSTPR001AParameters();
        // Swap expiries (invalid)
        (parameters.FrontExpiry, parameters.BackExpiry) = (parameters.BackExpiry, parameters.FrontExpiry);

        // Act & Assert
        await _engine.Invoking(e => e.PriceSTPR001A(parameters))
            .Should().ThrowAsync<ArgumentException>();
    }



    private STDT003A CreateStandardCallParameters()
    {
        return new STDT003A
        {
            UnderlyingPrice = 100.0,
            Strike = 100.0,
            Expiry = _valuationDate.AddDays(30),
            ImpliedVolatility = 0.25,
            RiskFreeRate = 0.05,
            DividendYield = 0.02,
            OptionType = OptionType.Call,
            ValuationDate = _valuationDate
        };
    }

    private STDT003A CreateStandardPutParameters()
    {
        return new STDT003A
        {
            UnderlyingPrice = 100.0,
            Strike = 100.0,
            Expiry = _valuationDate.AddDays(30),
            ImpliedVolatility = 0.25,
            RiskFreeRate = 0.05,
            DividendYield = 0.02,
            OptionType = OptionType.Put,
            ValuationDate = _valuationDate
        };
    }

    private STDT003A CreateHealyPutParameters()
    {
        // Healy (2021) benchmark parameters
        return new STDT003A
        {
            UnderlyingPrice = 100.0,
            Strike = 100.0,
            Expiry = _valuationDate.AddDays(365), // 1 year
            ImpliedVolatility = 0.08,
            RiskFreeRate = -0.005,
            DividendYield = -0.010,
            OptionType = OptionType.Put,
            ValuationDate = _valuationDate
        };
    }

    private STDT003A CreateHealyCallParameters()
    {
        return new STDT003A
        {
            UnderlyingPrice = 100.0,
            Strike = 100.0,
            Expiry = _valuationDate.AddDays(365),
            ImpliedVolatility = 0.08,
            RiskFreeRate = -0.005,
            DividendYield = -0.010,
            OptionType = OptionType.Call,
            ValuationDate = _valuationDate
        };
    }

    private STPR001AParameters CreateSTPR001AParameters()
    {
        return new STPR001AParameters
        {
            UnderlyingPrice = 150.0,
            Strike = 150.0,
            FrontExpiry = _valuationDate.AddDays(30),
            BackExpiry = _valuationDate.AddDays(60),
            ImpliedVolatility = 0.30,
            RiskFreeRate = 0.05,
            DividendYield = 0.02,
            OptionType = OptionType.Call,
            ValuationDate = _valuationDate
        };
    }


    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _engine?.Dispose();
                // CRTM005A is a struct, no disposal needed
            }
            _disposed = true;
        }
    }
}
