// STLN001A.cs - Alaris Earnings Volatility Trading Algorithm (LEAN integration)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Orders;
using QuantConnect.Securities.Option;
using Refit;

using StrategyPriceBar = Alaris.Strategy.Bridge.PriceBar;
using StrategyOptionChain = Alaris.Strategy.Model.STDT002A;
using AlarisTimeProvider = Alaris.Core.Time.ITimeProvider;
using Alaris.Core.Time;
using Alaris.Infrastructure.Data.Bridge;
using Alaris.Infrastructure.Data.Model;
using Alaris.Infrastructure.Data.Provider;
using Alaris.Infrastructure.Data.Provider.Polygon;
using Alaris.Infrastructure.Data.Provider.Nasdaq;
using Alaris.Infrastructure.Data.Provider.Treasury;
using Alaris.Infrastructure.Data.Http.Contracts;
using Alaris.Infrastructure.Data.Quality;
using Alaris.Infrastructure.Events;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Risk;
using Alaris.Strategy.Core;
using Alaris.Strategy.Cost;
using Alaris.Strategy.Hedge;
using Alaris.Algorithm.Universe;
using Alaris.Infrastructure.Events.Infrastructure;

using QCOptionRight = QuantConnect.OptionRight;
using AlarisOptionRight = Alaris.Infrastructure.Data.Model.OptionRight;

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
    // Configuration Settings
    private StrategySettings _strategySettings = StrategySettings.Empty;
    private BacktestSettings _backtestSettings = BacktestSettings.Empty;
    private DataProviderSettings _dataProviderSettings = DataProviderSettings.Empty;
    private ValidationSettings _validationSettings = ValidationSettings.Empty;
    private FeeSettings _feeSettings = FeeSettings.Empty;

    // Alaris Components (Instance-Based)
    
    // Data Infrastructure
    private AlarisDataBridge? _dataBridge;
    private DTpr003A? _marketDataProvider;
    private DTpr004A? _earningsProvider;
    private DTpr005A? _riskFreeRateProvider;
    private DTpr002A? _executionQuoteProvider;
    
    // Strategy Components
    private STCR003A? _yangZhangEstimator;
    private STTM001A? _termStructureAnalyzer;
    private STCR001A? _signalGenerator;
    private DataBridgeMarketDataAdapter? _marketDataAdapter;
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
    private IConfiguration? _configuration;
    
    // Time Provider (backtest-aware)
    private AlarisTimeProvider? _timeProvider;
    
    // State Tracking
    private readonly HashSet<Symbol> _activePositions = new();
    private readonly Dictionary<Symbol, DateTime> _positionEntryDates = new();

    // QCAlgorithm Lifecycle
    
    /// <summary>
    /// Initialises the algorithm at start.
    /// Called once by LEAN engine.
    /// </summary>
    public override void Initialize()
    {
        // Basic Algorithm Configuration
        
        // Load configuration first to set strategy and backtest settings
        var config = BuildConfiguration();
        LoadSettings(config);
        _configuration = config;
        
        var startDate = _backtestSettings.StartDate;
        var endDate = _backtestSettings.EndDate;
        var initialCash = _backtestSettings.InitialCash;
        
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

        if (endDate <= startDate)
            throw new InvalidOperationException("Backtest EndDate must be after StartDate.");
        
        SetStartDate(startDate);
        SetEndDate(endDate);
        SetCash(initialCash);
        
        // Use Interactive Brokers as brokerage
        SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage);
        
        // Set benchmark
        SetBenchmark("SPY");
        
        // Set warmup period for historical data preloading (required for History() to work)
        SetWarmUp(TimeSpan.FromDays(_backtestSettings.WarmUpDays), Resolution.Daily);
        
        // Pre-subscribe to session symbols for backtest validation
        // Use symbols from the session (set via ALARIS_SESSION_SYMBOLS env var)
        if (!LiveMode)
        {
            var envSymbols = Environment.GetEnvironmentVariable("ALARIS_SESSION_SYMBOLS");
            var testSymbols = string.IsNullOrEmpty(envSymbols)
                ? new[] { "SPY" }  // Default fallback
                : envSymbols.Split(',', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var ticker in testSymbols)
            {
                AddEquity(ticker.Trim(), Resolution.Daily);
            }
            Log($"STLN001A: Pre-subscribed {testSymbols.Length} session symbols for backtest validation");
        }
        
        // Initialise Alaris Components
        
        InitialiseLogging();
        InitialiseDataProviders();
        InitialiseStrategyComponents();
        InitialiseProductionValidation();
        InitialiseUniverseSelection();
        InitialiseAuditTrail();
        
        // Schedule Daily Evaluation
        
        ScheduleDailyEvaluation();
        
        // Log Initialisation Complete
        
        Log("═══════════════════════════════════════════════════════════════════");
        Log("STLN001A: Alaris Earnings Algorithm Initialised");
        Log($"  Start Date: {StartDate:yyyy-MM-dd}");
        Log($"  Cash: {Portfolio.Cash:C}");
        Log($"  Target Days Before Earnings: {_strategySettings.DaysBeforeEarningsMin}-{_strategySettings.DaysBeforeEarningsMax}");
        Log($"  Min Dollar Volume: {_strategySettings.MinimumDollarVolume:C}");
        Log($"  Max Portfolio Allocation: {_strategySettings.PortfolioAllocationLimit:P0}");
        Log($"  Max Position Allocation: {_strategySettings.MaxPositionAllocation:P0}");
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
        
        base.OnEndOfAlgorithm();
    }

    // Initialisation Methods
    
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
        // Initialise market data provider (Polygon) with Refit
        var configuration = _configuration ?? BuildConfiguration();
        var polygonHttpClient = new HttpClient { BaseAddress = _dataProviderSettings.PolygonBaseUri, Timeout = _dataProviderSettings.PolygonTimeout };
        polygonHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Alaris/1.0 (Quantitative Trading System)");
        IPolygonApi polygonApi = RestService.For<IPolygonApi>(polygonHttpClient);
        _marketDataProvider = new PolygonApiClient(
            polygonApi,
            configuration,
            _loggerFactory!.CreateLogger<PolygonApiClient>());
        
        // Initialise earnings provider (NASDAQ - free, no rate limits) with Refit
        var nasdaqHttpClient = new HttpClient { BaseAddress = _dataProviderSettings.NasdaqBaseUri, Timeout = _dataProviderSettings.NasdaqTimeout };
        nasdaqHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        nasdaqHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        INasdaqCalendarApi nasdaqApi = RestService.For<INasdaqCalendarApi>(nasdaqHttpClient);
        _earningsProvider = new NasdaqEarningsProvider(
            nasdaqApi,
            _loggerFactory!.CreateLogger<NasdaqEarningsProvider>());
        
        // Enable cache-only mode for backtests (prevents 403 errors from NASDAQ)
        if (!LiveMode)
        {
            _earningsProvider.EnableCacheOnlyMode();
            Log("STLN001A: Earnings provider set to cache-only mode (backtest)");
        }
        // Initialise risk-free rate provider (Treasury Direct) with Refit
        var treasuryHttpClient = new HttpClient { BaseAddress = _dataProviderSettings.TreasuryBaseUri, Timeout = _dataProviderSettings.TreasuryTimeout };
        treasuryHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Alaris/1.0 (Quantitative Trading System)");
        ITreasuryDirectApi treasuryApi = RestService.For<ITreasuryDirectApi>(treasuryHttpClient);
        _riskFreeRateProvider = new TreasuryDirectRateProvider(
            treasuryApi,
            _loggerFactory!.CreateLogger<TreasuryDirectRateProvider>());
        
        // Initialise execution quote provider (IBKR Snapshots)
        _executionQuoteProvider = new InteractiveBrokersSnapshotProvider(
            _loggerFactory!.CreateLogger<InteractiveBrokersSnapshotProvider>());
        
        // Create time provider for backtest-aware validation
        // Use LEAN's Time property as the simulated time source
        _timeProvider = new BacktestTimeProvider(() => Time);
        
        // Create data quality validators with time provider
        var validators = new DTqc002A[]
        {
            new PriceReasonablenessValidator(
                _loggerFactory!.CreateLogger<PriceReasonablenessValidator>(),
                _timeProvider),
            new IvArbitrageValidator(_loggerFactory!.CreateLogger<IvArbitrageValidator>()),
            new VolumeOpenInterestValidator(_loggerFactory!.CreateLogger<VolumeOpenInterestValidator>()),
            new EarningsDateValidator(
                _loggerFactory!.CreateLogger<EarningsDateValidator>(),
                _timeProvider)
        };
        
        // Create unified data bridge
        _dataBridge = new AlarisDataBridge(
            _marketDataProvider,
            _earningsProvider,
            _riskFreeRateProvider,
            validators,
            _loggerFactory!.CreateLogger<AlarisDataBridge>());
        
        // Set session data path for cached data access (options, etc.)
        var sessionDataPath = Environment.GetEnvironmentVariable("ALARIS_SESSION_DATA");
        if (!string.IsNullOrEmpty(sessionDataPath))
        {
            _dataBridge.SetSessionDataPath(sessionDataPath);
            Log($"STLN001A: Session data path set for cache: {sessionDataPath}");
        }
        
        Log("STLN001A: Data providers initialised");
    }

    /// <summary>
    /// Initialises strategy analysis components from Alaris.Strategy.
    /// </summary>
    private void InitialiseStrategyComponents()
    {
        // Yang-Zhang realised volatility estimator
        _yangZhangEstimator = new STCR003A();
        
        // Term structure analyser
        _termStructureAnalyzer = new STTM001A();
        
        // Signal generator (requires market data adapter)
        _marketDataAdapter = new DataBridgeMarketDataAdapter(_dataBridge!);
        _signalGenerator = new STCR001A(
            _marketDataAdapter,
            _yangZhangEstimator,
            _termStructureAnalyzer,
            earningsCalibrator: null,  // Use default calibration
            logger: _loggerFactory!.CreateLogger<STCR001A>());
        
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
            feePerContract: Convert.ToDouble(_feeSettings.PerContract),
            exchangeFeePerContract: Convert.ToDouble(_feeSettings.ExchangePerContract),
            regulatoryFeePerContract: Convert.ToDouble(_feeSettings.RegulatoryPerContract),
            logger: _loggerFactory!.CreateLogger<STCS005A>());
        
        // Cost validator (signal survives costs)
        _costValidator = new STCS006A(
            _feeModel,
            logger: _loggerFactory!.CreateLogger<STCS006A>());
        
        // Vega correlation analyser
        _vegaAnalyser = new STHD001A(
            maxAcceptableCorrelation: _validationSettings.MaxVegaCorrelation,
            minimumObservations: _validationSettings.MinimumVegaObservations,
            logger: _loggerFactory!.CreateLogger<STHD001A>());
        
        // Liquidity validator
        _liquidityValidator = new STCS008A(
            maxPositionToVolumeRatio: _validationSettings.MaxPositionToVolumeRatio,
            maxPositionToOpenInterestRatio: _validationSettings.MaxPositionToOpenInterestRatio,
            logger: _loggerFactory!.CreateLogger<STCS008A>());
        
        // Gamma risk manager
        _gammaRiskManager = new STHD003A(
            deltaRehedgeThreshold: _validationSettings.DeltaRehedgeThreshold,
            gammaWarningThreshold: _validationSettings.GammaWarningThreshold,
            moneynessAlertThreshold: _validationSettings.MoneynessAlertThreshold,
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
            daysBeforeEarningsMin: _strategySettings.DaysBeforeEarningsMin,
            daysBeforeEarningsMax: _strategySettings.DaysBeforeEarningsMax,
            minimumDollarVolume: _strategySettings.MinimumDollarVolume,
            minimumPrice: _strategySettings.MinimumPrice,
            maxFinalSymbols: _strategySettings.MaxUniverseSymbols,
            _loggerFactory!.CreateLogger<STUN001B>());
        
        // Register universe with LEAN
        AddUniverseSelection(polygonUniverseSelector);
        
        // Configure options for selected symbols
        UniverseSettings.Resolution = Resolution.Daily;
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
            TimeRules.At(_strategySettings.EvaluationTimeHour, _strategySettings.EvaluationTimeMinute),
            EvaluatePositions);

        Log($"STLN001A: Scheduled daily evaluation at {_strategySettings.EvaluationTimeHour:D2}:{_strategySettings.EvaluationTimeMinute:D2} ET");
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

    /// <summary>
    /// Loads configuration-backed settings with validation.
    /// </summary>
    private void LoadSettings(IConfiguration configuration)
    {
        _strategySettings = new StrategySettings(
            DaysBeforeEarningsMin: GetRequiredInt(configuration, "Alaris:Strategy:DaysBeforeEarningsMin"),
            DaysBeforeEarningsMax: GetRequiredInt(configuration, "Alaris:Strategy:DaysBeforeEarningsMax"),
            MinimumDollarVolume: GetRequiredDecimal(configuration, "Alaris:Strategy:MinimumDollarVolume"),
            MinimumPrice: GetRequiredDecimal(configuration, "Alaris:Strategy:MinimumPrice"),
            PortfolioAllocationLimit: GetRequiredDecimal(configuration, "Alaris:Strategy:PortfolioAllocationLimit"),
            MaxPositionAllocation: GetRequiredDecimal(configuration, "Alaris:Strategy:MaxPositionAllocation"),
            MaxConcurrentPositions: GetRequiredInt(configuration, "Alaris:Strategy:MaxConcurrentPositions"),
            MaxUniverseSymbols: GetRequiredInt(configuration, "Alaris:Strategy:MaxUniverseSymbols"),
            MaxCoarseSymbols: GetRequiredInt(configuration, "Alaris:Strategy:MaxCoarseSymbols"),
            EvaluationTimeHour: GetRequiredInt(configuration, "Alaris:Strategy:EvaluationTimeHour"),
            EvaluationTimeMinute: GetRequiredInt(configuration, "Alaris:Strategy:EvaluationTimeMinute"),
            RealisedVolatilityWindowDays: GetRequiredInt(configuration, "Alaris:Strategy:RealisedVolatilityWindowDays"),
            DefaultImpliedVolatility: GetRequiredDouble(configuration, "Alaris:Strategy:DefaultImpliedVolatility"));
        _strategySettings.Validate();

        _backtestSettings = new BacktestSettings(
            StartDate: GetRequiredDateTime(configuration, "Alaris:Backtest:StartDate"),
            EndDate: GetRequiredDateTime(configuration, "Alaris:Backtest:EndDate"),
            InitialCash: GetRequiredDecimal(configuration, "Alaris:Backtest:InitialCash"),
            WarmUpDays: GetRequiredInt(configuration, "Alaris:Backtest:WarmUpDays"),
            HistoryLookbackDays: GetRequiredInt(configuration, "Alaris:Backtest:HistoryLookbackDays"),
            EarningsLookaheadDays: GetRequiredInt(configuration, "Alaris:Backtest:EarningsLookaheadDays"),
            EarningsLookbackDays: GetRequiredInt(configuration, "Alaris:Backtest:EarningsLookbackDays"),
            EarningsQueryTimeout: TimeSpan.FromSeconds(GetRequiredInt(configuration, "Alaris:Backtest:EarningsQueryTimeoutSeconds")));
        _backtestSettings.Validate();
        if (_backtestSettings.HistoryLookbackDays <= _strategySettings.RealisedVolatilityWindowDays)
            throw new InvalidOperationException("HistoryLookbackDays must exceed RealisedVolatilityWindowDays.");

        _dataProviderSettings = new DataProviderSettings(
            PolygonBaseUri: GetRequiredUri(configuration, "Alaris:DataProviders:Polygon:BaseUrl"),
            PolygonTimeout: TimeSpan.FromSeconds(GetRequiredInt(configuration, "Alaris:DataProviders:Polygon:TimeoutSeconds")),
            NasdaqBaseUri: GetRequiredUri(configuration, "Alaris:DataProviders:Nasdaq:BaseUrl"),
            NasdaqTimeout: TimeSpan.FromSeconds(GetRequiredInt(configuration, "Alaris:DataProviders:Nasdaq:TimeoutSeconds")),
            TreasuryBaseUri: GetRequiredUri(configuration, "Alaris:DataProviders:Treasury:BaseUrl"),
            TreasuryTimeout: TimeSpan.FromSeconds(GetRequiredInt(configuration, "Alaris:DataProviders:Treasury:TimeoutSeconds")),
            MarketDataTimeout: TimeSpan.FromSeconds(GetRequiredInt(configuration, "Alaris:DataProviders:MarketDataTimeoutSeconds")),
            ExecutionQuoteTimeout: TimeSpan.FromSeconds(GetRequiredInt(configuration, "Alaris:DataProviders:ExecutionQuoteTimeoutSeconds")));
        _dataProviderSettings.Validate();

        _validationSettings = new ValidationSettings(
            MaxVegaCorrelation: GetRequiredDouble(configuration, "Alaris:Validation:MaxVegaCorrelation"),
            MinimumVegaObservations: GetRequiredInt(configuration, "Alaris:Validation:MinimumVegaObservations"),
            MaxPositionToVolumeRatio: GetRequiredDouble(configuration, "Alaris:Validation:MaxPositionToVolumeRatio"),
            MaxPositionToOpenInterestRatio: GetRequiredDouble(configuration, "Alaris:Validation:MaxPositionToOpenInterestRatio"),
            DeltaRehedgeThreshold: GetRequiredDouble(configuration, "Alaris:Validation:DeltaRehedgeThreshold"),
            GammaWarningThreshold: GetRequiredDouble(configuration, "Alaris:Validation:GammaWarningThreshold"),
            MoneynessAlertThreshold: GetRequiredDouble(configuration, "Alaris:Validation:MoneynessAlertThreshold"));
        _validationSettings.Validate();

        _feeSettings = new FeeSettings(
            PerContract: GetRequiredDecimal(configuration, "Alaris:Fees:PerContract"),
            ExchangePerContract: GetRequiredDecimal(configuration, "Alaris:Fees:ExchangePerContract"),
            RegulatoryPerContract: GetRequiredDecimal(configuration, "Alaris:Fees:RegulatoryPerContract"));
        _feeSettings.Validate();
    }

    // Main Strategy Logic
    
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
            if (GetCurrentAllocation() >= _strategySettings.PortfolioAllocationLimit)
            {
                Log("STLN001A: Portfolio allocation limit reached, skipping new entries");
                return;
            }
            
            // Check position count limit
            if (_activePositions.Count >= _strategySettings.MaxConcurrentPositions)
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
                    _auditLogger?.LogAsync(new Alaris.Infrastructure.Events.Core.AuditEntry
                    {
                        AuditId = Guid.NewGuid(),
                        OccurredAtUtc = DateTime.UtcNow,
                        Action = "EvaluationError",
                        EntityType = "Symbol",
                        EntityId = symbol.Value,
                        InitiatedBy = "STLN001A",
                        Description = $"Evaluation failed for {symbol}: {ex.Message}",
                        Severity = Alaris.Infrastructure.Events.Core.AuditSeverity.Error,
                        Outcome = Alaris.Infrastructure.Events.Core.AuditOutcome.Failure
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
            _auditLogger?.LogAsync(new Alaris.Infrastructure.Events.Core.AuditEntry
            {
                AuditId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                Action = "FatalEvaluationError",
                EntityType = "Algorithm",
                EntityId = "STLN001A",
                InitiatedBy = "STLN001A",
                Description = $"Daily evaluation failed: {ex.Message}",
                Severity = Alaris.Infrastructure.Events.Core.AuditSeverity.Error,
                Outcome = Alaris.Infrastructure.Events.Core.AuditOutcome.Failure
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
        
        // Backtest Mode: Use LEAN's native data instead of external APIs
        
        if (!LiveMode)
        {
            // In backtest mode, we use LEAN's built-in data
            // This avoids calling external APIs with DateTime.UtcNow
            return EvaluateSymbolBacktestMode(symbol);
        }
        
        // Live/Paper Mode: Use external APIs (Polygon, NASDAQ, etc.)
        
        // Phase 1: Market Data Acquisition
        
        MarketDataSnapshot snapshot;
        try
        {
            using var cts = new CancellationTokenSource(_dataProviderSettings.MarketDataTimeout);
            // Pass Time (simulation time) for consistency, even in live mode
            snapshot = _dataBridge!.GetMarketDataSnapshotAsync(ticker, Time, cts.Token)
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
        
        // Phase 2: Realised Volatility Calculation
        
        var priceBars = ConvertToPriceBars(snapshot.HistoricalBars);
        var rv = _yangZhangEstimator!.Calculate(priceBars, _strategySettings.RealisedVolatilityWindowDays, true);
        Log($"  {ticker}: {_strategySettings.RealisedVolatilityWindowDays}-day RV = {rv:P2}");
        
        // Phase 3: Term Structure Analysis
        
        var termStructure = _termStructureAnalyzer!.Analyze(ConvertToTermStructurePoints(snapshot.OptionChain));
        Log($"  {ticker}: Term structure = {termStructure.GetIVAt(30):P2} / {termStructure.GetIVAt(60):P2} / {termStructure.GetIVAt(90):P2}");
        
        // Phase 4: Signal Generation
        
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
        
        // Phase 5: Production Validation
        
        var validation = ValidateForProduction(signal, snapshot);
        
        if (!validation.ProductionReady)
        {
            Log($"  {ticker}: Failed production validation");
            foreach (var check in validation.Checks.Where(c => !c.Passed))
            {
                Log($"    - {check.Name}: {check.Detail}");
            }
            // Log warning to audit trail
            _auditLogger?.LogAsync(new Alaris.Infrastructure.Events.Core.AuditEntry
            {
                AuditId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                Action = "ValidationFailed",
                EntityType = "Signal",
                EntityId = ticker,
                InitiatedBy = "STLN001A",
                Description = $"{ticker}: Production validation failed",
                Severity = Alaris.Infrastructure.Events.Core.AuditSeverity.Warning,
                Outcome = Alaris.Infrastructure.Events.Core.AuditOutcome.Failure
            });
            return result;
        }
        
        Log($"  {ticker}: Passed all production validation checks");
        
        // Phase 6: Execution Pricing
        
        DTmd002A? spreadQuote;
        try
        {
            using var cts = new CancellationTokenSource(_dataProviderSettings.ExecutionQuoteTimeout);
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
        
        // Phase 7: Position Sizing
        
        var portfolioValue = Portfolio.TotalPortfolioValue;
        var spreadMid = spreadQuote.SpreadMid;
        var sizing = _positionSizer!.CalculateFromHistory(
            portfolioValue: (double)portfolioValue,
            historicalTrades: Array.Empty<Alaris.Strategy.Risk.Trade>(),
            spreadCost: (double)spreadMid,
            signal: signal);
        
        if (sizing.Contracts <= 0)
        {
            Log($"  {ticker}: Position sizing returned 0 contracts");
            return result;
        }
        
        var allocationPercent = sizing.AllocationPercent;
        var maxAllocationPercent = (double)_strategySettings.MaxPositionAllocation;
        var cappedContracts = sizing.Contracts;
        if (allocationPercent > maxAllocationPercent && allocationPercent > 0)
        {
            var scale = maxAllocationPercent / allocationPercent;
            cappedContracts = (int)Math.Floor(sizing.Contracts * scale);
        }

        var finalContracts = Math.Min(cappedContracts, validation.RecommendedContracts);
        if (finalContracts <= 0)
        {
            Log($"  {ticker}: Position sizing reduced to 0 contracts by allocation limits");
            return result;
        }

        var effectiveAllocationPercent = cappedContracts < sizing.Contracts && allocationPercent > 0
            ? maxAllocationPercent
            : allocationPercent;
        Log($"  {ticker}: Position size = {finalContracts} contracts ({effectiveAllocationPercent:P2} of portfolio)");
        
        // Phase 8: Order Execution
        
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
            _auditLogger?.LogAsync(new Alaris.Infrastructure.Events.Core.AuditEntry
            {
                AuditId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                Action = "TradeOpened",
                EntityType = "CalendarSpread",
                EntityId = ticker,
                InitiatedBy = "STLN001A",
                Description = $"Opened calendar spread: {finalContracts} contracts @ ${spreadQuote.SpreadMid:F2}",
                Severity = Alaris.Infrastructure.Events.Core.AuditSeverity.Information,
                Outcome = Alaris.Infrastructure.Events.Core.AuditOutcome.Success,
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
    /// Does not call external APIs - uses History API and cached earnings data.
    /// </summary>
    /// <param name="symbol">The symbol to evaluate.</param>
    /// <returns>Evaluation result.</returns>
    private EvaluationResult EvaluateSymbolBacktestMode(Symbol symbol)
    {
        var result = new EvaluationResult();
        var ticker = symbol.Value;

        // Use current simulation time from LEAN (not DateTime.UtcNow)
        var simulationDate = Time;

        // Step 1: Get historical bars from LEAN (no external API call)
        
        var historyBars = History<QuantConnect.Data.Market.TradeBar>(
            symbol,
            _backtestSettings.HistoryLookbackDays,
            Resolution.Daily);
        var barList = historyBars.ToList();

        var minimumBars = _strategySettings.RealisedVolatilityWindowDays + 1;
        if (barList.Count < minimumBars)
        {
            Log($"  {ticker}: Insufficient LEAN history ({barList.Count} bars, need {minimumBars}+)");
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

        // Step 2: Get earnings data from cache (NASDAQ provider in cache-only mode)
        // Pass simulation date as anchor so we search for earnings relative to backtest time
        
        EarningsEvent? nextEarnings = null;
        try
        {
            using var cts = new CancellationTokenSource(_backtestSettings.EarningsQueryTimeout);
            
            // Search for earnings: 2 years before simulation AND up to 90 days after
            // This finds both historical (for Leung-Santoli) and upcoming (for signals)
            var lookaheadDays = _backtestSettings.EarningsLookaheadDays;
            var lookbackDays = _backtestSettings.EarningsLookbackDays + lookaheadDays;
            var earnings = _earningsProvider!.GetHistoricalEarningsAsync(
                ticker,
                simulationDate.AddDays(lookaheadDays),
                lookbackDays,
                cts.Token).GetAwaiter().GetResult();

            // Find the next earnings AFTER current simulation date
            nextEarnings = earnings
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
            Log($"  {ticker}: No upcoming earnings found in cache");
            return result;
        }

        var daysToEarnings = (nextEarnings.Date - simulationDate.Date).Days;
        Log($"  {ticker}: Next earnings {nextEarnings.Date:yyyy-MM-dd} ({daysToEarnings} days away)");

        // Step 3: Calculate Realised Volatility using LEAN bars
        
        var priceBars = barList.Select(b => new StrategyPriceBar
        {
            Date = b.Time,
            Open = (double)b.Open,
            High = (double)b.High,
            Low = (double)b.Low,
            Close = (double)b.Close,
            Volume = 0 // Not needed for Yang-Zhang RV
        }).ToList();

        var rv = _yangZhangEstimator!.Calculate(priceBars, _strategySettings.RealisedVolatilityWindowDays, true);
        Log($"  {ticker}: {_strategySettings.RealisedVolatilityWindowDays}-day RV = {rv:P2}");

        // Step 4: Signal Generation (simplified for backtest)
        
        // Check if within target window for earnings
        if (daysToEarnings < _strategySettings.DaysBeforeEarningsMin
            || daysToEarnings > _strategySettings.DaysBeforeEarningsMax)
        {
            Log($"  {ticker}: Not in target window ({_strategySettings.DaysBeforeEarningsMin}-{_strategySettings.DaysBeforeEarningsMax} days before earnings)");
            return result;
        }

        // Generate signal - update adapter with simulation time first
        _marketDataAdapter?.SetEvaluationDate(simulationDate);
        var signal = _signalGenerator!.Generate(ticker, nextEarnings.Date, simulationDate);
        Log($"  {ticker}: Signal = {signal.Strength} (IV/RV = {signal.IVRVRatio:F3})");
        result.SignalGenerated = true;

        if (signal.Strength != STCR004AStrength.Recommended)
        {
            Log($"  {ticker}: Signal not recommended, skipping");
            return result;
        }

        // Step 5: Backtest-mode order simulation (no real execution)
        
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

    // Helper Methods
    
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
        
        var spotPrice = snapshot.SpotPrice;
        var strikePrice = signal.Strike;

        var backMonthVolume = GetBackMonthVolume(snapshot.OptionChain, signal.BackExpiry);
        var backMonthOpenInterest = GetBackMonthOpenInterest(snapshot.OptionChain, signal.BackExpiry);

        return _productionValidator!.Validate(
            signal,
            frontLegParams: CreateOptionParams(signal, true),
            backLegParams: CreateOptionParams(signal, false),
            frontIVHistory: Array.Empty<double>(),  // Would come from historical data
            backIVHistory: Array.Empty<double>(),
            backMonthVolume: backMonthVolume,
            backMonthOpenInterest: backMonthOpenInterest,
            spotPrice: Convert.ToDouble(spotPrice),
            strikePrice: Convert.ToDouble(strikePrice),
            spreadGreeks: ComputeSpreadGreeks(signal, snapshot),
            daysToEarnings: (signal.EarningsDate - Time).Days);
    }

    private static int GetBackMonthVolume(OptionChainSnapshot optionChain, DateTime backExpiry)
    {
        long totalVolume = 0;
        foreach (var contract in optionChain.Contracts)
        {
            if (contract.Expiration.Date != backExpiry.Date)
                continue;
            totalVolume += contract.Volume;
        }

        if (totalVolume <= 0)
            return 0;
        return totalVolume > int.MaxValue ? int.MaxValue : (int)totalVolume;
    }

    private static int GetBackMonthOpenInterest(OptionChainSnapshot optionChain, DateTime backExpiry)
    {
        long maxOpenInterest = 0;
        foreach (var contract in optionChain.Contracts)
        {
            if (contract.Expiration.Date != backExpiry.Date)
                continue;
            if (contract.OpenInterest > maxOpenInterest)
                maxOpenInterest = contract.OpenInterest;
        }

        if (maxOpenInterest <= 0)
            return 0;
        return maxOpenInterest > int.MaxValue ? int.MaxValue : (int)maxOpenInterest;
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
    /// Converts Alaris.Infrastructure.Data.Model.PriceBar to Alaris.Strategy.Bridge.PriceBar for volatility calculation.
    /// </summary>
    private IReadOnlyList<Alaris.Strategy.Bridge.PriceBar> ConvertToPriceBars(
        IReadOnlyList<Alaris.Infrastructure.Data.Model.PriceBar> dataBars)
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
    /// <param name="optionChain">The option chain snapshot.</param>
    /// <param name="evaluationDate">Optional evaluation date (defaults to snapshot timestamp).</param>
    private IReadOnlyList<STTM001APoint> ConvertToTermStructurePoints(
        OptionChainSnapshot optionChain,
        DateTime? evaluationDate = null)
    {
        var points = new List<STTM001APoint>();
        
        if (optionChain?.Contracts == null)
            return points;
        
        // Use provided evaluationDate, fallback to snapshot timestamp (already contains simulation time)
        var referenceDate = (evaluationDate ?? optionChain.Timestamp).Date;
        
        // Group by expiration and calculate average IV at ATM strikes
        var byExpiry = optionChain.Contracts
            .GroupBy(c => c.Expiration.Date)
            .OrderBy(g => g.Key);
        
        foreach (var group in byExpiry)
        {
            var daysToExpiry = (group.Key - referenceDate).Days;
            if (daysToExpiry <= 0) continue;
            
            // Average IV across ATM options
            var avgIV = group
                .Where(c => c.ImpliedVolatility.HasValue && c.ImpliedVolatility > 0)
                .Select(c => (double)c.ImpliedVolatility!.Value)
                .DefaultIfEmpty(_strategySettings.DefaultImpliedVolatility)
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
            
            _auditLogger?.LogAsync(new Alaris.Infrastructure.Events.Core.AuditEntry
            {
                AuditId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                Action = "OrderFill",
                EntityType = "Order",
                EntityId = orderEvent.OrderId.ToString(),
                InitiatedBy = "STLN001A",
                Description = $"Order filled: {orderEvent.Symbol} {orderEvent.FillQuantity} @ {orderEvent.FillPrice}",
                Severity = Alaris.Infrastructure.Events.Core.AuditSeverity.Information,
                Outcome = Alaris.Infrastructure.Events.Core.AuditOutcome.Success,
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

    // Supporting Types
    
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

    private sealed record StrategySettings(
        int DaysBeforeEarningsMin,
        int DaysBeforeEarningsMax,
        decimal MinimumDollarVolume,
        decimal MinimumPrice,
        decimal PortfolioAllocationLimit,
        decimal MaxPositionAllocation,
        int MaxConcurrentPositions,
        int MaxUniverseSymbols,
        int MaxCoarseSymbols,
        int EvaluationTimeHour,
        int EvaluationTimeMinute,
        int RealisedVolatilityWindowDays,
        double DefaultImpliedVolatility)
    {
        public static StrategySettings Empty => new(
            0, 0, 0m, 0m, 0m, 0m, 0, 0, 0, 0, 0, 0, 0.0);

        public void Validate()
        {
            if (DaysBeforeEarningsMin <= 0)
                throw new InvalidOperationException("DaysBeforeEarningsMin must be positive.");
            if (DaysBeforeEarningsMax < DaysBeforeEarningsMin)
                throw new InvalidOperationException("DaysBeforeEarningsMax must be greater than or equal to DaysBeforeEarningsMin.");
            if (MinimumDollarVolume <= 0m)
                throw new InvalidOperationException("MinimumDollarVolume must be positive.");
            if (MinimumPrice <= 0m)
                throw new InvalidOperationException("MinimumPrice must be positive.");
            if (PortfolioAllocationLimit <= 0m || PortfolioAllocationLimit > 1m)
                throw new InvalidOperationException("PortfolioAllocationLimit must be between 0 and 1.");
            if (MaxPositionAllocation <= 0m || MaxPositionAllocation > 1m)
                throw new InvalidOperationException("MaxPositionAllocation must be between 0 and 1.");
            if (MaxConcurrentPositions <= 0)
                throw new InvalidOperationException("MaxConcurrentPositions must be positive.");
            if (MaxUniverseSymbols <= 0)
                throw new InvalidOperationException("MaxUniverseSymbols must be positive.");
            if (MaxCoarseSymbols <= 0)
                throw new InvalidOperationException("MaxCoarseSymbols must be positive.");
            if (MaxUniverseSymbols > MaxCoarseSymbols)
                throw new InvalidOperationException("MaxUniverseSymbols must not exceed MaxCoarseSymbols.");
            if (EvaluationTimeHour < 0 || EvaluationTimeHour > 23)
                throw new InvalidOperationException("EvaluationTimeHour must be between 0 and 23.");
            if (EvaluationTimeMinute < 0 || EvaluationTimeMinute > 59)
                throw new InvalidOperationException("EvaluationTimeMinute must be between 0 and 59.");
            if (RealisedVolatilityWindowDays <= 0)
                throw new InvalidOperationException("RealisedVolatilityWindowDays must be positive.");
            if (DefaultImpliedVolatility <= 0)
                throw new InvalidOperationException("DefaultImpliedVolatility must be positive.");
        }
    }

    private sealed record BacktestSettings(
        DateTime StartDate,
        DateTime EndDate,
        decimal InitialCash,
        int WarmUpDays,
        int HistoryLookbackDays,
        int EarningsLookaheadDays,
        int EarningsLookbackDays,
        TimeSpan EarningsQueryTimeout)
    {
        public static BacktestSettings Empty => new(
            DateTime.MinValue,
            DateTime.MinValue,
            0m,
            0,
            0,
            0,
            0,
            TimeSpan.Zero);

        public void Validate()
        {
            if (StartDate == DateTime.MinValue || EndDate == DateTime.MinValue)
                throw new InvalidOperationException("Backtest StartDate and EndDate must be set.");
            if (EndDate <= StartDate)
                throw new InvalidOperationException("Backtest EndDate must be after StartDate.");
            if (InitialCash <= 0m)
                throw new InvalidOperationException("InitialCash must be positive.");
            if (WarmUpDays <= 0)
                throw new InvalidOperationException("WarmUpDays must be positive.");
            if (HistoryLookbackDays <= 0)
                throw new InvalidOperationException("HistoryLookbackDays must be positive.");
            if (EarningsLookaheadDays < 0)
                throw new InvalidOperationException("EarningsLookaheadDays must be non-negative.");
            if (EarningsLookbackDays <= 0)
                throw new InvalidOperationException("EarningsLookbackDays must be positive.");
            if (EarningsQueryTimeout <= TimeSpan.Zero)
                throw new InvalidOperationException("EarningsQueryTimeout must be positive.");
        }
    }

    private sealed record DataProviderSettings(
        Uri PolygonBaseUri,
        TimeSpan PolygonTimeout,
        Uri NasdaqBaseUri,
        TimeSpan NasdaqTimeout,
        Uri TreasuryBaseUri,
        TimeSpan TreasuryTimeout,
        TimeSpan MarketDataTimeout,
        TimeSpan ExecutionQuoteTimeout)
    {
        public static DataProviderSettings Empty => new(
            new Uri("http://localhost"),
            TimeSpan.Zero,
            new Uri("http://localhost"),
            TimeSpan.Zero,
            new Uri("http://localhost"),
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.Zero);

        public void Validate()
        {
            if (!PolygonBaseUri.IsAbsoluteUri)
                throw new InvalidOperationException("PolygonBaseUri must be absolute.");
            if (!NasdaqBaseUri.IsAbsoluteUri)
                throw new InvalidOperationException("NasdaqBaseUri must be absolute.");
            if (!TreasuryBaseUri.IsAbsoluteUri)
                throw new InvalidOperationException("TreasuryBaseUri must be absolute.");
            if (PolygonTimeout <= TimeSpan.Zero)
                throw new InvalidOperationException("PolygonTimeout must be positive.");
            if (NasdaqTimeout <= TimeSpan.Zero)
                throw new InvalidOperationException("NasdaqTimeout must be positive.");
            if (TreasuryTimeout <= TimeSpan.Zero)
                throw new InvalidOperationException("TreasuryTimeout must be positive.");
            if (MarketDataTimeout <= TimeSpan.Zero)
                throw new InvalidOperationException("MarketDataTimeout must be positive.");
            if (ExecutionQuoteTimeout <= TimeSpan.Zero)
                throw new InvalidOperationException("ExecutionQuoteTimeout must be positive.");
        }
    }

    private sealed record ValidationSettings(
        double MaxVegaCorrelation,
        int MinimumVegaObservations,
        double MaxPositionToVolumeRatio,
        double MaxPositionToOpenInterestRatio,
        double DeltaRehedgeThreshold,
        double GammaWarningThreshold,
        double MoneynessAlertThreshold)
    {
        public static ValidationSettings Empty => new(0, 0, 0, 0, 0, 0, 0);

        public void Validate()
        {
            if (MaxVegaCorrelation <= 0 || MaxVegaCorrelation > 1)
                throw new InvalidOperationException("MaxVegaCorrelation must be between 0 and 1.");
            if (MinimumVegaObservations <= 0)
                throw new InvalidOperationException("MinimumVegaObservations must be positive.");
            if (MaxPositionToVolumeRatio <= 0 || MaxPositionToVolumeRatio > 1)
                throw new InvalidOperationException("MaxPositionToVolumeRatio must be between 0 and 1.");
            if (MaxPositionToOpenInterestRatio <= 0 || MaxPositionToOpenInterestRatio > 1)
                throw new InvalidOperationException("MaxPositionToOpenInterestRatio must be between 0 and 1.");
            if (DeltaRehedgeThreshold <= 0)
                throw new InvalidOperationException("DeltaRehedgeThreshold must be positive.");
            if (GammaWarningThreshold >= 0)
                throw new InvalidOperationException("GammaWarningThreshold must be negative.");
            if (MoneynessAlertThreshold <= 0)
                throw new InvalidOperationException("MoneynessAlertThreshold must be positive.");
        }
    }

    private sealed record FeeSettings(
        decimal PerContract,
        decimal ExchangePerContract,
        decimal RegulatoryPerContract)
    {
        public static FeeSettings Empty => new(0m, 0m, 0m);

        public void Validate()
        {
            if (PerContract < 0m)
                throw new InvalidOperationException("PerContract must be non-negative.");
            if (ExchangePerContract < 0m)
                throw new InvalidOperationException("ExchangePerContract must be non-negative.");
            if (RegulatoryPerContract < 0m)
                throw new InvalidOperationException("RegulatoryPerContract must be non-negative.");
        }
    }

    private static string GetRequiredValue(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing configuration value: {key}");
        return value;
    }

    private static int GetRequiredInt(IConfiguration configuration, string key)
    {
        var value = GetRequiredValue(configuration, key);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidOperationException($"Invalid integer for {key}: {value}");
        return parsed;
    }

    private static double GetRequiredDouble(IConfiguration configuration, string key)
    {
        var value = GetRequiredValue(configuration, key);
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidOperationException($"Invalid double for {key}: {value}");
        return parsed;
    }

    private static decimal GetRequiredDecimal(IConfiguration configuration, string key)
    {
        var value = GetRequiredValue(configuration, key);
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidOperationException($"Invalid decimal for {key}: {value}");
        return parsed;
    }

    private static DateTime GetRequiredDateTime(IConfiguration configuration, string key)
    {
        var value = GetRequiredValue(configuration, key);
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            throw new InvalidOperationException($"Invalid date for {key}: {value}");
        return parsed;
    }

    private static Uri GetRequiredUri(IConfiguration configuration, string key)
    {
        var value = GetRequiredValue(configuration, key);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed))
            throw new InvalidOperationException($"Invalid URI for {key}: {value}");
        return parsed;
    }
}

// LEAN Logger Adapter

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

// Market Data Adapter

internal sealed class DataBridgeMarketDataAdapter : STDT001A
{
    private readonly AlarisDataBridge _bridge;
    private DateTime _evaluationDate = DateTime.UtcNow;

    public DataBridgeMarketDataAdapter(AlarisDataBridge bridge)
    {
        _bridge = bridge;
    }

    /// <summary>
    /// Sets the evaluation date for market data queries (use LEAN's Time for backtests).
    /// </summary>
    public void SetEvaluationDate(DateTime date) => _evaluationDate = date;

    public StrategyOptionChain GetSTDT002A(string symbol, DateTime expirationDate)
    {
        var snapshot = _bridge.GetMarketDataSnapshotAsync(symbol, _evaluationDate).GetAwaiter().GetResult();
        
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
        var snapshot = _bridge.GetMarketDataSnapshotAsync(symbol, _evaluationDate).GetAwaiter().GetResult();
        
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
        var snapshot = _bridge.GetMarketDataSnapshotAsync(symbol, _evaluationDate).GetAwaiter().GetResult();
        return (double)snapshot.SpotPrice;
    }

    public async Task<IReadOnlyList<DateTime>> GetEarningsDates(string symbol)
    {
        var snapshot = await _bridge.GetMarketDataSnapshotAsync(symbol, _evaluationDate);
        var dates = new List<DateTime>();
        if (snapshot.NextEarnings != null)
        {
            dates.Add(snapshot.NextEarnings.Date);
        }
        return dates;
    }

    public async Task<IReadOnlyList<DateTime>> GetHistoricalEarningsDates(string symbol, int lookbackQuarters = 12)
    {
        var snapshot = await _bridge.GetMarketDataSnapshotAsync(symbol, _evaluationDate);
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
