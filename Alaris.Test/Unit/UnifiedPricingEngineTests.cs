using Xunit;
using FluentAssertions;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Model;
using Alaris.Strategy.Pricing;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for the UnifiedPricingEngine component.
/// Tests regime detection, pricing accuracy, and calendar spread calculations.
/// </summary>
public sealed class UnifiedPricingEngineTests : IDisposable
{
    private readonly UnifiedPricingEngine _engine;
    private readonly Date _valuationDate;

    public UnifiedPricingEngineTests()
    {
        _engine = new UnifiedPricingEngine();
        _valuationDate = new Date(15, Month.January, 2024);
        Settings.instance().setEvaluationDate(_valuationDate);
    }

    #region Regime Detection Tests

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
        var regime = UnifiedPricingEngine.DetermineRegime(rate, dividend, isCall);

        // Assert
        regime.Should().Be(expected);
    }

    [Fact]
    public void DetermineRegime_PositiveRates_Standard()
    {
        // Arrange
        var r = 0.05;
        var q = 0.02;
        var isCall = false; // Put option

        // Act
        var regime = UnifiedPricingEngine.DetermineRegime(r, q, isCall);

        // Assert
        regime.Should().Be(PricingRegime.PositiveRates);
    }

    [Fact]
    public void DetermineRegime_NegativeRatesDoubleBoundary()
    {
        // Arrange: Healy (2021) parameters (put option)
        var r = -0.005;
        var q = -0.010;
        var isCall = false; // Put option

        // Act
        var regime = UnifiedPricingEngine.DetermineRegime(r, q, isCall);

        // Assert
        regime.Should().Be(PricingRegime.DoubleBoundary);
    }

    #endregion

    #region Positive Rate Pricing Tests

    [Fact]
    public async Task PriceOption_PositiveRates_CallOption()
    {
        // Arrange
        var parameters = CreateStandardCallParameters();

        // Act
        var result = await _engine.PriceOption(parameters);

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
        var parameters = CreateStandardPutParameters();

        // Act
        var result = await _engine.PriceOption(parameters);

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
        var parameters = CreateStandardCallParameters();
        parameters.Strike = parameters.UnderlyingPrice; // ATM

        // Act
        var result = await _engine.PriceOption(parameters);

        // Assert
        result.Gamma.Should().BeGreaterThan(0.01); // ATM options have highest gamma
    }

    #endregion

    #region Negative Rate Pricing Tests

    [Fact]
    public async Task PriceOption_NegativeRates_DoubleBoundary_Put()
    {
        // Arrange: Healy (2021) benchmark parameters
        var parameters = CreateHealyPutParameters();

        // Act
        var result = await _engine.PriceOption(parameters);

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
        var parameters = CreateHealyCallParameters();

        // Act
        var result = await _engine.PriceOption(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Price.Should().BeGreaterThan(0);
        result.Delta.Should().BeInRange(0, 1); // Call delta
    }

    [Fact]
    public async Task PriceOption_NegativeRates_ConsistentAcrossRegimes()
    {
        // Arrange: Test at rate boundary (r = 0)
        var paramsAtBoundary = CreateStandardCallParameters();
        paramsAtBoundary.RiskFreeRate = 0.0001; // Slightly positive

        var paramsSlightlyNegative = CreateStandardCallParameters();
        paramsSlightlyNegative.RiskFreeRate = -0.0001;
        paramsSlightlyNegative.DividendYield = 0.02; // q > r, so single boundary

        // Act
        var resultPositive = await _engine.PriceOption(paramsAtBoundary);
        var resultNegative = await _engine.PriceOption(paramsSlightlyNegative);

        // Assert: Prices should be very close at regime boundary
        var priceDifference = Math.Abs(resultPositive.Price - resultNegative.Price);
        priceDifference.Should().BeLessThan(0.5); // Within $0.50
    }

    #endregion

    #region Calendar Spread Tests

    [Fact]
    public async Task PriceCalendarSpread_PositiveRates_ValidSpread()
    {
        // Arrange
        var parameters = CreateCalendarSpreadParameters();

        // Act
        var result = await _engine.PriceCalendarSpread(parameters);

        // Assert
        result.Should().NotBeNull();
        result.SpreadCost.Should().BeGreaterThan(0); // Calendar spreads are debit spreads
        result.BackOption.Price.Should().BeGreaterThan(result.FrontOption.Price); // Back > Front
        result.SpreadVega.Should().BeGreaterThan(0); // Long vega
        result.MaxLoss.Should().Be(result.SpreadCost); // Max loss is debit paid
        result.MaxProfit.Should().BeGreaterThan(result.MaxLoss); // Profit potential exists
    }

    [Fact]
    public async Task PriceCalendarSpread_PositiveRates_CorrectGreeks()
    {
        // Arrange
        var parameters = CreateCalendarSpreadParameters();

        // Act
        var result = await _engine.PriceCalendarSpread(parameters);

        // Assert
        result.SpreadDelta.Should().BeInRange(-0.2, 0.2); // Near-neutral delta
        result.SpreadGamma.Should().BeLessThan(0); // Negative gamma
        result.SpreadVega.Should().BeGreaterThan(0); // Positive vega
        result.SpreadTheta.Should().BeGreaterThan(0); // Positive theta (benefits from time decay)
    }

    [Fact]
    public async Task PriceCalendarSpread_NegativeRates_ValidSpread()
    {
        // Arrange
        var parameters = CreateCalendarSpreadParameters();
        parameters.RiskFreeRate = -0.005;
        parameters.DividendYield = -0.010; // Double boundary regime

        // Act
        var result = await _engine.PriceCalendarSpread(parameters);

        // Assert
        result.Should().NotBeNull();
        result.SpreadCost.Should().BeGreaterThan(0);
        result.BackOption.Price.Should().BeGreaterThan(result.FrontOption.Price);
        result.SpreadVega.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PriceCalendarSpread_Validation_Works()
    {
        // Arrange
        var parameters = CreateCalendarSpreadParameters();

        // Act
        var result = await _engine.PriceCalendarSpread(parameters);

        // Assert: Should not throw when validating
        result.Invoking(r => r.Validate()).Should().NotThrow();
    }

    #endregion

    #region Implied Volatility Tests

    [Fact]
    public async Task CalculateImpliedVolatility_ConvergesCorrectly()
    {
        // Arrange
        var parameters = CreateStandardCallParameters();
        var targetIV = 0.25;
        parameters.ImpliedVolatility = targetIV;

        // Price the option to get target price
        var pricing = await _engine.PriceOption(parameters);
        var targetPrice = pricing.Price;

        // Remove IV from parameters
        parameters.ImpliedVolatility = 0.0;

        // Act
        var calculatedIV = await _engine.CalculateImpliedVolatility(targetPrice, parameters);

        // Assert
        calculatedIV.Should().BeApproximately(targetIV, 0.01); // Within 1% vol
    }

    [Fact]
    public async Task CalculateImpliedVolatility_InvalidPrice_Throws()
    {
        // Arrange
        var parameters = CreateStandardCallParameters();
        var invalidPrice = -1.0;

        // Act & Assert
        await _engine.Invoking(e => e.CalculateImpliedVolatility(invalidPrice, parameters))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CalculateImpliedVolatility_ZeroPrice_Throws()
    {
        // Arrange
        var parameters = CreateStandardCallParameters();
        var zeroPrice = 0.0;

        // Act & Assert
        await _engine.Invoking(e => e.CalculateImpliedVolatility(zeroPrice, parameters))
            .Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region Parameter Validation Tests

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
        var parameters = CreateStandardCallParameters();
        parameters.UnderlyingPrice = -100; // Invalid

        // Act & Assert
        await _engine.Invoking(e => e.PriceOption(parameters))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PriceCalendarSpread_NullParameters_Throws()
    {
        // Act & Assert
        await _engine.Invoking(e => e.PriceCalendarSpread(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PriceCalendarSpread_FrontAfterBack_Throws()
    {
        // Arrange
        var parameters = CreateCalendarSpreadParameters();
        // Swap expiries (invalid)
        (parameters.FrontExpiry, parameters.BackExpiry) = (parameters.BackExpiry, parameters.FrontExpiry);

        // Act & Assert
        await _engine.Invoking(e => e.PriceCalendarSpread(parameters))
            .Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region Helper Methods

    private OptionParameters CreateStandardCallParameters()
    {
        return new OptionParameters
        {
            UnderlyingPrice = 100.0,
            Strike = 100.0,
            Expiry = _valuationDate.Add(new Period(30, TimeUnit.Days)),
            ImpliedVolatility = 0.25,
            RiskFreeRate = 0.05,
            DividendYield = 0.02,
            OptionType = Option.Type.Call,
            ValuationDate = _valuationDate
        };
    }

    private OptionParameters CreateStandardPutParameters()
    {
        return new OptionParameters
        {
            UnderlyingPrice = 100.0,
            Strike = 100.0,
            Expiry = _valuationDate.Add(new Period(30, TimeUnit.Days)),
            ImpliedVolatility = 0.25,
            RiskFreeRate = 0.05,
            DividendYield = 0.02,
            OptionType = Option.Type.Put,
            ValuationDate = _valuationDate
        };
    }

    private OptionParameters CreateHealyPutParameters()
    {
        // Healy (2021) benchmark parameters
        return new OptionParameters
        {
            UnderlyingPrice = 100.0,
            Strike = 100.0,
            Expiry = _valuationDate.Add(new Period(365, TimeUnit.Days)), // 1 year
            ImpliedVolatility = 0.08,
            RiskFreeRate = -0.005,
            DividendYield = -0.010,
            OptionType = Option.Type.Put,
            ValuationDate = _valuationDate
        };
    }

    private OptionParameters CreateHealyCallParameters()
    {
        return new OptionParameters
        {
            UnderlyingPrice = 100.0,
            Strike = 100.0,
            Expiry = _valuationDate.Add(new Period(365, TimeUnit.Days)),
            ImpliedVolatility = 0.08,
            RiskFreeRate = -0.005,
            DividendYield = -0.010,
            OptionType = Option.Type.Call,
            ValuationDate = _valuationDate
        };
    }

    private CalendarSpreadParameters CreateCalendarSpreadParameters()
    {
        return new CalendarSpreadParameters
        {
            UnderlyingPrice = 150.0,
            Strike = 150.0,
            FrontExpiry = _valuationDate.Add(new Period(30, TimeUnit.Days)),
            BackExpiry = _valuationDate.Add(new Period(60, TimeUnit.Days)),
            ImpliedVolatility = 0.30,
            RiskFreeRate = 0.05,
            DividendYield = 0.02,
            OptionType = Option.Type.Call,
            ValuationDate = _valuationDate
        };
    }

    #endregion

    public void Dispose()
    {
        _engine.Dispose();
        _valuationDate.Dispose();
        GC.SuppressFinalize(this);
    }
}
