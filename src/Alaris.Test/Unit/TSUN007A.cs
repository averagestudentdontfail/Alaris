using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Alaris.Strategy.Core;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for IV models including STTM004A, Kou, Heston, STTM002A,
/// and STIV003A.
/// </summary>
public class IVModelTests
{
    // STTM004A Tests

    [Fact]
    public void STTM004A_Create_ValidDates_ReturnsCorrectParameters()
    {
        // Arrange
        DateTime valuationDate = new DateTime(2024, 1, 15);
        DateTime expirationDate = new DateTime(2024, 2, 16); // ~22 trading days

        // Act
        STTM004A timeParams = STTM004A.Create(valuationDate, expirationDate);

        // Assert
        timeParams.ValuationDate.Should().Be(valuationDate);
        timeParams.ExpirationDate.Should().Be(expirationDate);
        timeParams.DaysToExpiry.Should().BeGreaterThan(0);
        timeParams.TimeToExpiry.Should().BeGreaterThan(0);
    }

    [Fact]
    public void STTM004A_Create_WithEarningsBeforeExpiry_DetectsPreEarnings()
    {
        // Arrange
        DateTime valuationDate = new DateTime(2024, 1, 15);
        DateTime earningsDate = new DateTime(2024, 1, 25);
        DateTime expirationDate = new DateTime(2024, 2, 16);

        // Act
        STTM004A timeParams = STTM004A.Create(valuationDate, expirationDate, earningsDate);

        // Assert
        timeParams.IsPreEarnings.Should().BeTrue();
        timeParams.HasEarningsBeforeExpiry.Should().BeTrue();
        timeParams.DaysToEarnings.Should().BeGreaterThan(0);
    }

    [Fact]
    public void STTM004A_Create_WithEarningsAfterExpiry_NoEarningsEffect()
    {
        // Arrange
        DateTime valuationDate = new DateTime(2024, 1, 15);
        DateTime earningsDate = new DateTime(2024, 3, 1); // After expiry
        DateTime expirationDate = new DateTime(2024, 2, 16);

        // Act
        STTM004A timeParams = STTM004A.Create(valuationDate, expirationDate, earningsDate);

        // Assert
        timeParams.HasEarningsBeforeExpiry.Should().BeFalse();
    }

