// src/csharp/Algorithm/ArbitrageAlgorithm.cs
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Securities.Option;
using QuantConnect.Data.Market;
using QuantConnect.Brokerages;
using System;
using System.Collections.Generic;
using System.Linq;
using Alaris.IPC;
using Alaris.Monitoring;
using QuantConnect.Indicators;
using QuantConnect.Statistics;
using QuantConnect.Logging;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Symbol = QuantConnect.Symbol;
using QuantConnect.Configuration;

namespace Alaris.Algorithm
{
    public class ArbitrageAlgorithm : QCAlgorithm
    {
        private SharedMemoryBridge? _sharedMemory;
        private PerformanceMonitor? _performanceMonitor;
        private GCOptimizer? _gcOptimizer;
        private string _symbol = "SPY";
        private StrategyMode _strategyMode = StrategyMode.DeltaNeutral;
        private bool _isInitialized = false;
        private bool _quantlibConnected = false;

        // Algorithm configuration
        private readonly Dictionary<string, uint> _symbolToId = new Dictionary<string, uint>();
        private readonly Dictionary<uint, Symbol> _idToSymbol = new Dictionary<uint, Symbol>();
        private readonly Dictionary<uint, MarketDataMessage> _latestMarketData = new Dictionary<uint, MarketDataMessage>();

        // Risk management
        private decimal _maxPositionSize = 0.05m;
        private decimal _maxDailyLoss = 0.02m;
        private decimal _startingCash;
        private decimal _dailyStartingValue;

        // Position tracking - keyed by QuantLib symbol IDs
        private readonly Dictionary<uint, PositionInfo> _positions = new Dictionary<uint, PositionInfo>();

        // Performance tracking
        private int _marketDataReceived = 0;
        private int _signalsPublished = 0;
        private int _ordersPlaced = 0;

        // Store the main equity symbol
        private Symbol _mainEquitySymbol = null!;

        public override void Initialize()
        {
            try
            {
                Log("Starting Alaris Algorithm initialization...");
                
                // Load configuration from environment
                _symbol = Environment.GetEnvironmentVariable("ALARIS_SYMBOL") ?? "SPY";
                var strategyModeStr = Environment.GetEnvironmentVariable("ALARIS_STRATEGY")?.ToLower() ?? "deltaneutral";
                var frequency = Config.Get("data-resolution", "minute").ToLower();
                var debug = Config.GetBool("debug-mode", false);
                
                Log($"Configuration - Symbol: {_symbol}, Strategy: {strategyModeStr}, Frequency: {frequency}, Debug: {debug}");

                _strategyMode = strategyModeStr switch
                {
                    "gammascalping" => StrategyMode.GammaScalping,
                    "volatilitytiming" => StrategyMode.VolatilityTiming,
                    "relativevalue" => StrategyMode.RelativeValue,
                    _ => StrategyMode.DeltaNeutral
                };

                // Set up algorithm parameters
                SetStartDate(2018, 1, 1);
                SetEndDate(DateTime.Now);
                SetCash(100000);
                _startingCash = Portfolio.Cash;
                _dailyStartingValue = Portfolio.TotalPortfolioValue;

                SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage, AccountType.Margin);

                // Initialize universe
                Log("Initializing trading universe...");
                InitializeUniverse(frequency);
                _mainEquitySymbol = _idToSymbol[_symbolToId[_symbol]];

                // Initialize shared memory bridge AFTER universe setup
                Log("Connecting to QuantLib process via shared memory...");
                _sharedMemory = new SharedMemoryBridge();
                
                // Set up event handlers for data FROM QuantLib
                _sharedMemory.MarketDataReceived += OnMarketDataFromQuantLib;
                _sharedMemory.ControlMessageReceived += OnControlMessageFromQuantLib;

                // Send connection handshake to QuantLib
                _sharedMemory.SendConnectionHandshake();
                
                // Initialize performance monitoring
                _performanceMonitor = new PerformanceMonitor();
                _performanceMonitor.Initialize(_symbol, _strategyMode);

                // Initialize GC optimization
                _gcOptimizer = new GCOptimizer();

                // Schedule regular tasks
                var updateInterval = frequency switch
                {
                    "daily" => TimeSpan.FromDays(1),
                    "hour" => TimeSpan.FromHours(1),
                    _ => TimeSpan.FromMinutes(1)
                };

                Schedule.On(DateRules.EveryDay(), TimeRules.Every(updateInterval), () =>
                {
                    if (_isInitialized && _quantlibConnected)
                    {
                        CheckRiskLimits();
                        PublishPortfolioUpdate();
                    }
                });

                _isInitialized = true;
                Log($"Alaris algorithm initialized successfully - waiting for QuantLib connection");
            }
            catch (Exception ex)
            {
                Error($"Failed to initialize algorithm: {ex.Message}");
                Error($"Stack trace: {ex.StackTrace}");
                Quit($"Initialization failed: {ex.Message}");
            }
        }

