// src/csharp/Algorithm/DeterministicVolArbitrageAlgorithm.cs
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Securities.Option;
using QuantConnect.Data.Market;
using System;
using System.Collections.Generic;
using System.Linq;
using Alaris.IPC;
using Alaris.Monitoring;

namespace Alaris.Algorithm
{
    public class DeterministicVolArbitrageAlgorithm : QCAlgorithm
    {
        private SharedMemoryBridge _sharedMemory;
        private PerformanceMonitor _performanceMonitor;
        private GCOptimizer _gcOptimizer;

        // Algorithm configuration
        private readonly Dictionary<string, uint> _symbolToId = new Dictionary<string, uint>();
        private readonly Dictionary<uint, Symbol> _idToSymbol = new Dictionary<uint, Symbol>();
        private readonly HashSet<Symbol> _activeSymbols = new HashSet<Symbol>();

        // Risk management
        private decimal _maxPositionSize = 0.05m; // 5% of portfolio per position
        private decimal _maxDailyLoss = 0.02m;    // 2% daily loss limit
        private decimal _startingCash;
        private decimal _dailyStartingValue;

        // Position tracking
        private readonly Dictionary<Symbol, PositionInfo> _positions = new Dictionary<Symbol, PositionInfo>();

        // Performance tracking
        private int _signalsReceived = 0;
        private int _ordersPlaced = 0;
        private int _successfulTrades = 0;

        public override void Initialize()
        {
            try
            {
                SetStartDate(2024, 1, 1);
                SetEndDate(2024, 12, 31);
                SetCash(1000000);
                _startingCash = Portfolio.Cash;
                _dailyStartingValue = Portfolio.TotalPortfolioValue;

                // Configure Interactive Brokers brokerage
                SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage, AccountType.Margin);

                // Initialize universe
                InitializeUniverse();

                // Initialize shared memory bridge
                _sharedMemory = new SharedMemoryBridge();
                _sharedMemory.SignalReceived += OnTradingSignalReceived;
                _sharedMemory.ControlMessageReceived += OnControlMessageReceived;

                // Initialize performance monitoring
                _performanceMonitor = new PerformanceMonitor();

                // Initialize GC optimization
                _gcOptimizer = new GCOptimizer();

                // Send start trading signal to QuantLib process
                _sharedMemory.SendControlMessage(ControlMessageType.StartTrading);

                Log("Alaris Volatility Arbitrage Algorithm initialized successfully");
            }
            catch (Exception ex)
            {
                Error($"Failed to initialize algorithm: {ex.Message}");
                Quit($"Initialization failed: {ex.Message}");
            }
        }

        private void InitializeUniverse()
        {
            var symbols = new[] { "SPY", "QQQ", "IWM", "EFA", "EEM" };
            uint symbolId = 1;

            foreach (var symbol in symbols)
            {
                // Add underlying equity
                var equity = AddEquity(symbol, Resolution.Minute, Market.USA);
                _symbolToId[symbol] = symbolId;
                _idToSymbol[symbolId] = equity.Symbol;
                _activeSymbols.Add(equity.Symbol);

                // Add option chain
                var option = AddOption(symbol, Resolution.Minute);
                option.SetFilter(universe => universe.IncludeWeeklys()
                                                   .Strikes(-5, +5)
                                                   .Expiration(TimeSpan.FromDays(0), TimeSpan.FromDays(60)));

                symbolId++;
            }
        }

