// src/csharp/IPC/SharedMemoryBridge.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Alaris.IPC
{
    public class SharedMemoryBridge : IDisposable
    {
        private readonly SharedRingBuffer<MarketDataMessage> _marketDataBuffer;
        private readonly SharedRingBuffer<TradingSignalMessage> _signalBuffer;
        private readonly SharedRingBuffer<ControlMessage> _controlBuffer;

        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _marketDataProcessingTask;
        private readonly Task _controlProcessingTask;

        private bool _disposed = false;
        private uint _sequenceNumber = 0;

        public event Action<MarketDataMessage>? MarketDataReceived;
        public event Action<ControlMessage>? ControlMessageReceived;

        public SharedMemoryBridge()
        {
            try
            {
                // Connect to existing shared memory buffers created by QuantLib process
                // Use exact same names and sizes as QuantLib process
                _marketDataBuffer = new SharedRingBuffer<MarketDataMessage>("/alaris_market_data", 4096, false);
                _signalBuffer = new SharedRingBuffer<TradingSignalMessage>("/alaris_signals", 1024, false);
                _controlBuffer = new SharedRingBuffer<ControlMessage>("/alaris_control", 256, false);

                _cancellationTokenSource = new CancellationTokenSource();

                // Start background tasks for processing incoming data from QuantLib
                _marketDataProcessingTask = Task.Run(() => ProcessMarketData(_cancellationTokenSource.Token));
                _controlProcessingTask = Task.Run(() => ProcessControlMessages(_cancellationTokenSource.Token));

                Console.WriteLine("SharedMemoryBridge connected to QuantLib process successfully");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to connect to QuantLib process shared memory. Ensure QuantLib process is running first.", ex);
            }
        }

        // Consume market data published by QuantLib process
        private async Task ProcessMarketData(CancellationToken cancellationToken)
        {
            const int batchSize = 20; // Process in small batches for responsiveness
            var messageCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    messageCount = 0;

                    // Read market data messages from QuantLib
                    for (int i = 0; i < batchSize; i++)
                    {
                        if (_marketDataBuffer.TryRead(out MarketDataMessage marketData))
                        {
                            MarketDataReceived?.Invoke(marketData);
                            messageCount++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Small delay if no messages processed
                    if (messageCount == 0)
                    {
                        await Task.Delay(1, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing market data: {ex.Message}");
                    await Task.Delay(10, cancellationToken);
                }
            }
        }

        // Consume control messages from QuantLib process
        private async Task ProcessControlMessages(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_controlBuffer.TryRead(out ControlMessage message))
                    {
                        ControlMessageReceived?.Invoke(message);
                    }
                    else
                    {
                        await Task.Delay(5, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing control messages: {ex.Message}");
                    await Task.Delay(50, cancellationToken);
                }
            }
        }

        // Publish trading signals to QuantLib process
        public bool PublishTradingSignal(TradingSignalMessage signal)
        {
            if (_disposed) return false;

            try
            {
                return _signalBuffer.TryWrite(signal);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error publishing trading signal: {ex.Message}");
                return false;
            }
        }

        // Send control messages to QuantLib process
        public bool SendControlMessage(ControlMessageType messageType, uint param1 = 0, uint param2 = 0, 
                                     double value1 = 0.0, double value2 = 0.0)
        {
            if (_disposed) return false;

            try
            {
                var message = new ControlMessage();
                message.message_type = (uint)messageType;
                message.sequence_number = Interlocked.Increment(ref _sequenceNumber);
                message.source_process_id = 2; // Lean process ID
                message.target_process_id = 1; // QuantLib process ID
                message.priority = (uint)TTAPriority.MEDIUM;
                message.value1 = value1;
                message.value2 = value2;

                return _controlBuffer.TryWrite(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending control message: {ex.Message}");
                return false;
            }
        }

        // Helper method to create trading signals with proper structure
        public TradingSignalMessage CreateTradingSignal(
            uint symbolId,
            double theoreticalPrice,
            double marketPrice,
            double impliedVol,
            double forecastVol,
            double confidence,
            int quantity,
            byte side,
            byte urgency = 128,
            byte signalType = 0)
        {
            var signal = new TradingSignalMessage
            {
                timestamp_ns = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000,
                symbol_id = symbolId,
                theoretical_price = theoreticalPrice,
                market_price = marketPrice,
                implied_volatility = impliedVol,
                forecast_volatility = forecastVol,
                confidence = confidence,
                quantity = quantity,
                side = side,
                urgency = urgency,
                signal_type = signalType
            };

            return signal;
        }

        public SharedMemoryStatus GetStatus()
        {
            if (_disposed) return new SharedMemoryStatus();

            try
            {
                return new SharedMemoryStatus
                {
                    MarketDataBufferSize = _marketDataBuffer.Size,
                    SignalBufferSize = _signalBuffer.Size,
                    ControlBufferSize = _controlBuffer.Size,
                    MarketDataUtilization = _marketDataBuffer.Utilization,
                    SignalUtilization = _signalBuffer.Utilization,
                    ControlUtilization = _controlBuffer.Utilization,
                    IsHealthy = !_marketDataBuffer.IsFull && !_signalBuffer.IsFull && !_controlBuffer.IsFull
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting shared memory status: {ex.Message}");
                return new SharedMemoryStatus { IsHealthy = false };
            }
        }

        // Send initial connection handshake to QuantLib
        public void SendConnectionHandshake()
        {
            SendControlMessage(ControlMessageType.SYSTEM_STATUS, 
                             param1: 1, // Connection established
                             value1: 1.0); // Lean process ready
        }

        // Send shutdown notification to QuantLib
        public void SendShutdownNotification()
        {
            SendControlMessage(ControlMessageType.SYSTEM_STATUS,
                             param1: 0, // Disconnecting
                             value1: 0.0); // Lean process shutting down
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    // Send shutdown notification to QuantLib
                    SendShutdownNotification();
                    
                    // Cancel background tasks
                    _cancellationTokenSource?.Cancel();
                    
                    // Wait for tasks to complete
                    Task.WaitAll(new[] { _marketDataProcessingTask, _controlProcessingTask }, 
                               TimeSpan.FromSeconds(2));
                }
                catch (AggregateException)
                {
                    // Tasks didn't complete in time, continue with disposal
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during SharedMemoryBridge disposal: {ex.Message}");
                }
                finally
                {
                    _marketDataBuffer?.Dispose();
                    _signalBuffer?.Dispose();
                    _controlBuffer?.Dispose();
                    _cancellationTokenSource?.Dispose();

                    _disposed = true;
                }
            }
        }
    }

    public struct SharedMemoryStatus
    {
        public int MarketDataBufferSize;
        public int SignalBufferSize;
        public int ControlBufferSize;
        public double MarketDataUtilization;
        public double SignalUtilization;
        public double ControlUtilization;
        public bool IsHealthy;
    }
}