        private void InitializeUniverse(string frequency)
        {
            var symbols = new[] { "SPY", "QQQ", "IWM", "EFA", "EEM" };
            uint symbolId = 1;

            var resolution = frequency switch
            {
                "daily" => Resolution.Daily,
                "hour" => Resolution.Hour,
                _ => Resolution.Minute
            };

            foreach (var symbol in symbols)
            {
                // Add underlying equity
                var equity = AddEquity(symbol, resolution, Market.USA);
                _symbolToId[symbol] = symbolId;
                _idToSymbol[symbolId] = equity.Symbol;

                // Add option chain
                var option = AddOption(symbol, resolution);
                option.SetFilter(universe => universe.IncludeWeeklys()
                                                   .Strikes(-5, +5)
                                                   .Expiration(TimeSpan.FromDays(0), TimeSpan.FromDays(60)));

                symbolId++;
            }
        }

        // Receive market data FROM QuantLib process
        private void OnMarketDataFromQuantLib(MarketDataMessage marketData)
        {
            try
            {
                _marketDataReceived++;
                _performanceMonitor?.StartMeasurement("ProcessQuantLibData");

                // Store latest market data from QuantLib
                _latestMarketData[marketData.symbol_id] = marketData;

                // Convert QuantLib market data to Lean symbols for internal processing
                if (_idToSymbol.TryGetValue(marketData.symbol_id, out Symbol? symbol) && symbol != null)
                {
                    // Update our internal understanding but don't place trades yet
                    // The strategy decisions come from QuantLib via trading signals
                    
                    Debug($"Received market data from QuantLib - Symbol: {symbol}, " +
                          $"Price: {marketData.underlying_price:F2}, " +
                          $"Bid/Ask: {marketData.bid:F4}/{marketData.ask:F4}");
                }

                _performanceMonitor?.EndMeasurement("ProcessQuantLibData");
                
                if (!_quantlibConnected)
                {
                    _quantlibConnected = true;
                    Log("✓ Connected to QuantLib process successfully");
                }
            }
            catch (Exception ex)
            {
                Error($"Error processing market data from QuantLib: {ex.Message}");
            }
        }