        public override void OnData(Slice data)
        {
            try
            {
                _performanceMonitor.StartMeasurement("OnData");

                // Process underlying securities
                foreach (var kvp in data.Bars)
                {
                    var symbol = kvp.Key;
                    var bar = kvp.Value;

                    if (_symbolToId.TryGetValue(symbol.Value, out uint symbolId))
                    {
                        var security = Securities[symbol];
                        var marketData = new MarketDataMessage(
                            symbolId, 
                            (double)security.BidPrice, 
                            (double)security.AskPrice, 
                            (double)bar.Close
                        );

                        _sharedMemory.PublishMarketData(marketData);
                    }
                }

                // Process options data - FIXED: Use data.OptionChains instead of direct reference
                if (data.OptionChains != null)
                {
                    foreach (var chain in data.OptionChains)
                    {
                        ProcessOptionChain(chain.Key, chain.Value);
                    }
                }

                // Update positions
                UpdatePositions();

                // Check risk limits
                CheckRiskLimits();

                _performanceMonitor.EndMeasurement("OnData");
            }
            catch (Exception ex)
            {
                Error($"Error in OnData: {ex.Message}");
            }
        }

        private void ProcessOptionChain(Symbol underlying, OptionChain chain)
        {
            if (!_symbolToId.TryGetValue(underlying.Value, out uint symbolId))
                return;

            var underlyingPrice = (double)Securities[underlying].Price;

            foreach (var contract in chain)
            {
                if (contract.BidPrice > 0 && contract.AskPrice > 0)
                {
                    var optionData = new MarketDataMessage(
                        symbolId + (uint)contract.Strike.GetHashCode(), // Unique ID for option
                        (double)contract.BidPrice,
                        (double)contract.AskPrice,
                        underlyingPrice
                    )
                    {
                        BidIv = (double)contract.ImpliedVolatility,
                        AskIv = (double)contract.ImpliedVolatility
                    };

                    _sharedMemory.PublishMarketData(optionData);
                }
            }
        }

        private void OnTradingSignalReceived(TradingSignalMessage signal)
        {
            try
            {
                _signalsReceived++;
                _performanceMonitor.StartMeasurement("ProcessSignal");

                if (!_idToSymbol.TryGetValue(signal.SymbolId, out Symbol symbol))
                {
                    Log($"Unknown symbol ID: {signal.SymbolId}");
                    return;
                }

                // Validate signal
                if (signal.Confidence < 0.7) // Minimum confidence threshold
                {
                    Log($"Signal confidence too low: {signal.Confidence:F3}");
                    return;
                }

                // Check position limits
                var currentValue = Portfolio.TotalPortfolioValue;
                var positionValue = Math.Abs(signal.Quantity) * (decimal)signal.MarketPrice;
                
                if (positionValue > currentValue * _maxPositionSize)
                {
                    Log($"Position size too large: {positionValue:C} > {currentValue * _maxPositionSize:C}");
                    return;
                }

                // Place order
                PlaceSignalOrder(signal, symbol);

                _performanceMonitor.EndMeasurement("ProcessSignal");
            }
            catch (Exception ex)
            {
                Error($"Error processing trading signal: {ex.Message}");
            }
        }

        private void PlaceSignalOrder(TradingSignalMessage signal, Symbol symbol)
        {
            var quantity = signal.Quantity;
            if (signal.Side == 1) // Sell signal
            {
                quantity = -quantity;
            }

            OrderTicket ticket;
            
            // Use market order for immediate execution
            // In production, might use limit orders based on signal urgency
            if (signal.Urgency > 200) // High urgency
            {
                ticket = MarketOrder(symbol, quantity);
            }
            else
            {
                // Use limit order with small buffer
                var limitPrice = signal.Side == 0 ? 
                                (decimal)signal.MarketPrice * 1.001m : // Buy slightly above market
                                (decimal)signal.MarketPrice * 0.999m;  // Sell slightly below market
                
                ticket = LimitOrder(symbol, quantity, limitPrice);
            }

            if (ticket != null)
            {
                _ordersPlaced++;
                Log($"Placed order: {quantity} shares of {symbol} based on signal (Confidence: {signal.Confidence:F3})");

                // Store signal info for tracking
                if (!_positions.ContainsKey(symbol))
                {
                    _positions[symbol] = new PositionInfo();
                }
                _positions[symbol].LastSignal = signal;
            }
        }

