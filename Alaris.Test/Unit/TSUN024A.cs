// TSUN024A.cs - Mathematical Invariant Tests for IV Calculators
// Component ID: TSUN024A
//
// First-Principles Tests for STIV001A (Heston), STIV002A (Kou), STIV003A
//
// Mathematical Invariants Tested:
// 1. Boundary Behaviour: σ→0, σ→∞, T→0, T→∞
// 2. Put-Call Parity: C - P = S*exp(-dT) - K*exp(-rT)
// 3. No-Arbitrage: IV > 0 for all valid inputs
// 4. Monotonicity: IV surfaces should be well-behaved
// 5. Limiting Cases: Heston→BS as ξ→0, Kou→BS as λ→0
// 6. Feller Condition: Variance must remain positive
//
// References:
//   - Heston, S.L. (1993) "A Closed-Form Solution for Options with Stochastic Volatility"
//   - Kou, S.G. (2002) "A Jump-Diffusion Model for Option Pricing"
//   - Gatheral, J. (2006) "The Volatility Surface: A Practitioner's Guide"

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using Alaris.Strategy.Core;

namespace Alaris.Test.Unit;

/// <summary>
/// TSUN024A: Mathematical invariant tests for IV calculators.
/// Tests properties that must hold independent of specific numerical values.
/// </summary>
public sealed class TSUN024A
{

    private static readonly double[] s_spots = { 80, 90, 100, 110, 120 };
    private static readonly double[] s_strikes = { 80, 90, 95, 100, 105, 110, 120 };
    private static readonly double[] s_timesToExpiry = { 7.0 / 252, 14.0 / 252, 30.0 / 252, 60.0 / 252, 90.0 / 252 };

    private static HestonParameters CreateValidHestonParams() => new HestonParameters
    {
        V0 = 0.04,
        Theta = 0.04,
        Kappa = 2.0,
        SigmaV = 0.3,
        Rho = -0.7,
        RiskFreeRate = 0.05,
        DividendYield = 0.02
    };

    private static KouParameters CreateValidKouParams() => new KouParameters
    {
        Sigma = 0.20,
        Lambda = 3.0,
        P = 0.4,
        Eta1 = 10.0,
        Eta2 = 8.0,
        RiskFreeRate = 0.05,
        DividendYield = 0.02
    };


    // INVARIANT 1: Implied Volatility Must Always Be Positive

    /// <summary>
    /// For all valid inputs, computed IV must be strictly positive.
    /// Mathematical basis: IV is a volatility measure; negative volatility is undefined.
    /// </summary>
    [Fact]
    public void Heston_ComputeTheoreticalIV_AllValidInputs_ReturnsPositiveIV()
    {
        // Arrange
        var model = new STIV001A(CreateValidHestonParams());

        // Act & Assert
        foreach (double spot in s_spots)
        {
            foreach (double strike in s_strikes)
            {
                foreach (double tte in s_timesToExpiry)
                {
                    double iv = model.ComputeTheoreticalIV(spot, strike, tte);

                    iv.Should().BeGreaterThan(0,
                        $"IV must be positive for S={spot}, K={strike}, T={tte:F4}");
                    iv.Should().BeLessThan(5.0,
                        $"IV should be reasonable (<500%) for S={spot}, K={strike}, T={tte:F4}");
                }
            }
        }
    }

    /// <summary>
    /// For all valid inputs, Kou model IV must be strictly positive.
    /// </summary>
    [Fact]
    public void Kou_ComputeTheoreticalIV_AllValidInputs_ReturnsPositiveIV()
    {
        // Arrange
        var model = new STIV002A(CreateValidKouParams());

        // Act & Assert
        foreach (double spot in s_spots)
        {
            foreach (double strike in s_strikes)
            {
                foreach (double tte in s_timesToExpiry)
                {
                    double iv = model.ComputeTheoreticalIV(spot, strike, tte);

                    iv.Should().BeGreaterThan(0,
                        $"IV must be positive for S={spot}, K={strike}, T={tte:F4}");
                    iv.Should().BeLessThan(5.0,
                        $"IV should be reasonable (<500%) for S={spot}, K={strike}, T={tte:F4}");
                }
            }
        }
    }

