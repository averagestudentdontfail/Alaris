// TSUN026A.cs - Execution Cost Model Unit Tests
// Component ID: TSUN026A
//
// Tests for Cost Model Components:
// - STCS002A: Order parameters validation
// - STCS003A: Single leg cost calculation invariants
// - STCS004A: Spread cost aggregation properties
// - STCS005A: Constant fee model implementation
// - STCS006A: Signal cost validation
//
// Mathematical Invariants Tested:
// 1. Cost Additivity: TotalCost = Commission + ExchangeFees + RegulatoryFees + Slippage
// 2. Non-Negativity: All cost components ≥ 0
// 3. Spread Composition: SpreadCost = FrontLegCost + BackLegCost
// 4. Slippage Bounds: HalfSpread ≤ Slippage ≤ FullSpread (per unit)
// 5. Proportionality: Costs scale linearly with contract count
//
// References:
//   - InteractiveBrokersFeeModel (IBKR tiered pricing)
//   - Atilgan (2014) IV/RV threshold analysis

using System;
using Xunit;
using FluentAssertions;
using Alaris.Strategy.Cost;

namespace Alaris.Test.Unit;

/// <summary>
/// TSUN026A: Unit tests for execution cost model components.
/// Tests mathematical properties that must hold for all cost calculations.
/// </summary>
public sealed class TSUN026A
{

    private static STCS002A CreateValidBuyOrderParams() => new STCS002A
    {
        Contracts = 5,
        MidPrice = 2.50,
        BidPrice = 2.45,
        AskPrice = 2.55,
        Direction = OrderDirection.Buy,
        Premium = 2.50,
        Symbol = "AAPL",
        ContractMultiplier = 100.0
    };

    private static STCS002A CreateValidSellOrderParams() => new STCS002A
    {
        Contracts = 5,
        MidPrice = 1.20,
        BidPrice = 1.15,
        AskPrice = 1.25,
        Direction = OrderDirection.Sell,
        Premium = 1.20,
        Symbol = "AAPL",
        ContractMultiplier = 100.0
    };

    private static STCS005A CreateDefaultCostModel() => new STCS005A();


    // STCS002A: Order Parameters Validation Tests

    /// <summary>
    /// Valid order parameters should pass validation.
    /// </summary>
    [Fact]
    public void STCS002A_Validate_ValidParameters_DoesNotThrow()
    {
        // Arrange
        STCS002A parameters = CreateValidBuyOrderParams();

        // Act & Assert
        Action act = () => parameters.Validate();
        act.Should().NotThrow();
    }

    /// <summary>
    /// Zero or negative contract count should fail validation.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void STCS002A_Validate_InvalidContracts_Throws(int contracts)
    {
        // Arrange
        STCS002A parameters = CreateValidBuyOrderParams() with { Contracts = contracts };

        // Act & Assert
        Action act = () => parameters.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Contract*");
    }

    /// <summary>
    /// Negative mid price should fail validation.
    /// </summary>
    [Fact]
    public void STCS002A_Validate_NegativeMidPrice_Throws()
    {
        // Arrange
        STCS002A parameters = CreateValidBuyOrderParams() with { MidPrice = -0.01 };

        // Act & Assert
        Action act = () => parameters.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Mid price*");
    }

    /// <summary>
    /// Ask price less than bid price should fail validation.
    /// </summary>
    [Fact]
    public void STCS002A_Validate_AskLessThanBid_Throws()
    {
        // Arrange
        STCS002A parameters = CreateValidBuyOrderParams() with
        {
            BidPrice = 2.60,
            AskPrice = 2.55
        };

        // Act & Assert
        Action act = () => parameters.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Ask*bid*");
    }

    /// <summary>
    /// BidAskSpread computed property should equal Ask - Bid.
    /// </summary>
    [Fact]
    public void STCS002A_BidAskSpread_EqualsAskMinusBid()
    {
        // Arrange
        STCS002A parameters = CreateValidBuyOrderParams();

        // Act
        double spread = parameters.BidAskSpread;

        // Assert
        spread.Should().BeApproximately(
            parameters.AskPrice - parameters.BidPrice,
            1e-10);
    }

