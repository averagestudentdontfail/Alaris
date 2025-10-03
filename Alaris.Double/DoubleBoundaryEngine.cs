// Alaris.Double/DoubleBoundaryEngine.cs
using System;

namespace Alaris.Double
{
    /// <summary>
    /// Extended American option pricing engine that handles both single and double 
    /// exercise boundaries under positive and negative interest rates.
    /// 
    /// Automatically detects the interest rate regime and dispatches to the appropriate solver:
    /// - Single boundary for r >= 0 or r >= q
    /// - Double boundary for q &lt; r &lt; 0
    /// - European pricing when never optimal to exercise
    /// 
    /// Based on: Healy, J. (2021). "Pricing American options under negative rates."
    /// </summary>
    public class DoubleBoundaryEngine : PricingEngine
    {
        private readonly GeneralizedBlackScholesProcess _process;
        private readonly QdFpIterationScheme? _iterationScheme;
        private readonly QdFpAmericanEngine.FixedPointEquation _fpEquation;
        
        // Fallback engines
        private readonly QdFpAmericanEngine _singleBoundaryEngine;
        
        private const double EPSILON = 1e-8;
        private const double CROSSING_TIME_THRESHOLD = 1e-2;
        private const int DEFAULT_COLLOCATION_POINTS = 100;
        
        /// <summary>
        /// Initializes a new instance of the double boundary engine.
        /// </summary>
        /// <param name="process">Black-Scholes process with market parameters</param>
        /// <param name="iterationScheme">Iteration scheme (null uses high precision)</param>
        /// <param name="fpEquation">Fixed point equation selection</param>
        public DoubleBoundaryEngine(
            GeneralizedBlackScholesProcess process,
            QdFpIterationScheme? iterationScheme = null,
            QdFpAmericanEngine.FixedPointEquation fpEquation = QdFpAmericanEngine.FixedPointEquation.Auto)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
            _iterationScheme = iterationScheme ?? QdFpAmericanEngine.highPrecisionScheme();
            _fpEquation = fpEquation;
            
            // Initialize fallback for single boundary case
            _singleBoundaryEngine = new QdFpAmericanEngine(process, _iterationScheme, fpEquation);
        }
        
