// Alaris.Double/DoubleBoundaryEngine.cs
using System;

namespace Alaris.Double
{
    /// <summary>
    /// Wrapper for American option pricing that automatically handles double boundaries
    /// under negative rates. Does NOT inherit from PricingEngine - use as a factory.
    /// </summary>
    public class DoubleBoundaryEngine
    {
        private readonly GeneralizedBlackScholesProcess _process;
        private readonly QdFpIterationScheme _iterationScheme;
        private readonly QdFpAmericanEngine.FixedPointEquation _fpEquation;
        
        public DoubleBoundaryEngine(
            GeneralizedBlackScholesProcess process,
            QdFpIterationScheme? iterationScheme = null,
            QdFpAmericanEngine.FixedPointEquation fpEquation = QdFpAmericanEngine.FixedPointEquation.Auto)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
            _iterationScheme = iterationScheme ?? QdFpAmericanEngine.highPrecisionScheme();
            _fpEquation = fpEquation;
        }
        
        /// <summary>
        /// Creates the appropriate pricing engine based on market regime.
        /// </summary>
        public PricingEngine CreateEngine()
        {
            var r = ExtractRate(_process.riskFreeRate());
            var q = ExtractRate(_process.dividendYield());
            
            var regime = DetectRegime(r, q);
            
            // For now, always use single boundary engine as fallback
            // Double boundary calculation happens in PriceOption method
            return new QdFpAmericanEngine(_process, _iterationScheme, _fpEquation);
        }
        
        /// <summary>
        /// Prices an American option, automatically handling double boundaries.
        /// </summary>
        public OptionResult PriceOption(VanillaOption option)
        {
            var r = ExtractRate(_process.riskFreeRate());
            var q = ExtractRate(_process.dividendYield());
            var sigma = _process.blackVolatility().currentLink().blackVol(
                0.0, _process.stateVariable().currentLink().value());
            
            var regime = DetectRegime(r, q);
            
            switch (regime)
            {
                case PricingRegime.DoubleBoundary:
                    return PriceWithDoubleBoundaries(option, r, q, sigma);
                
                case PricingRegime.NeverExercise:
                    return PriceAsEuropean(option);
                
                default:
                    return PriceWithSingleBoundary(option);
            }
        }
        
        private PricingRegime DetectRegime(double r, double q)
        {
            if (r <= 0 && r <= q)
                return PricingRegime.NeverExercise;
            
            if (q < r && r < 0)
                return PricingRegime.DoubleBoundary;
            
            return PricingRegime.SingleBoundary;
        }
        
        private OptionResult PriceWithSingleBoundary(VanillaOption option)
        {
            var engine = new QdFpAmericanEngine(_process, _iterationScheme, _fpEquation);
            option.setPricingEngine(engine);
            
            return new OptionResult
            {
                Price = option.NPV(),
                Delta = option.delta(),
                Gamma = option.gamma(),
                Vega = option.vega(),
                Theta = option.theta(),
                Rho = option.rho()
            };
        }
        
        private OptionResult PriceAsEuropean(VanillaOption option)
        {
            var europeanExercise = new EuropeanExercise(option.exercise().lastDate());
            var payoff = option.payoff() as StrikedTypePayoff;
            if (payoff == null)
                throw new InvalidOperationException("Only striked payoffs supported");
            var europeanOption = new VanillaOption(payoff, europeanExercise);
            var engine = new AnalyticEuropeanEngine(_process);
            europeanOption.setPricingEngine(engine);
            
            return new OptionResult
            {
                Price = europeanOption.NPV(),
                Delta = europeanOption.delta(),
                Gamma = europeanOption.gamma(),
                Vega = europeanOption.vega(),
                Theta = europeanOption.theta(),
                Rho = europeanOption.rho()
            };
        }
        