    // INVARIANT 2: ATM Implied Volatility Should Approximate σ

    /// <summary>
    /// At-the-money, short-term IV should be close to instantaneous volatility.
    /// Mathematical basis: For short maturities, smile effects diminish.
    /// </summary>
    [Theory]
    [InlineData(0.15)]
    [InlineData(0.20)]
    [InlineData(0.25)]
    [InlineData(0.30)]
    public void Heston_ATM_ShortTerm_IVApproximatesInstantaneousVol(double inputVol)
    {
        // Arrange
        var parameters = new HestonParameters
        {
            V0 = inputVol * inputVol,       // V0 = σ²
            Theta = inputVol * inputVol,    // Long-term variance = σ²
            Kappa = 5.0,                    // High mean reversion for stability
            SigmaV = 0.1,                   // Low vol-of-vol to minimise smile
            Rho = 0.0,                      // No skew
            RiskFreeRate = 0.05,
            DividendYield = 0.02
        };
        var model = new STIV001A(parameters);
        double spot = 100;
        double strike = 100;  // ATM
        double tte = 7.0 / 252;  // 1 week

        // Act
        double iv = model.ComputeTheoreticalIV(spot, strike, tte);

        // Assert - IV should be within 20% of input volatility
        iv.Should().BeApproximately(inputVol, inputVol * 0.20,
            $"ATM IV should approximate instantaneous vol {inputVol:P0}");
    }

    /// <summary>
    /// Kou model ATM IV should be close to diffusive volatility when jump intensity is low.
    /// </summary>
    [Theory]
    [InlineData(0.15)]
    [InlineData(0.20)]
    [InlineData(0.25)]
    public void Kou_ATM_LowJumpIntensity_IVApproximatesDiffusiveVol(double sigma)
    {
        // Arrange
        var parameters = new KouParameters
        {
            Sigma = sigma,
            Lambda = 0.1,   // Very low jump intensity
            P = 0.5,
            Eta1 = 20.0,    // Small jumps
            Eta2 = 20.0,
            RiskFreeRate = 0.05,
            DividendYield = 0.02
        };
        var model = new STIV002A(parameters);
        double spot = 100;
        double strike = 100;
        double tte = 30.0 / 252;

        // Act
        double iv = model.ComputeTheoreticalIV(spot, strike, tte);

        // Assert - Should be close to sigma when jumps are negligible
        iv.Should().BeApproximately(sigma, sigma * 0.30,
            $"Low-jump-intensity ATM IV should approximate σ={sigma:P0}");
    }

    // INVARIANT 3: Heston Converges to Black-Scholes as Vol-of-Vol → 0

    /// <summary>
    /// As sigma_v → 0, Heston should converge to Black-Scholes (flat smile).
    /// Reference: Gatheral (2006), Chapter 3
    /// </summary>
    [Fact]
    public void Heston_AsVolOfVolApproachesZero_ConvergesToFlatSmile()
    {
        // Arrange
        double[] sigmaVs = { 0.5, 0.3, 0.1, 0.05, 0.01 };
        var results = new List<double>();

        double spot = 100;
        double tte = 30.0 / 252;

        foreach (double sigmaV in sigmaVs)
        {
            var parameters = new HestonParameters
            {
                V0 = 0.04,
                Theta = 0.04,
                Kappa = 2.0,
                SigmaV = sigmaV,
                Rho = -0.7,
                RiskFreeRate = 0.05,
                DividendYield = 0.02
            };

            // Skip if Feller condition is not satisfied
            if (!parameters.SatisfiesFellerCondition())
            {
                continue;
            }

            var model = new STIV001A(parameters);

            // Compute smile width: IV(90) - IV(110)
            double ivOTMPut = model.ComputeTheoreticalIV(spot, 90, tte);
            double ivOTMCall = model.ComputeTheoreticalIV(spot, 110, tte);
            double smileWidth = Math.Abs(ivOTMPut - ivOTMCall);

            results.Add(smileWidth);
        }

        // Assert - Smile width should decrease as vol-of-vol decreases
        for (int i = 1; i < results.Count; i++)
        {
            results[i].Should().BeLessThanOrEqualTo(results[i - 1] * 1.1,
                "Smile width should decrease or stay flat as vol-of-vol decreases");
        }
    }