        /// <summary>
        /// Calculates the option price, automatically detecting the regime.
        /// </summary>
        public void calculate()
        {
            try
            {
                // Extract market parameters
                var r = _process.riskFreeRate().currentLink().zeroRate(0, Compounding.Continuous).value();
                var q = _process.dividendYield().currentLink().zeroRate(0, Compounding.Continuous).value();
                var sigma = _process.blackVolatility().currentLink().blackVol(0, _process.x0());
                
                // Detect regime and route to appropriate solver
                var regime = DetectRegime(r, q);
                
                switch (regime)
                {
                    case PricingRegime.SingleBoundary:
                        // Standard case: use existing single boundary engine
                        PriceWithSingleBoundary();
                        break;
                        
                    case PricingRegime.DoubleBoundary:
                        // Negative rate case: use double boundary solver
                        PriceWithDoubleBoundaries(r, q, sigma);
                        break;
                        
                    case PricingRegime.NeverExercise:
                        // Never optimal to exercise: price as European
                        PriceAsEuropean();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Double boundary engine error: {ex.Message}");
                // Fall back to single boundary as last resort
                PriceWithSingleBoundary();
            }
        }
        
        private PricingRegime DetectRegime(double r, double q)
        {
            // Healy Section 2: When is it never optimal to exercise?
            // For American puts: never exercise if r <= 0 AND r <= q
            if (r <= 0 && r <= q)
                return PricingRegime.NeverExercise;
            
            // Healy Section 3: Double boundary exists when q < r < 0
            if (q < r && r < 0)
                return PricingRegime.DoubleBoundary;
            
            // Default: single boundary (positive rates or r >= q)
            return PricingRegime.SingleBoundary;
        }
        
        private void PriceWithSingleBoundary()
        {
            _singleBoundaryEngine.calculate();
            
            // Copy results from single boundary engine
            var singleResults = _singleBoundaryEngine.getResults() as VanillaOption.Results;
            var ourResults = results_ as VanillaOption.Results;
            
            if (singleResults != null && ourResults != null)
            {
                ourResults.value = singleResults.value;
                ourResults.delta = singleResults.delta;
                ourResults.gamma = singleResults.gamma;
                ourResults.vega = singleResults.vega;
                ourResults.theta = singleResults.theta;
                ourResults.rho = singleResults.rho;
            }
        }
        
        private void PriceAsEuropean()
        {
            var europeanEngine = new AnalyticEuropeanEngine(_process);
            var option = arguments_ as VanillaOption.Arguments;
            var results = results_ as VanillaOption.Results;
            
            if (option != null && results != null)
            {
                // Create temporary European option for pricing
                var europeanOption = new VanillaOption(option.payoff, new EuropeanExercise(option.exercise.lastDate()));
                europeanOption.setPricingEngine(europeanEngine);
                
                results.value = europeanOption.NPV();
                results.delta = europeanOption.delta();
                results.gamma = europeanOption.gamma();
                results.vega = europeanOption.vega();
                results.theta = europeanOption.theta();
                results.rho = europeanOption.rho();
            }
        }
        
        private void PriceWithDoubleBoundaries(double r, double q, double sigma)
        {
            var option = arguments_ as VanillaOption.Arguments;
            if (option == null)
                throw new InvalidOperationException("Invalid option arguments");
            
            var payoff = option.payoff as PlainVanillaPayoff;
            var exercise = option.exercise as AmericanExercise;
            
            if (payoff == null || exercise == null)
                throw new InvalidOperationException("Requires plain vanilla payoff and American exercise");
            
            double K = payoff.strike();
            double T = _process.time(exercise.lastDate());
            double S = _process.x0();
            
            // Step 1: Compute initial boundary estimates
            var approximation = new DoubleBoundaryApproximation(_process, K, T, r, q, sigma);
            var initialResult = approximation.ComputeInitialBoundaries(DEFAULT_COLLOCATION_POINTS);
            
            // Step 2: Refine crossing time estimate if boundaries cross
            double crossingTime = initialResult.CrossingTime;
            if (crossingTime > EPSILON && crossingTime < T)
            {
                crossingTime = RefineCrossingTime(
                    initialResult.UpperBoundary, 
                    initialResult.LowerBoundary, 
                    T);
            }
            
            // Step 3: Solve using FP-B' iteration
            var solver = new DoubleBoundarySolver(_process, K, T, r, q, sigma, _iterationScheme!);
            var refinedResult = solver.Solve(
                initialResult.UpperBoundary, 
                initialResult.LowerBoundary, 
                crossingTime);
            
            // Step 4: Calculate price and Greeks
            double price = CalculatePrice(S, K, T, r, q, sigma, 
                refinedResult.UpperBoundary, refinedResult.LowerBoundary, crossingTime);
            
            var greeks = CalculateGreeks(S, K, T, r, q, sigma,
                refinedResult.UpperBoundary, refinedResult.LowerBoundary, crossingTime);
            
            // Step 5: Set results
            var results = results_ as VanillaOption.Results;
            if (results != null)
            {
                results.value = price;
                results.delta = greeks.Delta;
                results.gamma = greeks.Gamma;
                results.vega = greeks.Vega;
                results.theta = greeks.Theta;
                results.rho = greeks.Rho;
            }
        }
        
        private double RefineCrossingTime(double[] upper, double[] lower, double T)
        {
            int n = upper.Length;
            double dt = T / (n - 1);
            
            // Find approximate crossing region
            for (int i = n - 1; i >= 0; i--)
            {
                if (upper[i] > lower[i])
                {
                    // Binary search refinement
                    double tLower = i * dt;
                    double tUpper = Math.Min((i + 1) * dt, T);
                    
                    while (tUpper - tLower > CROSSING_TIME_THRESHOLD)
                    {
                        double tMid = (tLower + tUpper) / 2;
                        double uMid = Interpolate(upper, tMid, T);
                        double lMid = Interpolate(lower, tMid, T);
                        
                        if (uMid > lMid)
                            tLower = tMid;
                        else
                            tUpper = tMid;
                    }
                    
                    return tLower;
                }
            }
            
            return 0; // No crossing
        }
        
        private double CalculatePrice(double S, double K, double T, double r, double q, double sigma,
            double[] upper, double[] lower, double ts)
        {
            // Healy Equation 27: Modified Kim equation for double boundaries
            double europeanPrice = BlackScholesPut(S, K, T, r, q, sigma);
            double premium = CalculateEarlyExercisePremium(S, K, T, r, q, sigma, upper, lower, ts);
            
            return europeanPrice + premium;
        }
        
        private double CalculateEarlyExercisePremium(double S, double K, double T, double r, double q, double sigma,
            double[] upper, double[] lower, double ts)
        {
            int m = upper.Length;
            double dt = T / (m - 1);
            double premium = 0;
            
            for (int i = 0; i < m; i++)
            {
                double t = i * dt;
                if (t < ts) continue; // Only integrate after crossing time
                
                double tau = T - t;
                if (tau < EPSILON) continue;
                
                // Upper boundary contribution
                double d2_u = D2(S, upper[i], tau, r, q, sigma);
                double d1_u = d2_u + sigma * Math.Sqrt(tau);
                double term_u = r * K * Math.Exp(-r * tau) * CDF(-d2_u)
                              - q * S * Math.Exp(-q * tau) * CDF(-d1_u);
                
                // Lower boundary contribution (subtract)
                double d2_l = D2(S, lower[i], tau, r, q, sigma);
                double d1_l = d2_l + sigma * Math.Sqrt(tau);
                double term_l = r * K * Math.Exp(-r * tau) * CDF(-d2_l)
                              - q * S * Math.Exp(-q * tau) * CDF(-d1_l);
                
                premium += (term_u - term_l) * dt;
            }
            
            return premium;
        }
        
        private Greeks CalculateGreeks(double S, double K, double T, double r, double q, double sigma,
            double[] upper, double[] lower, double ts)
        {
            double h = S * 0.0001; // Finite difference step
            
            double V0 = CalculatePrice(S, K, T, r, q, sigma, upper, lower, ts);
            double Vp = CalculatePrice(S + h, K, T, r, q, sigma, upper, lower, ts);
            double Vm = CalculatePrice(S - h, K, T, r, q, sigma, upper, lower, ts);
            
            double delta = (Vp - Vm) / (2 * h);
            double gamma = (Vp - 2 * V0 + Vm) / (h * h);
            
            // Vega calculation
            double sigmaShift = sigma * 0.01;
            double Vv = CalculatePrice(S, K, T, r, q, sigma + sigmaShift, upper, lower, ts);
            double vega = (Vv - V0) / sigmaShift;
            
            return new Greeks
            {
                Delta = delta,
                Gamma = gamma,
                Vega = vega / 100, // Per 1% vol change
                Theta = 0, // Would require time shift
                Rho = 0    // Would require rate shift
            };
        }
        
        // Helper functions
        private double Interpolate(double[] array, double t, double T)
        {
            int n = array.Length;
            double dt = T / (n - 1);
            int i = (int)(t / dt);
            
            if (i >= n - 1) return array[n - 1];
            if (i < 0) return array[0];
            
            double alpha = (t - i * dt) / dt;
            return array[i] * (1 - alpha) + array[i + 1] * alpha;
        }
        
        private double BlackScholesPut(double S, double K, double T, double r, double q, double sigma)
        {
            if (T < EPSILON) return Math.Max(K - S, 0);
            
            double d1 = (Math.Log(S / K) + (r - q + 0.5 * sigma * sigma) * T) / (sigma * Math.Sqrt(T));
            double d2 = d1 - sigma * Math.Sqrt(T);
            
            return K * Math.Exp(-r * T) * CDF(-d2) - S * Math.Exp(-q * T) * CDF(-d1);
        }
        
        private double D2(double S, double B, double tau, double r, double q, double sigma)
        {
            if (tau < EPSILON) return S > B ? 10 : -10;
            return (Math.Log(S / B) + (r - q - 0.5 * sigma * sigma) * tau) / (sigma * Math.Sqrt(tau));
        }
        
        private double CDF(double x)
        {
            if (x > 8) return 1.0;
            if (x < -8) return 0.0;
            return 0.5 * (1 + Erf(x / Math.Sqrt(2)));
        }
        
        private double Erf(double x)
        {
            // Abramowitz and Stegun approximation
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
        
        private enum PricingRegime
        {
            SingleBoundary,
            DoubleBoundary,
            NeverExercise
        }
        
        private class Greeks
        {
            public double Delta { get; set; }
            public double Gamma { get; set; }
            public double Vega { get; set; }
            public double Theta { get; set; }
            public double Rho { get; set; }
        }
    }
}