using System;
using Xunit;
using Xunit.Abstractions;
using Alaris.Double;

namespace Alaris.Test.Diagnostic;

/// <summary>
/// Deep diagnostic to trace Super Halley iteration step-by-step.
/// </summary>
public class SuperHalleyDiagnostic
{
    private readonly ITestOutputHelper _output;
    
    public SuperHalleyDiagnostic(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [Fact]
    public void TraceUpperBoundaryIteration()
    {
        // Healy benchmark parameters
        double spot = 100.0;
        double strike = 100.0;
        double maturity = 10.0;
        double rate = -0.005;
        double dividendYield = -0.01;
        double volatility = 0.08;
        
        _output.WriteLine("=".PadRight(80, '='));
        _output.WriteLine("SUPER HALLEY ITERATION TRACE - UPPER BOUNDARY");
        _output.WriteLine("=".PadRight(80, '='));
        _output.WriteLine("");
        
        // Create approximation
        var approximation = new DBAP001A(
            spot, strike, maturity, rate, dividendYield, volatility, isCall: false);
        
        // Calculate parameters manually to trace
        double h = 1.0 - Math.Exp(-rate * maturity);
        double sigma2 = volatility * volatility;
        double omega = 2.0 * (rate - dividendYield) / sigma2;
        double discriminant = (omega - 1.0) * (omega - 1.0) + 8.0 * rate / (sigma2 * h);
        double sqrtDiscriminant = Math.Sqrt(discriminant);
        double lambda1 = (-(omega - 1.0) + sqrtDiscriminant) / 2.0;
        double lambda2 = (-(omega - 1.0) - sqrtDiscriminant) / 2.0;
        double lambdaUpper = Math.Min(lambda1, lambda2);
        
        _output.WriteLine($"Lambda (upper): {lambdaUpper:F6}");
        _output.WriteLine($"h: {h:F6}");
        _output.WriteLine("");
        
        // Test the boundary equation at initial guess
        double sqrtT = Math.Sqrt(maturity);
        double S0 = strike * (0.70 - 0.01 * sqrtT);
        
        _output.WriteLine($"Initial guess S0: {S0:F4}");
        _output.WriteLine("");
        _output.WriteLine("Testing boundary function at initial guess:");
        
        // We need to test f, df, d2f values
        // Since we can't call private methods, we'll calculate boundaries and check results
        var (upper, lower) = approximation.CalculateBoundaries();
        
        _output.WriteLine($"Final upper boundary: {upper:F4}");
        _output.WriteLine("");
        
        if (Math.Abs(upper - S0) < 0.01)
        {
            _output.WriteLine("CRITICAL: Upper boundary = initial guess!");
            _output.WriteLine("   Super Halley exited immediately without iterating.");
            _output.WriteLine("");
            _output.WriteLine("Possible causes:");
            _output.WriteLine("  1. f(S0) ≈ 0 (boundary equation already satisfied at S0)");
            _output.WriteLine("  2. df ≈ 0 (derivative too small, causing huge correction)");
            _output.WriteLine("  3. NaN/Inf in calculations (breaking iteration)");
            _output.WriteLine("  4. Convergence tolerance too loose");
        }
        else
        {
            _output.WriteLine($"✓ Super Halley iterated: moved {Math.Abs(upper - S0):F4} from initial guess");
        }
        
        // Test expected value
        double expectedUpper = 69.62;
        double error = upper - expectedUpper;
        double errorPct = error / expectedUpper * 100.0;
        
        _output.WriteLine("");
        _output.WriteLine($"Expected: {expectedUpper:F2}");
        _output.WriteLine($"Actual:   {upper:F2}");
        _output.WriteLine($"Error:    {error:F2} ({errorPct:F2}%)");
        
        if (Math.Abs(errorPct) > 5.0)
        {
            _output.WriteLine("");
            _output.WriteLine("FAIL: Error exceeds 5% threshold");
        }
    }
    
    [Fact]
    public void TestBoundaryFunctionSign()
    {
        _output.WriteLine("=".PadRight(80, '='));
        _output.WriteLine("BOUNDARY FUNCTION SIGN TEST");
        _output.WriteLine("=".PadRight(80, '='));
        _output.WriteLine("");
        
        double spot = 100.0;
        double strike = 100.0;
        double maturity = 10.0;
        double rate = -0.005;
        double dividendYield = -0.01;
        double volatility = 0.08;
        
        var approximation = new DBAP001A(
            spot, strike, maturity, rate, dividendYield, volatility, isCall: false);
        
        // Test a range of S values to see where the root should be
        _output.WriteLine("Testing f(S) = S^λ - K^λ*exp(c0) across S values:");
        _output.WriteLine("");
        _output.WriteLine("   S      Should be near zero at correct boundary");
        _output.WriteLine("------    -------------------------------------");
        
        // The expected boundary is around 69.62, so test around that
        for (double S = 60.0; S <= 75.0; S += 2.5)
        {
            // We can't evaluate f directly, but we can infer from the behavior
            _output.WriteLine($"  {S:F1}");
        }
        
        _output.WriteLine("");
        _output.WriteLine("Initial guess: 66.84");
        _output.WriteLine("Expected root: 69.62");
        _output.WriteLine("");
        _output.WriteLine("If Super Halley doesn't move from 66.84, then either:");
        _output.WriteLine("  - f(66.84) ≈ 0 (wrong root location)");
        _output.WriteLine("  - f(66.84) / df(66.84) ≈ 0 (derivative issue)");
    }
}