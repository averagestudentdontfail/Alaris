using System;
using Alaris.Strategy.Core;

/// <summary>
/// Simple test program to verify probability calculations after fixes.
/// </summary>
class TestProbabilities
{
    static void Main(string[] args)
    {
        Console.WriteLine("Testing Heston Probability Calculations");
        Console.WriteLine("=========================================\n");

        // Test parameters
        var hestonParams = HestonParameters.DefaultEquity;
        double spot = 100.0;
        double strike = 100.0; // ATM
        double timeToExpiry = 30.0 / 252.0; // 30 days

        Console.WriteLine("Parameters:");
        Console.WriteLine($"  Spot: {spot}");
        Console.WriteLine($"  Strike: {strike}");
        Console.WriteLine($"  Time to Expiry: {timeToExpiry:F4} years");
        Console.WriteLine($"  V0: {hestonParams.V0}");
        Console.WriteLine($"  Theta: {hestonParams.Theta}");
        Console.WriteLine($"  Kappa: {hestonParams.Kappa}");
        Console.WriteLine($"  SigmaV: {hestonParams.SigmaV}");
        Console.WriteLine($"  Rho: {hestonParams.Rho}");
        Console.WriteLine($"  Risk-Free Rate: {hestonParams.RiskFreeRate}");
        Console.WriteLine($"  Dividend Yield: {hestonParams.DividendYield}\n");

        // Test Heston model
        var hestonModel = new HestonModel(hestonParams);

        Console.WriteLine("Testing Heston Model:");
        double hestonIV = hestonModel.ComputeTheoreticalIV(spot, strike, timeToExpiry);
        Console.WriteLine($"  ATM Implied Volatility: {hestonIV:F4}");

        // Test across different strikes
        Console.WriteLine("\nVolatility Smile:");
        Console.WriteLine("  Strike | Moneyness | IV");
        Console.WriteLine("  -------|-----------|------");

        double[] strikes = { 80, 90, 95, 100, 105, 110, 120 };
        foreach (var k in strikes)
        {
            double iv = hestonModel.ComputeTheoreticalIV(spot, k, timeToExpiry);
            double moneyness = k / spot;
            Console.WriteLine($"  {k,6:F1} | {moneyness,9:F3} | {iv:F4}");
        }

        // Test Kou model
        Console.WriteLine("\n\nTesting Kou Model:");
        var kouParams = KouParameters.DefaultEquity;
        var kouModel = new KouModel(kouParams);

        Console.WriteLine("Parameters:");
        Console.WriteLine($"  Sigma: {kouParams.Sigma}");
        Console.WriteLine($"  Lambda: {kouParams.Lambda}");
        Console.WriteLine($"  P: {kouParams.P}");
        Console.WriteLine($"  Eta1: {kouParams.Eta1}");
        Console.WriteLine($"  Eta2: {kouParams.Eta2}");
        Console.WriteLine($"  Kappa: {kouParams.ComputeKappa():F6}\n");

        double kouIV = kouModel.ComputeTheoreticalIV(spot, strike, timeToExpiry);
        Console.WriteLine($"  ATM Implied Volatility: {kouIV:F4}");

        Console.WriteLine("\nVolatility Smile:");
        Console.WriteLine("  Strike | Moneyness | IV");
        Console.WriteLine("  -------|-----------|------");

        foreach (var k in strikes)
        {
            double iv = kouModel.ComputeTheoreticalIV(spot, k, timeToExpiry);
            double moneyness = k / spot;
            Console.WriteLine($"  {k,6:F1} | {moneyness,9:F3} | {iv:F4}");
        }

        Console.WriteLine("\n✓ All calculations completed successfully");
    }
}