        private void OnControlMessageReceived(ControlMessage message)
        {
            var messageType = (ControlMessageType)message.MessageType;
            
            switch (messageType)
            {
                case ControlMessageType.StopTrading:
                    Log("Received stop trading signal");
                    CloseAllPositions();
                    break;
                    
                case ControlMessageType.SystemStatus:
                    // Log system status updates
                    break;
                    
                case ControlMessageType.Heartbeat:
                    // Respond to heartbeat
                    _sharedMemory.SendControlMessage(ControlMessageType.Heartbeat);
                    break;
            }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            try
            {
                if (orderEvent.Status == OrderStatus.Filled)
                {
                    _successfulTrades++;
                    
                    Log($"Order filled: {orderEvent.Symbol} - {orderEvent.FillQuantity} @ {orderEvent.FillPrice:C}");
                    
                    // Update position tracking
                    if (_positions.TryGetValue(orderEvent.Symbol, out var position))
                    {
                        position.Quantity += (double)orderEvent.FillQuantity;
                        position.AveragePrice = ((position.AveragePrice * (position.Quantity - (double)orderEvent.FillQuantity)) + 
                                               ((double)orderEvent.FillPrice * (double)orderEvent.FillQuantity)) / position.Quantity;
                    }
                }
                else if (orderEvent.Status == OrderStatus.Canceled || orderEvent.Status == OrderStatus.Invalid)
                {
                    Log($"Order failed: {orderEvent.Symbol} - {orderEvent.Message}");
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
                var symbol = kvp.Key;
                var position = kvp.Value;
                
                if (Securities.ContainsKey(symbol))
                {
                    var security = Securities[symbol];
                    position.CurrentPrice = (double)security.Price;
                    position.UnrealizedPnL = position.Quantity * (position.CurrentPrice - position.AveragePrice);
                    
                    // Check for exit conditions based on unrealized P&L
                    if (Math.Abs(position.UnrealizedPnL) > Math.Abs(position.AveragePrice * position.Quantity) * 0.1) // 10% stop loss
                    {
                        Log($"Closing position in {symbol} due to stop loss");
                        MarketOrder(symbol, -(int)position.Quantity);
                        _positions.Remove(symbol);
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
                _sharedMemory.SendControlMessage(ControlMessageType.StopTrading);
            }
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
            // Reset daily tracking
            if (symbol == _activeSymbols.First()) // Only do this once per day
            {
                _dailyStartingValue = Portfolio.TotalPortfolioValue;
                
                // Log daily performance
                var dailyReturn = (Portfolio.TotalPortfolioValue - _startingCash) / _startingCash;
                Log($"Daily Performance - Portfolio Value: {Portfolio.TotalPortfolioValue:C}, " +
                    $"Return: {dailyReturn:P2}, Signals: {_signalsReceived}, Orders: {_ordersPlaced}");
                
                // Send daily metrics to QuantLib process
                _sharedMemory.SendControlMessage(ControlMessageType.SystemStatus, 
                                               (uint)_signalsReceived, (uint)_ordersPlaced, 
                                               (double)dailyReturn, (double)Portfolio.TotalPortfolioValue);
            }
        }

        public override void OnEndOfAlgorithm()
        {
            try
            {
                var totalReturn = (Portfolio.TotalPortfolioValue - _startingCash) / _startingCash;
                var winRate = _ordersPlaced > 0 ? (double)_successfulTrades / _ordersPlaced : 0.0;
                
                Log($"Algorithm Performance Summary:");
                Log($"Total Return: {totalReturn:P2}");
                Log($"Win Rate: {winRate:P2}");
                Log($"Signals Received: {_signalsReceived}");
                Log($"Orders Placed: {_ordersPlaced}");
                Log($"Successful Trades: {_successfulTrades}");
                
                // Send stop signal to QuantLib process
                _sharedMemory.SendControlMessage(ControlMessageType.StopTrading);
                
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
            public double Quantity { get; set; }
            public double AveragePrice { get; set; }
            public double CurrentPrice { get; set; }
            public double UnrealizedPnL { get; set; }
            public TradingSignalMessage LastSignal { get; set; }
        }
    }
}