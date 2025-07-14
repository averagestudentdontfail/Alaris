namespace Alaris.Double;

/// <summary>
/// Simplified constants - eliminates redundancy and focuses on essential parameters
/// </summary>
public static class Constants
{
    #region Core Mathematical Constants
    public const double EPSILON = 1e-12;
    public const double MIN_TIME_TO_MATURITY = 1e-6; // ~5 minutes
    public const double MAX_TIME_TO_MATURITY = 30.0; // 30 years
    public const double MIN_VOLATILITY = 0.001;      // 0.1% per annum
    public const double MAX_VOLATILITY = 5.0;        // 500% per annum
    #endregion

    #region Engine Configuration  
    public const int DEFAULT_SPECTRAL_NODES = 6;     // Reduced from 8 for performance
    public const int MIN_SPECTRAL_NODES = 3;
    public const int MAX_SPECTRAL_NODES = 16;        // Reduced from 32
    
    public const double DEFAULT_TOLERANCE = 1e-10;   // Relaxed from 1e-12 for performance
    public const int DEFAULT_MAX_ITERATIONS = 50;    // Reduced from 100
    #endregion

    #region Performance Profiles
    /// <summary>Fast computation profile</summary>
    public static class Fast
    {
        public const int SpectralNodes = 4;
        public const double Tolerance = 1e-8;
        public const int MaxIterations = 25;
    }
    
    /// <summary>Standard computation profile - recommended for most uses</summary>
    public static class Standard  
    {
        public const int SpectralNodes = 6;
        public const double Tolerance = 1e-10;
        public const int MaxIterations = 50;
    }
    
    /// <summary>High precision profile - for research applications</summary>
    public static class HighPrecision
    {
        public const int SpectralNodes = 12;
        public const double Tolerance = 1e-12;
        public const int MaxIterations = 100;
    }
    #endregion

    #region Benchmark Values (for validation)
    /// <summary>Classic American put benchmark (Haug, 2007)</summary>
    public static class ClassicBenchmark
    {
        public const double Spot = 36.0;
        public const double Strike = 40.0;
        public const double Rate = 0.06;
        public const double Dividend = 0.02;
        public const double Volatility = 0.20;
        public const double TimeToMaturity = 1.0;
        public const double ExpectedPrice = 4.48927603;
    }
    
    /// <summary>Double boundary test case (Healy, 2021)</summary>
    public static class DoubleBoundaryBenchmark
    {
        public const double Spot = 100.0;
        public const double Strike = 100.0;
        public const double Rate = -0.01;
        public const double Dividend = -0.02;
        public const double Volatility = 0.15;
        public const double TimeToMaturity = 0.5;
        public const double ExpectedPrice = 3.988472; // From your test results
        public const double CriticalVolatility = 0.0732;
    }
    #endregion
}