        private OptionResult PriceWithDoubleBoundaries(VanillaOption option, double r, double q, double sigma)
        {
            var payoff = option.payoff() as PlainVanillaPayoff;
            if (payoff == null)
                throw new InvalidOperationException("Only plain vanilla payoffs supported");
            
            double K = payoff.strike();
            double S = _process.stateVariable().currentLink().value();
            
            // Calculate time to maturity
            var exercise = option.exercise();
            var lastDate = exercise.lastDate();
            var evalDate = Settings.instance().getEvaluationDate();
            double T = (lastDate.serialNumber() - evalDate.serialNumber()) / 365.0;
            
            // Compute boundaries
            var approximation = new DoubleBoundaryApproximation(_process, K, T, r, q, sigma);
            var initialResult = approximation.ComputeInitialBoundaries(100);
            
            var solver = new DoubleBoundarySolver(_process, K, T, r, q, sigma, _iterationScheme);
            var refinedResult = solver.Solve(
                initialResult.UpperBoundary,
                initialResult.LowerBoundary,
                initialResult.CrossingTime);
            
            // Calculate price
            double price = CalculatePrice(S, K, T, r, q, sigma,
                refinedResult.UpperBoundary, refinedResult.LowerBoundary, refinedResult.CrossingTime);
            
            var greeks = CalculateGreeks(S, K, T, r, q, sigma,
                refinedResult.UpperBoundary, refinedResult.LowerBoundary, refinedResult.CrossingTime);
            
            return new OptionResult
            {
                Price = price,
                Delta = greeks.Delta,
                Gamma = greeks.Gamma,
                Vega = greeks.Vega,
                Theta = greeks.Theta,
                Rho = greeks.Rho
            };
        }
        
        private double CalculatePrice(double S, double K, double T, double r, double q, double sigma,
            double[] upper, double[] lower, double ts)
        {
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
                if (t < ts) continue;
                
                double tau = T - t;
                if (tau < 1e-8) continue;
                
                double d2_u = D2(S, upper[i], tau, r, q, sigma);
                double d1_u = d2_u + sigma * Math.Sqrt(tau);
                double term_u = r * K * Math.Exp(-r * tau) * CDF(-d2_u)
                              - q * S * Math.Exp(-q * tau) * CDF(-d1_u);
                
                double d2_l = D2(S, lower[i], tau, r, q, sigma);
                double d1_l = d2_l + sigma * Math.Sqrt(tau);
                double term_l = r * K * Math.Exp(-r * tau) * CDF(-d2_l)
                              - q * S * Math.Exp(-q * tau) * CDF(-d1_l);
                
                premium += (term_u - term_l) * dt;
            }
            
            return premium;
        }
        
        private OptionResult CalculateGreeks(double S, double K, double T, double r, double q, double sigma,
            double[] upper, double[] lower, double ts)
        {
            double h = S * 0.0001;
            
            double V0 = CalculatePrice(S, K, T, r, q, sigma, upper, lower, ts);
            double Vp = CalculatePrice(S + h, K, T, r, q, sigma, upper, lower, ts);
            double Vm = CalculatePrice(S - h, K, T, r, q, sigma, upper, lower, ts);
            
            return new OptionResult
            {
                Delta = (Vp - Vm) / (2 * h),
                Gamma = (Vp - 2 * V0 + Vm) / (h * h),
                Vega = (CalculatePrice(S, K, T, r, q, sigma * 1.01, upper, lower, ts) - V0) / (sigma * 0.01) / 100,
                Theta = 0,
                Rho = 0
            };
        }
        
        private double ExtractRate(YieldTermStructureHandle handle)
        {
            return handle.currentLink().zeroRate(0.0, Compounding.Continuous, Frequency.Annual).rate();
        }
        
        private double BlackScholesPut(double S, double K, double T, double r, double q, double sigma)
        {
            if (T < 1e-8) return Math.Max(K - S, 0);
            
            double d1 = (Math.Log(S / K) + (r - q + 0.5 * sigma * sigma) * T) / (sigma * Math.Sqrt(T));
            double d2 = d1 - sigma * Math.Sqrt(T);
            
            return K * Math.Exp(-r * T) * CDF(-d2) - S * Math.Exp(-q * T) * CDF(-d1);
        }
        
        private double D2(double S, double B, double tau, double r, double q, double sigma)
        {
            if (tau < 1e-8) return S > B ? 10 : -10;
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
    }
    
    /// <summary>
    /// Result container for option pricing.
    /// </summary>
    public class OptionResult
    {
        public double Price { get; set; }
        public double Delta { get; set; }
        public double Gamma { get; set; }
        public double Vega { get; set; }
        public double Theta { get; set; }
        public double Rho { get; set; }
    }
}