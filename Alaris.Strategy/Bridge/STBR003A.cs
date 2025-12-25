// STBR003A.cs - cached quantlib infrastructure for greek calculations

using Alaris.Strategy.Model;

namespace Alaris.Strategy.Bridge;

/// <summary>
/// Cached QuantLib infrastructure for American option pricing that enables reuse
/// of QuantLib objects across Greek calculations via mutable SimpleQuote updates.
/// </summary>

public sealed class STBR003A : IDisposable
{
    // Mutable quotes for parameter updates
    private readonly SimpleQuote _spotQuote;
    private readonly SimpleQuote _volQuote;
    private readonly SimpleQuote _rateQuote;
    private readonly SimpleQuote _dividendQuote;
    
    // Quote handles (wrappers for the mutable quotes)
    private readonly QuoteHandle _spotHandle;
    private readonly QuoteHandle _volHandle;
    private readonly QuoteHandle _rateHandle;
    private readonly QuoteHandle _dividendHandle;
    
    // Day counter (shared, immutable)
    private readonly Actual365Fixed _dayCounter;
    
    // Current option configuration
    private Date? _currentValuationDate;
    private Date? _currentExpiry;
    private double _currentStrike;
    private Option.Type _currentOptionType;
    
    // Cached option and engine (recreated on strike/expiry/type change)
    private VanillaOption? _cachedOption;
    private FdBlackScholesVanillaEngine? _cachedEngine;
    private BlackScholesMertonProcess? _cachedProcess;
    private AmericanExercise? _cachedExercise;
    private PlainVanillaPayoff? _cachedPayoff;
    private TARGET? _cachedCalendar;
    
    // Term structure wrappers
    private FlatForward? _flatRateTs;
    private YieldTermStructureHandle? _riskFreeRateHandle;
    private FlatForward? _flatDividendTs;
    private YieldTermStructureHandle? _dividendYieldHandle;
    private BlackConstantVol? _flatVolTs;
    private BlackVolTermStructureHandle? _volatilityHandle;
    
    private bool _disposed;
    
    /// <summary>
    /// Initializes a new cached QuantLib infrastructure instance.
    /// </summary>
    /// <param name="initialSpot">Initial spot price.</param>
    /// <param name="initialVol">Initial volatility.</param>
    /// <param name="initialRate">Initial risk-free rate.</param>
    /// <param name="initialDividend">Initial dividend yield.</param>
    public STBR003A(
        double initialSpot = 100.0,
        double initialVol = 0.20,
        double initialRate = 0.05,
        double initialDividend = 0.0)
    {
        // Create mutable quotes
        _spotQuote = new SimpleQuote(initialSpot);
        _volQuote = new SimpleQuote(initialVol);
        _rateQuote = new SimpleQuote(initialRate);
        _dividendQuote = new SimpleQuote(initialDividend);
        
        // Create quote handles
        _spotHandle = new QuoteHandle(_spotQuote);
        _volHandle = new QuoteHandle(_volQuote);
        _rateHandle = new QuoteHandle(_rateQuote);
        _dividendHandle = new QuoteHandle(_dividendQuote);
        
        // Shared day counter
        _dayCounter = new Actual365Fixed();
    }
    
    /// <summary>
    /// Prices an option using the cached infrastructure, updating only the
    /// mutable quotes when parameters change.
    /// </summary>
    /// <param name="parameters">Option parameters.</param>
    /// <returns>The option NPV.</returns>
    public double Price(STDT003As parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        // Set global evaluation date
        Settings.instance().setEvaluationDate(parameters.ValuationDate);
        
        // Update mutable quotes (fast path - no allocations)
        _spotQuote.setValue(parameters.UnderlyingPrice);
        _volQuote.setValue(parameters.ImpliedVolatility);
        _rateQuote.setValue(parameters.RiskFreeRate);
        _dividendQuote.setValue(parameters.DividendYield);
        
        // Check if we need to rebuild the option infrastructure
        bool needsRebuild = NeedsRebuild(parameters);
        
        if (needsRebuild)
        {
            RebuildInfrastructure(parameters);
        }
        else
        {
            // Just update the term structures with new valuation date
            UpdateTermStructures(parameters);
        }
        
        return _cachedOption!.NPV();
    }
    
    /// <summary>
    /// Calculates Delta using central finite differences with cached infrastructure.
    /// </summary>
    public double CalculateDelta(STDT003As parameters, double bumpSize = 0.0001)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        double originalSpot = parameters.UnderlyingPrice;
        
        // Up bump
        parameters.UnderlyingPrice = originalSpot + bumpSize;
        double priceUp = Price(parameters);
        
        // Down bump
        parameters.UnderlyingPrice = originalSpot - bumpSize;
        double priceDown = Price(parameters);
        
        // Restore
        parameters.UnderlyingPrice = originalSpot;
        
