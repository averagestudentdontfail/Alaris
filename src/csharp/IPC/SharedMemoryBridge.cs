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
        private readonly Task _signalProcessingTask;
        private readonly Task _controlProcessingTask;

        private bool _disposed = false;

        public event Action<TradingSignalMessage>? SignalReceived;
        public event Action<ControlMessage>? ControlMessageReceived;

        public SharedMemoryBridge()
        {
            try
            {
                // Open existing shared memory buffers created by QuantLib process
                _marketDataBuffer = new SharedRingBuffer<MarketDataMessage>("/alaris_market_data", 4096, false);
                _signalBuffer = new SharedRingBuffer<TradingSignalMessage>("/alaris_signals", 1024, false);
                _controlBuffer = new SharedRingBuffer<ControlMessage>("/alaris_control", 256, false);

                _cancellationTokenSource = new CancellationTokenSource();

                // Start background tasks for processing signals and control messages
                _signalProcessingTask = Task.Run(() => ProcessSignals(_cancellationTokenSource.Token));
                _controlProcessingTask = Task.Run(() => ProcessControlMessages(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize shared memory bridge. Ensure QuantLib process is running.", ex);
            }
        }

        public bool PublishMarketData(MarketDataMessage marketData)
        {
            if (_disposed) return false;
            return _marketDataBuffer.TryWrite(marketData);
        }

        public bool PublishMarketData(uint symbolId, double bid, double ask, double underlying, 
                                    double bidIv = 0.0, double askIv = 0.0)
        {
            var message = new MarketDataMessage(symbolId, bid, ask, underlying)
            {
                BidIv = bidIv,
                AskIv = askIv
            };
            return PublishMarketData(message);
        }

        public bool SendControlMessage(ControlMessageType messageType, uint param1 = 0, uint param2 = 0, 
                                     double value1 = 0.0, double value2 = 0.0)
        {
            if (_disposed) return false;

            var message = new ControlMessage((uint)messageType)
            {
                Parameter1 = param1,
                Parameter2 = param2,
                Value1 = value1,
                Value2 = value2
            };

            return _controlBuffer.TryWrite(message);
        }

        private async Task ProcessSignals(CancellationToken cancellationToken)
        {
            const int batchSize = 50;
            var signalBatch = new List<TradingSignalMessage>(batchSize);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    signalBatch.Clear();

                    // Read signals in batches for efficiency
                    for (int i = 0; i < batchSize; i++)
                    {
                        if (_signalBuffer.TryRead(out TradingSignalMessage signal))
                        {
                            signalBatch.Add(signal);
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Process batch
                    foreach (var signal in signalBatch)
                    {
                        SignalReceived?.Invoke(signal);
                    }

                    // Small delay if no signals were processed
                    if (signalBatch.Count == 0)
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
                    Console.WriteLine($"Error processing signals: {ex.Message}");
                    await Task.Delay(100, cancellationToken);
                }
            }
        }

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
                        await Task.Delay(10, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing control messages: {ex.Message}");
                    await Task.Delay(100, cancellationToken);
                }
            }
        }

        public SharedMemoryStatus GetStatus()
        {
            if (_disposed) return new SharedMemoryStatus();

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

        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource?.Cancel();
                
                try
                {
                    Task.WaitAll(new[] { _signalProcessingTask, _controlProcessingTask }, TimeSpan.FromSeconds(5));
                }
                catch (AggregateException)
                {
                    // Tasks didn't complete in time, continue with disposal
                }

                _marketDataBuffer?.Dispose();
                _signalBuffer?.Dispose();
                _controlBuffer?.Dispose();
                _cancellationTokenSource?.Dispose();

                _disposed = true;
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