    /// <summary>
    /// HalfSpread should equal BidAskSpread / 2.
    /// </summary>
    [Fact]
    public void STCS002A_HalfSpread_EqualsSpreadDividedByTwo()
    {
        // Arrange
        STCS002A parameters = CreateValidBuyOrderParams();

        // Act
        double halfSpread = parameters.HalfSpread;

        // Assert
        halfSpread.Should().BeApproximately(
            parameters.BidAskSpread / 2.0,
            1e-10);
    }

    // STCS005A: Constant Fee Model Tests

    /// <summary>
    /// Default constructor should use standard fee values.
    /// </summary>
    [Fact]
    public void STCS005A_DefaultConstructor_UsesStandardFees()
    {
        // Act
        STCS005A model = new STCS005A();

        // Assert
        model.ModelName.Should().Be("ConstantFeeModel");
    }

    /// <summary>
    /// Negative fees should throw ArgumentOutOfRangeException.
    /// </summary>
    [Theory]
    [InlineData(-0.01, 0.30, 0.02)]
    [InlineData(0.65, -0.01, 0.02)]
    [InlineData(0.65, 0.30, -0.01)]
    public void STCS005A_Constructor_NegativeFees_Throws(
        double fee, double exchange, double regulatory)
    {
        // Act & Assert
        Action act = () => _ = new STCS005A(fee, exchange, regulatory);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // STCS003A: Single Leg Cost Invariants

    /// <summary>
    /// INVARIANT: TotalCost = Commission + ExchangeFees + RegulatoryFees + Slippage.
    /// </summary>
    [Fact]
    public void STCS003A_TotalCost_EqualsSumOfComponents()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A parameters = CreateValidBuyOrderParams();

        // Act
        STCS003A result = model.ComputeOptionCost(parameters);

        // Assert
        double expectedTotal = result.Commission + result.ExchangeFees +
                               result.RegulatoryFees + result.Slippage;
        result.TotalCost.Should().BeApproximately(expectedTotal, 1e-10);
    }

    /// <summary>
    /// INVARIANT: All cost components must be non-negative.
    /// </summary>
    [Fact]
    public void STCS003A_AllComponents_AreNonNegative()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A parameters = CreateValidBuyOrderParams();

        // Act
        STCS003A result = model.ComputeOptionCost(parameters);

        // Assert
        result.Commission.Should().BeGreaterThanOrEqualTo(0);
        result.ExchangeFees.Should().BeGreaterThanOrEqualTo(0);
        result.RegulatoryFees.Should().BeGreaterThanOrEqualTo(0);
        result.Slippage.Should().BeGreaterThanOrEqualTo(0);
        result.TotalCost.Should().BeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// INVARIANT: Costs scale linearly with contract count.
    /// </summary>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(5, 10)]
    [InlineData(10, 100)]
    public void STCS003A_Costs_ScaleLinearlyWithContracts(int contracts1, int contracts2)
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A params1 = CreateValidBuyOrderParams() with { Contracts = contracts1 };
        STCS002A params2 = CreateValidBuyOrderParams() with { Contracts = contracts2 };

        // Act
        STCS003A result1 = model.ComputeOptionCost(params1);
        STCS003A result2 = model.ComputeOptionCost(params2);