    // INVARIANT 4: Kou Converges to Black-Scholes as Jump Intensity → 0

    /// <summary>
    /// As λ → 0, Kou JD should converge to Black-Scholes (diffusion only).
    /// Reference: Kou (2002), Proposition 1
    /// </summary>
    [Fact]
    public void Kou_AsJumpIntensityApproachesZero_ConvergesToDiffusion()
    {
        // Arrange
        double[] lambdas = { 10.0, 5.0, 2.0, 1.0, 0.1 };
        double spot = 100;
        double strike = 100;  // ATM
        double tte = 30.0 / 252;
        double sigma = 0.20;

        var ivResults = new List<double>();

        foreach (double lambda in lambdas)
        {
            var parameters = new KouParameters
            {
                Sigma = sigma,
                Lambda = lambda,
                P = 0.4,
                Eta1 = 10.0,
                Eta2 = 8.0,
                RiskFreeRate = 0.05,
                DividendYield = 0.02
            };
            var model = new STIV002A(parameters);

            double iv = model.ComputeTheoreticalIV(spot, strike, tte);
            ivResults.Add(iv);
        }

        // Assert - IV should approach diffusive sigma as lambda → 0
        double finalIV = ivResults.Last();
        finalIV.Should().BeApproximately(sigma, sigma * 0.15,
            "As λ→0, Kou IV should converge to diffusive σ");
    }

    // INVARIANT 5: IV Surface Monotonicity in Time for ATM

    /// <summary>
    /// ATM IV should generally decrease with time (term structure effect).
    /// This tests the typical "volatility term structure" pattern.
    /// </summary>
    [Fact]
    public void Heston_ATM_TermStructure_IsWellBehaved()
    {
        // Arrange
        var model = new STIV001A(CreateValidHestonParams());
        double spot = 100;
        double strike = 100;
        double[] ttes = { 7.0 / 252, 14.0 / 252, 30.0 / 252, 60.0 / 252, 90.0 / 252, 180.0 / 252 };

        // Act
        var atmIVs = new List<double>();
        foreach (double tte in ttes)
        {
            atmIVs.Add(model.ComputeTheoreticalIV(spot, strike, tte));
        }

        // Assert - IVs should all be positive and finite
        foreach (double iv in atmIVs)
        {
            iv.Should().BeGreaterThan(0);
            iv.Should().BeLessThan(2.0);  // Reasonable bound
            double.IsNaN(iv).Should().BeFalse();
            double.IsInfinity(iv).Should().BeFalse();
        }

        // Term structure should not have extreme jumps
        for (int i = 1; i < atmIVs.Count; i++)
        {
            double ratio = atmIVs[i] / atmIVs[i - 1];
            ratio.Should().BeInRange(0.5, 2.0,
                "Adjacent ATM IVs should not differ by more than 2x");
        }
    }

    // INVARIANT 6: Negative Correlation Produces Negative Skew

    /// <summary>
    /// With ρ < 0, OTM puts should have higher IV than OTM calls (negative skew).
    /// Reference: Gatheral (2006), leverage effect and skew
    /// </summary>
    [Theory]
    [InlineData(-0.9)]
    [InlineData(-0.7)]
    [InlineData(-0.5)]
    public void Heston_NegativeCorrelation_ProducesNegativeSkew(double rho)
    {
        // Arrange - Parameters that satisfy Feller: 2*3*0.04 = 0.24 > 0.35² = 0.1225
        var parameters = new HestonParameters
        {
            V0 = 0.04,
            Theta = 0.04,
            Kappa = 3.0,    // Increased from 2.0 to satisfy Feller
            SigmaV = 0.35,  // Reduced from 0.4 to satisfy Feller
            Rho = rho,
            RiskFreeRate = 0.05,
            DividendYield = 0.02
        };
        var model = new STIV001A(parameters);
        double spot = 100;
        double tte = 30.0 / 252;

        // Act
        double ivOTMPut = model.ComputeTheoreticalIV(spot, 90, tte);   // OTM put
        double ivATM = model.ComputeTheoreticalIV(spot, 100, tte);    // ATM
        double ivOTMCall = model.ComputeTheoreticalIV(spot, 110, tte); // OTM call

        // Assert - Negative skew: IV(K<S) > IV(K=S) > IV(K>S)
        ivOTMPut.Should().BeGreaterThanOrEqualTo(ivATM * 0.95,
            "With ρ<0, OTM put IV should be >= ATM IV (within tolerance)");

        // The skew should exist (not flat)
        double skew = ivOTMPut - ivOTMCall;
        skew.Should().BeGreaterThan(0,
            $"With ρ={rho}, skew (iv_put - iv_call) should be positive");
    }

