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
using Alaris.Core.Options;
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
    private ForwardtestSettings _forwardtestSettings = ForwardtestSettings.Empty;
    private DataProviderSettings _dataProviderSettings = DataProviderSettings.Empty;
    private ValidationSettings _validationSettings = ValidationSettings.Empty;
    private FeeSettings _feeSettings = FeeSettings.Empty;
    private bool _requireOptionChainCache;
    private bool _requireEarningsCache;

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
    private STBR001A? _pricingEngine;
    
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

        _requireOptionChainCache = LiveMode
            ? _forwardtestSettings.RequireOptionChainCache
            : _backtestSettings.RequireOptionChainCache;
        _requireEarningsCache = LiveMode
            ? _forwardtestSettings.RequireEarningsCache
            : _backtestSettings.RequireEarningsCache;
        
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
        
        // Only set cash for backtests; live/paper mode uses actual IBKR account balance
        if (!LiveMode)
        {
            SetCash(initialCash);
        }
        
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
                var equity = AddEquity(ticker.Trim(), Resolution.Daily);
#pragma warning disable CS0618 // SetDataNormalizationMode is obsolete but required for options trading
                equity.SetDataNormalizationMode(DataNormalizationMode.Raw);
#pragma warning restore CS0618
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

        _pricingEngine?.Dispose();
        
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
        var configuration = _configuration ?? BuildConfiguration();
        
        // Check if we have a session data path (backtest with pre-downloaded data)
        var sessionDataPath = Environment.GetEnvironmentVariable("ALARIS_SESSION_DATA");
        bool hasSessionData = !string.IsNullOrEmpty(sessionDataPath) && System.IO.Directory.Exists(sessionDataPath);
        if (_requireEarningsCache && !hasSessionData)
        {
            Log("STLN001A: Earnings cache required but no session data path was provided.");
        }
        
        // Initialise market data provider (Polygon) with Refit
        // API key is passed via ALARIS_Polygon__ApiKey environment variable from Host when running backtests
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
        // Live/paper mode connects to IBKR Gateway; backtest uses simulated quotes
        _executionQuoteProvider = LiveMode
            ? new InteractiveBrokersSnapshotProvider(
                _configuration!,
                _loggerFactory!.CreateLogger<InteractiveBrokersSnapshotProvider>())
            : new InteractiveBrokersSnapshotProvider(
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
            _marketDataProvider,  // May be null in backtest mode with cached data
            _earningsProvider,
            _riskFreeRateProvider,
            validators,
            _loggerFactory!.CreateLogger<AlarisDataBridge>());
        _dataBridge.SetOptionChainFallbackEnabled(!_requireOptionChainCache);
        _dataBridge.SetEarningsFallbackEnabled(!_requireEarningsCache);
        
        // Set session data path for cached data access (options, etc.)
        if (hasSessionData)
        {
            _dataBridge.SetSessionDataPath(sessionDataPath!);
            Log($"STLN001A: Session data path set for cache: {sessionDataPath}");
        }

        if (_requireOptionChainCache)
        {
            Log("STLN001A: Option chain cache required (no live fallback).");
        }
        if (_requireEarningsCache)
        {
            Log("STLN001A: Earnings cache required (no live fallback).");
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

        // Pricing engine for IV and Greeks
        _pricingEngine = new STBR001A(
            _loggerFactory!.CreateLogger<STBR001A>());
        
        // Signal generator (requires market data adapter)
        _marketDataAdapter = new DataBridgeMarketDataAdapter(_dataBridge!);
        _signalGenerator = new STCR001A(
            _marketDataAdapter,
            _yangZhangEstimator,
            _termStructureAnalyzer,
            minimumIvRvRatio: _strategySettings.MinIvRvRatio,
            maximumTermSlope: _strategySettings.MaxTermSlope,
            minimumAverageVolume: _strategySettings.MinimumAverageVolume,
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
            feePerContract: _feeSettings.PerContract,
            exchangeFeePerContract: _feeSettings.ExchangePerContract,
            regulatoryFeePerContract: _feeSettings.RegulatoryPerContract,
            logger: _loggerFactory!.CreateLogger<STCS005A>());
        
        // Cost validator (signal survives costs)
        _costValidator = new STCS006A(
            _feeModel,
            minimumPostCostRatio: _validationSettings.MinimumPostCostRatio,
            maximumSlippagePercent: _validationSettings.MaxSlippagePercent,
            maximumExecutionCostPercent: _validationSettings.MaxExecutionCostPercent,
            maximumSlippagePerSpread: _validationSettings.MaxSlippagePerSpread,
            maximumExecutionCostPerSpread: _validationSettings.MaxExecutionCostPerSpread,
            minimumCapitalForCostPercent: _validationSettings.MinimumCapitalForCostPercent,
            logger: _loggerFactory!.CreateLogger<STCS006A>());
        
        // Vega correlation analyser
        _vegaAnalyser = new STHD001A(
            maxAcceptableCorrelation: _validationSettings.MaxVegaCorrelation,
            minimumObservations: _validationSettings.MinimumVegaObservations,
            allowInsufficientData: _validationSettings.AllowInsufficientVegaData,
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
            "lib/Alaris.Lean/Data",
            "../lib/Alaris.Lean/Data",
            "../../lib/Alaris.Lean/Data",
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
        return System.IO.Path.GetFullPath("lib/Alaris.Lean/Data");
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
    /// Loads appsettings.jsonc (base) then appsettings.local.jsonc (secrets) then user-secrets.
    /// </summary>
    private IConfiguration BuildConfiguration()
    {
        // Find the repository root (where appsettings.jsonc lives)
        var basePath = FindRepositoryRoot();
        
        // Load user-secrets from Host's secrets file (shared UserSecretsId)
        // Path: ~/.microsoft/usersecrets/{UserSecretsId}/secrets.json
        const string HostUserSecretsId = "71fcddd5-6de5-41ed-b500-b64783facdab";
        var userSecretsPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".microsoft", "usersecrets", HostUserSecretsId, "secrets.json");
        
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.jsonc", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.local.jsonc", optional: true, reloadOnChange: false);
        
        // Add user secrets if the file exists (when running standalone via LEAN)
        if (System.IO.File.Exists(userSecretsPath))
        {
            builder.AddJsonFile(userSecretsPath, optional: true, reloadOnChange: false);
        }
        
        return builder
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
            OptionRight: GetRequiredOptionRight(configuration, "Alaris:Strategy:OptionRight"),
            RealisedVolatilityWindowDays: GetRequiredInt(configuration, "Alaris:Strategy:RealisedVolatilityWindowDays"),
            MinIvRvRatio: GetRequiredDouble(configuration, "Alaris:Strategy:MinIvRvRatio"),
            MaxTermSlope: GetRequiredDouble(configuration, "Alaris:Strategy:MaxTermSlope"),
            MinimumAverageVolume: GetRequiredLong(configuration, "Alaris:Strategy:MinimumAverageVolume"),
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
            EarningsQueryTimeout: TimeSpan.FromSeconds(GetRequiredInt(configuration, "Alaris:Backtest:EarningsQueryTimeoutSeconds")),
            RequireOptionChainCache: GetRequiredBool(configuration, "Alaris:Backtest:RequireOptionChainCache"),
            RequireEarningsCache: GetRequiredBool(configuration, "Alaris:Backtest:RequireEarningsCache"));
        _backtestSettings.Validate();
        if (_backtestSettings.HistoryLookbackDays <= _strategySettings.RealisedVolatilityWindowDays)
            throw new InvalidOperationException("HistoryLookbackDays must exceed RealisedVolatilityWindowDays.");

        _forwardtestSettings = new ForwardtestSettings(
            RequireOptionChainCache: GetRequiredBool(configuration, "Alaris:Forwardtest:RequireOptionChainCache"),
            RequireEarningsCache: GetRequiredBool(configuration, "Alaris:Forwardtest:RequireEarningsCache"));
        _forwardtestSettings.Validate();

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

        string validationKey = LiveMode ? "Alaris:ForwardValidation" : "Alaris:BackValidation";
        _validationSettings = LoadValidationSettings(configuration, validationKey);
        _validationSettings.Validate();

        _feeSettings = new FeeSettings(
            PerContract: GetRequiredDecimal(configuration, "Alaris:Fees:PerContract"),
            ExchangePerContract: GetRequiredDecimal(configuration, "Alaris:Fees:ExchangePerContract"),
            RegulatoryPerContract: GetRequiredDecimal(configuration, "Alaris:Fees:RegulatoryPerContract"));
        _feeSettings.Validate();
    }

    private static ValidationSettings LoadValidationSettings(IConfiguration configuration, string baseKey)
    {
        return new ValidationSettings(
            MaxVegaCorrelation: GetRequiredDouble(configuration, $"{baseKey}:MaxVegaCorrelation"),
            MinimumVegaObservations: GetRequiredInt(configuration, $"{baseKey}:MinimumVegaObservations"),
            VegaCorrelationLookbackDays: GetRequiredInt(configuration, $"{baseKey}:VegaCorrelationLookbackDays"),
            MaxPositionToVolumeRatio: GetRequiredDouble(configuration, $"{baseKey}:MaxPositionToVolumeRatio"),
            MaxPositionToOpenInterestRatio: GetRequiredDouble(configuration, $"{baseKey}:MaxPositionToOpenInterestRatio"),
            DeltaRehedgeThreshold: GetRequiredDouble(configuration, $"{baseKey}:DeltaRehedgeThreshold"),
            GammaWarningThreshold: GetRequiredDouble(configuration, $"{baseKey}:GammaWarningThreshold"),
            MoneynessAlertThreshold: GetRequiredDouble(configuration, $"{baseKey}:MoneynessAlertThreshold"),
            MinimumPostCostRatio: GetRequiredDouble(configuration, $"{baseKey}:MinimumPostCostRatio"),
            MaxSlippagePercent: GetRequiredDecimal(configuration, $"{baseKey}:MaxSlippagePercent"),
            MaxExecutionCostPercent: GetRequiredDecimal(configuration, $"{baseKey}:MaxExecutionCostPercent"),
            MaxSlippagePerSpread: GetRequiredDecimal(configuration, $"{baseKey}:MaxSlippagePerSpread"),
            MaxExecutionCostPerSpread: GetRequiredDecimal(configuration, $"{baseKey}:MaxExecutionCostPerSpread"),
            MinimumCapitalForCostPercent: GetRequiredDecimal(configuration, $"{baseKey}:MinimumCapitalForCostPercent"),
            AllowInsufficientVegaData: GetRequiredBool(configuration, $"{baseKey}:AllowInsufficientVegaData"));
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

        // Phase 1: Market Data Acquisition

        MarketDataSnapshot snapshot;
        try
        {
            using var cts = new CancellationTokenSource(_dataProviderSettings.MarketDataTimeout);
            var evaluationDate = Time;
            _marketDataAdapter?.SetEvaluationDate(evaluationDate);
            snapshot = _marketDataAdapter!.GetSnapshot(ticker, evaluationDate, cts.Token);
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

        if (_requireOptionChainCache && snapshot.OptionChain.Contracts.Count == 0)
        {
            Log($"  {ticker}: No cached options data available (backtest mode), skipping evaluation");
            return result;
        }
        
        if (snapshot.NextEarnings == null)
        {
            Log($"  {ticker}: No upcoming earnings found");
            return result;
        }

        return EvaluateSnapshot(symbol, snapshot, useExecutionQuoteProvider: LiveMode);
    }

    private EvaluationResult EvaluateSnapshot(Symbol symbol, MarketDataSnapshot snapshot, bool useExecutionQuoteProvider)
    {
        var result = new EvaluationResult();
        var ticker = symbol.Value;

        // Phase 2: Realised Volatility Calculation

        var priceBars = ConvertToPriceBars(snapshot.HistoricalBars);
        var minimumBars = _strategySettings.RealisedVolatilityWindowDays + 1;
        if (priceBars.Count < minimumBars)
        {
            Log($"  {ticker}: Insufficient price history ({priceBars.Count} bars, need {minimumBars}+)");
            return result;
        }

        var rv = _yangZhangEstimator!.Calculate(priceBars, _strategySettings.RealisedVolatilityWindowDays, true);
        Log($"  {ticker}: {_strategySettings.RealisedVolatilityWindowDays}-day RV = {rv:P2}");

        // Phase 3: Term Structure Analysis

        var termPoints = ConvertToTermStructurePoints(snapshot.OptionChain, snapshot.Timestamp);
        if (termPoints.Count >= 2)
        {
            var termStructure = _termStructureAnalyzer!.Analyze(termPoints);
            Log($"  {ticker}: Term structure = {termStructure.GetIVAt(30):P2} / {termStructure.GetIVAt(60):P2} / {termStructure.GetIVAt(90):P2}");
        }
        else
        {
            Log($"  {ticker}: Insufficient term structure points for analysis");
        }

        // Phase 4: Signal Generation

        var historicalEarningsDates = snapshot.HistoricalEarnings
            .Select(e => e.Date)
            .ToList();

        var signal = _signalGenerator!.Generate(
            ticker,
            snapshot.NextEarnings!.Date,
            snapshot.Timestamp,
            historicalEarningsDates);

        Log($"  {ticker}: Signal = {signal.Strength} (IV/RV = {signal.IVRVRatio:F3})");
        result.SignalGenerated = true;

        if (signal.Strength != STCR004AStrength.Recommended)
        {
            Log($"  {ticker}: Signal not recommended, skipping");
            return result;
        }

        if (!TrySelectCalendarSpread(snapshot, signal.EarningsDate, _strategySettings.OptionRight, out var selection, out var selectionDetail))
        {
            Log($"  {ticker}: {selectionDetail}");
            return result;
        }

        PopulateSignalLegs(signal, selection);

        if (selection.BackLeg.Volume <= 0 || selection.BackLeg.OpenInterest <= 0)
        {
            Log($"  {ticker}: Back leg liquidity unavailable (vol={selection.BackLeg.Volume}, OI={selection.BackLeg.OpenInterest})");
            return result;
        }

        // Phase 5: Execution Pricing

        DTmd002A? spreadQuote = GetSpreadQuote(selection, useExecutionQuoteProvider);
        if (spreadQuote == null)
        {
            Log($"  {ticker}: No execution quote available");
            return result;
        }
        if (spreadQuote.SpreadAsk <= 0m || spreadQuote.SpreadAsk < spreadQuote.SpreadBid)
        {
            Log($"  {ticker}: Invalid spread quote (bid/ask: ${spreadQuote.SpreadBid:F4}/${spreadQuote.SpreadAsk:F4})");
            return result;
        }
        if (spreadQuote.SpreadMid <= 0m)
        {
            Log($"  {ticker}: Invalid spread mid price ({spreadQuote.SpreadMid:F4})");
            return result;
        }

        Log($"  {ticker}: Spread quote = ${spreadQuote.SpreadMid:F2} (bid/ask: ${spreadQuote.SpreadBid:F2}/${spreadQuote.SpreadAsk:F2})");

        // Phase 6: Position Sizing

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
        var proposedContracts = sizing.Contracts;
        if (allocationPercent > maxAllocationPercent && allocationPercent > 0)
        {
            var scale = maxAllocationPercent / allocationPercent;
            proposedContracts = (int)Math.Floor(sizing.Contracts * scale);
        }

        if (proposedContracts <= 0)
        {
            Log($"  {ticker}: Position sizing reduced to 0 contracts by allocation limits");
            return result;
        }

        // Phase 7: Production Validation

        STHD006A validation;
        try
        {
            validation = ValidateForProduction(signal, snapshot, selection, proposedContracts);
        }
        catch (InvalidOperationException ex)
        {
            Log($"  {ticker}: Production validation error - {ex.Message}");
            return result;
        }
        catch (ArgumentException ex)
        {
            Log($"  {ticker}: Production validation error - {ex.Message}");
            return result;
        }

        if (!validation.ProductionReady)
        {
            Log($"  {ticker}: Failed production validation");
            foreach (var check in validation.Checks.Where(c => !c.Passed))
            {
                Log($"    - {check.Name}: {check.Detail}");
            }
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

        var finalContracts = Math.Min(proposedContracts, validation.RecommendedContracts);
        if (finalContracts <= 0)
        {
            Log($"  {ticker}: Position sizing reduced to 0 contracts by validation limits");
            return result;
        }

        var effectiveAllocationPercent = allocationPercent;
        if (proposedContracts > 0 && finalContracts < proposedContracts && allocationPercent > 0)
        {
            effectiveAllocationPercent = allocationPercent * ((double)finalContracts / proposedContracts);
        }

        Log($"  {ticker}: Position size = {finalContracts} contracts ({effectiveAllocationPercent:P2} of portfolio)");
        Log($"  {ticker}: Passed all production validation checks");

        // Phase 8: Order Execution

        var orderResult = ExecuteCalendarSpread(
            symbol,
            selection,
            spreadQuote,
            finalContracts);

        if (orderResult.Success)
        {
            result.OrderSubmitted = true;
            _activePositions.Add(symbol);
            _positionEntryDates[symbol] = Time;

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
                    ["FrontExpiry"] = selection.FrontExpiry.ToString("O"),
                    ["BackExpiry"] = selection.BackExpiry.ToString("O")
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

    // Helper Methods
    
    /// <summary>
    /// Checks if all components are properly initialised.
    /// </summary>
    private bool AreComponentsInitialised()
    {
        return _dataBridge != null
            && _marketDataAdapter != null
            && _signalGenerator != null
            && _pricingEngine != null
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
    private STHD006A ValidateForProduction(
        STCR004A signal,
        MarketDataSnapshot snapshot,
        CalendarSpreadSelection selection,
        int proposedContracts)
    {
        var (frontIVHistory, backIVHistory) = BuildIvHistory(selection, snapshot);
        var frontLegParams = BuildOptionParams(
            selection.FrontLeg,
            Alaris.Strategy.Cost.OrderDirection.Sell,
            signal.Symbol,
            proposedContracts);
        var backLegParams = BuildOptionParams(
            selection.BackLeg,
            Alaris.Strategy.Cost.OrderDirection.Buy,
            signal.Symbol,
            proposedContracts);
        var spreadGreeks = ComputeSpreadGreeks(selection, snapshot);

        var backMonthVolume = selection.BackLeg.Volume > int.MaxValue
            ? int.MaxValue
            : (int)selection.BackLeg.Volume;
        var backMonthOpenInterest = selection.BackLeg.OpenInterest > int.MaxValue
            ? int.MaxValue
            : (int)selection.BackLeg.OpenInterest;

        return _productionValidator!.Validate(
            signal,
            frontLegParams: frontLegParams,
            backLegParams: backLegParams,
            frontIVHistory: frontIVHistory,
            backIVHistory: backIVHistory,
            backMonthVolume: backMonthVolume,
            backMonthOpenInterest: backMonthOpenInterest,
            spotPrice: Convert.ToDouble(snapshot.SpotPrice),
            strikePrice: Convert.ToDouble(selection.Strike),
            spreadGreeks: spreadGreeks,
            daysToEarnings: Math.Max(0, (signal.EarningsDate - snapshot.Timestamp).Days));
    }

    private bool TrySelectCalendarSpread(
        MarketDataSnapshot snapshot,
        DateTime earningsDate,
        AlarisOptionRight right,
        out CalendarSpreadSelection selection,
        out string detail)
    {
        selection = null!;
        detail = "Unable to select calendar spread legs.";

        var contracts = snapshot.OptionChain.Contracts;
        if (contracts.Count == 0)
        {
            detail = "No option contracts available for selection";
            return false;
        }

        var expiries = contracts
            .Where(c => c.Right == right)
            .Select(c => c.Expiration.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (expiries.Count == 0)
        {
            detail = "No expiries available for selected option right";
            return false;
        }

        var frontExpiry = expiries.FirstOrDefault(d => d >= earningsDate.Date);
        if (frontExpiry == default)
        {
            detail = "No expiry available on or after earnings date";
            return false;
        }

        var backExpiry = expiries.FirstOrDefault(d => d > frontExpiry);
        if (backExpiry == default)
        {
            detail = "No back-month expiry available after front expiry";
            return false;
        }

        var frontContracts = contracts
            .Where(c => c.Right == right && c.Expiration.Date == frontExpiry)
            .ToList();
        var backContracts = contracts
            .Where(c => c.Right == right && c.Expiration.Date == backExpiry)
            .ToList();

        if (frontContracts.Count == 0 || backContracts.Count == 0)
        {
            detail = "Missing contracts for front or back expiry";
            return false;
        }

        var backByStrike = new Dictionary<decimal, OptionContract>(backContracts.Count);
        foreach (var back in backContracts)
        {
            if (!IsQuoteValid(back))
                continue;
            backByStrike[back.Strike] = back;
        }

        OptionContract? bestFront = null;
        OptionContract? bestBack = null;
        double bestFrontIv = 0;
        double bestBackIv = 0;
        bool bestUsesSyntheticIv = false;
        decimal bestDistance = decimal.MaxValue;
        var spot = snapshot.SpotPrice;
        if (spot <= 0m)
        {
            detail = "Spot price unavailable for selection";
            return false;
        }
        var spotPrice = Convert.ToDouble(spot);
        var riskFreeRate = Convert.ToDouble(snapshot.RiskFreeRate);
        var dividendYield = Convert.ToDouble(snapshot.DividendYield);
        var valuationDate = snapshot.Timestamp;

        foreach (var front in frontContracts)
        {
            if (!IsQuoteValid(front))
                continue;
            if (!backByStrike.TryGetValue(front.Strike, out var back))
                continue;

            var distance = Math.Abs(front.Strike - spot);
            if (distance >= bestDistance)
                continue;

            if (!TryResolveImpliedVolatility(
                front,
                spotPrice,
                riskFreeRate,
                dividendYield,
                valuationDate,
                right,
                out var frontIv,
                out var frontSynthetic))
                continue;
            if (!TryResolveImpliedVolatility(
                back,
                spotPrice,
                riskFreeRate,
                dividendYield,
                valuationDate,
                right,
                out var backIv,
                out var backSynthetic))
                continue;

            bestDistance = distance;
            bestFront = front;
            bestBack = back;
            bestFrontIv = frontIv;
            bestBackIv = backIv;
            bestUsesSyntheticIv = frontSynthetic || backSynthetic;
        }

        if (bestFront == null || bestBack == null)
        {
            detail = "No matching front/back strikes with valid quotes and IV";
            return false;
        }

        selection = new CalendarSpreadSelection(
            snapshot.Symbol,
            bestFront,
            bestBack,
            bestFront.Strike,
            frontExpiry,
            backExpiry,
            right,
            bestUsesSyntheticIv,
            bestFrontIv,
            bestBackIv);
        detail = "Calendar spread legs selected";
        return true;
    }

    private void PopulateSignalLegs(STCR004A signal, CalendarSpreadSelection selection)
    {
        signal.Strike = selection.Strike;
        signal.FrontExpiry = selection.FrontExpiry;
        signal.BackExpiry = selection.BackExpiry;
        signal.FrontIV = selection.FrontIV;
        signal.BackIV = selection.BackIV;
        signal.UsingSyntheticIV = selection.UsesSyntheticIv;
    }

    private DTmd002A? GetSpreadQuote(CalendarSpreadSelection selection, bool useExecutionQuoteProvider)
    {
        if (useExecutionQuoteProvider)
        {
            try
            {
                using var cts = new CancellationTokenSource(_dataProviderSettings.ExecutionQuoteTimeout);
                var quote = _executionQuoteProvider!.GetDTmd002AAsync(
                    selection.Symbol,
                    selection.Strike,
                    selection.FrontExpiry,
                    selection.BackExpiry,
                    selection.Right,
                    cts.Token)
                    .GetAwaiter().GetResult();

                if (quote != null)
                {
                    return quote;
                }
            }
            catch (Exception ex)
            {
                Log($"  {selection.Symbol}: Execution quote provider failed - {ex.Message}");
            }
        }

        return BuildSpreadQuote(selection);
    }

    private static DTmd002A? BuildSpreadQuote(CalendarSpreadSelection selection)
    {
        var spreadBid = selection.BackLeg.Bid - selection.FrontLeg.Ask;
        var spreadAsk = selection.BackLeg.Ask - selection.FrontLeg.Bid;
        var spreadMid = (spreadBid + spreadAsk) / 2m;

        if (spreadAsk <= 0m || spreadAsk < spreadBid || spreadMid <= 0m)
        {
            return null;
        }

        return new DTmd002A
        {
            UnderlyingSymbol = selection.Symbol,
            Strike = selection.Strike,
            FrontLeg = selection.FrontLeg,
            BackLeg = selection.BackLeg,
            Timestamp = selection.BackLeg.Timestamp > selection.FrontLeg.Timestamp
                ? selection.BackLeg.Timestamp
                : selection.FrontLeg.Timestamp
        };
    }

    private (IReadOnlyList<double> FrontIVHistory, IReadOnlyList<double> BackIVHistory) BuildIvHistory(
        CalendarSpreadSelection selection,
        MarketDataSnapshot snapshot)
    {
        var requiredLevels = _validationSettings.MinimumVegaObservations + 1;
        var lookbackDays = _validationSettings.VegaCorrelationLookbackDays;

        var frontLevels = new List<double>(requiredLevels);
        var backLevels = new List<double>(requiredLevels);

        for (int offset = 0; offset <= lookbackDays && frontLevels.Count < requiredLevels; offset++)
        {
            var date = snapshot.Timestamp.Date.AddDays(-offset);

            MarketDataSnapshot dailySnapshot;
            try
            {
                dailySnapshot = _dataBridge!.GetMarketDataSnapshotAsync(selection.Symbol, date)
                    .GetAwaiter().GetResult();
            }
            catch
            {
                continue;
            }

            var chain = dailySnapshot.OptionChain;
            if (chain.Contracts.Count == 0)
                continue;

            if (!TryGetContractForStrike(chain, selection.FrontExpiry, selection.Strike, selection.Right, out var front))
                continue;
            if (!TryGetContractForStrike(chain, selection.BackExpiry, selection.Strike, selection.Right, out var back))
                continue;

            var spotPrice = Convert.ToDouble(chain.SpotPrice);
            if (spotPrice <= 0)
                continue;

            var riskFreeRate = Convert.ToDouble(dailySnapshot.RiskFreeRate);
            var dividendYield = Convert.ToDouble(dailySnapshot.DividendYield);
            if (!TryResolveImpliedVolatility(
                front,
                spotPrice,
                riskFreeRate,
                dividendYield,
                dailySnapshot.Timestamp,
                selection.Right,
                out var frontIv,
                out _))
                continue;
            if (!TryResolveImpliedVolatility(
                back,
                spotPrice,
                riskFreeRate,
                dividendYield,
                dailySnapshot.Timestamp,
                selection.Right,
                out var backIv,
                out _))
                continue;

            frontLevels.Add(frontIv);
            backLevels.Add(backIv);
        }

        frontLevels.Reverse();
        backLevels.Reverse();

        return (frontLevels, backLevels);
    }

    private static STCS002A BuildOptionParams(
        OptionContract contract,
        Alaris.Strategy.Cost.OrderDirection direction,
        string symbol,
        int contracts)
    {
        return new STCS002A
        {
            Contracts = contracts,
            MidPrice = contract.Mid,
            BidPrice = contract.Bid,
            AskPrice = contract.Ask,
            Direction = direction,
            Premium = contract.Mid,
            Symbol = symbol
        };
    }

    private SpreadGreeks ComputeSpreadGreeks(CalendarSpreadSelection selection, MarketDataSnapshot snapshot)
    {
        var frontPricing = _pricingEngine!.PriceOption(BuildPricingParameters(
                snapshot,
                selection.FrontLeg,
                selection.Right,
                selection.FrontIV))
            .GetAwaiter()
            .GetResult();

        var backPricing = _pricingEngine!.PriceOption(BuildPricingParameters(
                snapshot,
                selection.BackLeg,
                selection.Right,
                selection.BackIV))
            .GetAwaiter()
            .GetResult();

        return new SpreadGreeks
        {
            Delta = backPricing.Delta - frontPricing.Delta,
            Gamma = backPricing.Gamma - frontPricing.Gamma,
            Vega = backPricing.Vega - frontPricing.Vega,
            Theta = backPricing.Theta - frontPricing.Theta
        };
    }

    private Alaris.Strategy.Model.STDT003A BuildPricingParameters(
        MarketDataSnapshot snapshot,
        OptionContract contract,
        AlarisOptionRight right,
        double impliedVolatility)
    {
        return new Alaris.Strategy.Model.STDT003A
        {
            UnderlyingPrice = Convert.ToDouble(snapshot.SpotPrice),
            Strike = Convert.ToDouble(contract.Strike),
            Expiry = CRTM005A.FromDateTime(contract.Expiration),
            ImpliedVolatility = impliedVolatility,
            RiskFreeRate = Convert.ToDouble(snapshot.RiskFreeRate),
            DividendYield = Convert.ToDouble(snapshot.DividendYield),
            OptionType = ToOptionType(right),
            ValuationDate = CRTM005A.FromDateTime(snapshot.Timestamp)
        };
    }

    private bool TryResolveImpliedVolatility(
        OptionContract contract,
        double spotPrice,
        double riskFreeRate,
        double dividendYield,
        DateTime valuationDate,
        AlarisOptionRight right,
        out double impliedVolatility,
        out bool usedSynthetic)
    {
        if (contract.ImpliedVolatility.HasValue && contract.ImpliedVolatility.Value > 0m)
        {
            impliedVolatility = (double)contract.ImpliedVolatility.Value;
            usedSynthetic = false;
            return true;
        }

        if (contract.Mid <= 0m)
        {
            impliedVolatility = 0;
            usedSynthetic = false;
            return false;
        }

        try
        {
            var valuation = CRTM005A.FromDateTime(valuationDate);
            var expiry = CRTM005A.FromDateTime(contract.Expiration);
            if (expiry.SerialNumber <= valuation.SerialNumber)
            {
                impliedVolatility = 0;
                usedSynthetic = false;
                return false;
            }

            var parameters = new Alaris.Strategy.Model.STDT003A
            {
                UnderlyingPrice = spotPrice,
                Strike = Convert.ToDouble(contract.Strike),
                Expiry = CRTM005A.FromDateTime(contract.Expiration),
                ImpliedVolatility = 0,
                RiskFreeRate = riskFreeRate,
                DividendYield = dividendYield,
                OptionType = ToOptionType(right),
                ValuationDate = CRTM005A.FromDateTime(valuationDate)
            };

            impliedVolatility = _pricingEngine!.CalculateImpliedVolatility((double)contract.Mid, parameters)
                .GetAwaiter()
                .GetResult();
            usedSynthetic = true;
        }
        catch
        {
            impliedVolatility = 0;
            usedSynthetic = false;
            return false;
        }

        if (double.IsNaN(impliedVolatility) || impliedVolatility <= 0)
        {
            impliedVolatility = 0;
            usedSynthetic = false;
            return false;
        }

        return true;
    }

    private static bool TryGetContractForStrike(
        OptionChainSnapshot chain,
        DateTime expiry,
        decimal strike,
        AlarisOptionRight right,
        out OptionContract contract)
    {
        const decimal strikeTolerance = 0.0001m;

        foreach (var candidate in chain.Contracts)
        {
            if (candidate.Right != right)
                continue;
            if (candidate.Expiration.Date != expiry.Date)
                continue;
            if (Math.Abs(candidate.Strike - strike) > strikeTolerance)
                continue;

            contract = candidate;
            return true;
        }

        contract = null!;
        return false;
    }

    private static bool IsQuoteValid(OptionContract contract)
    {
        return contract.Bid > 0m && contract.Ask > 0m && contract.Ask >= contract.Bid;
    }

    private static QCOptionRight ToQcOptionRight(AlarisOptionRight right)
    {
        return right == AlarisOptionRight.Call ? QCOptionRight.Call : QCOptionRight.Put;
    }

    private static OptionType ToOptionType(AlarisOptionRight right)
    {
        return right == AlarisOptionRight.Call ? OptionType.Call : OptionType.Put;
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

        var spot = optionChain.SpotPrice;

        // Group by expiration and compute ATM call/put IV average
        var byExpiry = optionChain.Contracts
            .GroupBy(c => c.Expiration.Date)
            .OrderBy(g => g.Key);

        foreach (var group in byExpiry)
        {
            var daysToExpiry = (group.Key - referenceDate).Days;
            if (daysToExpiry <= 0)
                continue;

            OptionContract? atmCall = null;
            OptionContract? atmPut = null;
            decimal callDistance = decimal.MaxValue;
            decimal putDistance = decimal.MaxValue;

            foreach (var contract in group)
            {
                if (contract.ImpliedVolatility is null || contract.ImpliedVolatility <= 0m)
                    continue;
                if (contract.OpenInterest <= 0)
                    continue;

                var distance = Math.Abs(contract.Strike - spot);
                if (contract.Right == AlarisOptionRight.Call && distance < callDistance)
                {
                    callDistance = distance;
                    atmCall = contract;
                }
                else if (contract.Right == AlarisOptionRight.Put && distance < putDistance)
                {
                    putDistance = distance;
                    atmPut = contract;
                }
            }

            if (atmCall == null || atmPut == null)
                continue;

            var avgIV = ((double)atmCall.ImpliedVolatility!.Value + (double)atmPut.ImpliedVolatility!.Value) / 2.0;
            points.Add(new STTM001APoint
            {
                DaysToExpiry = daysToExpiry,
                ImpliedVolatility = avgIV,
                Strike = (double)atmCall.Strike
            });
        }
        
        return points;
    }

    /// <summary>
    /// Executes a calendar spread order using LEAN's combo order functionality.
    /// </summary>
    private OrderExecutionResult ExecuteCalendarSpread(
        Symbol underlyingSymbol,
        CalendarSpreadSelection selection,
        DTmd002A quote,
        int contracts)
    {
        try
        {
            // Create option symbols
            var frontOption = CreateOptionSymbol(
                underlyingSymbol,
                selection.Strike,
                selection.FrontExpiry,
                ToQcOptionRight(selection.Right));

            var backOption = CreateOptionSymbol(
                underlyingSymbol,
                selection.Strike,
                selection.BackExpiry,
                ToQcOptionRight(selection.Right));

            // Add option contracts to universe
            AddOptionContract(frontOption);
            AddOptionContract(backOption);

            // Create combo order legs using ratio format (1:-1) - global quantity controls sizing
            var legs = new List<QuantConnect.Orders.Leg>
            {
                QuantConnect.Orders.Leg.Create(frontOption, -1),  // Sell front month (ratio)
                QuantConnect.Orders.Leg.Create(backOption, 1)      // Buy back month (ratio)
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
    
    private sealed record CalendarSpreadSelection(
        string Symbol,
        OptionContract FrontLeg,
        OptionContract BackLeg,
        decimal Strike,
        DateTime FrontExpiry,
        DateTime BackExpiry,
        AlarisOptionRight Right,
        bool UsesSyntheticIv,
        double FrontIV,
        double BackIV);

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
        AlarisOptionRight OptionRight,
        int RealisedVolatilityWindowDays,
        double MinIvRvRatio,
        double MaxTermSlope,
        long MinimumAverageVolume,
        double DefaultImpliedVolatility)
    {
        public static StrategySettings Empty => new(
            0, 0, 0m, 0m, 0m, 0m, 0, 0, 0, 0, 0, AlarisOptionRight.Call, 0, 0.0, 0.0, 0, 0.0);

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
            if (!Enum.IsDefined(OptionRight))
                throw new InvalidOperationException("OptionRight must be Call or Put.");
            if (RealisedVolatilityWindowDays <= 0)
                throw new InvalidOperationException("RealisedVolatilityWindowDays must be positive.");
            if (MinIvRvRatio < 1.0 || MinIvRvRatio > 3.0)
                throw new InvalidOperationException("MinIvRvRatio must be between 1.0 and 3.0.");
            if (MaxTermSlope >= 0)
                throw new InvalidOperationException("MaxTermSlope must be negative.");
            if (MinimumAverageVolume <= 0)
                throw new InvalidOperationException("MinimumAverageVolume must be positive.");
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
        TimeSpan EarningsQueryTimeout,
        bool RequireOptionChainCache,
        bool RequireEarningsCache)
    {
        public static BacktestSettings Empty => new(
            DateTime.MinValue,
            DateTime.MinValue,
            0m,
            0,
            0,
            0,
            0,
            TimeSpan.Zero,
            false,
            false);

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

    private sealed record ForwardtestSettings(bool RequireOptionChainCache, bool RequireEarningsCache)
    {
        public static ForwardtestSettings Empty => new(false, false);

        public void Validate()
        {
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
        int VegaCorrelationLookbackDays,
        double MaxPositionToVolumeRatio,
        double MaxPositionToOpenInterestRatio,
        double DeltaRehedgeThreshold,
        double GammaWarningThreshold,
        double MoneynessAlertThreshold,
        double MinimumPostCostRatio,
        decimal MaxSlippagePercent,
        decimal MaxExecutionCostPercent,
        decimal MaxSlippagePerSpread,
        decimal MaxExecutionCostPerSpread,
        decimal MinimumCapitalForCostPercent,
        bool AllowInsufficientVegaData)
    {
        public static ValidationSettings Empty => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false);

        public void Validate()
        {
            if (MaxVegaCorrelation <= 0 || MaxVegaCorrelation > 1)
                throw new InvalidOperationException("MaxVegaCorrelation must be between 0 and 1.");
            if (MinimumVegaObservations <= 0)
                throw new InvalidOperationException("MinimumVegaObservations must be positive.");
            if (VegaCorrelationLookbackDays < MinimumVegaObservations)
                throw new InvalidOperationException("VegaCorrelationLookbackDays must be at least MinimumVegaObservations.");
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
            if (MinimumPostCostRatio < 0)
                throw new InvalidOperationException("MinimumPostCostRatio must be non-negative.");
            if (MaxSlippagePercent <= 0)
                throw new InvalidOperationException("MaxSlippagePercent must be positive.");
            if (MaxExecutionCostPercent <= 0)
                throw new InvalidOperationException("MaxExecutionCostPercent must be positive.");
            if (MaxSlippagePerSpread <= 0)
                throw new InvalidOperationException("MaxSlippagePerSpread must be positive.");
            if (MaxExecutionCostPerSpread <= 0)
                throw new InvalidOperationException("MaxExecutionCostPerSpread must be positive.");
            if (MinimumCapitalForCostPercent <= 0)
                throw new InvalidOperationException("MinimumCapitalForCostPercent must be positive.");
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

    private static long GetRequiredLong(IConfiguration configuration, string key)
    {
        var value = GetRequiredValue(configuration, key);
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidOperationException($"Invalid long for {key}: {value}");
        return parsed;
    }

    private static double GetRequiredDouble(IConfiguration configuration, string key)
    {
        var value = GetRequiredValue(configuration, key);
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidOperationException($"Invalid double for {key}: {value}");
        return parsed;
    }

    private static bool GetRequiredBool(IConfiguration configuration, string key)
    {
        var value = GetRequiredValue(configuration, key);
        if (!bool.TryParse(value, out var parsed))
            throw new InvalidOperationException($"Invalid boolean for {key}: {value}");
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

    private static AlarisOptionRight GetRequiredOptionRight(IConfiguration configuration, string key)
    {
        var value = GetRequiredValue(configuration, key);
        if (!Enum.TryParse<AlarisOptionRight>(value, true, out var parsed))
            throw new InvalidOperationException($"Invalid option right for {key}: {value}");
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
    private string? _cachedSymbol;
    private DateTime _cachedEvaluationDate;
    private MarketDataSnapshot? _cachedSnapshot;

    public DataBridgeMarketDataAdapter(AlarisDataBridge bridge)
    {
        _bridge = bridge;
    }

    /// <summary>
    /// Sets the evaluation date for market data queries (use LEAN's Time for backtests).
    /// </summary>
    public void SetEvaluationDate(DateTime date)
    {
        if (_evaluationDate == date)
        {
            return;
        }

        _evaluationDate = date;
        _cachedSnapshot = null;
        _cachedSymbol = null;
    }

    public MarketDataSnapshot GetSnapshot(string symbol, DateTime evaluationDate, CancellationToken cancellationToken)
    {
        _evaluationDate = evaluationDate;
        return GetSnapshotInternal(symbol, evaluationDate, cancellationToken);
    }

    public StrategyOptionChain GetSTDT002A(string symbol, DateTime evaluationDate)
    {
        _evaluationDate = evaluationDate;
        var snapshot = GetSnapshotInternal(symbol, evaluationDate, CancellationToken.None);
        
        var chain = new StrategyOptionChain
        {
            Symbol = symbol,
            UnderlyingPrice = (double)snapshot.SpotPrice,
            Timestamp = snapshot.Timestamp
        };
        
        if (snapshot.OptionChain == null)
        {
            return chain;
        }

        var byExpiry = snapshot.OptionChain.Contracts
            .GroupBy(c => c.Expiration.Date)
            .OrderBy(g => g.Key);

        foreach (var group in byExpiry)
        {
            var expiry = new Alaris.Strategy.Model.OptionExpiry
            {
                ExpiryDate = group.Key
            };

            foreach (var c in group)
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
                {
                    expiry.Calls.Add(contract);
                }
                else
                {
                    expiry.Puts.Add(contract);
                }
            }

            if (expiry.Calls.Count > 0 || expiry.Puts.Count > 0)
            {
                chain.Expiries.Add(expiry);
            }
        }

        return chain;
    }

    public IReadOnlyList<StrategyPriceBar> GetHistoricalPrices(string symbol, int days)
    {
        var snapshot = GetSnapshotInternal(symbol, _evaluationDate, CancellationToken.None);

        var bars = snapshot.HistoricalBars
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

        if (days <= 0 || bars.Count <= days)
        {
            return bars;
        }

        return bars
            .Skip(bars.Count - days)
            .ToList();
    }

    public double GetCurrentPrice(string symbol)
    {
        var snapshot = GetSnapshotInternal(symbol, _evaluationDate, CancellationToken.None);
        return (double)snapshot.SpotPrice;
    }

    public async Task<IReadOnlyList<DateTime>> GetEarningsDates(string symbol)
    {
        var snapshot = await GetSnapshotInternalAsync(symbol, _evaluationDate, CancellationToken.None);
        var dates = new List<DateTime>();
        if (snapshot.NextEarnings != null)
        {
            dates.Add(snapshot.NextEarnings.Date);
        }
        return dates;
    }

    public async Task<IReadOnlyList<DateTime>> GetHistoricalEarningsDates(string symbol, int lookbackQuarters = 12)
    {
        var snapshot = await GetSnapshotInternalAsync(symbol, _evaluationDate, CancellationToken.None);
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

    private MarketDataSnapshot GetSnapshotInternal(string symbol, DateTime evaluationDate, CancellationToken cancellationToken)
    {
        _evaluationDate = evaluationDate;
        if (_cachedSnapshot != null && _cachedSymbol == symbol && _cachedEvaluationDate == evaluationDate)
        {
            return _cachedSnapshot;
        }

        var snapshot = _bridge.GetMarketDataSnapshotAsync(symbol, evaluationDate, cancellationToken)
            .GetAwaiter()
            .GetResult();

        _cachedSnapshot = snapshot;
        _cachedSymbol = symbol;
        _cachedEvaluationDate = evaluationDate;
        return snapshot;
    }

    private async Task<MarketDataSnapshot> GetSnapshotInternalAsync(
        string symbol,
        DateTime evaluationDate,
        CancellationToken cancellationToken)
    {
        _evaluationDate = evaluationDate;
        if (_cachedSnapshot != null && _cachedSymbol == symbol && _cachedEvaluationDate == evaluationDate)
        {
            return _cachedSnapshot;
        }

        var snapshot = await _bridge.GetMarketDataSnapshotAsync(symbol, evaluationDate, cancellationToken)
            .ConfigureAwait(false);

        _cachedSnapshot = snapshot;
        _cachedSymbol = symbol;
        _cachedEvaluationDate = evaluationDate;
        return snapshot;
    }
}
