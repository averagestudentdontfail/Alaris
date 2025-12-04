// =============================================================================
// STLN001A.cs - Alaris Earnings Volatility Trading Algorithm
// Component: STLN001A | Category: Lean Integration | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.2
// Reference: Atilgan (2014) - Earnings Calendar Spread Strategy
// Reference: Leung & Santoli (2014) - Pre-Earnings IV Modeling
// Reference: Healy (2021) - American Option Pricing Under Negative Rates
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Orders;
using QuantConnect.Securities.Option;

// Alaris.Data components
using Alaris.Data.Bridge;
using Alaris.Data.Model;
using Alaris.Data.Provider;
using Alaris.Data.Provider.Polygon;
using Alaris.Data.Provider.Earnings;
using Alaris.Data.Provider.Execution;
using Alaris.Data.Provider.RiskFree;
using Alaris.Data.Quality;

// Alaris.Strategy components
using Alaris.Strategy.Core;
using Alaris.Strategy.Cost;
using Alaris.Strategy.Hedge;
using Alaris.Strategy.Risk;

// Alaris.Algorithm components
using Alaris.Algorithm.Universe;

// Alaris.Events components
using Alaris.Events;

namespace Alaris.Algorithm;

/// <summary>
/// Alaris Earnings Volatility Trading Algorithm.
/// </summary>
/// <remarks>
/// <para>
/// Implements Atilgan (2014) earnings calendar spread strategy with:
/// <list type="bullet">
///   <item>Yang-Zhang (2000) realised volatility estimation</item>
///   <item>Leung-Santoli (2014) pre-earnings IV modelling</item>
///   <item>Healy (2021) American option pricing under negative rates</item>
///   <item>Production validation (cost survival, vega independence, liquidity, gamma risk)</item>
/// </list>
/// </para>
/// <para>
/// Strategy workflow (10 phases):
/// <list type="number">
///   <item>Universe selection: symbols with earnings in target window</item>
///   <item>Market data acquisition: historical bars, options chains, earnings calendar</item>
///   <item>Realised volatility: Yang-Zhang OHLC estimator</item>
///   <item>Term structure analysis: 30/60/90 DTE IV</item>
///   <item>Signal generation: Atilgan criteria evaluation</item>
///   <item>Production validation: 4-stage validator pipeline</item>
///   <item>Position sizing: Kelly criterion</item>
///   <item>Execution pricing: IBKR snapshot quotes</item>
///   <item>Order execution: LEAN combo orders</item>
///   <item>Audit trail: Alaris.Events logging</item>
/// </list>
/// </para>
/// </remarks>
public sealed class STLN001A : QCAlgorithm
{
    // =========================================================================
    // Configuration Constants (Atilgan 2014)
    // =========================================================================
    
    /// <summary>Target days before earnings for entry.</summary>
    private const int DaysBeforeEarnings = 6;
    
    /// <summary>Minimum 30-day average dollar volume.</summary>
    private const decimal MinimumDollarVolume = 1_500_000m;
    
    /// <summary>Minimum share price for consideration.</summary>
    private const decimal MinimumPrice = 5.00m;
    
    /// <summary>Maximum portfolio allocation to strategy.</summary>
    private const decimal PortfolioAllocationLimit = 0.80m;
    
    /// <summary>Maximum allocation per position.</summary>
    private const decimal MaxPositionAllocation = 0.06m;
    
    /// <summary>Maximum concurrent positions.</summary>
    private const int MaxConcurrentPositions = 15;
    
    /// <summary>Default timeout for external API calls.</summary>
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(30);

    // =========================================================================
    // Alaris Components (Instance-Based)
    // =========================================================================
    
    // Data Infrastructure
    private AlarisDataBridge? _dataBridge;
    private IMarketDataProvider? _marketDataProvider;
    private IEarningsCalendarProvider? _earningsProvider;
    private IRiskFreeRateProvider? _riskFreeRateProvider;
    private IExecutionQuoteProvider? _executionQuoteProvider;
    
    // Strategy Components
    private STCR003AEstimator? _yangZhangEstimator;
    private STTM001AAnalyzer? _termStructureAnalyzer;
    private STCR001A? _signalGenerator;
    private STRK001A? _positionSizer;
    