        return (priceUp - priceDown) / (2 * bumpSize);
    }
    
    /// <summary>
    /// Calculates Gamma using central finite differences with cached infrastructure.
    /// </summary>
    public double CalculateGamma(STDT003As parameters, double bumpSize = 0.0001)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        double originalSpot = parameters.UnderlyingPrice;
        
        // Original price
        double priceOriginal = Price(parameters);
        
        // Up bump
        parameters.UnderlyingPrice = originalSpot + bumpSize;
        double priceUp = Price(parameters);
        
        // Down bump
        parameters.UnderlyingPrice = originalSpot - bumpSize;
        double priceDown = Price(parameters);
        
        // Restore
        parameters.UnderlyingPrice = originalSpot;
        
        return (priceUp - (2 * priceOriginal) + priceDown) / (bumpSize * bumpSize);
    }
    
    /// <summary>
    /// Calculates Vega using central finite differences with cached infrastructure.
    /// </summary>
    public double CalculateVega(STDT003As parameters, double volBumpSize = 0.01)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        double originalVol = parameters.ImpliedVolatility;
        
        // Up bump
        parameters.ImpliedVolatility = originalVol + volBumpSize;
        double priceUp = Price(parameters);
        
        // Down bump
        parameters.ImpliedVolatility = originalVol - volBumpSize;
        double priceDown = Price(parameters);
        
        // Restore
        parameters.ImpliedVolatility = originalVol;
        
        return (priceUp - priceDown) / (2 * volBumpSize);
    }
    
    /// <summary>
    /// Calculates Theta using forward finite difference with cached infrastructure.
    /// </summary>
    public double CalculateTheta(STDT003As parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        Date originalDate = parameters.ValuationDate;
        
        // Original price
        double priceOriginal = Price(parameters);
        
        // Forward date by 1 day - need to rebuild infrastructure for new dates
        int dayBump = 1;
        using Period period = new Period(dayBump, TimeUnit.Days);
        Date forwardDate = originalDate.Add(period);
        parameters.ValuationDate = forwardDate;
        double priceForward = Price(parameters);
        
        // Restore
        parameters.ValuationDate = originalDate;
        
        // Theta is price change per day (negative for time decay)
        return (priceForward - priceOriginal) / dayBump;
    }
    
    /// <summary>
    /// Calculates Rho using central finite differences with cached infrastructure.
    /// </summary>
    public double CalculateRho(STDT003As parameters, double rateBumpSize = 0.01)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        double originalRate = parameters.RiskFreeRate;
        
        // Up bump
        parameters.RiskFreeRate = originalRate + rateBumpSize;
        double priceUp = Price(parameters);
        
        // Down bump
        parameters.RiskFreeRate = originalRate - rateBumpSize;
        double priceDown = Price(parameters);
        
        // Restore
        parameters.RiskFreeRate = originalRate;
        
        return (priceUp - priceDown) / (2 * rateBumpSize);
    }
    
    /// <summary>
    /// Calculates all Greeks in a single pass with minimal infrastructure recreation.
    /// </summary>
    /// <param name="parameters">Option parameters.</param>
    /// <returns>Tuple of (Delta, Gamma, Vega, Theta, Rho).</returns>
    public (double Delta, double Gamma, double Vega, double Theta, double Rho) CalculateAllGreeks(
        STDT003As parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        
        const double spotBump = 0.0001;    // 1bp for delta/gamma
        const double volBump = 0.01;       // 1% for vega
        const double rateBump = 0.01;      // 1% for rho
        
        double originalSpot = parameters.UnderlyingPrice;
        double originalVol = parameters.ImpliedVolatility;
        double originalRate = parameters.RiskFreeRate;
        Date originalDate = parameters.ValuationDate;
        
        // Price at original parameters
        double priceOriginal = Price(parameters);
        
        // Spot bumps (for delta and gamma)
        parameters.UnderlyingPrice = originalSpot + spotBump;
        double priceSpotUp = Price(parameters);
        
        parameters.UnderlyingPrice = originalSpot - spotBump;
        double priceSpotDown = Price(parameters);
        parameters.UnderlyingPrice = originalSpot;
        
        // Vol bumps (for vega)
        parameters.ImpliedVolatility = originalVol + volBump;
        double priceVolUp = Price(parameters);
        
        parameters.ImpliedVolatility = originalVol - volBump;
        double priceVolDown = Price(parameters);
        parameters.ImpliedVolatility = originalVol;
        
        // Rate bumps (for rho)
        parameters.RiskFreeRate = originalRate + rateBump;
        double priceRateUp = Price(parameters);
        
        parameters.RiskFreeRate = originalRate - rateBump;
        double priceRateDown = Price(parameters);
        parameters.RiskFreeRate = originalRate;
        
        // Time bump (for theta)
        using Period period = new Period(1, TimeUnit.Days);
        Date forwardDate = originalDate.Add(period);
        parameters.ValuationDate = forwardDate;
        double priceForward = Price(parameters);
        parameters.ValuationDate = originalDate;
        
        // Calculate Greeks
        double delta = (priceSpotUp - priceSpotDown) / (2 * spotBump);
        double gamma = (priceSpotUp - (2 * priceOriginal) + priceSpotDown) / (spotBump * spotBump);
        double vega = (priceVolUp - priceVolDown) / (2 * volBump);
        double theta = (priceForward - priceOriginal) / 1.0;
        double rho = (priceRateUp - priceRateDown) / (2 * rateBump);
        
        return (delta, gamma, vega, theta, rho);
    }
    
    private bool NeedsRebuild(STDT003As parameters)
    {
        // Rebuild if option structure changed
        return _cachedOption == null
            || _currentExpiry != parameters.Expiry
            || Math.Abs(_currentStrike - parameters.Strike) > 1e-10
            || _currentOptionType != parameters.OptionType
            || _currentValuationDate != parameters.ValuationDate;
    }
    
    private void UpdateTermStructures(STDT003As parameters)
    {
        // When dates haven't changed, we just need to update the quotes
        // The quote values are already updated in Price()
        
        // However, if the valuation date changes, we need to rebuild term structures
        if (_currentValuationDate != parameters.ValuationDate)
        {
            RebuildTermStructures(parameters);
            _currentValuationDate = parameters.ValuationDate;
        }
    }
    
    private void RebuildInfrastructure(STDT003As parameters)
    {
        // Dispose old infrastructure in reverse creation order (Rule 16)
        DisposeOptionInfrastructure();
        
        // Rebuild term structures
        RebuildTermStructures(parameters);
        
        // Create calendar
        _cachedCalendar = new TARGET();
        
        // Create BSM process
        _cachedProcess = new BlackScholesMertonProcess(
            _spotHandle,
            _dividendYieldHandle!,
            _riskFreeRateHandle!,
            _volatilityHandle!);
        
        // Create option
        _cachedExercise = new AmericanExercise(parameters.ValuationDate, parameters.Expiry);
        _cachedPayoff = new PlainVanillaPayoff(parameters.OptionType, parameters.Strike);
        _cachedOption = new VanillaOption(_cachedPayoff, _cachedExercise);
        
        // Adaptive grid sizing for short maturities
        double timeToExpiry = _dayCounter.yearFraction(parameters.ValuationDate, parameters.Expiry);
        uint timeSteps = (uint)Math.Max(100, (int)(timeToExpiry * 365 * 2));
        uint priceSteps = 100;
        
        _cachedEngine = new FdBlackScholesVanillaEngine(_cachedProcess, timeSteps, priceSteps);
        _cachedOption.setPricingEngine(_cachedEngine);
        
        // Update cached state
        _currentValuationDate = parameters.ValuationDate;
        _currentExpiry = parameters.Expiry;
        _currentStrike = parameters.Strike;
        _currentOptionType = parameters.OptionType;
    }
    
    private void RebuildTermStructures(STDT003As parameters)
    {
        // Dispose old term structures
        DisposeTermStructures();
        
        // Create new term structures anchored to valuation date
        _flatRateTs = new FlatForward(
            parameters.ValuationDate,
            _rateHandle,
            _dayCounter);
        _riskFreeRateHandle = new YieldTermStructureHandle(_flatRateTs);
        
        _flatDividendTs = new FlatForward(
            parameters.ValuationDate,
            _dividendHandle,
            _dayCounter);
        _dividendYieldHandle = new YieldTermStructureHandle(_flatDividendTs);
        
        TARGET calendar = new TARGET();
        _flatVolTs = new BlackConstantVol(
            parameters.ValuationDate,
            calendar,
            _volHandle,
            _dayCounter);
        _volatilityHandle = new BlackVolTermStructureHandle(_flatVolTs);
        calendar.Dispose();
    }
    
    private void DisposeTermStructures()
    {
        _volatilityHandle?.Dispose();
        _flatVolTs?.Dispose();
        _dividendYieldHandle?.Dispose();
        _flatDividendTs?.Dispose();
        _riskFreeRateHandle?.Dispose();
        _flatRateTs?.Dispose();
        
        _volatilityHandle = null;
        _flatVolTs = null;
        _dividendYieldHandle = null;
        _flatDividendTs = null;
        _riskFreeRateHandle = null;
        _flatRateTs = null;
    }
    
    private void DisposeOptionInfrastructure()
    {
        _cachedEngine?.Dispose();
        _cachedOption?.Dispose();
        _cachedPayoff?.Dispose();
        _cachedExercise?.Dispose();
        _cachedProcess?.Dispose();
        _cachedCalendar?.Dispose();
        
        _cachedEngine = null;
        _cachedOption = null;
        _cachedPayoff = null;
        _cachedExercise = null;
        _cachedProcess = null;
        _cachedCalendar = null;
    }
    
    /// <summary>
    /// Disposes all QuantLib resources in reverse creation order (Rule 16).
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        
        // Dispose in reverse order (Rule 16)
        DisposeOptionInfrastructure();
        DisposeTermStructures();
        
        // Dispose base infrastructure
        _dayCounter.Dispose();
        _dividendHandle.Dispose();
        _rateHandle.Dispose();
        _volHandle.Dispose();
        _spotHandle.Dispose();
        _dividendQuote.Dispose();
        _rateQuote.Dispose();
        _volQuote.Dispose();
        _spotQuote.Dispose();
        
        _disposed = true;
    }
}