        // Handle control messages FROM QuantLib process
        private void OnControlMessageFromQuantLib(ControlMessage message)
        {
            try
            {
                var messageType = (ControlMessageType)message.message_type;
                
                switch (messageType)
                {
                    case ControlMessageType.START_TRADING:
                        Log("QuantLib process authorized trading to begin");
                        break;
                        
                    case ControlMessageType.STOP_TRADING:
                        Log("QuantLib process requested trading halt");
                        CloseAllPositions();
                        break;
                        
                    case ControlMessageType.EMERGENCY_LIQUIDATION:
                        Log("QuantLib process requested emergency liquidation");
                        CloseAllPositions();
                        Quit("Emergency liquidation requested by QuantLib");
                        break;
                        
                    case ControlMessageType.HEARTBEAT:
                        // Respond to heartbeat
                        _sharedMemory?.SendControlMessage(ControlMessageType.HEARTBEAT);
                        break;
                        
                    case ControlMessageType.SYSTEM_STATUS:
                        Debug($"QuantLib system status: {message.value1}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Error($"Error processing control message from QuantLib: {ex.Message}");
            }
        }

        public override void OnData(Slice data)
        {
            if (!_isInitialized || !_quantlibConnected) return;

            try
            {
                _performanceMonitor?.StartMeasurement("OnData");

                // Process market data and send to QuantLib for analysis
                foreach (var kvp in data.Bars)
                {
                    ProcessMarketDataForQuantLib(kvp.Key, kvp.Value);
                }

                // Process options data
                if (data.OptionChains != null)
                {
                    foreach (var chain in data.OptionChains)
                    {
                        ProcessOptionChainForQuantLib(chain.Key, chain.Value);
                    }
                }

                // Generate trading signals based on current strategy
                // This is where we would normally generate signals, but now
                // QuantLib generates the signals and we execute them
                var tradingSignals = GenerateTradingSignalsForQuantLib();
                
                foreach (var signal in tradingSignals)
                {
                    if (_sharedMemory?.PublishTradingSignal(signal) == true)
                    {
                        _signalsPublished++;
                        Debug($"Published signal to QuantLib: {signal.SymbolId} qty={signal.Quantity}");
                    }
                }

                // Update positions based on latest market data
                UpdatePositions();

                _performanceMonitor?.EndMeasurement("OnData");
            }
            catch (Exception ex)
            {
                Error($"Error in OnData: {ex.Message}");
            }
        }

        private void ProcessMarketDataForQuantLib(Symbol symbol, TradeBar bar)
        {
            if (_symbolToId.TryGetValue(symbol.Value, out uint symbolId))
            {
                var security = Securities[symbol];
                
                // Create market data message for QuantLib
                // Note: QuantLib is the consumer of this data
                var marketData = new MarketDataMessage(
                    symbolId,
                    (double)security.BidPrice,
                    (double)security.AskPrice, 
                    (double)bar.Close
                );

                // In the integrated system, we don't send market data TO QuantLib
                // because QuantLib gets its market data from external feeds
                // We receive analysis and signals FROM QuantLib
                
                Debug($"Market data available: {symbol} = {bar.Close:C}");
            }
        }

        private void ProcessOptionChainForQuantLib(Symbol underlying, OptionChain chain)
        {
            if (!_symbolToId.TryGetValue(underlying.Value, out uint symbolId))
                return;

            var underlyingPrice = (double)Securities[underlying].Price;

            foreach (var contract in chain)
            {
                if (contract.BidPrice > 0 && contract.AskPrice > 0)
                {
                    Debug($"Option data: {contract.Symbol} Bid={contract.BidPrice:F2} " +
                          $"Ask={contract.AskPrice:F2} IV={contract.ImpliedVolatility:P1}");
                }
            }
        }

        private List<TradingSignalMessage> GenerateTradingSignalsForQuantLib()
        {
            var signals = new List<TradingSignalMessage>();

            // In this integrated approach, the sophisticated strategy logic
            // lives in the QuantLib process. Here we just provide basic
            // portfolio information that QuantLib can use for decision making.

            // Example: Send portfolio exposure information to QuantLib
            foreach (var position in _positions.Values)
            {
                if (Math.Abs(position.Quantity) > 0)
                {
                    // Create a signal that informs QuantLib of our current position
                    var signal = _sharedMemory!.CreateTradingSignal(
                        symbolId: position.SymbolId,
                        theoreticalPrice: position.CurrentPrice,
                        marketPrice: position.CurrentPrice,
                        impliedVol: 0.0, // We don't calculate this in Lean
                        forecastVol: 0.0, // QuantLib handles this
                        confidence: 1.0,
                        quantity: 0, // This is an info signal, not a trade signal
                        side: 0,
                        urgency: 1,
                        signalType: 3 // Custom: Portfolio update
                    );
                    
                    signals.Add(signal);
                }
            }

            return signals;
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            try
            {
                _performanceMonitor?.ProcessOrderEvent(orderEvent);
                
                if (orderEvent.Status == OrderStatus.Filled)
                {
                    _ordersPlaced++;
                    
                    Log($"Order filled: {orderEvent.Symbol} - {orderEvent.FillQuantity} @ {orderEvent.FillPrice:C}");
                    
                    // Find the corresponding QuantLib symbol ID
                    var symbolName = orderEvent.Symbol.Value;
                    if (_symbolToId.TryGetValue(symbolName, out uint symbolId))
                    {
                        // Update position tracking
                        if (!_positions.TryGetValue(symbolId, out var position))
                        {
                            position = new PositionInfo { SymbolId = symbolId };
                            _positions[symbolId] = position;
                        }

                        position.Quantity += (double)orderEvent.FillQuantity;
                        position.AveragePrice = ((position.AveragePrice * (position.Quantity - (double)orderEvent.FillQuantity)) + 
                                               ((double)orderEvent.FillPrice * (double)orderEvent.FillQuantity)) / position.Quantity;
                        position.CurrentPrice = (double)orderEvent.FillPrice;

                        // Notify QuantLib of the fill
                        var fillNotification = _sharedMemory!.CreateTradingSignal(
                            symbolId: symbolId,
                            theoreticalPrice: (double)orderEvent.FillPrice,
                            marketPrice: (double)orderEvent.FillPrice,
                            impliedVol: 0.0,
                            forecastVol: 0.0,
                            confidence: 1.0,
                            quantity: (int)orderEvent.FillQuantity,
                            side: orderEvent.FillQuantity > 0 ? (byte)0 : (byte)1,
                            urgency: 255,
                            signalType: 4 // Custom: Fill notification
                        );

                        _sharedMemory?.PublishTradingSignal(fillNotification);
                    }
                }
                else if (orderEvent.Status == OrderStatus.Canceled || orderEvent.Status == OrderStatus.Invalid)
                {
                    Debug($"Order failed: {orderEvent.Symbol} - {orderEvent.Message}");
                }
            }
            catch (Exception ex)
            {
                Error($"Error processing order event: {ex.Message}");
            }
        }

        private void UpdatePositions()
        {
            foreach (var kvp in _positions.ToList())
            {
                var symbolId = kvp.Key;
                var position = kvp.Value;
                
                if (_idToSymbol.TryGetValue(symbolId, out Symbol? symbol) && 
                    symbol != null && Securities.ContainsKey(symbol))
                {
                    var security = Securities[symbol];
                    position.CurrentPrice = (double)security.Price;
                    position.UnrealizedPnL = position.Quantity * (position.CurrentPrice - position.AveragePrice);
                    
                    // Basic stop loss (QuantLib should handle sophisticated risk management)
                    if (Math.Abs(position.UnrealizedPnL) > Math.Abs(position.AveragePrice * position.Quantity) * 0.2) // 20% stop
                    {
                        Log($"Emergency stop loss triggered for {symbol}");
                        MarketOrder(symbol, -(int)position.Quantity);
                        _positions.Remove(symbolId);
                    }
                }
            }
        }

        private void CheckRiskLimits()
        {
            var currentValue = Portfolio.TotalPortfolioValue;
            var dailyPnL = currentValue - _dailyStartingValue;
            
            if (dailyPnL < -_dailyStartingValue * _maxDailyLoss)
            {
                Log($"Daily loss limit exceeded: {dailyPnL:C}");
                CloseAllPositions();
                _sharedMemory?.SendControlMessage(ControlMessageType.EMERGENCY_LIQUIDATION);
            }
        }

        private void PublishPortfolioUpdate()
        {
            // Send portfolio metrics to QuantLib for risk management
            var totalValue = (double)Portfolio.TotalPortfolioValue;
            var totalPnL = (double)(Portfolio.TotalPortfolioValue - _startingCash);
            
            _sharedMemory?.SendControlMessage(
                ControlMessageType.SYSTEM_STATUS,
                param1: (uint)_positions.Count,
                param2: (uint)_ordersPlaced,
                value1: totalValue,
                value2: totalPnL
            );
        }

        private void CloseAllPositions()
        {
            foreach (var holding in Portfolio.Values.Where(x => x.Invested))
            {
                MarketOrder(holding.Symbol, -holding.Quantity);
            }
            _positions.Clear();
        }

        public override void OnEndOfDay(Symbol symbol)
        {
            if (symbol == _idToSymbol[_symbolToId[_symbol]])
            {
                _dailyStartingValue = Portfolio.TotalPortfolioValue;
                
                var dailyReturn = (Portfolio.TotalPortfolioValue - _startingCash) / _startingCash;
                Log($"Daily Performance - Portfolio: {Portfolio.TotalPortfolioValue:C}, " +
                    $"Return: {dailyReturn:P2}, Market Data: {_marketDataReceived}, " +
                    $"Signals: {_signalsPublished}, Orders: {_ordersPlaced}");
                
                // Send daily summary to QuantLib
                PublishPortfolioUpdate();
            }
        }

        public override void OnEndOfAlgorithm()
        {
            try
            {
                var totalReturn = (Portfolio.TotalPortfolioValue - _startingCash) / _startingCash;
                
                Log($"Algorithm Performance Summary:");
                Log($"Total Return: {totalReturn:P2}");
                Log($"Market Data Received: {_marketDataReceived}");
                Log($"Signals Published: {_signalsPublished}");
                Log($"Orders Placed: {_ordersPlaced}");
                
                // Notify QuantLib of shutdown
                _sharedMemory?.SendControlMessage(ControlMessageType.STOP_TRADING);
                
                // Cleanup
                _sharedMemory?.Dispose();
                _performanceMonitor?.Dispose();
                _gcOptimizer?.Dispose();
            }
            catch (Exception ex)
            {
                Error($"Error in OnEndOfAlgorithm: {ex.Message}");
            }
        }

        private class PositionInfo
        {
            public uint SymbolId { get; set; }
            public double Quantity { get; set; }
            public double AveragePrice { get; set; }
            public double CurrentPrice { get; set; }
            public double UnrealizedPnL { get; set; }
        }
    }
}