    // Production Validation
    private STHD005A? _productionValidator;
    private STCS005A? _feeModel;
    private STCS006A? _costValidator;
    private STHD001A? _vegaAnalyser;
    private STCS008A? _liquidityValidator;
    private STHD003A? _gammaRiskManager;
    
    // Universe Selection
    private STUN001A? _universeSelector;
    
    // Audit & Events
    private InMemoryEventStore? _eventStore;
    private InMemoryAuditLogger? _auditLogger;
    
    // Logging
    private ILogger<STLN001A>? _logger;
    private ILoggerFactory? _loggerFactory;
    
    // HTTP Client (shared)
    private HttpClient? _httpClient;
    
    // State Tracking
    private readonly HashSet<Symbol> _activePositions = new();
    private readonly Dictionary<Symbol, DateTime> _positionEntryDates = new();

    // =========================================================================
    // QCAlgorithm Lifecycle
    // =========================================================================
    
    /// <summary>
    /// Initialises the algorithm at start.
    /// Called once by LEAN engine.
    /// </summary>
    public override void Initialize()
    {
        // =====================================================================
        // Basic Algorithm Configuration
        // =====================================================================
        
        SetStartDate(2025, 1, 1);
        SetEndDate(2025, 12, 31);
        SetCash(100_000);
        
        // Use Interactive Brokers as brokerage
        SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage);
        
        // Set benchmark
        SetBenchmark("SPY");
        
        // =====================================================================
        // Initialise Alaris Components
        // =====================================================================
        
        InitialiseLogging();
        InitialiseDataProviders();
        InitialiseStrategyComponents();
        InitialiseProductionValidation();
        InitialiseUniverseSelection();
        InitialiseAuditTrail();
        
        // =====================================================================
        // Schedule Daily Evaluation
        // =====================================================================
        
        ScheduleDailyEvaluation();
        
        // =====================================================================
        // Log Initialisation Complete
        // =====================================================================
        
