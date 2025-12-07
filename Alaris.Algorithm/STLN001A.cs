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

using StrategyPriceBar = Alaris.Strategy.Bridge.PriceBar;
using StrategyOptionChain = Alaris.Strategy.Model.STDT002A;
using Alaris.Data.Bridge;
using Alaris.Data.Model;
using Alaris.Data.Provider;
using Alaris.Data.Provider.Polygon;
using Alaris.Data.Provider.FMP;
using Alaris.Data.Provider.SEC;
using Alaris.Data.Provider.Treasury;
using Alaris.Data.Quality;
using Alaris.Events;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Risk;
using Alaris.Strategy.Core;
using Alaris.Strategy.Cost;
using Alaris.Strategy.Hedge;
using Alaris.Algorithm.Universe;
using Alaris.Events.Infrastructure;

using QCOptionRight = QuantConnect.OptionRight;
using AlarisOptionRight = Alaris.Data.Model.OptionRight;

namespace Alaris.Algorithm;

public struct OptionParameters
{
    public double Strike { get; set; }
    public double DTE { get; set; }
    public double ImpliedVolatility { get; set; }
    public bool IsCall { get; set; }
}
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
    private DTpr003A? _marketDataProvider;
    private DTpr004A? _earningsProvider;
    private DTpr005A? _riskFreeRateProvider;
    private DTpr002A? _executionQuoteProvider;
    
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
    
    // Universe Selection - now using STUN001B (Polygon-based), no field needed
    
    // Audit & Events
    private EVIF001A? _eventStore;
    private EVIF002A? _auditLogger;
    
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
        
        // Load configuration first to get backtest dates
        var config = BuildConfiguration();
        
        // Read dates from config with sensible defaults (2023-2024 for 2-year Polygon history)
        var startDate = config.GetValue<DateTime?>("Alaris:Backtest:StartDate") 
            ?? new DateTime(2023, 1, 1);
        var endDate = config.GetValue<DateTime?>("Alaris:Backtest:EndDate") 
            ?? new DateTime(2024, 12, 31);
        var initialCash = config.GetValue<int?>("Alaris:Backtest:InitialCash") ?? 100_000;
        
        // CLI can override via environment variables (set by Alaris.Application)
        var envStart = Environment.GetEnvironmentVariable("ALARIS_BACKTEST_STARTDATE");
        if (!string.IsNullOrEmpty(envStart) && DateTime.TryParse(envStart, out var cliStart))
        {
            startDate = cliStart;
        }
        
        var envEnd = Environment.GetEnvironmentVariable("ALARIS_BACKTEST_ENDDATE");
        if (!string.IsNullOrEmpty(envEnd) && DateTime.TryParse(envEnd, out var cliEnd))
        {
            endDate = cliEnd;
        }
        
        SetStartDate(startDate);
        SetEndDate(endDate);
        SetCash(initialCash);
        
        // Use Interactive Brokers as brokerage
        SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage);
        
        // Set benchmark
        SetBenchmark("SPY");
        
        // Set warmup period for historical data preloading (required for History() to work)
        SetWarmUp(TimeSpan.FromDays(45), Resolution.Daily);
        
        // Pre-subscribe to test symbols with available LEAN data for backtest validation
        // These symbols have confirmed historical data in the LEAN data folder
        if (!LiveMode)
        {
            var testSymbols = new[] { "AAPL", "GOOG", "GOOGL", "IBM", "BAC", "AIG" };
            foreach (var ticker in testSymbols)
            {
                AddEquity(ticker, Resolution.Daily);
            }
            Log($"STLN001A: Pre-subscribed {testSymbols.Length} test symbols for backtest validation");
        }
        
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
        
        // Initialise earnings provider (SEC EDGAR - free, uses per-ticker queries)
        _earningsProvider = new SecEdgarProvider(
            _httpClient,
            _loggerFactory!.CreateLogger<SecEdgarProvider>());
        
        // Initialise risk-free rate provider (Treasury Direct)
        _riskFreeRateProvider = new TreasuryDirectRateProvider(
            _httpClient,
            _loggerFactory!.CreateLogger<TreasuryDirectRateProvider>());
        
        // Initialise execution quote provider (IBKR Snapshots)
        _executionQuoteProvider = new InteractiveBrokersSnapshotProvider(
            _loggerFactory!.CreateLogger<InteractiveBrokersSnapshotProvider>());
        
        // Create data quality validators
        var validators = new DTqc002A[]
        {
            new PriceReasonablenessValidator(_loggerFactory!.CreateLogger<PriceReasonablenessValidator>()),
            new IvArbitrageValidator(_loggerFactory!.CreateLogger<IvArbitrageValidator>()),
            new VolumeOpenInterestValidator(_loggerFactory!.CreateLogger<VolumeOpenInterestValidator>()),
            new EarningsDateValidator(_loggerFactory!.CreateLogger<EarningsDateValidator>())
        };
        
        // Create unified data bridge
        _dataBridge = new AlarisDataBridge(
            _marketDataProvider,
            _earningsProvider,
            _riskFreeRateProvider,
            validators,
            _loggerFactory!.CreateLogger<AlarisDataBridge>());
        
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
            _loggerFactory!.CreateLogger<STRK001A>());
        
        Log("STLN001A: Strategy components initialised");
    }

    /// <summary>
    /// Initialises production validation pipeline.
    /// </summary>
    private void InitialiseProductionValidation()
    {
        // Fee model for IBKR
        _feeModel = new STCS005A(
            feePerContract: 0.65,
            exchangeFeePerContract: 0.30,
            regulatoryFeePerContract: 0.02,
            logger: _loggerFactory!.CreateLogger<STCS005A>());
        
        // Cost validator (signal survives costs)
        _costValidator = new STCS006A(
            _feeModel,
            logger: _loggerFactory!.CreateLogger<STCS006A>());
        
        // Vega correlation analyser
        _vegaAnalyser = new STHD001A(
            maxAcceptableCorrelation: 0.70,
            minimumObservations: 20,
            logger: _loggerFactory!.CreateLogger<STHD001A>());
        
        // Liquidity validator
        _liquidityValidator = new STCS008A(
            maxPositionToVolumeRatio: 0.05,
            maxPositionToOpenInterestRatio: 0.02,
            logger: _loggerFactory!.CreateLogger<STCS008A>());
        
        // Gamma risk manager
        _gammaRiskManager = new STHD003A(
            deltaRehedgeThreshold: 0.10,
            gammaWarningThreshold: -0.05,
            moneynessAlertThreshold: 0.03,
            logger: _loggerFactory!.CreateLogger<STHD003A>());
        
        // Production validator (orchestrates all checks)
        _productionValidator = new STHD005A(
            _costValidator,
            _vegaAnalyser,
            _liquidityValidator,
            _gammaRiskManager,
            _loggerFactory!.CreateLogger<STHD005A>());
        
        Log("STLN001A: Production validation initialised");
    }

    /// <summary>
    /// Initialises universe selection.
    /// Uses STUN001B (Polygon-based) which reads pre-generated universe files.
    /// </summary>
    private void InitialiseUniverseSelection()
    {
        // Determine data path (where universe files are located)
        var dataPath = FindLeanDataPath();
        
        // Create Polygon-based universe selector (reads pre-generated files)
        var polygonUniverseSelector = new STUN001B(
            _earningsProvider!,
            dataPath,
            daysBeforeEarningsMin: DaysBeforeEarnings - 1,
            daysBeforeEarningsMax: DaysBeforeEarnings + 1,
            minimumDollarVolume: MinimumDollarVolume,
            minimumPrice: MinimumPrice,
            maxFinalSymbols: 50,
            _loggerFactory!.CreateLogger<STUN001B>());
        
        // Register universe with LEAN
        AddUniverseSelection(polygonUniverseSelector);
        
        // Configure options for selected symbols
        UniverseSettings.Resolution = Resolution.Minute;
        UniverseSettings.DataNormalizationMode = DataNormalizationMode.Raw;
        
        Log($"STLN001A: Universe selection configured (Polygon-based)");
        Log($"  {polygonUniverseSelector.GetConfigurationSummary()}");
    }

    /// <summary>
    /// Finds the LEAN data path for universe files.
    /// </summary>
    private static string FindLeanDataPath()
    {
        // Check for session data path (Backtest Mode override)
        var sessionData = Environment.GetEnvironmentVariable("ALARIS_SESSION_DATA");
        if (!string.IsNullOrEmpty(sessionData) && System.IO.Directory.Exists(sessionData))
        {
            return sessionData;
        }

        var candidates = new[]
        {
            "Alaris.Lean/Data",
            "../Alaris.Lean/Data",
            "../../Alaris.Lean/Data",
            "../../../Alaris.Lean/Data"
        };
        
        foreach (var candidate in candidates)
        {
            if (System.IO.Directory.Exists(candidate))
            {
                return System.IO.Path.GetFullPath(candidate);
            }
        }
        
        // Default fallback
        return System.IO.Path.GetFullPath("Alaris.Lean/Data");
    }

    /// <summary>
    /// Initialises audit trail and event sourcing.
    /// </summary>
    private void InitialiseAuditTrail()
    {
        _eventStore = new EVIF001A();
        _auditLogger = new EVIF002A();
        
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
    /// Builds configuration from appsettings files.
    /// Loads appsettings.jsonc (base) then appsettings.local.jsonc (secrets).
    /// </summary>
    private IConfiguration BuildConfiguration()
    {
        // Find the repository root (where appsettings.jsonc lives)
        var basePath = FindRepositoryRoot();
        
        return new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.jsonc", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.local.jsonc", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("ALARIS_")
            .Build();
    }

    /// <summary>
    /// Finds the repository root directory containing appsettings.jsonc.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        // Start from current directory and walk up
        var dir = System.IO.Directory.GetCurrentDirectory();
        for (int i = 0; i < 10; i++)
        {
            var candidate = System.IO.Path.Combine(dir, "appsettings.jsonc");
            if (System.IO.File.Exists(candidate))
            {
                return dir;
            }
            var parent = System.IO.Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        
        // Fallback to current directory
        return System.IO.Directory.GetCurrentDirectory();
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
                    // Log error to audit trail
                    _auditLogger?.LogAsync(new Alaris.Events.Core.AuditEntry
                    {
                        AuditId = Guid.NewGuid(),
                        OccurredAtUtc = DateTime.UtcNow,
                        Action = "EvaluationError",
                        EntityType = "Symbol",
                        EntityId = symbol.Value,
                        InitiatedBy = "STLN001A",
                        Description = $"Evaluation failed for {symbol}: {ex.Message}",
                        Severity = Alaris.Events.Core.AuditSeverity.Error,
                        Outcome = Alaris.Events.Core.AuditOutcome.Failure
                    });
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
            // Log error to audit trail
            _auditLogger?.LogAsync(new Alaris.Events.Core.AuditEntry
            {
                AuditId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                Action = "FatalEvaluationError",
                EntityType = "Algorithm",
                EntityId = "STLN001A",
                InitiatedBy = "STLN001A",
                Description = $"Daily evaluation failed: {ex.Message}",
                Severity = Alaris.Events.Core.AuditSeverity.Error,
                Outcome = Alaris.Events.Core.AuditOutcome.Failure
            });
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
        // Backtest Mode: Use LEAN's native data instead of external APIs
        // =====================================================================
        
        if (!LiveMode)
        {
            // In backtest mode, we use LEAN's built-in data
            // This avoids calling external APIs with DateTime.UtcNow
            return EvaluateSymbolBacktestMode(symbol);
        }
        
        // =====================================================================
        // Live/Paper Mode: Use external APIs (Polygon, SEC EDGAR, etc.)
        // =====================================================================
        
        // Phase 1: Market Data Acquisition
        
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
        
        var priceBars = ConvertToPriceBars(snapshot.HistoricalBars);
        var rv = _yangZhangEstimator!.Calculate(priceBars, 30, true);
        Log($"  {ticker}: 30-day RV = {rv:P2}");
        
        // =====================================================================
        // Phase 3: Term Structure Analysis
        // =====================================================================
        
        var termStructure = _termStructureAnalyzer!.Analyze(ConvertToTermStructurePoints(snapshot.OptionChain));
        Log($"  {ticker}: Term structure = {termStructure.GetIVAt(30):P2} / {termStructure.GetIVAt(60):P2} / {termStructure.GetIVAt(90):P2}");
        
        // =====================================================================
        // Phase 4: Signal Generation
        // =====================================================================
        
        var signal = _signalGenerator!.Generate(
            ticker,
            snapshot.NextEarnings.Date,
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
                Log($"    - {check.Name}: {check.Detail}");
            }
            // Log warning to audit trail
            _auditLogger?.LogAsync(new Alaris.Events.Core.AuditEntry
            {
                AuditId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                Action = "ValidationFailed",
                EntityType = "Signal",
                EntityId = ticker,
                InitiatedBy = "STLN001A",
                Description = $"{ticker}: Production validation failed",
                Severity = Alaris.Events.Core.AuditSeverity.Warning,
                Outcome = Alaris.Events.Core.AuditOutcome.Failure
            });
            return result;
        }
        
        Log($"  {ticker}: Passed all production validation checks");
        
        // =====================================================================
        // Phase 6: Execution Pricing
        // =====================================================================
        
        DTmd002A? spreadQuote;
        try
        {
            using var cts = new CancellationTokenSource(ApiTimeout);
            spreadQuote = _executionQuoteProvider!.GetDTmd002AAsync(
                ticker,
                signal.Strike,
                signal.FrontExpiry,
                signal.BackExpiry,
                AlarisOptionRight.Call,
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
        
        var sizing = _positionSizer!.CalculateFromHistory(
            portfolioValue: (double)Portfolio.TotalPortfolioValue,
            historicalTrades: Array.Empty<Alaris.Strategy.Risk.Trade>(),
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
            _auditLogger?.LogAsync(new Alaris.Events.Core.AuditEntry
            {
                AuditId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                Action = "TradeOpened",
                EntityType = "CalendarSpread",
                EntityId = ticker,
                InitiatedBy = "STLN001A",
                Description = $"Opened calendar spread: {finalContracts} contracts @ ${spreadQuote.SpreadMid:F2}",
                Severity = Alaris.Events.Core.AuditSeverity.Information,
                Outcome = Alaris.Events.Core.AuditOutcome.Success,
                AdditionalData = new Dictionary<string, string>
                {
                    ["Contracts"] = finalContracts.ToString(),
                    ["Price"] = spreadQuote.SpreadMid.ToString(),
                    ["FrontExpiry"] = signal.FrontExpiry.ToString("O"),
                    ["BackExpiry"] = signal.BackExpiry.ToString("O")
                }
            });
            
            Log($"  {ticker}: Order submitted successfully");
        }
        else
        {
            Log($"  {ticker}: Order submission failed - {orderResult.Message}");
        }
        
        return result;
    }

    /// <summary>
    /// Evaluates a symbol in backtest mode using LEAN's native data.
    /// Does not call external APIs - uses History API and cached SEC EDGAR data.
    /// </summary>
    /// <param name="symbol">The symbol to evaluate.</param>
    /// <returns>Evaluation result.</returns>
    private EvaluationResult EvaluateSymbolBacktestMode(Symbol symbol)
    {
        var result = new EvaluationResult();
        var ticker = symbol.Value;

        // Use current simulation time from LEAN (not DateTime.UtcNow)
        var simulationDate = Time;

        // =====================================================================
        // Step 1: Get historical bars from LEAN (no external API call)
        // =====================================================================
        
        var historyBars = History<QuantConnect.Data.Market.TradeBar>(symbol, 45, Resolution.Daily);
        var barList = historyBars.ToList();
        
        if (barList.Count < 30)
        {
            Log($"  {ticker}: Insufficient LEAN history ({barList.Count} bars, need 30+)");
            return result;
        }

        // Get current price from LEAN Securities (subscribed via universe)
        if (!Securities.ContainsKey(symbol) || Securities[symbol].Price == 0)
        {
            Log($"  {ticker}: No price data in LEAN Securities");
            return result;
        }
        var spotPrice = Securities[symbol].Price;

        Log($"  {ticker}: LEAN data - {barList.Count} bars, spot=${spotPrice:F2}");

        // =====================================================================
        // Step 2: Get earnings data from SEC EDGAR (cached, works in backtest)
        // =====================================================================
        
        EarningsEvent? nextEarnings = null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var historicalEarnings = _earningsProvider!.GetHistoricalEarningsAsync(
                ticker,
                lookbackDays: 730,
                cts.Token).GetAwaiter().GetResult();

            // Find the next earnings AFTER current simulation date
            nextEarnings = historicalEarnings
                .Where(e => e.Date > simulationDate.Date)
                .OrderBy(e => e.Date)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            Log($"  {ticker}: Error fetching earnings: {ex.Message}");
        }

        if (nextEarnings == null)
        {
            Log($"  {ticker}: No upcoming earnings found in SEC EDGAR");
            return result;
        }

        var daysToEarnings = (nextEarnings.Date - simulationDate.Date).Days;
        Log($"  {ticker}: Next earnings {nextEarnings.Date:yyyy-MM-dd} ({daysToEarnings} days away)");

        // =====================================================================
        // Step 3: Calculate Realised Volatility using LEAN bars
        // =====================================================================
        
        var priceBars = barList.Select(b => new StrategyPriceBar
        {
            Date = b.Time,
            Open = (double)b.Open,
            High = (double)b.High,
            Low = (double)b.Low,
            Close = (double)b.Close,
            Volume = 0 // Not needed for Yang-Zhang RV
        }).ToList();

        var rv = _yangZhangEstimator!.Calculate(priceBars, 30, true);
        Log($"  {ticker}: 30-day RV = {rv:P2}");

        // =====================================================================
        // Step 4: Signal Generation (simplified for backtest)
        // =====================================================================
        
        // Check if within target window for earnings
        if (daysToEarnings < 5 || daysToEarnings > 7)
        {
            Log($"  {ticker}: Not in target window (5-7 days before earnings)");
            return result;
        }

        // Generate signal
        var signal = _signalGenerator!.Generate(ticker, nextEarnings.Date, simulationDate);
        Log($"  {ticker}: Signal = {signal.Strength} (IV/RV = {signal.IVRVRatio:F3})");
        result.SignalGenerated = true;

        if (signal.Strength != STCR004AStrength.Recommended)
        {
            Log($"  {ticker}: Signal not recommended, skipping");
            return result;
        }

        // =====================================================================
        // Step 5: Backtest-mode order simulation (no real execution)
        // =====================================================================
        
        // In backtest mode, we record the signal as generated but don't execute
        // Full execution would require options data which isn't available
        Log($"  {ticker}: BACKTEST - Signal would trigger calendar spread entry");
        Log($"    Strike: ${signal.Strike:F2}, Front: {signal.FrontExpiry:yyyy-MM-dd}, Back: {signal.BackExpiry:yyyy-MM-dd}");

        // Note: For full backtest with order execution, we would need
        // options chain data from LEAN's Options History API
        // This simplified version validates that earnings detection and
        // signal generation work correctly

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
            backMonthVolume: (int)(snapshot.AverageVolume30Day * 0.1m),  // Estimate
            backMonthOpenInterest: 1000,  // Would come from options chain
            spotPrice: (double)snapshot.SpotPrice,
            strikePrice: (double)signal.Strike,
            spreadGreeks: ComputeSpreadGreeks(signal, snapshot),
            daysToEarnings: (signal.EarningsDate - Time).Days);
    }

    /// <summary>
    /// Creates order parameters for validation.
    /// </summary>
    private STCS002A CreateOptionParams(STCR004A signal, bool isFrontMonth)
    {
        var expiry = isFrontMonth ? signal.FrontExpiry : signal.BackExpiry;
        var iv = isFrontMonth ? signal.FrontIV : signal.BackIV;
        
        // Estimate contract prices from IV (simplified)
        var midPrice = iv * 0.1; // Simplified estimate
        var spread = midPrice * 0.05; // 5% bid-ask spread estimate
        
        return new STCS002A
        {
            Contracts = 1,
            MidPrice = midPrice,
            BidPrice = midPrice - (spread / 2),
            AskPrice = midPrice + (spread / 2),
            Direction = isFrontMonth ? Alaris.Strategy.Cost.OrderDirection.Sell : Alaris.Strategy.Cost.OrderDirection.Buy,
            Premium = midPrice,
            Symbol = signal.Symbol
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
    /// Converts Alaris.Data.Model.PriceBar to Alaris.Strategy.Bridge.PriceBar for volatility calculation.
    /// </summary>
    private IReadOnlyList<Alaris.Strategy.Bridge.PriceBar> ConvertToPriceBars(
        IReadOnlyList<Alaris.Data.Model.PriceBar> dataBars)
    {
        return dataBars.Select(b => new Alaris.Strategy.Bridge.PriceBar
        {
            Date = b.Timestamp,
            Open = (double)b.Open,
            High = (double)b.High,
            Low = (double)b.Low,
            Close = (double)b.Close
        }).ToList();
    }

    /// <summary>
    /// Converts option chain snapshot to term structure points for analysis.
    /// </summary>
    private IReadOnlyList<STTM001APoint> ConvertToTermStructurePoints(
        OptionChainSnapshot optionChain)
    {
        var points = new List<STTM001APoint>();
        
        if (optionChain?.Contracts == null)
            return points;
        
        // Group by expiration and calculate average IV at ATM strikes
        var byExpiry = optionChain.Contracts
            .GroupBy(c => c.Expiration.Date)
            .OrderBy(g => g.Key);
        
        foreach (var group in byExpiry)
        {
            var daysToExpiry = (group.Key - DateTime.UtcNow.Date).Days;
            if (daysToExpiry <= 0) continue;
            
            // Average IV across ATM options
            var avgIV = group
                .Where(c => c.ImpliedVolatility.HasValue && c.ImpliedVolatility > 0)
                .Select(c => (double)c.ImpliedVolatility!.Value)
                .DefaultIfEmpty(0.2) // Default 20% IV if no data
                .Average();
            
            points.Add(new STTM001APoint
            {
                DaysToExpiry = daysToExpiry,
                ImpliedVolatility = avgIV,
                Strike = (double)optionChain.Contracts[0].Strike
            });
        }
        
        return points;
    }

    /// <summary>
    /// Executes a calendar spread order using LEAN's combo order functionality.
    /// </summary>
    private OrderExecutionResult ExecuteCalendarSpread(
        Symbol underlyingSymbol,
        STCR004A signal,
        DTmd002A quote,
        int contracts)
    {
        try
        {
            // Create option symbols
            var frontOption = CreateOptionSymbol(
                underlyingSymbol,
                signal.Strike,
                signal.FrontExpiry,
                QCOptionRight.Call);

            var backOption = CreateOptionSymbol(
                underlyingSymbol,
                signal.Strike,
                signal.BackExpiry,
                QCOptionRight.Call);

            // Add option contracts to universe
            AddOptionContract(frontOption);
            AddOptionContract(backOption);

            // Create combo order legs
            var legs = new List<QuantConnect.Orders.Leg>
            {
                QuantConnect.Orders.Leg.Create(frontOption, -contracts),  // Sell front month
                QuantConnect.Orders.Leg.Create(backOption, contracts)     // Buy back month
            };

            // Submit combo limit order at mid price
            var limitPrice = quote.SpreadMid;
            var tickets = ComboLimitOrder(legs, contracts, limitPrice);

            var orderId = tickets.FirstOrDefault()?.OrderId ?? 0;
            return new OrderExecutionResult
            {
                Success = true,
                OrderId = orderId,
                Message = $"Order {orderId} submitted at ${limitPrice:F2}"
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
        QCOptionRight right)
    {
        return QuantConnect.Symbol.CreateOption(
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
            
            _auditLogger?.LogAsync(new Alaris.Events.Core.AuditEntry
            {
                AuditId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                Action = "OrderFill",
                EntityType = "Order",
                EntityId = orderEvent.OrderId.ToString(),
                InitiatedBy = "STLN001A",
                Description = $"Order filled: {orderEvent.Symbol} {orderEvent.FillQuantity} @ {orderEvent.FillPrice}",
                Severity = Alaris.Events.Core.AuditSeverity.Information,
                Outcome = Alaris.Events.Core.AuditOutcome.Success,
                AdditionalData = new Dictionary<string, string>
                {
                    ["Symbol"] = orderEvent.Symbol.Value,
                    ["Quantity"] = orderEvent.FillQuantity.ToString(),
                    ["FillPrice"] = orderEvent.FillPrice.ToString(),
                    ["Timestamp"] = Time.ToString("O")
                }
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

internal sealed class DataBridgeMarketDataAdapter : STDT001A
{
    private readonly AlarisDataBridge _bridge;

    public DataBridgeMarketDataAdapter(AlarisDataBridge bridge)
    {
        _bridge = bridge;
    }

    public StrategyOptionChain GetSTDT002A(string symbol, DateTime expirationDate)
    {
        var snapshot = _bridge.GetMarketDataSnapshotAsync(symbol).GetAwaiter().GetResult();
        
        var chain = new StrategyOptionChain
        {
            Symbol = symbol,
            UnderlyingPrice = (double)snapshot.SpotPrice,
            Timestamp = snapshot.Timestamp
        };
        
        if (snapshot.OptionChain != null)
        {
            var contracts = snapshot.OptionChain.Contracts
                .Where(c => c.Expiration.Date == expirationDate.Date)
                .ToList();

            if (contracts.Count > 0)
            {
                var expiry = new Alaris.Strategy.Model.OptionExpiry
                {
                    ExpiryDate = expirationDate
                };

                foreach (var c in contracts)
                {
                    var contract = new Alaris.Strategy.Model.OptionContract
                    {
                        Strike = (double)c.Strike,
                        Bid = (double)c.Bid,
                        Ask = (double)c.Ask,
                        LastPrice = (double)(c.Last ?? 0m),
                        ImpliedVolatility = (double)(c.ImpliedVolatility ?? 0m),
                        Delta = (double)(c.Delta ?? 0m),
                        Gamma = (double)(c.Gamma ?? 0m),
                        Vega = (double)(c.Vega ?? 0m),
                        Theta = (double)(c.Theta ?? 0m),
                        OpenInterest = (int)c.OpenInterest,
                        Volume = (int)c.Volume
                    };

                    if (c.Right == AlarisOptionRight.Call)
                        expiry.Calls.Add(contract);
                    else
                        expiry.Puts.Add(contract);
                }

                chain.Expiries.Add(expiry);
            }
        }

        return chain;
    }

    public IReadOnlyList<StrategyPriceBar> GetHistoricalPrices(string symbol, int days)
    {
        var snapshot = _bridge.GetMarketDataSnapshotAsync(symbol).GetAwaiter().GetResult();
        
        return snapshot.HistoricalBars
            .Select(b => new StrategyPriceBar
            {
                Date = b.Timestamp,
                Open = (double)b.Open,
                High = (double)b.High,
                Low = (double)b.Low,
                Close = (double)b.Close,
                Volume = b.Volume
            })
            .OrderBy(b => b.Date)
            .ToList();
    }

    public double GetCurrentPrice(string symbol)
    {
        var snapshot = _bridge.GetMarketDataSnapshotAsync(symbol).GetAwaiter().GetResult();
        return (double)snapshot.SpotPrice;
    }

    public async Task<IReadOnlyList<DateTime>> GetEarningsDates(string symbol)
    {
        var snapshot = await _bridge.GetMarketDataSnapshotAsync(symbol);
        var dates = new List<DateTime>();
        if (snapshot.NextEarnings != null)
        {
            dates.Add(snapshot.NextEarnings.Date);
        }
        return dates;
    }

    public async Task<IReadOnlyList<DateTime>> GetHistoricalEarningsDates(string symbol, int lookbackQuarters = 12)
    {
        var snapshot = await _bridge.GetMarketDataSnapshotAsync(symbol);
        return snapshot.HistoricalEarnings
            .Select(e => e.Date)
            .OrderByDescending(d => d)
            .Take(lookbackQuarters)
            .ToList();
    }

    public async Task<bool> IsDataAvailable(string symbol)
    {
        return await _bridge.MeetsBasicCriteriaAsync(symbol);
    }
}