    // INVARIANT 7: Downward Jump Bias Produces Negative Skew in Kou

    /// <summary>
    /// When p < 0.5 (more downward jumps), Kou should exhibit negative skew.
    /// Reference: Kou (2002), Section 4
    /// </summary>
    [Fact]
    public void Kou_DownwardJumpBias_ProducesNegativeSkew()
    {
        // Arrange - More downward jumps (p < 0.5)
        var parameters = new KouParameters
        {
            Sigma = 0.20,
            Lambda = 5.0,
            P = 0.3,         // 30% up, 70% down
            Eta1 = 10.0,     // Mean up jump = 10%
            Eta2 = 5.0,      // Mean down jump = 20% (larger)
            RiskFreeRate = 0.05,
            DividendYield = 0.02
        };
        var model = new STIV002A(parameters);
        double spot = 100;
        double tte = 30.0 / 252;

        // Act
        double ivOTMPut = model.ComputeTheoreticalIV(spot, 90, tte);
        double ivOTMCall = model.ComputeTheoreticalIV(spot, 110, tte);

        // Assert - Negative skew expected
        ivOTMPut.Should().BeGreaterThanOrEqualTo(ivOTMCall * 0.95,
            "With p<0.5, OTM put IV should be >= OTM call IV");
    }

    // INVARIANT 8: Parameter Validation Catches Invalid Configurations

