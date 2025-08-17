// Alaris.Strategy/Bridge/Bridge.cs
using System;
using System.Threading.Tasks;
using Alaris.Quantlib;

namespace Alaris.Strategy.Bridge
{
    public class Bridge
    {
        private readonly QdPlusAmericanEngine _qdPlusEngine;
        private readonly QdFpAmericanEngine _qdFpEngine;
        
        public Bridge()
        {
            // Initialize with your existing QuantLib bindings
        }
        
        public async Task<CalendarSpreadPricing> PriceCalendarSpread(
            CalendarSpreadParameters parameters)
        {
            // Create market data handles
            var spot = new SimpleQuote(parameters.UnderlyingPrice);
            var spotHandle = new QuoteHandle(spot);
            
            var riskFreeRate = new FlatForward(
                parameters.ValuationDate,
                parameters.RiskFreeRate,
                new Actual365Fixed());
            var riskFreeHandle = new YieldTermStructureHandle(riskFreeRate);
            
            var dividendYield = new FlatForward(
                parameters.ValuationDate,
                parameters.DividendYield,
                new Actual365Fixed());
            var dividendHandle = new YieldTermStructureHandle(dividendYield);
            
            var volatility = new BlackConstantVol(
                parameters.ValuationDate,
                new TARGET(),
                parameters.ImpliedVolatility,
                new Actual365Fixed());
            var volHandle = new BlackVolTermStructureHandle(volatility);
            
            // Create stochastic process
            var process = new GeneralizedBlackScholesProcess(
                spotHandle,
                dividendHandle,
                riskFreeHandle,
                volHandle);
            
            // Price front month (short) option
            var frontOption = await PriceAmericanOption(
                process,
                parameters.Strike,
                parameters.FrontExpiry,
                parameters.OptionType);
            
            // Price back month (long) option  
            var backOption = await PriceAmericanOption(
                process,
                parameters.Strike,
                parameters.BackExpiry,
                parameters.OptionType);
            
            // Calculate spread metrics
            var spreadCost = backOption.Price - frontOption.Price;
            var spreadDelta = backOption.Delta - frontOption.Delta;
            var spreadGamma = backOption.Gamma - frontOption.Gamma;
            var spreadVega = backOption.Vega - frontOption.Vega;
            var spreadTheta = backOption.Theta - frontOption.Theta;
            
            return new CalendarSpreadPricing
            {
                FrontOption = frontOption,
                BackOption = backOption,
                SpreadCost = spreadCost,
                SpreadDelta = spreadDelta,
                SpreadGamma = spreadGamma,
                SpreadVega = spreadVega,
                SpreadTheta = spreadTheta,
                MaxProfit = CalculateMaxProfit(frontOption, backOption, parameters),
                MaxLoss = spreadCost,
                BreakEven = CalculateBreakEven(parameters)
            };
        }
        
        private async Task<OptionPricing> PriceAmericanOption(
            GeneralizedBlackScholesProcess process,
            double strike,
            Date expiry,
            Option.Type optionType)
        {
            // Create American option
            var exercise = new AmericanExercise(
                new Date(1, Month.January, 2020),
                expiry);
            
            var payoff = new PlainVanillaPayoff(optionType, strike);
            var option = new VanillaOption(payoff, exercise);
            
            // Price using QD+ engine (fast approximation)
            _qdPlusEngine = new QdPlusAmericanEngine(
                process,
                interpolationPoints: 8,
                QdPlusAmericanEngine.SolverType.Newton);
            
            option.setPricingEngine(_qdPlusEngine);
            var qdPlusPrice = option.NPV();
            var qdPlusDelta = option.delta();
            
            // Refine using QD Fixed Point engine
            var iterationScheme = QdFpAmericanEngine.highPrecisionScheme();
            _qdFpEngine = new QdFpAmericanEngine(
                process,
                iterationScheme,
                QdFpAmericanEngine.FixedPointEquation.Auto);
            
            option.setPricingEngine(_qdFpEngine);
            var qdFpPrice = option.NPV();
            
            // Use refined price with QD+ Greeks (faster)
            return new OptionPricing
            {
                Price = qdFpPrice,
                Delta = option.delta(),
                Gamma = option.gamma(),
                Vega = option.vega(),
                Theta = option.theta(),
                Rho = option.rho(),
                ExerciseBoundary = ExtractExerciseBoundary(option)
            };
        }
        
        private double[] ExtractExerciseBoundary(VanillaOption option)
        {
            // Extract the optimal exercise boundary from the engine
            // This would interface with the spectral collocation results
            // Implementation depends on your specific QuantLib bindings
            return new double[0];
        }
        
        private double CalculateMaxProfit(
            OptionPricing front,
            OptionPricing back,
            CalendarSpreadParameters parameters)
        {
            // Maximum profit occurs when underlying = strike at front expiry
            // and volatility remains elevated for back month
            return back.Price * Math.Exp(-parameters.RiskFreeRate * 
                (parameters.BackExpiry - parameters.FrontExpiry).Days / 365.0);
        }
        
        private double CalculateBreakEven(CalendarSpreadParameters parameters)
        {
            // Simplified break-even calculation
            // In practice, this requires solving for the underlying price
            // where the spread P&L = 0 at front expiry
            return parameters.Strike;
        }
    }
    
    public class CalendarSpreadParameters
    {
        public double UnderlyingPrice { get; set; }
        public double Strike { get; set; }
        public Date FrontExpiry { get; set; }
        public Date BackExpiry { get; set; }
        public double ImpliedVolatility { get; set; }
        public double RiskFreeRate { get; set; }
        public double DividendYield { get; set; }
        public Option.Type OptionType { get; set; }
        public Date ValuationDate { get; set; }
    }
    
    public class OptionPricing
    {
        public double Price { get; set; }
        public double Delta { get; set; }
        public double Gamma { get; set; }
        public double Vega { get; set; }
        public double Theta { get; set; }
        public double Rho { get; set; }
        public double[] ExerciseBoundary { get; set; }
    }
    
    public class CalendarSpreadPricing
    {
        public OptionPricing FrontOption { get; set; }
        public OptionPricing BackOption { get; set; }
        public double SpreadCost { get; set; }
        public double SpreadDelta { get; set; }
        public double SpreadGamma { get; set; }
        public double SpreadVega { get; set; }
        public double SpreadTheta { get; set; }
        public double MaxProfit { get; set; }
        public double MaxLoss { get; set; }
        public double BreakEven { get; set; }
    }
}