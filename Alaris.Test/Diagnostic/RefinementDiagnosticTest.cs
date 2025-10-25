using System;
using Xunit;
using FluentAssertions;
using Alaris.Double;

namespace Alaris.Test.Diagnostic;

/// <summary>
/// Diagnostic test to trace QD+ refinement equation evaluation.
/// This helps identify why boundaries converge to incorrect values.
/// </summary>
public class RefinementDiagnosticTest
{
    // Test parameters from Healy Table 2
    private const double Strike = 100.0;
    private const double Maturity = 10.0;
    private const double Rate = -0.005;
    private const double DividendYield = -0.01;
    private const double Volatility = 0.08;
    
    [Fact]
    public void DiagnosticRefinementEquation_TracesBoundaryConvergence()
    {
        // Arrange
        var approximation = new QdPlusApproximation(
            spot: 100.0,
            strike: Strike,
            maturity: Maturity,
            rate: Rate,
            dividendYield: DividendYield,
            volatility: Volatility,
            isCall: false
        );
        
        // Calculate lambda values
        double h = 1.0 - Math.Exp(-Rate * Maturity);
        double sigma2 = Volatility * Volatility;
        double alpha = 2.0 * Rate / sigma2;
        double beta = 2.0 * (Rate - DividendYield) / sigma2;
        double discriminant = Math.Sqrt((beta - 1.0) * (beta - 1.0) + 4.0 * alpha / h);
        double lambda1 = (-(beta - 1.0) - discriminant) / 2.0;
        double lambda2 = (-(beta - 1.0) + discriminant) / 2.0;
        
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("QD+ REFINEMENT EQUATION DIAGNOSTIC");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
        Console.WriteLine($"Parameters: K={Strike}, T={Maturity}, σ={Volatility}, r={Rate}, q={DividendYield}");
        Console.WriteLine();
        Console.WriteLine($"h = {h:F6} (negative for r < 0)");
        Console.WriteLine($"α = {alpha:F6}");
        Console.WriteLine($"β = {beta:F6}");
        Console.WriteLine($"λ₁ = {lambda1:F6} (negative root)");
        Console.WriteLine($"λ₂ = {lambda2:F6} (positive root)");
        Console.WriteLine();
        
        // Test boundary values
        double[] testBoundaries = { 60.80, 64.16, 69.62, 75.0, 100.0 };
        
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("REFINEMENT EQUATION EVALUATION AT DIFFERENT BOUNDARIES");
        Console.WriteLine("=".PadRight(80, '='));
        
        foreach (double S in testBoundaries)
        {
            EvaluateAtBoundary(S, lambda1, h, alpha, beta);
        }
        
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ANALYSIS");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
        Console.WriteLine("The boundary converges where f(S*) = 0.");
        Console.WriteLine("Current implementation converges to S* = 64.16");
        Console.WriteLine("With negated theta, converges to S* = 60.80");
        Console.WriteLine("Expected value from Healy: S* = 69.62");
        Console.WriteLine();
        Console.WriteLine("Look for which S* value gives f(S*) closest to zero.");
        
        // Assert: This test is for diagnostic purposes, always passes
        true.Should().BeTrue("This is a diagnostic test");
    }
    
    private void EvaluateAtBoundary(double S, double lambda1, double h, double alpha, double beta)
    {
        Console.WriteLine($"\nS* = {S:F2}");
        Console.WriteLine("-".PadRight(40, '-'));
        
        // Calculate components
        double eta = -1.0; // Put option
        double VE = CalculateEuropeanPut(S);
        double intrinsic = -1.0 * (S - Strike);  // K - S for put
        double earlyExPremium = intrinsic - VE;
        
        Console.WriteLine($"  European Value (VE): {VE:F4}");
        Console.WriteLine($"  Intrinsic (K - S*): {intrinsic:F4}");
        Console.WriteLine($"  Early Ex Premium: {earlyExPremium:F4}");
        
        if (earlyExPremium <= 0)
        {
            Console.WriteLine("  WARNING: Early exercise premium is non-positive!");
        }
        
        // Calculate theta (both conventions)
        double theta_dt = CalculateThetaPut(S);
        double theta_dtau = -theta_dt;
        
        Console.WriteLine($"  Theta (dV/dt): {theta_dt:F6} (standard, negative)");
        Console.WriteLine($"  Theta (dV/dτ): {theta_dtau:F6} (Healy convention?)");
        
        // Calculate lambda derivative
        double discriminant = Math.Sqrt((beta - 1.0) * (beta - 1.0) + 4.0 * alpha / h);
        double dLambda1Dh = alpha / (h * h * discriminant);
        
        // Calculate c0 with both theta values
        double c0_dt = CalculateC0(S, lambda1, dLambda1Dh, alpha, beta, h, theta_dt, VE);
        double c0_dtau = CalculateC0(S, lambda1, dLambda1Dh, alpha, beta, h, theta_dtau, VE);
        
        Console.WriteLine($"  c₀ (with dV/dt): {c0_dt:F6}");
        Console.WriteLine($"  c₀ (with dV/dτ): {c0_dtau:F6}");
        Console.WriteLine($"  λ₁ + c₀ (dt): {lambda1 + c0_dt:F6}");
        Console.WriteLine($"  λ₁ + c₀ (dτ): {lambda1 + c0_dtau:F6}");
        
        // Evaluate refinement equation
        double f_dt = EvaluateRefinement(S, lambda1, c0_dt, VE);
        double f_dtau = EvaluateRefinement(S, lambda1, c0_dtau, VE);
        
        Console.WriteLine($"  f(S*) with dV/dt: {f_dt:F6}");
        Console.WriteLine($"  f(S*) with dV/dτ: {f_dtau:F6}");
        
        if (Math.Abs(f_dt) < 1e-4)
            Console.WriteLine("  -> Close to root with dV/dt!");
        if (Math.Abs(f_dtau) < 1e-4)
            Console.WriteLine("  -> Close to root with dV/dτ!");
    }
    
