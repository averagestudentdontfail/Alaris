// CLmn002A.cs - Backtest monitor implementation for LEAN

using System.Diagnostics;
using System.IO;

namespace Alaris.Host.Application.Cli.Monitor;

/// <summary>
/// Monitors a LEAN backtest in progress and publishes events.
/// Component ID: CLmn002A
/// </summary>
public sealed class CLmn002A : CLmn001A, IDisposable
{
    private readonly string _sessionId;
    private readonly string _resultsPath;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private decimal _lastEquity;

    public bool IsRunning { get; private set; }
    public AlgorithmMode Mode => AlgorithmMode.Backtest;

    public event EventHandler<EquityUpdate>? EquityChanged;
    public event EventHandler<TradeUpdate>? TradeExecuted;
    public event EventHandler<StatusUpdate>? StatusChanged;

    public CLmn002A(string sessionId, string resultsPath)
    {
        _sessionId = sessionId;
        _resultsPath = resultsPath;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitorTask = MonitorLoopAsync(_cts.Token);
        IsRunning = true;

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
            return;

        _cts?.Cancel();

        if (_monitorTask != null)
        {
            try { await _monitorTask; }
            catch (OperationCanceledException) { }
        }

        IsRunning = false;
    }

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollResultsAsync(ct);
                await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Log and continue
            }
        }
    }

    private async Task PollResultsAsync(CancellationToken ct)
    {
        // Look for LEAN's live log output or intermediate results
        string logPath = System.IO.Path.Combine(_resultsPath, "log.txt");
        
        if (File.Exists(logPath))
        {
            string[] lines = await File.ReadAllLinesAsync(logPath, ct);
            
            // Parse equity updates from log
            int start = lines.Length > 20 ? lines.Length - 20 : 0;
            for (int i = start; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Contains("Total Performance"))
                {
                    // Parse performance line
                    ParsePerformanceLine(line);
                }
                else if (line.Contains("Order Filled"))
                {
                    // Parse trade execution
                    ParseTradeLine(line);
                }
            }
        }

        // Check for completion
        string[] resultFiles = Directory.Exists(_resultsPath) 
            ? Directory.GetFiles(_resultsPath, "*-result.json")
            : Array.Empty<string>();

        if (resultFiles.Length > 0)
        {
            StatusChanged?.Invoke(this, new StatusUpdate(
                DateTime.UtcNow,
                "Complete",
                100.0,
                null));
        }
    }

    private void ParsePerformanceLine(string line)
    {
        // Extract equity from LEAN log format
        // Format: "Total Performance: $X.XX ($Y.YY%)"
        try
        {
            int dollarIndex = line.IndexOf('$');
            if (dollarIndex >= 0)
            {
                string equityStr = line.Substring(dollarIndex + 1).Split(' ')[0].Replace(",", "");
                if (decimal.TryParse(equityStr, out decimal equity) && equity != _lastEquity)
                {
                    _lastEquity = equity;
                    EquityChanged?.Invoke(this, new EquityUpdate(
                        DateTime.UtcNow,
                        equity,
                        0, // Cash not available from log
                        0, // Holdings not available
                        0, // Unrealized PnL
                        0  // Realized PnL
                    ));
                }
            }
        }
        catch
        {
            // Ignore parse failures
        }
    }

    private void ParseTradeLine(string line)
    {
        // Extract trade from LEAN log format
        // Format: "Order Filled: SYMBOL - BUY 100 @ $X.XX"
        try
        {
            string[] parts = line.Split(' ');
            string symbol = parts[2];
            string direction = parts[4];
            
            if (decimal.TryParse(parts[5], out decimal qty) &&
                decimal.TryParse(parts[7].TrimStart('$'), out decimal price))
            {
                TradeExecuted?.Invoke(this, new TradeUpdate(
                    DateTime.UtcNow,
                    symbol,
                    direction,
                    qty,
                    price,
                    0, // Commission from separate line
                    0  // PnL calculated separately
                ));
            }
        }
        catch
        {
            // Ignore parse failures
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _cts?.Dispose();
    }
}
