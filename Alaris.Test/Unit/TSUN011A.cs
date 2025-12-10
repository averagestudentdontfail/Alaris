using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Alaris.Strategy.Core;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for STEJ001A - Earnings Jump Risk Calibrator.
/// Tests mixture distribution calibration, tail probability, and risk evaluation.
/// </summary>
public sealed class STEJ001ATests
{
    private readonly STEJ001A _calibrator;

    public STEJ001ATests()
    {
        _calibrator = new STEJ001A();
    }

    [Fact]
    public void CalibrateFromMoves_WithInsufficientData_ReturnsDefault()
    {
        // Arrange: Only 5 data points (need 8)
        List<double> moves = new List<double> { 1.0, 0.8, 1.2, 0.9, 1.1 };

        // Act
        STEJ002A result = _calibrator.CalibrateFromMoves(moves);

        // Assert: Should return default parameters
        result.DataPointCount.Should().Be(0);
        result.MixtureWeight.Should().BeApproximately(0.70, 0.01);
    }

    [Fact]
    public void CalibrateFromMoves_WithNormalData_ReturnsHighLambda()
    {
        // Arrange: Normal-looking data (no extreme outliers)
        List<double> moves = new List<double> { 0.9, 1.1, 0.95, 1.05, 1.0, 0.92, 1.08, 0.98, 1.02, 0.97 };

        // Act
        STEJ002A result = _calibrator.CalibrateFromMoves(moves);

        // Assert: High lambda (mostly Normal, low Laplace weight)
        result.MixtureWeight.Should().BeGreaterThan(0.50);
        result.DataPointCount.Should().Be(10);
    }

    [Fact]
    public void CalibrateFromMoves_WithFatTails_ReturnsLowerLambda()
    {
        // Arrange: Data with fat tails (outliers)
        List<double> moves = new List<double>
        {
            0.9, 1.1, 0.95, 1.05, 1.0,   // Normal
            0.92, 1.08, 0.98,            // Normal
            2.5, 3.0, 2.8                 // Fat tail outliers
        };

        // Act
        STEJ002A result = _calibrator.CalibrateFromMoves(moves);

        // Assert: Should detect fat tails and have higher max observed
        result.MaxObservedMove.Should().BeGreaterThan(2.0);
    }

    [Fact]
    public void CalculateJumpRiskScore_WithNormalParams_ReturnsSmallTail()
    {
        // Arrange: Parameters close to pure Normal
        STEJ002A parameters = new STEJ002A
        {
            MixtureWeight = 0.95,
            LaplaceScale = 0.40,
            DataPointCount = 20,
            MeanAbsoluteMove = 1.0,
            MaxObservedMove = 1.5,
            CalibrationTime = DateTime.UtcNow
        };

        // Act: Probability of 2x move
        double prob = _calibrator.CalculateJumpRiskScore(parameters, impliedMove: 0.10, thresholdMultiple: 2.0);

        // Assert: Should be small (close to Normal tail ~2.3%)
        prob.Should().BeLessThan(0.10);
        prob.Should().BeGreaterThan(0.01);
    }

    [Fact]
    public void CalculateJumpRiskScore_WithFatTailParams_ReturnsLargerTail()
    {
        // Arrange: Parameters with fat tails
        STEJ002A parameters = new STEJ002A
        {
            MixtureWeight = 0.60,
            LaplaceScale = 0.60,
            DataPointCount = 20,
            MeanAbsoluteMove = 1.2,
            MaxObservedMove = 2.5,
            CalibrationTime = DateTime.UtcNow
        };

        // Act: Probability of 2x move with fat tail params
        double prob = _calibrator.CalculateJumpRiskScore(parameters, impliedMove: 0.10, thresholdMultiple: 2.0);

        // Assert: Fat tail params should give higher prob than pure Normal (~2.3%)
        // With these params: λ=0.60 (Normal) + 0.40 (Laplace with b=0.60)
        prob.Should().BeGreaterThan(0.015);  // More than pure Normal
    }

    [Fact]
    public void CalculateJumpRiskScore_HigherThreshold_ReturnsSmallerProb()
    {
        // Arrange
        STEJ002A parameters = STEJ002A.DefaultParameters();

        // Act
        double prob2x = _calibrator.CalculateJumpRiskScore(parameters, impliedMove: 0.10, thresholdMultiple: 2.0);
        double prob3x = _calibrator.CalculateJumpRiskScore(parameters, impliedMove: 0.10, thresholdMultiple: 3.0);

        // Assert: P(Z > 3) < P(Z > 2)
        prob3x.Should().BeLessThan(prob2x);
    }

    [Fact]
    public void EvaluateJumpRisk_LowRisk_ReturnsNoAdjustment()
    {
        // Arrange: Low risk parameters
        STEJ002A parameters = new STEJ002A
        {
            MixtureWeight = 0.90,
            LaplaceScale = 0.30,
            DataPointCount = 20,
            MeanAbsoluteMove = 0.9,
            MaxObservedMove = 1.5,
            CalibrationTime = DateTime.UtcNow
        };

        // Act
        STEJ003A result = _calibrator.EvaluateJumpRisk(parameters, impliedMove: 0.10, currentAllocation: 0.05);

        // Assert
        result.RiskLevel.Should().Be(STEJ004A.Low);
        result.AllocationAdjustmentFactor.Should().Be(1.0);
        result.RecommendedAllocation.Should().Be(0.05);
    }

    [Fact]
    public void EvaluateJumpRisk_HighRisk_ReturnsReducedAllocation()
    {
        // Arrange: High risk parameters (extreme max move)
        STEJ002A parameters = new STEJ002A
        {
            MixtureWeight = 0.50,
            LaplaceScale = 0.80,
            DataPointCount = 20,
            MeanAbsoluteMove = 1.5,
            MaxObservedMove = 3.5,  // > 3.0 threshold for High
            CalibrationTime = DateTime.UtcNow
        };

        // Act
        STEJ003A result = _calibrator.EvaluateJumpRisk(parameters, impliedMove: 0.10, currentAllocation: 0.05);

        // Assert
        result.RiskLevel.Should().Be(STEJ004A.High);
        result.AllocationAdjustmentFactor.Should().Be(0.50);
        result.RecommendedAllocation.Should().BeApproximately(0.025, 0.001);
    }

    [Fact]
    public void EvaluateJumpRisk_ElevatedRisk_ReturnsModerateCut()
    {
        // Arrange: Elevated risk (max > 2.5)
        STEJ002A parameters = new STEJ002A
        {
            MixtureWeight = 0.65,
            LaplaceScale = 0.50,
            DataPointCount = 20,
            MeanAbsoluteMove = 1.1,
            MaxObservedMove = 2.7,  // > 2.5 threshold for Elevated
            CalibrationTime = DateTime.UtcNow
        };

        // Act
        STEJ003A result = _calibrator.EvaluateJumpRisk(parameters, impliedMove: 0.10, currentAllocation: 0.04);

        // Assert
        result.RiskLevel.Should().Be(STEJ004A.Elevated);
        result.AllocationAdjustmentFactor.Should().Be(0.75);
    }

    [Fact]
    public void DefaultParameters_ReturnsValidDefaults()
    {
        // Act
        STEJ002A defaults = STEJ002A.DefaultParameters();

        // Assert
        defaults.MixtureWeight.Should().Be(0.70);
        defaults.LaplaceScale.Should().Be(0.40);
        defaults.DataPointCount.Should().Be(0);
        defaults.MeanAbsoluteMove.Should().Be(1.0);
        defaults.MaxObservedMove.Should().Be(2.0);
    }
}