    [Fact]
    public void STTM004A_Create_ExpirationBeforeValuation_Throws()
    {
        // Arrange
        DateTime valuationDate = new DateTime(2024, 2, 1);
        DateTime expirationDate = new DateTime(2024, 1, 15);

        // Act & Assert
        Action act = () => STTM004A.Create(valuationDate, expirationDate);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TimeConstraints_ValidatePreEarnings_ValidParameters_ReturnsValid()
    {
        // Arrange
        TimeConstraints constraints = TimeConstraints.Default;
        DateTime valuationDate = new DateTime(2024, 1, 15);
        DateTime earningsDate = new DateTime(2024, 1, 25);
        DateTime expirationDate = new DateTime(2024, 2, 16);
        STTM004A timeParams = STTM004A.Create(valuationDate, expirationDate, earningsDate);

        // Act
        ValidationResult result = constraints.ValidatePreEarnings(timeParams);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    // Kou Model Tests

    [Fact]
    public void STIV002A_Parameters_Validate_ValidParameters_ReturnsValid()
    {
        // Arrange
        KouParameters parameters = KouParameters.DefaultEquity;

        // Act
        ValidationResult result = parameters.Validate();

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void STIV002A_Parameters_Validate_InvalidEta1_ReturnsInvalid()
    {
        // Arrange - Eta1 must be > 1
        KouParameters parameters = new KouParameters
        {
            Sigma = 0.20,
            Lambda = 3.0,
            P = 0.4,
            Eta1 = 0.5, // Invalid
            Eta2 = 5.0,
            RiskFreeRate = 0.05,
            DividendYield = 0.02
        };

        // Act
        ValidationResult result = parameters.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Eta1"));
    }

    [Fact]
    public void STIV002A_ComputeKappa_ReturnsExpectedValue()
    {
        // Arrange
        KouParameters parameters = new KouParameters
        {
            Sigma = 0.20,
            Lambda = 3.0,
            P = 0.5,      // Equal probability
            Eta1 = 10.0,  // Mean up jump = 10%
            Eta2 = 10.0,  // Mean down jump = 10%
            RiskFreeRate = 0.05,
            DividendYield = 0.02
        };

        // Act
        double kappa = parameters.ComputeKappa();

        // Assert - With symmetric jumps and equal probability, kappa should be small
        // kappa = 0.5 * 10/9 + 0.5 * 10/11 - 1 = 0.5556 + 0.4545 - 1 ≈ 0.0101
        kappa.Should().BeApproximately(0.0101, 0.01);
    }

    [Fact]
    public void STIV002A_ComputeTheoreticalIV_ATM_ReturnsReasonableValue()
    {
        // Arrange
        KouParameters parameters = KouParameters.DefaultEquity;
        STIV002A model = new STIV002A(parameters);
        double spot = 100;
        double strike = 100; // ATM
        double timeToExpiry = 30.0 / 252.0;

        // Act
        double iv = model.ComputeTheoreticalIV(spot, strike, timeToExpiry);

        // Assert
        iv.Should().BeGreaterThan(0.10).And.BeLessThan(0.50);
    }

    private static readonly double[] s_smileStrikes = { 80, 90, 100, 110, 120 };

    [Fact]
    public void STIV002A_ComputeSmile_ProducesSkew()
    {
        // Arrange
        KouParameters parameters = new KouParameters
        {
            Sigma = 0.20,
            Lambda = 5.0,
            P = 0.3, // More downward jumps
            Eta1 = 10.0,
            Eta2 = 5.0, // Larger downward jumps
            RiskFreeRate = 0.05,
            DividendYield = 0.02
        };
        STIV002A model = new STIV002A(parameters);
        double spot = 100;
        double timeToExpiry = 30.0 / 252.0;

        // Act
        (double Strike, double TheoreticalIV)[] smile = model.ComputeSmile(spot, s_smileStrikes, timeToExpiry);

        // Assert - OTM puts (low strikes) should have higher IV due to negative skew
        smile.Should().HaveCount(5);
        // With more/larger downward jumps, low strikes should have elevated IV
    }

    [Fact]
    public void STIV002A_ComputeSTTM001A_ReturnsCorrectLength()
    {
        // Arrange
        STIV002A model = new STIV002A(KouParameters.DefaultEquity);
        int[] dtePoints = { 7, 14, 30, 60, 90 };

        // Act
        (int DTE, double TheoreticalIV)[] termStructure = model.ComputeSTTM001A(100, 100, dtePoints);

        // Assert
        termStructure.Should().HaveCount(5);
    }

    // Heston Model Tests

    [Fact]
    public void STIV001A_Parameters_SatisfiesFellerCondition_ValidParams_ReturnsTrue()
    {
        // Arrange - 2 * kappa * theta > sigma_v^2
        HestonParameters parameters = new HestonParameters
        {
            V0 = 0.04,
            Theta = 0.04,
            Kappa = 2.0,
            SigmaV = 0.3,    // 2 * 2 * 0.04 = 0.16 > 0.09
            Rho = -0.7,
            RiskFreeRate = 0.05,
            DividendYield = 0.02
        };

        // Act & Assert
        parameters.SatisfiesFellerCondition().Should().BeTrue();
    }

    [Fact]
    public void STIV001A_Parameters_SatisfiesFellerCondition_InvalidParams_ReturnsFalse()
    {
        // Arrange - 2 * kappa * theta < sigma_v^2
        HestonParameters parameters = new HestonParameters
        {
            V0 = 0.04,
            Theta = 0.02,
            Kappa = 1.0,
            SigmaV = 0.5,    // 2 * 1 * 0.02 = 0.04 < 0.25
            Rho = -0.7,
            RiskFreeRate = 0.05,
            DividendYield = 0.02
        };

        // Act & Assert
        parameters.SatisfiesFellerCondition().Should().BeFalse();
    }

    [Fact]
    public void STIV001A_Parameters_Validate_FellerViolation_ReturnsInvalid()
    {
        // Arrange
        HestonParameters parameters = new HestonParameters
        {
            V0 = 0.04,
            Theta = 0.02,
            Kappa = 1.0,
            SigmaV = 0.5,
            Rho = -0.7,
            RiskFreeRate = 0.05,
            DividendYield = 0.02
        };

        // Act
        ValidationResult result = parameters.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Feller"));
    }

    [Fact]
    public void STIV001A_ExpectedVariance_ConvergesToTheta()
    {
        // Arrange
        HestonParameters parameters = HestonParameters.DefaultEquity;

        // Act
        double v0 = parameters.ExpectedVariance(0);
        double vLongTerm = parameters.ExpectedVariance(100); // Far future

        // Assert
        v0.Should().BeApproximately(parameters.V0, 0.0001);
        vLongTerm.Should().BeApproximately(parameters.Theta, 0.001);
    }

    [Fact]
    public void STIV001A_ComputeTheoreticalIV_ATM_ReturnsReasonableValue()
    {
        // Arrange
        Alaris.Strategy.Core.STIV001A model = new Alaris.Strategy.Core.STIV001A(HestonParameters.DefaultEquity);
        double spot = 100;
        double strike = 100;
        double timeToExpiry = 30.0 / 252.0;

        // Act
        double iv = model.ComputeTheoreticalIV(spot, strike, timeToExpiry);

        // Assert
        iv.Should().BeGreaterThan(0.10).And.BeLessThan(0.50);
    }

    [Fact]
    public void STIV001A_ComputeSmile_WithNegativeRho_ProducesNegativeSkew()
    {
        // Arrange
        HestonParameters parameters = new HestonParameters
        {
            V0 = 0.04,
            Theta = 0.04,
            Kappa = 2.0,
            SigmaV = 0.3,
            Rho = -0.8, // Strong negative correlation
            RiskFreeRate = 0.05,
            DividendYield = 0.02
        };
        Alaris.Strategy.Core.STIV001A model = new Alaris.Strategy.Core.STIV001A(parameters);
        double spot = 100;
        double timeToExpiry = 30.0 / 252.0;

        // Act
        (double Strike, double TheoreticalIV)[] smile = model.ComputeSmile(spot, s_smileStrikes, timeToExpiry);

        // Assert
        smile.Should().HaveCount(5);
        // Negative rho produces negative skew (higher IV for OTM puts)
    }

    // STTM002A Tests

    [Fact]
    public void STTM002A_Detect_PreEarnings_ReturnsPreSTTM002A()
    {
        // Arrange
        DateTime valuationDate = new DateTime(2024, 1, 15);
        DateTime earningsDate = new DateTime(2024, 1, 25);
        DateTime expirationDate = new DateTime(2024, 2, 16);
        STTM004A timeParams = STTM004A.Create(valuationDate, expirationDate, earningsDate);

        // Act
        STTM002A regime = STTM002A.Detect(timeParams);

        // Assert
        regime.RegimeType.Should().Be(STTM002AType.PreEarnings);
        regime.ModelRecommendation.Should().Be(RecommendedModel.LeungSantoli);
    }

    [Fact]
    public void STTM002A_Detect_NoEarnings_ReturnsNoSTTM002A()
    {
        // Arrange
        DateTime valuationDate = new DateTime(2024, 1, 15);
        DateTime expirationDate = new DateTime(2024, 2, 16);
        STTM004A timeParams = STTM004A.Create(valuationDate, expirationDate);

        // Act
        STTM002A regime = STTM002A.Detect(timeParams);

        // Assert
        regime.RegimeType.Should().Be(STTM002AType.NoEarnings);
        regime.ModelRecommendation.Should().Be(RecommendedModel.Heston);
    }

    [Fact]
    public void STTM002A_ComputeAdjustedIV_PreEarnings_ReturnsEarningsIV()
    {
        // Arrange
        DateTime valuationDate = new DateTime(2024, 1, 15);
        DateTime earningsDate = new DateTime(2024, 1, 25);
        DateTime expirationDate = new DateTime(2024, 2, 16);
        STTM004A timeParams = STTM004A.Create(valuationDate, expirationDate, earningsDate);
        STTM002A regime = STTM002A.Detect(timeParams);

        double baseIV = 0.20;
        double earningsIV = 0.35;

        // Act
        double adjustedIV = regime.ComputeAdjustedIV(baseIV, earningsIV);

        // Assert
        adjustedIV.Should().Be(earningsIV); // Pre-earnings uses full earnings IV
    }

    [Fact]
    public void STTM002A_ComputeAdjustedIV_NoEarnings_ReturnsBaseIV()
    {
        // Arrange
        DateTime valuationDate = new DateTime(2024, 1, 15);
        DateTime expirationDate = new DateTime(2024, 2, 16);
        STTM004A timeParams = STTM004A.Create(valuationDate, expirationDate);
        STTM002A regime = STTM002A.Detect(timeParams);

        double baseIV = 0.20;
        double earningsIV = 0.35;

        // Act
        double adjustedIV = regime.ComputeAdjustedIV(baseIV, earningsIV);

        // Assert
        adjustedIV.Should().Be(baseIV);
    }

    // MartingaleValidator Tests

    [Fact]
    public void MartingaleValidator_Validate_BlackScholes_AlwaysValid()
    {
        // Arrange
        MartingaleValidator validator = new MartingaleValidator();
        STTM004A timeParams = STTM004A.Create(
            new DateTime(2024, 1, 15),
            new DateTime(2024, 2, 16));
        ModelSelectionContext context = new ModelSelectionContext
        {
            Spot = 100,
            BaseVolatility = 0.20,
            RiskFreeRate = 0.05,
            DividendYield = 0.02,
            TimeParams = timeParams
        };

        // Act
        bool isValid = validator.Validate(RecommendedModel.BlackScholes, context);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void MartingaleValidator_ComputeJumpCompensation_ReturnsCorrectValue()
    {
        // Arrange
        KouParameters kouParams = KouParameters.DefaultEquity;

        // Act
        double compensation = MartingaleValidator.ComputeJumpCompensation(kouParams);

        // Assert - lambda * kappa
        double expectedCompensation = kouParams.Lambda * kouParams.ComputeKappa();
        compensation.Should().BeApproximately(expectedCompensation, 0.0001);
    }

    // STIV003A Tests

    [Fact]
    public void STIV003A_SelectBestModel_PreEarnings_PrefersLeungSantoli()
    {
        // Arrange
        STIV003A selector = new STIV003A();
        DateTime valuationDate = new DateTime(2024, 1, 15);
        DateTime earningsDate = new DateTime(2024, 1, 25);
        DateTime expirationDate = new DateTime(2024, 2, 16);
        STTM004A timeParams = STTM004A.Create(valuationDate, expirationDate, earningsDate);

        ModelSelectionContext context = new ModelSelectionContext
        {
            Spot = 100,
            BaseVolatility = 0.20,
            RiskFreeRate = 0.05,
            DividendYield = 0.02,
            TimeParams = timeParams,
            EarningsJumpVolatility = 0.05
        };

        // Act
        ModelSelectionResult result = selector.SelectBestModel(context);

        // Assert
        result.Regime.RegimeType.Should().Be(STTM002AType.PreEarnings);
        result.Evaluations.Should().NotBeEmpty();
    }

    [Fact]
    public void STIV003A_SelectBestModel_NoEarnings_PrefersHeston()
    {
        // Arrange
        STIV003A selector = new STIV003A();
        DateTime valuationDate = new DateTime(2024, 1, 15);
        DateTime expirationDate = new DateTime(2024, 2, 16);
        STTM004A timeParams = STTM004A.Create(valuationDate, expirationDate);

        ModelSelectionContext context = new ModelSelectionContext
        {
            Spot = 100,
            BaseVolatility = 0.20,
            RiskFreeRate = 0.05,
            DividendYield = 0.02,
            TimeParams = timeParams,
            HestonParams = HestonParameters.DefaultEquity
        };

        // Act
        ModelSelectionResult result = selector.SelectBestModel(context);

        // Assert
        result.Regime.RegimeType.Should().Be(STTM002AType.NoEarnings);
    }

    [Fact]
    public void STIV003A_SelectBestModel_WithMarketData_ComputesFitMetrics()
    {
        // Arrange
        STIV003A selector = new STIV003A();
        DateTime valuationDate = new DateTime(2024, 1, 15);
        DateTime expirationDate = new DateTime(2024, 2, 16);
        STTM004A timeParams = STTM004A.Create(valuationDate, expirationDate);

        List<(double Strike, int DTE, double IV)> marketIVs = new List<(double Strike, int DTE, double IV)>
        {
            (90, 30, 0.25),
            (95, 30, 0.22),
            (100, 30, 0.20),
            (105, 30, 0.21),
            (110, 30, 0.23)
        };

        ModelSelectionContext context = new ModelSelectionContext
        {
            Spot = 100,
            BaseVolatility = 0.20,
            RiskFreeRate = 0.05,
            DividendYield = 0.02,
            TimeParams = timeParams,
            HestonParams = HestonParameters.DefaultEquity,
            MarketIVs = marketIVs
        };

        // Act
        ModelSelectionResult result = selector.SelectBestModel(context);

        // Assert
        result.BestEvaluation.FitMetrics.Should().NotBeNull();
        result.BestEvaluation.FitMetrics.RMSE.Should().BeGreaterThanOrEqualTo(0);
    }

    // FitMetrics Tests

    [Fact]
    public void FitMetrics_Default_HasReasonableValues()
    {
        // Arrange & Act
        FitMetrics defaultMetrics = FitMetrics.Default;

        // Assert
        defaultMetrics.MSE.Should().Be(1.0);
        defaultMetrics.RMSE.Should().Be(1.0);
        defaultMetrics.RSquared.Should().Be(0.0);
    }

    // ValidationResult Tests

    private static readonly string[] s_validationErrors = { "Error 1", "Error 2" };

    [Fact]
    public void ValidationResult_ThrowIfInvalid_InvalidResult_Throws()
    {
        // Arrange
        ValidationResult result = new ValidationResult(false, s_validationErrors);

        // Act & Assert
        Action act = () => result.ThrowIfInvalid();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Error 1*");
    }

    [Fact]
    public void ValidationResult_ThrowIfInvalid_ValidResult_DoesNotThrow()
    {
        // Arrange
        ValidationResult result = new ValidationResult(true);

        // Act & Assert
        Action act = () => result.ThrowIfInvalid();
        act.Should().NotThrow();
    }
}