        // Assert - Cost per contract should be identical
        double ratio = (double)contracts2 / contracts1;
        result2.TotalCost.Should().BeApproximately(result1.TotalCost * ratio, 1e-6);
    }

    /// <summary>
    /// Buy order should fill at ask price (execution price = ask).
    /// </summary>
    [Fact]
    public void STCS003A_BuyOrder_ExecutionPriceIsAsk()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A parameters = CreateValidBuyOrderParams();

        // Act
        STCS003A result = model.ComputeOptionCost(parameters);

        // Assert
        result.ExecutionPrice.Should().BeApproximately(parameters.AskPrice, 1e-10);
    }

    /// <summary>
    /// Sell order should fill at bid price (execution price = bid).
    /// </summary>
    [Fact]
    public void STCS003A_SellOrder_ExecutionPriceIsBid()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A parameters = CreateValidSellOrderParams();

        // Act
        STCS003A result = model.ComputeOptionCost(parameters);

        // Assert
        result.ExecutionPrice.Should().BeApproximately(parameters.BidPrice, 1e-10);
    }

    /// <summary>
    /// INVARIANT: Slippage = |ExecutionPrice - MidPrice| × Contracts × Multiplier.
    /// </summary>
    [Fact]
    public void STCS003A_Slippage_EqualsHalfSpreadTimesContractsTimesMultiplier()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A parameters = CreateValidBuyOrderParams();

        // Act
        STCS003A result = model.ComputeOptionCost(parameters);

        // Assert
        double expectedSlippage = Math.Abs(parameters.AskPrice - parameters.MidPrice)
                                  * parameters.Contracts
                                  * parameters.ContractMultiplier;
        result.Slippage.Should().BeApproximately(expectedSlippage, 1e-10);
    }

    /// <summary>
    /// CostPerContract should equal TotalCost / Contracts.
    /// </summary>
    [Fact]
    public void STCS003A_CostPerContract_EqualsTotalDividedByContracts()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A parameters = CreateValidBuyOrderParams();

        // Act
        STCS003A result = model.ComputeOptionCost(parameters);

        // Assert
        result.CostPerContract.Should().BeApproximately(
            result.TotalCost / result.Contracts,
            1e-10);
    }

    // STCS004A: Spread Cost Aggregation Tests

    /// <summary>
    /// INVARIANT: TotalExecutionCost = FrontLegCost.TotalCost + BackLegCost.TotalCost.
    /// </summary>
    [Fact]
    public void STCS004A_TotalExecutionCost_IsSumOfLegs()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A frontParams = CreateValidSellOrderParams();
        STCS002A backParams = CreateValidBuyOrderParams();

        // Act
        STCS004A result = model.ComputeSpreadCost(frontParams, backParams);

        // Assert
        double expectedTotal = result.FrontLegCost.TotalCost + result.BackLegCost.TotalCost;
        result.TotalExecutionCost.Should().BeApproximately(expectedTotal, 1e-10);
    }

    /// <summary>
    /// INVARIANT: TotalSlippage = FrontLegSlippage + BackLegSlippage.
    /// </summary>
    [Fact]
    public void STCS004A_TotalSlippage_IsSumOfLegSlippages()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A frontParams = CreateValidSellOrderParams();
        STCS002A backParams = CreateValidBuyOrderParams();

        // Act
        STCS004A result = model.ComputeSpreadCost(frontParams, backParams);

        // Assert
        double expectedSlippage = result.FrontLegCost.Slippage + result.BackLegCost.Slippage;
        result.TotalSlippage.Should().BeApproximately(expectedSlippage, 1e-10);
    }

    /// <summary>
    /// INVARIANT: TheoreticalDebit = BackMidPrice - FrontMidPrice.
    /// </summary>
    [Fact]
    public void STCS004A_TheoreticalDebit_EqualsBackMidMinusFrontMid()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A frontParams = CreateValidSellOrderParams();  // Mid = 1.20
        STCS002A backParams = CreateValidBuyOrderParams();    // Mid = 2.50

        // Act
        STCS004A result = model.ComputeSpreadCost(frontParams, backParams);

        // Assert
        double expectedDebit = backParams.MidPrice - frontParams.MidPrice;
        result.TheoreticalDebit.Should().BeApproximately(expectedDebit, 1e-10);
    }

    /// <summary>
    /// INVARIANT: ExecutionDebit = BackAsk - FrontBid (worst-case fill for debit spread).
    /// </summary>
    [Fact]
    public void STCS004A_ExecutionDebit_EqualsBackAskMinusFrontBid()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A frontParams = CreateValidSellOrderParams();  // Bid = 1.15
        STCS002A backParams = CreateValidBuyOrderParams();    // Ask = 2.55

        // Act
        STCS004A result = model.ComputeSpreadCost(frontParams, backParams);

        // Assert
        double expectedDebit = backParams.AskPrice - frontParams.BidPrice;
        result.ExecutionDebit.Should().BeApproximately(expectedDebit, 1e-10);
    }

    /// <summary>
    /// INVARIANT: ExecutionDebit ≥ TheoreticalDebit (slippage always increases cost).
    /// </summary>
    [Fact]
    public void STCS004A_ExecutionDebit_GreaterOrEqualToTheoreticalDebit()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A frontParams = CreateValidSellOrderParams();
        STCS002A backParams = CreateValidBuyOrderParams();

        // Act
        STCS004A result = model.ComputeSpreadCost(frontParams, backParams);

        // Assert
        result.ExecutionDebit.Should().BeGreaterThanOrEqualTo(result.TheoreticalDebit);
    }

    /// <summary>
    /// TotalCapitalRequired should include debit and all fees.
    /// </summary>
    [Fact]
    public void STCS004A_TotalCapitalRequired_IncludesDebitAndFees()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A frontParams = CreateValidSellOrderParams();
        STCS002A backParams = CreateValidBuyOrderParams();

        // Act
        STCS004A result = model.ComputeSpreadCost(frontParams, backParams);

        // Assert
        double expectedCapital = (result.ExecutionDebit * result.Contracts * result.ContractMultiplier)
                                 + result.TotalFees;
        result.TotalCapitalRequired.Should().BeApproximately(expectedCapital, 1e-10);
    }

    // Edge Cases and Boundary Tests

    /// <summary>
    /// Zero bid-ask spread should produce zero slippage.
    /// </summary>
    [Fact]
    public void STCS003A_ZeroSpread_ProducesZeroSlippage()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A parameters = CreateValidBuyOrderParams() with
        {
            BidPrice = 2.50,
            AskPrice = 2.50,
            MidPrice = 2.50
        };

        // Act
        STCS003A result = model.ComputeOptionCost(parameters);

        // Assert
        result.Slippage.Should().BeApproximately(0, 1e-10);
    }

    /// <summary>
    /// Single contract should compute correctly.
    /// </summary>
    [Fact]
    public void STCS003A_SingleContract_ComputesCorrectly()
    {
        // Arrange
        STCS005A model = new STCS005A(
            feePerContract: 0.65,
            exchangeFeePerContract: 0.30,
            regulatoryFeePerContract: 0.02);
        STCS002A parameters = CreateValidBuyOrderParams() with { Contracts = 1 };

        // Act
        STCS003A result = model.ComputeOptionCost(parameters);

        // Assert
        result.Commission.Should().BeApproximately(0.65, 1e-10);
        result.ExchangeFees.Should().BeApproximately(0.30, 1e-10);
        result.RegulatoryFees.Should().BeApproximately(0.02, 1e-10);
        result.TotalFees.Should().BeApproximately(0.97, 1e-10);
    }

    /// <summary>
    /// Zero-cost model (all fees = 0) should only have slippage.
    /// </summary>
    [Fact]
    public void STCS003A_ZeroCostModel_OnlyHasSlippage()
    {
        // Arrange
        STCS005A model = new STCS005A(
            feePerContract: 0.0,
            exchangeFeePerContract: 0.0,
            regulatoryFeePerContract: 0.0);
        STCS002A parameters = CreateValidBuyOrderParams();

        // Act
        STCS003A result = model.ComputeOptionCost(parameters);

        // Assert
        result.TotalFees.Should().BeApproximately(0, 1e-10);
        result.TotalCost.Should().BeApproximately(result.Slippage, 1e-10);
        result.Slippage.Should().BeGreaterThan(0);  // Still has spread slippage
    }

    // Interface Compliance Tests

    /// <summary>
    /// STCS005A should implement STCS001A interface.
    /// </summary>
    [Fact]
    public void STCS005A_ImplementsInterface()
    {
        // Arrange & Act
        STCS001A model = new STCS005A();

        // Assert
        model.Should().BeAssignableTo<STCS001A>();
        model.ModelName.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Null parameters should throw ArgumentNullException.
    /// </summary>
    [Fact]
    public void STCS005A_ComputeOptionCost_NullParams_Throws()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();

        // Act & Assert
        Action act = () => model.ComputeOptionCost(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Null parameters for spread cost should throw ArgumentNullException.
    /// </summary>
    [Fact]
    public void STCS005A_ComputeSpreadCost_NullParams_Throws()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();
        STCS002A validParams = CreateValidBuyOrderParams();

        // Act & Assert
        Action act1 = () => model.ComputeSpreadCost(null!, validParams);
        act1.Should().Throw<ArgumentNullException>();

        Action act2 = () => model.ComputeSpreadCost(validParams, null!);
        act2.Should().Throw<ArgumentNullException>();
    }

    // Realistic Scenario Tests

    /// <summary>
    /// Typical AAPL calendar spread cost calculation.
    /// </summary>
    [Fact]
    public void STCS004A_TypicalCalendarSpread_RealisticCosts()
    {
        // Arrange - Typical AAPL options around $2-3 premium
        STCS005A model = CreateDefaultCostModel();

        STCS002A frontParams = new STCS002A
        {
            Contracts = 10,
            MidPrice = 2.10,
            BidPrice = 2.05,
            AskPrice = 2.15,
            Direction = OrderDirection.Sell,
            Premium = 2.10,
            Symbol = "AAPL",
            ContractMultiplier = 100.0
        };

        STCS002A backParams = new STCS002A
        {
            Contracts = 10,
            MidPrice = 3.50,
            BidPrice = 3.45,
            AskPrice = 3.55,
            Direction = OrderDirection.Buy,
            Premium = 3.50,
            Symbol = "AAPL",
            ContractMultiplier = 100.0
        };

        // Act
        STCS004A result = model.ComputeSpreadCost(frontParams, backParams);

        // Assert - Sanity checks for realistic values
        result.TheoreticalDebit.Should().BeApproximately(1.40, 0.01);  // 3.50 - 2.10
        result.ExecutionDebit.Should().BeApproximately(1.50, 0.01);   // 3.55 - 2.05
        result.Contracts.Should().Be(10);
        result.TotalExecutionCost.Should().BeGreaterThan(10);  // At least $1 per contract
        result.TotalExecutionCost.Should().BeLessThan(200);    // Not unreasonably high
        result.ExecutionCostPercent.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Wide spread options (low liquidity) should have higher slippage percentage.
    /// </summary>
    [Fact]
    public void STCS003A_WideSpread_HigherSlippagePercent()
    {
        // Arrange
        STCS005A model = CreateDefaultCostModel();

        STCS002A narrowSpread = CreateValidBuyOrderParams() with
        {
            BidPrice = 2.48,
            AskPrice = 2.52,
            MidPrice = 2.50
        };

        STCS002A wideSpread = CreateValidBuyOrderParams() with
        {
            BidPrice = 2.30,
            AskPrice = 2.70,
            MidPrice = 2.50
        };

        // Act
        STCS003A narrowResult = model.ComputeOptionCost(narrowSpread);
        STCS003A wideResult = model.ComputeOptionCost(wideSpread);

        // Assert
        wideResult.Slippage.Should().BeGreaterThan(narrowResult.Slippage);
        wideResult.SlippagePercent.Should().BeGreaterThan(narrowResult.SlippagePercent);
    }
}