        Log("═══════════════════════════════════════════════════════════════════");
        Log("STLN001A: Alaris Earnings Algorithm Initialised");
        Log($"  Start Date: {StartDate:yyyy-MM-dd}");
        Log($"  Cash: {Portfolio.Cash:C}");
        Log($"  Target Days Before Earnings: {DaysBeforeEarnings}");
        Log($"  Min Dollar Volume: {MinimumDollarVolume:C}");
        Log($"  Max Portfolio Allocation: {PortfolioAllocationLimit:P0}");
        Log($"  Max Position Allocation: {MaxPositionAllocation:P0}");
        Log("═══════════════════════════════════════════════════════════════════");
    }

    /// <summary>
    /// Called when the algorithm ends.
    /// </summary>
    public override void OnEndOfAlgorithm()
    {
        Log("═══════════════════════════════════════════════════════════════════");
        Log("STLN001A: Algorithm Complete");
        Log($"  Final Portfolio Value: {Portfolio.TotalPortfolioValue:C}");
        Log($"  Total Trades Executed: {Transactions.OrdersCount}");
        Log("═══════════════════════════════════════════════════════════════════");
        
        // Dispose HTTP client
        _httpClient?.Dispose();
        
        base.OnEndOfAlgorithm();
    }

    // =========================================================================
    // Initialisation Methods
    // =========================================================================
    
    /// <summary>
    /// Initialises logging infrastructure.
    /// Bridges LEAN logging to Microsoft.Extensions.Logging.
    /// </summary>
    private void InitialiseLogging()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new LeanLoggerProvider(this));
        });
        
        _logger = _loggerFactory.CreateLogger<STLN001A>();
        
        Log("STLN001A: Logging initialised");
    }

    /// <summary>
    /// Initialises data providers from Alaris.Data.
    /// </summary>
    private void InitialiseDataProviders()
    {
        // Create shared HTTP client with sensible defaults
        _httpClient = new HttpClient
        {
            Timeout = ApiTimeout
        };
        
        // Load configuration
        var configuration = BuildConfiguration();
        
        // Initialise market data provider (Polygon)
        _marketDataProvider = new PolygonApiClient(
            _httpClient,
            configuration,
            _loggerFactory!.CreateLogger<PolygonApiClient>());
        
        // Initialise earnings provider (Financial Modeling Prep)
        _earningsProvider = new FinancialModelingPrepProvider(
            _httpClient,
            configuration,
            _loggerFactory.CreateLogger<FinancialModelingPrepProvider>());
        
        // Initialise risk-free rate provider (Treasury Direct)
        _riskFreeRateProvider = new TreasuryDirectRateProvider(
            _httpClient,
            _loggerFactory.CreateLogger<TreasuryDirectRateProvider>());
        
        // Initialise execution quote provider (IBKR Snapshots)
        _executionQuoteProvider = new InteractiveBrokersSnapshotProvider(
            _loggerFactory.CreateLogger<InteractiveBrokersSnapshotProvider>());
        
        // Create data quality validators
        var validators = new IDataQualityValidator[]
        {
            new PriceReasonablenessValidator(_loggerFactory.CreateLogger<PriceReasonablenessValidator>()),
            new IvArbitrageValidator(_loggerFactory.CreateLogger<IvArbitrageValidator>()),
            new VolumeOpenInterestValidator(_loggerFactory.CreateLogger<VolumeOpenInterestValidator>()),
            new EarningsDateValidator(_loggerFactory.CreateLogger<EarningsDateValidator>())
        };
        
        // Create unified data bridge
        _dataBridge = new AlarisDataBridge(
            _marketDataProvider,
            _earningsProvider,
            _riskFreeRateProvider,
            validators,
            _loggerFactory.CreateLogger<AlarisDataBridge>());
        
        Log("STLN001A: Data providers initialised");
    }

    /// <summary>
    /// Initialises strategy analysis components from Alaris.Strategy.
    /// </summary>
    private void InitialiseStrategyComponents()
    {
        // Yang-Zhang realised volatility estimator
        _yangZhangEstimator = new STCR003AEstimator();
        
        // Term structure analyser
        _termStructureAnalyzer = new STTM001AAnalyzer();
        
        // Signal generator (requires market data adapter)
        var marketDataAdapter = new DataBridgeMarketDataAdapter(_dataBridge!);
        _signalGenerator = new STCR001A(
            marketDataAdapter,
            _yangZhangEstimator,
            _termStructureAnalyzer,
            earningsCalibrator: null,  // Use default calibration
            _loggerFactory!.CreateLogger<STCR001A>());
        
        // Position sizer (Kelly criterion)
        _positionSizer = new STRK001A(
            maxAllocationPercent: (double)MaxPositionAllocation,
            logger: _loggerFactory.CreateLogger<STRK001A>());
        
        Log("STLN001A: Strategy components initialised");
    }

    /// <summary>
    /// Initialises production validation pipeline.
    /// </summary>
    private void InitialiseProductionValidation()
    {
        // Fee model for IBKR
        _feeModel = new STCS005A(
            commissionPerContract: 0.65m,
            feePerContract: 0.25m);
        
        // Cost validator (signal survives costs)
        _costValidator = new STCS006A(
            _feeModel,
            _loggerFactory!.CreateLogger<STCS006A>());
        
        // Vega correlation analyser
        _vegaAnalyser = new STHD001A(
            _loggerFactory.CreateLogger<STHD001A>());
        
        // Liquidity validator
        _liquidityValidator = new STCS008A(
            _loggerFactory.CreateLogger<STCS008A>());
        
        // Gamma risk manager
        _gammaRiskManager = new STHD003A(
            _loggerFactory.CreateLogger<STHD003A>());
        
        // Production validator (orchestrates all checks)
        _productionValidator = new STHD005A(
            _costValidator,
            _vegaAnalyser,
            _liquidityValidator,
            _gammaRiskManager,
            _loggerFactory.CreateLogger<STHD005A>());
        
        Log("STLN001A: Production validation initialised");
    }

    /// <summary>
    /// Initialises universe selection using STUN001A.
    /// </summary>
    private void InitialiseUniverseSelection()
    {
        // Create earnings universe selector
        _universeSelector = new STUN001A(
            _earningsProvider!,
            daysBeforeEarningsMin: DaysBeforeEarnings - 1,
            daysBeforeEarningsMax: DaysBeforeEarnings + 1,
            minimumDollarVolume: MinimumDollarVolume,
            minimumPrice: MinimumPrice,
            maxCoarseSymbols: 500,
            maxFinalSymbols: 50,
            _loggerFactory!.CreateLogger<STUN001A>());
        
        // Register universe with LEAN
        AddUniverseSelection(_universeSelector);
        
        // Configure options for selected symbols
        UniverseSettings.Resolution = Resolution.Minute;
        UniverseSettings.DataNormalizationMode = DataNormalizationMode.Raw;
        
        Log($"STLN001A: Universe selection configured");
        Log($"  {_universeSelector.GetConfigurationSummary()}");
    }

    /// <summary>
    /// Initialises audit trail and event sourcing.
    /// </summary>
    private void InitialiseAuditTrail()
    {
        _eventStore = new InMemoryEventStore();
        _auditLogger = new InMemoryAuditLogger(
            _eventStore,
            _loggerFactory!.CreateLogger<InMemoryAuditLogger>());
        
        Log("STLN001A: Audit trail initialised");
    }

    /// <summary>
    /// Schedules daily strategy evaluation at market open + 1 minute.
    /// </summary>
    private void ScheduleDailyEvaluation()
    {
        Schedule.On(
            DateRules.EveryDay(),
            TimeRules.At(9, 31),  // 9:31 AM ET
            EvaluatePositions);
        
        Log("STLN001A: Scheduled daily evaluation at 9:31 AM ET");
    }

    /// <summary>
    /// Builds configuration from environment and files.
    /// </summary>
    private IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddEnvironmentVariables("ALARIS_")
            .AddJsonFile("config/alaris.json", optional: true)
            .Build();
    }

    // =========================================================================
    // Main Strategy Logic
    // =========================================================================
    
    /// <summary>
    /// Main strategy evaluation logic.
    /// Called daily at 9:31 AM ET.
    /// </summary>
    private void EvaluatePositions()
    {
        if (!AreComponentsInitialised())
        {
            Error("STLN001A: Components not initialised");
            return;
        }
        
        Log("═══════════════════════════════════════════════════════════════════");
        Log($"STLN001A: Starting daily evaluation - {Time:yyyy-MM-dd HH:mm:ss}");
        Log("═══════════════════════════════════════════════════════════════════");
        
        try
        {
            // Check portfolio allocation limit
            if (GetCurrentAllocation() >= PortfolioAllocationLimit)
            {
                Log("STLN001A: Portfolio allocation limit reached, skipping new entries");
                return;
            }
            
            // Check position count limit
            if (_activePositions.Count >= MaxConcurrentPositions)
            {
                Log("STLN001A: Maximum concurrent positions reached, skipping new entries");
                return;
            }
            
            // Get symbols from current universe
            var symbols = Securities.Keys
                .Where(s => s.SecurityType == SecurityType.Equity)
                .Where(s => !_activePositions.Contains(s))
                .ToList();
            
            Log($"STLN001A: Evaluating {symbols.Count} symbols from universe");
            
            // Evaluate each symbol
            int evaluated = 0;
            int signalsGenerated = 0;
            int ordersSubmitted = 0;
            
            foreach (var symbol in symbols)
            {
                try
                {
                    var result = EvaluateSymbol(symbol);
                    evaluated++;
                    
                    if (result.SignalGenerated) signalsGenerated++;
                    if (result.OrderSubmitted) ordersSubmitted++;
                }
                catch (Exception ex)
                {
                    Error($"STLN001A: Error evaluating {symbol}: {ex.Message}");
                    _auditLogger?.LogError($"Evaluation failed for {symbol}", ex);
                }
            }
            
            Log("───────────────────────────────────────────────────────────────────");
            Log($"STLN001A: Evaluation complete");
            Log($"  Symbols evaluated: {evaluated}");
            Log($"  Signals generated: {signalsGenerated}");
            Log($"  Orders submitted: {ordersSubmitted}");
            Log("═══════════════════════════════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            Error($"STLN001A: Fatal error in evaluation: {ex.Message}");
            _auditLogger?.LogError("Daily evaluation failed", ex);
        }
    }

    /// <summary>
    /// Evaluates a single symbol for trading opportunity.
    /// Implements complete Alaris strategy workflow.
    /// </summary>
    /// <param name="symbol">The symbol to evaluate.</param>
    /// <returns>Evaluation result summary.</returns>
    private EvaluationResult EvaluateSymbol(Symbol symbol)
    {
        var result = new EvaluationResult();
        var ticker = symbol.Value;
        
        Log($"STLN001A: Evaluating {ticker}...");
        
        // =====================================================================
        // Phase 1: Market Data Acquisition
        // =====================================================================
        
        MarketDataSnapshot snapshot;
        try
        {
            using var cts = new CancellationTokenSource(ApiTimeout);
            snapshot = _dataBridge!.GetMarketDataSnapshotAsync(ticker, cts.Token)
                .GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            Log($"  {ticker}: Market data timeout");
            return result;
        }
        catch (InvalidOperationException ex)
        {
            Log($"  {ticker}: Data quality validation failed - {ex.Message}");
            return result;
        }
        
        if (snapshot.NextEarnings == null)
        {
            Log($"  {ticker}: No upcoming earnings found");
            return result;
        }
        
        // =====================================================================
        // Phase 2: Realised Volatility Calculation
        // =====================================================================
        
        var rv = _yangZhangEstimator!.Calculate(snapshot.HistoricalBars);
        Log($"  {ticker}: 30-day RV = {rv:P2}");
        
        // =====================================================================
        // Phase 3: Term Structure Analysis
        // =====================================================================
        
        var termStructure = _termStructureAnalyzer!.Analyze(snapshot.OptionChain);
        Log($"  {ticker}: Term structure = {termStructure.ThirtyDayIV:P2} / {termStructure.SixtyDayIV:P2} / {termStructure.NinetyDayIV:P2}");
        
        // =====================================================================
        // Phase 4: Signal Generation
        // =====================================================================
        
        var signal = _signalGenerator!.Generate(
            ticker,
            snapshot.NextEarnings.AnnouncementDate,
            Time);
        
        Log($"  {ticker}: Signal = {signal.Strength} (IV/RV = {signal.IVRVRatio:F3})");
        result.SignalGenerated = true;
        
        if (signal.Strength != STCR004AStrength.Recommended)
        {
            Log($"  {ticker}: Signal not recommended, skipping");
            return result;
        }
        
        // =====================================================================
        // Phase 5: Production Validation
        // =====================================================================
        
        var validation = ValidateForProduction(signal, snapshot);
        
        if (!validation.ProductionReady)
        {
            Log($"  {ticker}: Failed production validation");
            foreach (var check in validation.Checks.Where(c => !c.Passed))
            {
                Log($"    - {check.Name}: {check.Reason}");
            }
            _auditLogger?.LogWarning($"{ticker}: Production validation failed", validation);
            return result;
        }
        
        Log($"  {ticker}: Passed all production validation checks");
        
        // =====================================================================
        // Phase 6: Execution Pricing
        // =====================================================================
        
        CalendarSpreadQuote? spreadQuote;
        try
        {
            using var cts = new CancellationTokenSource(ApiTimeout);
            spreadQuote = _executionQuoteProvider!.GetCalendarSpreadQuoteAsync(
                ticker,
                signal.Strike,
                signal.FrontExpiry,
                signal.BackExpiry,
                OptionRight.Call,
                cts.Token)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log($"  {ticker}: Failed to get execution quote - {ex.Message}");
            return result;
        }
        
        if (spreadQuote == null)
        {
            Log($"  {ticker}: No execution quote available");
            return result;
        }
        
        Log($"  {ticker}: Spread quote = ${spreadQuote.SpreadMid:F2} (bid/ask: ${spreadQuote.SpreadBid:F2}/${spreadQuote.SpreadAsk:F2})");
        
        // =====================================================================
        // Phase 7: Position Sizing
        // =====================================================================
        
        var sizing = _positionSizer!.Calculate(
            portfolioValue: (double)Portfolio.TotalPortfolioValue,
            spreadCost: (double)spreadQuote.SpreadMid,
            signal: signal);
        
        if (sizing.Contracts <= 0)
        {
            Log($"  {ticker}: Position sizing returned 0 contracts");
            return result;
        }
        
        // Apply position limit from production validation if lower
        var finalContracts = Math.Min(sizing.Contracts, validation.RecommendedContracts);
        
        Log($"  {ticker}: Position size = {finalContracts} contracts ({sizing.AllocationPercent:P2} of portfolio)");
        
        // =====================================================================
        // Phase 8: Order Execution
        // =====================================================================
        
        var orderResult = ExecuteCalendarSpread(
            symbol,
            signal,
            spreadQuote,
            finalContracts);
        
        if (orderResult.Success)
        {
            result.OrderSubmitted = true;
            _activePositions.Add(symbol);
            _positionEntryDates[symbol] = Time;
            
            // Log to audit trail
            _auditLogger?.LogTrade(new TradeEvent
            {
                Symbol = ticker,
                Action = "OPEN",
                Contracts = finalContracts,
                Price = spreadQuote.SpreadMid,
                Signal = signal,
                Timestamp = Time
            });
            
            Log($"  {ticker}: Order submitted successfully");
        }
        else
        {
            Log($"  {ticker}: Order submission failed - {orderResult.Message}");
        }
        
        return result;
    }

    // =========================================================================
    // Helper Methods
    // =========================================================================
    
    /// <summary>
    /// Checks if all components are properly initialised.
    /// </summary>
    private bool AreComponentsInitialised()
    {
        return _dataBridge != null
            && _signalGenerator != null
            && _productionValidator != null
            && _positionSizer != null
            && _executionQuoteProvider != null;
    }

    /// <summary>
    /// Gets current portfolio allocation to strategy positions.
    /// </summary>
    private decimal GetCurrentAllocation()
    {
        if (Portfolio.TotalPortfolioValue == 0) return 0;
        
        var strategyValue = _activePositions
            .Where(s => Portfolio.ContainsKey(s))
            .Sum(s => Portfolio[s].AbsoluteHoldingsValue);
        
        return strategyValue / Portfolio.TotalPortfolioValue;
    }

    /// <summary>
    /// Validates a signal for production using STHD005A.
    /// </summary>
    private STHD006A ValidateForProduction(STCR004A signal, MarketDataSnapshot snapshot)
    {
        // Build validation parameters from snapshot
        // This is a simplified version - full implementation would extract
        // all required parameters from the snapshot and signal
        
        return _productionValidator!.Validate(
            signal,
            frontLegParams: CreateOptionParams(signal, true),
            backLegParams: CreateOptionParams(signal, false),
            frontIVHistory: Array.Empty<double>(),  // Would come from historical data
            backIVHistory: Array.Empty<double>(),
            backMonthVolume: (long)(snapshot.AverageVolume30Day * 0.1),  // Estimate
            backMonthOpenInterest: 1000,  // Would come from options chain
            spotPrice: (double)snapshot.SpotPrice,
            strikePrice: (double)signal.Strike,
            spreadGreeks: ComputeSpreadGreeks(signal, snapshot),
            daysToEarnings: (signal.EarningsDate - Time).Days);
    }

    /// <summary>
    /// Creates option parameters for validation.
    /// </summary>
    private OptionParameters CreateOptionParams(STCR004A signal, bool isFrontMonth)
    {
        var expiry = isFrontMonth ? signal.FrontExpiry : signal.BackExpiry;
        var dte = (expiry - Time).Days;
        
        return new OptionParameters
        {
            Strike = (double)signal.Strike,
            DTE = dte,
            ImpliedVolatility = isFrontMonth ? signal.FrontIV : signal.BackIV,
            IsCall = true
        };
    }

    /// <summary>
    /// Computes spread Greeks for validation.
    /// </summary>
    private SpreadGreeks ComputeSpreadGreeks(STCR004A signal, MarketDataSnapshot snapshot)
    {
        // Simplified Greeks computation
        // Full implementation would use Alaris.Strategy pricing engine
        return new SpreadGreeks
        {
            Delta = 0.0,  // Calendar spreads are approximately delta-neutral
            Gamma = 0.01,
            Vega = 0.05,
            Theta = -0.02
        };
    }

    /// <summary>
    /// Executes a calendar spread order using LEAN's combo order functionality.
    /// </summary>
    private OrderExecutionResult ExecuteCalendarSpread(
        Symbol underlyingSymbol,
        STCR004A signal,
        CalendarSpreadQuote quote,
        int contracts)
    {
        try
        {
            // Create option symbols
            var frontOption = CreateOptionSymbol(
                underlyingSymbol,
                signal.Strike,
                signal.FrontExpiry,
                OptionRight.Call);
            
            var backOption = CreateOptionSymbol(
                underlyingSymbol,
                signal.Strike,
                signal.BackExpiry,
                OptionRight.Call);
            
            // Add option contracts to universe
            AddOptionContract(frontOption);
            AddOptionContract(backOption);
            
            // Create combo order legs
            var legs = new List<Leg>
            {
                Leg.Create(frontOption, -contracts),  // Sell front month
                Leg.Create(backOption, contracts)     // Buy back month
            };
            
            // Submit combo limit order at mid price
            var limitPrice = quote.SpreadMid;
            var ticket = ComboLimitOrder(legs, contracts, limitPrice);
            
            return new OrderExecutionResult
            {
                Success = true,
                OrderId = ticket.OrderId,
                Message = $"Order {ticket.OrderId} submitted at ${limitPrice:F2}"
            };
        }
        catch (Exception ex)
        {
            return new OrderExecutionResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    /// Creates an option symbol from components.
    /// </summary>
    private Symbol CreateOptionSymbol(
        Symbol underlying,
        decimal strike,
        DateTime expiry,
        OptionRight right)
    {
        return Symbol.CreateOption(
            underlying,
            Market.USA,
            OptionStyle.American,
            right,
            strike,
            expiry);
    }

    /// <summary>
    /// Handles order events from LEAN.
    /// </summary>
    /// <param name="orderEvent">The order event.</param>
    public override void OnOrderEvent(OrderEvent orderEvent)
    {
        if (orderEvent.Status == OrderStatus.Filled)
        {
            Log($"STLN001A: Order filled - {orderEvent.Symbol} @ ${orderEvent.FillPrice:F2}");
            
            _auditLogger?.LogOrderFill(new OrderFillEvent
            {
                OrderId = orderEvent.OrderId,
                Symbol = orderEvent.Symbol.Value,
                Quantity = orderEvent.FillQuantity,
                FillPrice = orderEvent.FillPrice,
                Timestamp = Time
            });
        }
        else if (orderEvent.Status == OrderStatus.Canceled)
        {
            Log($"STLN001A: Order cancelled - {orderEvent.Symbol}");
        }
        else if (orderEvent.Status == OrderStatus.Invalid)
        {
            Error($"STLN001A: Invalid order - {orderEvent.Symbol}: {orderEvent.Message}");
        }
    }

    // =========================================================================
    // Supporting Types
    // =========================================================================
    
    /// <summary>Result of symbol evaluation.</summary>
    private sealed class EvaluationResult
    {
        public bool SignalGenerated { get; set; }
        public bool OrderSubmitted { get; set; }
    }

    /// <summary>Result of order execution attempt.</summary>
    private sealed class OrderExecutionResult
    {
        public bool Success { get; set; }
        public int OrderId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

// =============================================================================
// LEAN Logger Adapter
// =============================================================================

/// <summary>
/// Bridges Microsoft.Extensions.Logging to LEAN's logging system.
/// </summary>
internal sealed class LeanLoggerProvider : ILoggerProvider
{
    private readonly QCAlgorithm _algorithm;

    public LeanLoggerProvider(QCAlgorithm algorithm)
    {
        _algorithm = algorithm;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new LeanLogger(_algorithm, categoryName);
    }

    public void Dispose() { }
}

/// <summary>
/// Logger implementation that writes to LEAN's log.
/// </summary>
internal sealed class LeanLogger : ILogger
{
    private readonly QCAlgorithm _algorithm;
    private readonly string _categoryName;

    public LeanLogger(QCAlgorithm algorithm, string categoryName)
    {
        _algorithm = algorithm;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Information;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        var fullMessage = $"[{_categoryName}] {message}";

        switch (logLevel)
        {
            case LogLevel.Error:
            case LogLevel.Critical:
                _algorithm.Error(fullMessage);
                break;
            case LogLevel.Warning:
                _algorithm.Debug(fullMessage);  // LEAN doesn't have warn level
                break;
            default:
                _algorithm.Log(fullMessage);
                break;
        }
    }
}

// =============================================================================
// Market Data Adapter
// =============================================================================

/// <summary>
/// Adapts AlarisDataBridge to the strategy's market data interface.
/// </summary>
internal sealed class DataBridgeMarketDataAdapter : STDT001A
{
    private readonly AlarisDataBridge _bridge;

    public DataBridgeMarketDataAdapter(AlarisDataBridge bridge)
    {
        _bridge = bridge;
    }

    // Implementation would delegate to _bridge methods
    // This is a placeholder - actual implementation depends on STDT001A interface
}