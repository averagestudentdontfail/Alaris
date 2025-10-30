using System;
using Xunit;
using Xunit.Abstractions;
using Alaris.Double;

namespace Alaris.Test.Diagnostic;

/// <summary>
/// Diagnostic tests to debug QD+ convergence issues.
/// </summary>
public class QdPlusDiagnostic
{
    private readonly ITestOutputHelper _output;
    
    public QdPlusDiagnostic(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [Fact]
    public void DiagnoseBoundaryConvergence()
    {
        // Healy benchmark parameters
        double spot = 100.0;
        double strike = 100.0;
        double maturity = 10.0;
        double rate = -0.005;
        double dividendYield = -0.01;
        double volatility = 0.08;
        
        _output.WriteLine("=".PadRight(80, '='));
        _output.WriteLine("QD+ BOUNDARY CONVERGENCE DIAGNOSTIC");
        _output.WriteLine("=".PadRight(80, '='));
        _output.WriteLine($"Parameters: K={strike}, T={maturity}, r={rate}, q={dividendYield}, σ={volatility}");
        _output.WriteLine("");
        
        var approximation = new QdPlusApproximation(
            spot, strike, maturity, rate, dividendYield, volatility, isCall: false);
        
        // Calculate parameters
        double h = 1.0 - Math.Exp(-rate * maturity);
        double sigma2 = volatility * volatility;
        double omega = 2.0 * (rate - dividendYield) / sigma2;
        
        _output.WriteLine($"Intermediate values:");
        _output.WriteLine($"  h = {h:F6}");
        _output.WriteLine($"  ω = {omega:F6}");
        _output.WriteLine("");
        
        // Calculate lambda roots
        double discriminant = (omega - 1.0) * (omega - 1.0) + 8.0 * rate / (sigma2 * h);
        _output.WriteLine($"Lambda calculation:");
        _output.WriteLine($"  Discriminant = {discriminant:F6}");
        
        double sqrtDiscriminant = Math.Sqrt(discriminant);
        double lambda1 = (-(omega - 1.0) + sqrtDiscriminant) / 2.0;
        double lambda2 = (-(omega - 1.0) - sqrtDiscriminant) / 2.0;
        
        _output.WriteLine($"  λ₁ = {lambda1:F6}");
        _output.WriteLine($"  λ₂ = {lambda2:F6}");
        _output.WriteLine("");
        
        // For puts with r < 0: upper uses negative lambda, lower uses positive lambda
        double lambdaUpper = Math.Min(lambda1, lambda2);
        double lambdaLower = Math.Max(lambda1, lambda2);
        
        _output.WriteLine($"Lambda assignment:");
        _output.WriteLine($"  λ_upper = {lambdaUpper:F6} (negative root)");
        _output.WriteLine($"  λ_lower = {lambdaLower:F6} (positive root)");
        _output.WriteLine("");
        
        // Test initial guesses
        double sqrtT = Math.Sqrt(maturity);
        double upperInitial = strike * (0.70 - 0.01 * sqrtT);
        double lowerInitial = strike * (0.60 - 0.01 * sqrtT);
        
        _output.WriteLine($"Initial guesses:");
        _output.WriteLine($"  Upper: {upperInitial:F4}");
        _output.WriteLine($"  Lower: {lowerInitial:F4}");
        _output.WriteLine("");
        
        // Calculate actual boundaries
        var (upper, lower) = approximation.CalculateBoundaries();
        
        _output.WriteLine($"Final boundaries:");
        _output.WriteLine($"  Upper: {upper:F4}");
        _output.WriteLine($"  Lower: {lower:F4}");
        _output.WriteLine($"  Spread: {(upper - lower):F4}");
        _output.WriteLine("");
        
        // Compare to expected Healy values
        _output.WriteLine($"Expected (Healy Table 2):");
        _output.WriteLine($"  Upper: ~69.62");
        _output.WriteLine($"  Lower: ~58.72");
        _output.WriteLine("");
        
        _output.WriteLine($"Deviations:");
        _output.WriteLine($"  Upper error: {(upper - 69.62):F4} ({(upper - 69.62) / 69.62 * 100:F2}%)");
        _output.WriteLine($"  Lower error: {(lower - 58.72):F4} ({(lower - 58.72) / 58.72 * 100:F2}%)");
        _output.WriteLine("");
        
        if (Math.Abs(upper - upperInitial) < 0.01)
        {
            _output.WriteLine("⚠️ WARNING: Upper boundary unchanged from initial guess!");
            _output.WriteLine("   Super Halley may not be converging.");
        }
        
        if (Math.Abs(lower - lowerInitial) < 0.01)
        {
            _output.WriteLine("⚠️ WARNING: Lower boundary unchanged from initial guess!");
            _output.WriteLine("   Super Halley may not be converging.");
        }
    }
}