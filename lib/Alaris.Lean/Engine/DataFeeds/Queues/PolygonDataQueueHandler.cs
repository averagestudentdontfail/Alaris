/*
 * PolygonDataQueueHandler - Polygon.io WebSocket Data Queue Handler for LEAN
 * Component ID: DTwd001A
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Lean.Engine.DataFeeds.Queues
{
    /// <summary>
    /// Polygon.io WebSocket Data Queue Handler for LEAN live trading.
    /// Component ID: DTwd001A
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements IDataQueueHandler to stream delayed market data from Polygon.io WebSocket API.
    /// Connects to wss://delayed.polygon.io/stocks for 15-minute delayed data.
    /// </para>
    /// <para>
    /// Authentication uses API key from configuration (polygon-api-key).
    /// Supports minute aggregates (AM) and trades (T) for equity symbols.
    /// </para>
    /// </remarks>
    public class PolygonDataQueueHandler : IDataQueueHandler
    {
        private const string StocksEndpoint = "wss://delayed.polygon.io/stocks";
        
        private readonly string _apiKey;
        private readonly IDataAggregator _aggregator;
        private readonly EventBasedDataQueueHandlerSubscriptionManager _subscriptionManager;
        private readonly ConcurrentDictionary<Symbol, TimeZoneOffsetProvider> _symbolExchangeTimeZones;
        private readonly MarketHoursDatabase _marketHoursDatabase;
        private readonly CancellationTokenSource _cts;
        
        private ClientWebSocket _stocksWebSocket;
        private Task _stocksReceiveTask;
        private bool _isConnected;
        private bool _isAuthenticated;
        private bool _disposed;

        /// <summary>
        /// Returns whether the data provider is connected
        /// </summary>
        public bool IsConnected => _isConnected && _isAuthenticated;

        /// <summary>
        /// Continuous UTC time provider
        /// </summary>
        private ITimeProvider TimeProvider { get; } = RealTimeProvider.Instance;

        /// <summary>
        /// Initializes a new instance of the PolygonDataQueueHandler
        /// </summary>
        public PolygonDataQueueHandler()
            : this(Composer.Instance.GetExportedValueByTypeName<IDataAggregator>(nameof(AggregationManager)))
        {
        }

        /// <summary>
        /// Initializes a new instance of the PolygonDataQueueHandler with a data aggregator
        /// </summary>
        public PolygonDataQueueHandler(IDataAggregator dataAggregator)
        {
            _aggregator = dataAggregator ?? throw new ArgumentNullException(nameof(dataAggregator));
            _cts = new CancellationTokenSource();
            
            // Get API key from multiple sources:
            // 1. LEAN config (polygon-api-key in config.json)
            // 2. Environment variable ALARIS_Polygon__ApiKey (.NET secrets format)
            // 3. Environment variable POLYGON_API_KEY (standard format)
            _apiKey = Config.Get("polygon-api-key");
            if (string.IsNullOrEmpty(_apiKey))
            {
                _apiKey = Environment.GetEnvironmentVariable("ALARIS_Polygon__ApiKey");
            }
            if (string.IsNullOrEmpty(_apiKey))
            {
                _apiKey = Environment.GetEnvironmentVariable("POLYGON_API_KEY");
            }
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException(
                    "Polygon API key not configured. Set one of: " +
                    "'polygon-api-key' in config.json, " +
                    "ALARIS_Polygon__ApiKey env var, or " +
                    "POLYGON_API_KEY env var.");
            }
            
            var maskedKey = _apiKey.Length > 4 ? _apiKey[..4] + new string('*', _apiKey.Length - 4) : "****";
            Log.Trace($"PolygonDataQueueHandler: Initialized with API key {maskedKey}");
            
            _marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
            _symbolExchangeTimeZones = new ConcurrentDictionary<Symbol, TimeZoneOffsetProvider>();
            
            _subscriptionManager = new EventBasedDataQueueHandlerSubscriptionManager();
            _subscriptionManager.SubscribeImpl += OnSubscribe;
            _subscriptionManager.UnsubscribeImpl += OnUnsubscribe;
        }

        /// <summary>
        /// Subscribe to the specified configuration
        /// </summary>
        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            if (dataConfig.SecurityType != SecurityType.Equity)
            {
                Log.Trace($"PolygonDataQueueHandler.Subscribe(): Unsupported security type {dataConfig.SecurityType}, only Equity supported");
                return null;
            }
            
            var enumerator = _aggregator.Add(dataConfig, newDataAvailableHandler);
            _subscriptionManager.Subscribe(dataConfig);
            
            // Connect WebSocket on first subscription
            if (!_isConnected)
            {
                Task.Run(async () => await ConnectAsync());
            }
            
            return enumerator;
        }

        /// <summary>
        /// Removes the specified configuration
        /// </summary>
        public void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            _subscriptionManager.Unsubscribe(dataConfig);
            _aggregator.Remove(dataConfig);
        }

        /// <summary>
        /// Sets the job we're subscribing for
        /// </summary>
        public void SetJob(LiveNodePacket job)
        {
            // No additional setup required
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            _cts.Cancel();
            
            try
            {
                _stocksWebSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None).Wait(5000);
            }
            catch { }
            
            _stocksWebSocket?.Dispose();
            _cts.Dispose();
            
            Log.Trace("PolygonDataQueueHandler: Disposed");
        }

        private bool OnSubscribe(IEnumerable<Symbol> symbols, TickType tickType)
        {
            if (!_isAuthenticated)
            {
                Log.Trace("PolygonDataQueueHandler.OnSubscribe(): Not yet authenticated, symbols will be subscribed after connection");
                return true;
            }
            
            foreach (var symbol in symbols)
            {
                var subscribeParams = GetSubscriptionParams(symbol, tickType);
                if (!string.IsNullOrEmpty(subscribeParams))
                {
                    SendSubscribe(subscribeParams);
                }
            }
            
            return true;
        }

        private bool OnUnsubscribe(IEnumerable<Symbol> symbols, TickType tickType)
        {
            if (!_isAuthenticated) return true;
            
            foreach (var symbol in symbols)
            {
                var subscribeParams = GetSubscriptionParams(symbol, tickType);
                if (!string.IsNullOrEmpty(subscribeParams))
                {
                    SendUnsubscribe(subscribeParams);
                }
            }
            
            return true;
        }

        private string GetSubscriptionParams(Symbol symbol, TickType tickType)
        {
            // Polygon uses AM.{symbol} for minute aggregates, T.{symbol} for trades
            return tickType switch
            {
                TickType.Trade => $"AM.{symbol.Value},T.{symbol.Value}",
                TickType.Quote => $"Q.{symbol.Value}",
                _ => $"AM.{symbol.Value}"
            };
        }

        private async Task ConnectAsync()
        {
            if (_isConnected) return;
            
            try
            {
                _stocksWebSocket = new ClientWebSocket();
                await _stocksWebSocket.ConnectAsync(new Uri(StocksEndpoint), _cts.Token);
                _isConnected = true;
                
                Log.Trace($"PolygonDataQueueHandler: Connected to {StocksEndpoint}");
                
                // Start receive loop
                _stocksReceiveTask = Task.Run(async () => await ReceiveLoopAsync());
                
                // Authenticate
                await AuthenticateAsync();
            }
            catch (Exception ex)
            {
                Log.Error($"PolygonDataQueueHandler.ConnectAsync(): Failed to connect - {ex.Message}");
                _isConnected = false;
            }
        }

        private async Task AuthenticateAsync()
        {
            var authMessage = JsonSerializer.Serialize(new { action = "auth", @params = _apiKey });
            await SendMessageAsync(authMessage);
            Log.Trace("PolygonDataQueueHandler: Authentication message sent");
        }

        private void SendSubscribe(string @params)
        {
            var message = JsonSerializer.Serialize(new { action = "subscribe", @params });
            _ = SendMessageAsync(message);
            Log.Trace($"PolygonDataQueueHandler: Subscribed to {@params}");
        }

        private void SendUnsubscribe(string @params)
        {
            var message = JsonSerializer.Serialize(new { action = "unsubscribe", @params });
            _ = SendMessageAsync(message);
            Log.Trace($"PolygonDataQueueHandler: Unsubscribed from {@params}");
        }

        private async Task SendMessageAsync(string message)
        {
            if (_stocksWebSocket?.State != WebSocketState.Open) return;
            
            var bytes = Encoding.UTF8.GetBytes(message);
            await _stocksWebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[8192];
            var messageBuilder = new StringBuilder();
            
            while (!_cts.IsCancellationRequested && _stocksWebSocket?.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _stocksWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Log.Trace("PolygonDataQueueHandler: WebSocket closed by server");
                        _isConnected = false;
                        _isAuthenticated = false;
                        break;
                    }
                    
                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    
                    if (result.EndOfMessage)
                    {
                        var message = messageBuilder.ToString();
                        messageBuilder.Clear();
                        ProcessMessage(message);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error($"PolygonDataQueueHandler.ReceiveLoopAsync(): Error - {ex.Message}");
                }
            }
        }

        private void ProcessMessage(string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                
                // Polygon sends arrays of events
                if (root.ValueKind != JsonValueKind.Array) return;
                
                foreach (var element in root.EnumerateArray())
                {
                    if (!element.TryGetProperty("ev", out var evProp)) continue;
                    var eventType = evProp.GetString();
                    
                    switch (eventType)
                    {
                        case "status":
                            HandleStatusMessage(element);
                            break;
                        case "AM":
                            HandleAggregateMessage(element);
                            break;
                        case "T":
                            HandleTradeMessage(element);
                            break;
                        case "Q":
                            HandleQuoteMessage(element);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"PolygonDataQueueHandler.ProcessMessage(): Failed to parse - {ex.Message}");
            }
        }

        private void HandleStatusMessage(JsonElement element)
        {
            if (!element.TryGetProperty("status", out var statusProp)) return;
            var status = statusProp.GetString();
            
            if (status == "auth_success")
            {
                _isAuthenticated = true;
                Log.Trace("PolygonDataQueueHandler: Authentication successful");
                
                // Subscribe to all pending symbols
                var symbols = _subscriptionManager.GetSubscribedSymbols();
                foreach (var symbol in symbols)
                {
                    var @params = GetSubscriptionParams(symbol, TickType.Trade);
                    SendSubscribe(@params);
                }
            }
            else if (status == "auth_failed")
            {
                Log.Error("PolygonDataQueueHandler: Authentication failed - check API key");
                _isAuthenticated = false;
            }
            else if (status == "connected")
            {
                Log.Trace("PolygonDataQueueHandler: WebSocket connected");
            }
        }

        private void HandleAggregateMessage(JsonElement element)
        {
            // AM event format: {"ev":"AM","sym":"AAPL","v":1000,"av":50000,"op":180.5,"vw":181.2,"o":181.0,"c":181.5,"h":182.0,"l":180.8,"a":181.3,"z":100,"s":1704067200000,"e":1704067260000}
            if (!element.TryGetProperty("sym", out var symProp)) return;
            var ticker = symProp.GetString();
            
            var symbol = Symbol.Create(ticker, SecurityType.Equity, Market.USA);
            if (!_subscriptionManager.IsSubscribed(symbol, TickType.Trade)) return;
            
            var offsetProvider = GetTimeZoneOffsetProvider(symbol);
            var utcNow = TimeProvider.GetUtcNow();
            var exchangeTime = offsetProvider.ConvertFromUtc(utcNow);
            
            var open = element.TryGetProperty("o", out var oProp) ? oProp.GetDecimal() : 0m;
            var high = element.TryGetProperty("h", out var hProp) ? hProp.GetDecimal() : 0m;
            var low = element.TryGetProperty("l", out var lProp) ? lProp.GetDecimal() : 0m;
            var close = element.TryGetProperty("c", out var cProp) ? cProp.GetDecimal() : 0m;
            var volume = element.TryGetProperty("v", out var vProp) ? vProp.GetInt64() : 0L;
            
            var bar = new TradeBar
            {
                Symbol = symbol,
                Time = exchangeTime,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                Period = TimeSpan.FromMinutes(1)
            };
            
            _aggregator.Update(bar);
        }

        private void HandleTradeMessage(JsonElement element)
        {
            // T event format: {"ev":"T","sym":"AAPL","i":"1234","x":1,"p":181.5,"s":100,"c":[37],"t":1704067200000}
            if (!element.TryGetProperty("sym", out var symProp)) return;
            var ticker = symProp.GetString();
            
            var symbol = Symbol.Create(ticker, SecurityType.Equity, Market.USA);
            if (!_subscriptionManager.IsSubscribed(symbol, TickType.Trade)) return;
            
            var offsetProvider = GetTimeZoneOffsetProvider(symbol);
            var utcNow = TimeProvider.GetUtcNow();
            var exchangeTime = offsetProvider.ConvertFromUtc(utcNow);
            
            var price = element.TryGetProperty("p", out var pProp) ? pProp.GetDecimal() : 0m;
            var size = element.TryGetProperty("s", out var sProp) ? sProp.GetInt32() : 0;
            
            var tick = new Tick
            {
                Symbol = symbol,
                Time = exchangeTime,
                TickType = TickType.Trade,
                Value = price,
                Quantity = size
            };
            
            _aggregator.Update(tick);
        }

        private void HandleQuoteMessage(JsonElement element)
        {
            // Q event format: {"ev":"Q","sym":"AAPL","bx":1,"bp":181.4,"bs":100,"ax":2,"ap":181.6,"as":200,"t":1704067200000}
            if (!element.TryGetProperty("sym", out var symProp)) return;
            var ticker = symProp.GetString();
            
            var symbol = Symbol.Create(ticker, SecurityType.Equity, Market.USA);
            if (!_subscriptionManager.IsSubscribed(symbol, TickType.Quote)) return;
            
            var offsetProvider = GetTimeZoneOffsetProvider(symbol);
            var utcNow = TimeProvider.GetUtcNow();
            var exchangeTime = offsetProvider.ConvertFromUtc(utcNow);
            
            var bidPrice = element.TryGetProperty("bp", out var bpProp) ? bpProp.GetDecimal() : 0m;
            var bidSize = element.TryGetProperty("bs", out var bsProp) ? bsProp.GetDecimal() : 0m;
            var askPrice = element.TryGetProperty("ap", out var apProp) ? apProp.GetDecimal() : 0m;
            var askSize = element.TryGetProperty("as", out var asProp) ? asProp.GetDecimal() : 0m;
            
            var tick = new Tick(exchangeTime, symbol, "", "", bidSize: bidSize, bidPrice: bidPrice, askPrice: askPrice, askSize: askSize);
            
            _aggregator.Update(tick);
        }

        private TimeZoneOffsetProvider GetTimeZoneOffsetProvider(Symbol symbol)
        {
            if (!_symbolExchangeTimeZones.TryGetValue(symbol, out var offsetProvider))
            {
                var exchangeTimeZone = _marketHoursDatabase.GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType).TimeZone;
                _symbolExchangeTimeZones[symbol] = offsetProvider = new TimeZoneOffsetProvider(exchangeTimeZone, TimeProvider.GetUtcNow(), Time.EndOfTime);
            }
            return offsetProvider;
        }
    }
}