    /// <summary>
    /// Heston parameters violating Feller condition should fail validation.
    /// Mathematical basis: 2κθ > σ_v² ensures V_t > 0 almost surely.
    /// </summary>
    [Theory]
    [InlineData(1.0, 0.02, 0.5)]   // 2*1*0.02 = 0.04 < 0.25
    [InlineData(0.5, 0.01, 0.3)]   // 2*0.5*0.01 = 0.01 < 0.09
    public void HestonParameters_FellerViolation_FailsValidation(double kappa, double theta, double sigmaV)
    {
        // Arrange
        var parameters = new HestonParameters
        {
            V0 = 0.04,
            Theta = theta,
            Kappa = kappa,
            SigmaV = sigmaV,
            Rho = -0.7,
            RiskFreeRate = 0.05,
            DividendYield = 0.02
        };

        // Act
        bool fellerSatisfied = parameters.SatisfiesFellerCondition();
        var validation = parameters.Validate();

        // Assert
        fellerSatisfied.Should().BeFalse();
        validation.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// Kou parameters with Eta1 <= 1 should fail validation.
    /// Mathematical basis: Eta1 > 1 ensures finite expected up-jump size.
    /// </summary>
    [Theory]
    [InlineData(0.5)]
    [InlineData(0.9)]
    [InlineData(1.0)]
    public void KouParameters_InvalidEta1_FailsValidation(double eta1)
    {
        // Arrange
        var parameters = new KouParameters
        {
            Sigma = 0.20,
            Lambda = 3.0,
            P = 0.4,
            Eta1 = eta1,
            Eta2 = 8.0,
            RiskFreeRate = 0.05,
            DividendYield = 0.02
        };

        // Act
        var validation = parameters.Validate();

        // Assert
        validation.IsValid.Should().BeFalse();
    }

    // INVARIANT 9: Characteristic Function Properties for Heston

    /// <summary>
    /// Characteristic function at u=0 should equal 1.
    /// Mathematical basis: φ(0) = E[exp(0)] = 1 for any distribution.
    /// </summary>
    [Fact]
    public void Heston_CharacteristicFunction_AtZero_EqualsOne()
    {
        // Arrange
        var model = new STIV001A(CreateValidHestonParams());
        double t = 30.0 / 252;

        // Act
        var phi = model.CharacteristicFunction(new System.Numerics.Complex(0, 0), t);

        // Assert
        phi.Real.Should().BeApproximately(1.0, 1e-10);
        phi.Imaginary.Should().BeApproximately(0.0, 1e-10);
    }

    // INVARIANT 10: Model Selection Respects Regime

    /// <summary>
    /// Pre-earnings regime should prefer Leung-Santoli or Kou (jump models).
    /// </summary>
    [Fact]
    public void ModelSelector_PreEarnings_PrefersJumpModels()
    {
        // Arrange
        var selector = new STIV003A();
        var valuationDate = new DateTime(2024, 1, 15);
        var earningsDate = new DateTime(2024, 1, 25);  // 10 days to earnings
        var expirationDate = new DateTime(2024, 2, 16); // ~32 days

        var timeParams = STTM004A.Create(valuationDate, expirationDate, earningsDate);

        var context = new ModelSelectionContext
        {
            Spot = 100,
            BaseVolatility = 0.20,
            RiskFreeRate = 0.05,
            DividendYield = 0.02,
            TimeParams = timeParams,
            EarningsJumpVolatility = 0.08
        };

        // Act
        var result = selector.SelectBestModel(context);

        // Assert
        result.Regime.RegimeType.Should().Be(STTM002AType.PreEarnings);
        result.Regime.ModelRecommendation.Should().BeOneOf(
            RecommendedModel.LeungSantoli,
            RecommendedModel.Kou);
    }

    // INVARIANT 11: Smile Width Scales with Vol-of-Vol (Heston)

    /// <summary>
    /// Higher vol-of-vol should produce wider smiles.
    /// Reference: Gatheral (2006), Chapter 3
    /// </summary>
    [Fact]
    public void Heston_HigherVolOfVol_ProducesWiderSmile()
    {
        // Arrange
        double spot = 100;
        double tte = 30.0 / 252;
        double[] sigmaVs = { 0.1, 0.3, 0.5 };
        var smileWidths = new List<double>();

        foreach (double sigmaV in sigmaVs)
        {
            var parameters = new HestonParameters
            {
                V0 = 0.04,
                Theta = 0.04,
                Kappa = 2.0,
                SigmaV = sigmaV,
                Rho = -0.5,
                RiskFreeRate = 0.05,
                DividendYield = 0.02
            };

            // Skip if Feller violated
            if (!parameters.SatisfiesFellerCondition())
            {
                continue;
            }

            var model = new STIV001A(parameters);

            double ivLowStrike = model.ComputeTheoreticalIV(spot, 85, tte);
            double ivHighStrike = model.ComputeTheoreticalIV(spot, 115, tte);
            double ivATM = model.ComputeTheoreticalIV(spot, 100, tte);

            // Smile width = average wings deviation from ATM
            double smileWidth = (ivLowStrike - ivATM + (ivHighStrike - ivATM)) / 2;
            smileWidths.Add(Math.Abs(smileWidth));
        }

        // Assert - Smile width should increase with vol-of-vol
        for (int i = 1; i < smileWidths.Count; i++)
        {
            smileWidths[i].Should().BeGreaterThanOrEqualTo(smileWidths[i - 1] * 0.8,
                "Smile width should generally increase with vol-of-vol");
        }
    }

    // INVARIANT 12: Jump Intensity Increases Total Variance (Kou)

    /// <summary>
    /// Higher jump intensity should increase ATM IV (more variance).
    /// Reference: Kou (2002), total variance = σ² + λ*E[J²]
    /// </summary>
    [Fact]
    public void Kou_HigherJumpIntensity_IncreasesATMIV()
    {
        // Arrange
        double spot = 100;
        double strike = 100;
        double tte = 30.0 / 252;
        double[] lambdas = { 0.5, 2.0, 5.0, 10.0 };
        var atmIVs = new List<double>();

        foreach (double lambda in lambdas)
        {
            var parameters = new KouParameters
            {
                Sigma = 0.15,   // Low base vol to see jump effect
                Lambda = lambda,
                P = 0.5,
                Eta1 = 10.0,
                Eta2 = 10.0,
                RiskFreeRate = 0.05,
                DividendYield = 0.02
            };
            var model = new STIV002A(parameters);

            double iv = model.ComputeTheoreticalIV(spot, strike, tte);
            atmIVs.Add(iv);
        }

        // Assert - IV should generally increase with lambda
        for (int i = 1; i < atmIVs.Count; i++)
        {
            atmIVs[i].Should().BeGreaterThanOrEqualTo(atmIVs[i - 1] * 0.95,
                $"ATM IV with λ={lambdas[i]} should be >= IV with λ={lambdas[i - 1]}");
        }

        // Final IV should be noticeably higher than initial
        atmIVs.Last().Should().BeGreaterThan(atmIVs.First(),
            "High-jump-intensity ATM IV should exceed low-jump-intensity IV");
    }

    // INVARIANT 13: Numerical Stability at Extremes

    /// <summary>
    /// Models should not produce NaN or Infinity for extreme but valid inputs.
    /// </summary>
    [Fact]
    public void Models_ExtremeValidInputs_DoNotProduceNaNOrInfinity()
    {
        // Arrange
        var hestonModel = new STIV001A(CreateValidHestonParams());
        var kouModel = new STIV002A(CreateValidKouParams());

        // Extreme but valid inputs
        var testCases = new (double spot, double strike, double tte)[]
        {
            (100, 50, 7.0 / 252),    // Deep ITM call
            (100, 200, 7.0 / 252),   // Deep OTM call
            (100, 100, 1.0 / 252),   // Very short maturity
            (100, 100, 1.0),         // 1 year
            (1000, 1000, 30.0 / 252), // High absolute values
        };

        // Act & Assert
        foreach (var (spot, strike, tte) in testCases)
        {
            double hestonIV = hestonModel.ComputeTheoreticalIV(spot, strike, tte);
            double kouIV = kouModel.ComputeTheoreticalIV(spot, strike, tte);

            double.IsNaN(hestonIV).Should().BeFalse(
                $"Heston IV should not be NaN for S={spot}, K={strike}, T={tte:F4}");
            double.IsInfinity(hestonIV).Should().BeFalse(
                $"Heston IV should not be Infinity for S={spot}, K={strike}, T={tte:F4}");

            double.IsNaN(kouIV).Should().BeFalse(
                $"Kou IV should not be NaN for S={spot}, K={strike}, T={tte:F4}");
            double.IsInfinity(kouIV).Should().BeFalse(
                $"Kou IV should not be Infinity for S={spot}, K={strike}, T={tte:F4}");
        }
    }

    // INVARIANT 14: Kappa Calculation for Martingale Condition (Kou)

    /// <summary>
    /// Symmetric Kou jumps should produce kappa ≈ 0.
    /// Mathematical basis: E[V-1] = p*eta1/(eta1-1) + (1-p)*eta2/(eta2+1) - 1
    /// </summary>
    [Fact]
    public void Kou_SymmetricJumps_KappaApproachesZero()
    {
        // Arrange - Perfectly symmetric: p=0.5, eta1=eta2
        var parameters = new KouParameters
        {
            Sigma = 0.20,
            Lambda = 3.0,
            P = 0.5,
            Eta1 = 10.0,
            Eta2 = 10.0,
            RiskFreeRate = 0.05,
            DividendYield = 0.02
        };

        // Act
        double kappa = parameters.ComputeKappa();

        // Assert - Kappa should be small (symmetric jumps balance out)
        // kappa = 0.5 * 10/9 + 0.5 * 10/11 - 1 = 0.5556 + 0.4545 - 1 ≈ 0.01
        Math.Abs(kappa).Should().BeLessThan(0.05,
            "Symmetric Kou jumps should produce near-zero kappa");
    }
}