    private double CalculateEuropeanPut(double S)
    {
        double sqrtT = Math.Sqrt(Maturity);
        double d1 = (Math.Log(S / Strike) + (Rate - DividendYield + 0.5 * Volatility * Volatility) * Maturity) / (Volatility * sqrtT);
        double d2 = d1 - Volatility * sqrtT;
        
        return Strike * Math.Exp(-Rate * Maturity) * NormalCDF(-d2) - S * Math.Exp(-DividendYield * Maturity) * NormalCDF(-d1);
    }
    
    private double CalculateThetaPut(double S)
    {
        double sqrtT = Math.Sqrt(Maturity);
        double d1 = (Math.Log(S / Strike) + (Rate - DividendYield + 0.5 * Volatility * Volatility) * Maturity) / (Volatility * sqrtT);
        double d2 = d1 - Volatility * sqrtT;
        
        // Standard theta (dV/dt) for a put
        double term1 = -(S * Math.Exp(-DividendYield * Maturity) * NormalPDF(d1) * Volatility) / (2.0 * sqrtT);
        double term2 = -DividendYield * S * Math.Exp(-DividendYield * Maturity) * NormalCDF(-d1);
        double term3 = Rate * Strike * Math.Exp(-Rate * Maturity) * NormalCDF(-d2);
        
        return term1 + term2 + term3;
    }
    
    private double CalculateC0(double S, double lambda, double dLambdaDh, double alpha, double beta, 
        double h, double theta, double VE)
    {
        double eta = -1.0; // Put
        double intrinsic = eta * (S - Strike);  // K - S for put
        double denominator = intrinsic - VE;
        
        if (Math.Abs(denominator) < 1e-10)
            return dLambdaDh / (2.0 * lambda + beta - 1.0);
        
        double lambdaDenom = 2.0 * lambda + beta - 1.0;
        if (Math.Abs(lambdaDenom) < 1e-10)
            return 0.0;
        
        // c0 formula from Healy
        double factor1 = -((1.0 - h) * alpha) / lambdaDenom;
        double term1 = 1.0 / h;
        double term2 = theta / (Rate * denominator);
        double part1 = factor1 * (term1 - term2);
        double part2 = dLambdaDh / lambdaDenom;
        
        return part1 + part2;
    }
    
    private double EvaluateRefinement(double S, double lambda, double c0, double VE)
    {
        double eta = -1.0; // Put
        double sqrtT = Math.Sqrt(Maturity);
        double d1 = (Math.Log(S / Strike) + (Rate - DividendYield + 0.5 * Volatility * Volatility) * Maturity) / (Volatility * sqrtT);
        
        // Refinement equation: η - η*exp(-qT)*Φ(η*d1) - (λ+c0)*(η(S*-K)-VE)/S* = 0
        double lhs = eta;
        double term1 = eta * Math.Exp(-DividendYield * Maturity) * NormalCDF(eta * d1);
        double intrinsic = eta * (S - Strike);
        double term2 = (lambda + c0) * (intrinsic - VE) / S;
        
        return lhs - term1 - term2;
    }
    
    private double NormalCDF(double x)
    {
        if (x > 8.0) return 1.0;
        if (x < -8.0) return 0.0;
        return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));
    }
    
    private double NormalPDF(double x)
    {
        return Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);
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
        x = Math.Abs(x);
        
        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
        
        return sign * y;
    }
    
    [Fact]
    public void DiagnosticBoundaryValues_ShowsCurrentConvergence()
    {
        // Arrange & Act
        var approximation = new QdPlusApproximation(
            spot: 100.0,
            strike: Strike,
            maturity: Maturity,
            rate: Rate,
            dividendYield: DividendYield,
            volatility: Volatility,
            isCall: false
        );
        
        var (upper, lower) = approximation.CalculateBoundaries();
        
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("CURRENT IMPLEMENTATION RESULTS");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
        Console.WriteLine($"Upper Boundary: {upper:F4}");
        Console.WriteLine($"Lower Boundary: {lower:F4}");
        Console.WriteLine();
        Console.WriteLine($"Expected Upper: 69.62 (Error: {Math.Abs(upper - 69.62):F2})");
        Console.WriteLine($"Expected Lower: 58.72 (Error: {Math.Abs(lower - 58.72):F2})");
        Console.WriteLine();
        
        // Assert: Document current state
        upper.Should().BeGreaterThan(0, "boundary should be positive");
        lower.Should().BeGreaterThan(0, "boundary should be positive");
    }
}