// CLdb001A.cs - Dashboard renderer for live algorithm monitoring

using Spectre.Console;
using Spectre.Console.Rendering;
using Alaris.Host.Application.Cli.Monitor;

namespace Alaris.Host.Application.Cli.Dashboard;

/// <summary>
/// Renders a live dashboard for algorithm monitoring.
/// Uses Spectre.Console Live display for smooth updates.
/// Component ID: CLdb001A
/// </summary>
public sealed class CLdb001A : IDisposable
{
    private readonly CLmn001A _monitor;
    private readonly List<EquityUpdate> _equityHistory = new();
    private readonly List<TradeUpdate> _trades = new();
    private StatusUpdate? _lastStatus;
    private readonly object _lock = new();

    public CLdb001A(CLmn001A monitor)
    {
        _monitor = monitor;
        _monitor.EquityChanged += OnEquityChanged;
        _monitor.TradeExecuted += OnTradeExecuted;
        _monitor.StatusChanged += OnStatusChanged;
    }

    private void OnEquityChanged(object? sender, EquityUpdate e)
    {
        lock (_lock)
        {
            _equityHistory.Add(e);
            // Keep last 100 points for chart
            if (_equityHistory.Count > 100)
                _equityHistory.RemoveAt(0);
        }
    }

    private void OnTradeExecuted(object? sender, TradeUpdate e)
    {
        lock (_lock)
        {
            _trades.Add(e);
            // Keep last 20 trades
            if (_trades.Count > 20)
                _trades.RemoveAt(0);
        }
    }

    private void OnStatusChanged(object? sender, StatusUpdate e)
    {
        lock (_lock)
        {
            _lastStatus = e;
        }
    }

    /// <summary>
    /// Runs the dashboard until cancelled or algorithm completes.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await AnsiConsole.Live(BuildDashboard())
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Top)
            .StartAsync(async ctx =>
            {
                while (!cancellationToken.IsCancellationRequested && _monitor.IsRunning)
                {
                    ctx.UpdateTarget(BuildDashboard());
                    await Task.Delay(250, cancellationToken);
                }
                
                // Final update
                ctx.UpdateTarget(BuildDashboard());
            });
    }

    private IRenderable BuildDashboard()
    {
        Layout layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3),
                new Layout("Content").SplitColumns(
                    new Layout("Left"),
                    new Layout("Right")));

        // Header: Status bar
        layout["Header"].Update(BuildStatusBar());

        // Left: Equity and metrics
        layout["Left"].Update(new Rows(
            BuildEquityPanel(),
            BuildMetricsPanel()));

        // Right: Trades
        layout["Right"].Update(BuildTradesPanel());

        return layout;
    }

    private Panel BuildStatusBar()
    {
        lock (_lock)
        {
            string mode = _monitor.Mode.ToString().ToUpperInvariant();
            string status = _lastStatus?.Status ?? "Initializing";
            double progress = _lastStatus?.ProgressPercent ?? 0;
            string remaining = _lastStatus?.EstimatedRemaining?.ToString(@"hh\:mm\:ss") ?? "--:--:--";

            Markup content = new Markup(
                $"[bold blue]ALARIS[/] │ Mode: [yellow]{mode}[/] │ " +
                $"Status: [green]{status}[/] │ " +
                $"Progress: [blue]{progress:F1}%[/] │ " +
                $"ETA: [grey]{remaining}[/]");

            return new Panel(content)
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Blue)
                .Padding(0, 0);
        }
    }

    private Panel BuildEquityPanel()
    {
        lock (_lock)
        {
            if (_equityHistory.Count == 0)
            {
                return new Panel(new Markup("[grey]Waiting for data...[/]"))
                    .Header("[bold]Equity Curve[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Grey);
            }

            // Build simple text-based chart using bar characters
            decimal maxEquity = decimal.MinValue;
            decimal minEquity = decimal.MaxValue;
            for (int i = 0; i < _equityHistory.Count; i++)
            {
                decimal equity = _equityHistory[i].Equity;
                if (equity > maxEquity)
                {
                    maxEquity = equity;
                }
                if (equity < minEquity)
                {
                    minEquity = equity;
                }
            }
            decimal range = maxEquity - minEquity;
            
            if (range == 0) range = 1;

            const int chartHeight = 8;
            string[] chartLines = new string[chartHeight];
            
            // Sample points for display
            int pointCount = _equityHistory.Count > 50 ? 50 : _equityHistory.Count;
            List<EquityUpdate> points = new List<EquityUpdate>(pointCount);
            for (int i = _equityHistory.Count - pointCount; i < _equityHistory.Count; i++)
            {
                points.Add(_equityHistory[i]);
            }
            
            for (int row = 0; row < chartHeight; row++)
            {
                decimal threshold = maxEquity - (range * row / (chartHeight - 1));
                System.Text.StringBuilder line = new System.Text.StringBuilder();
                
                foreach (EquityUpdate point in points)
                {
                    char c = point.Equity >= threshold ? '█' : ' ';
                    line.Append(c);
                }
                
                chartLines[row] = line.ToString();
            }

            decimal currentEquity = _equityHistory[_equityHistory.Count - 1].Equity;
            decimal startEquity = _equityHistory[0].Equity;
            decimal pnl = currentEquity - startEquity;
            string pnlColor = pnl >= 0 ? "green" : "red";

            Rows content = new Rows(
                new Text(string.Join("\n", chartLines)),
                new Markup($"\n[bold]Current:[/] ${currentEquity:N2} ([{pnlColor}]{pnl:+#,##0.00;-#,##0.00}[/])"));

            return new Panel(content)
                .Header("[bold]Equity Curve[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Blue);
        }
    }

    private Panel BuildMetricsPanel()
    {
        lock (_lock)
        {
            if (_equityHistory.Count < 2)
            {
                return new Panel(new Markup("[grey]Calculating...[/]"))
                    .Header("[bold]Performance[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Grey);
            }

            decimal startEquity = _equityHistory[0].Equity;
            decimal currentEquity = _equityHistory[_equityHistory.Count - 1].Equity;
            decimal returnPct = startEquity > 0 ? (currentEquity - startEquity) / startEquity * 100 : 0;

            // Calculate drawdown
            decimal peak = startEquity;
            decimal maxDrawdown = 0;
            foreach (EquityUpdate e in _equityHistory)
            {
                if (e.Equity > peak) peak = e.Equity;
                decimal dd = (peak - e.Equity) / peak * 100;
                if (dd > maxDrawdown) maxDrawdown = dd;
            }

            Table table = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .AddColumn("Metric")
                .AddColumn("Value");

            string returnColor = returnPct >= 0 ? "green" : "red";
            table.AddRow("Return", $"[{returnColor}]{returnPct:+0.00;-0.00}%[/]");
            table.AddRow("Max DD", $"[red]{maxDrawdown:F2}%[/]");
            table.AddRow("Trades", $"{_trades.Count}");
            
            int winners = 0;
            foreach (TradeUpdate trade in _trades)
            {
                if (trade.PnL > 0)
                {
                    winners++;
                }
            }
            double winRate = _trades.Count > 0 ? (double)winners / _trades.Count * 100 : 0;
            table.AddRow("Win Rate", $"{winRate:F0}%");

            return new Panel(table)
                .Header("[bold]Performance[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green);
        }
    }

    private Panel BuildTradesPanel()
    {
        lock (_lock)
        {
            Table table = new Table()
                .Border(TableBorder.Simple)
                .BorderColor(Color.Grey)
                .AddColumn("[grey]Time[/]")
                .AddColumn("[grey]Symbol[/]")
                .AddColumn("[grey]Dir[/]")
                .AddColumn("[grey]Qty[/]")
                .AddColumn("[grey]Price[/]");

            int tradeCount = _trades.Count > 10 ? 10 : _trades.Count;
            for (int i = _trades.Count - tradeCount; i < _trades.Count; i++)
            {
                TradeUpdate trade = _trades[i];
                string dirColor = trade.Direction == "BUY" ? "green" : "red";
                table.AddRow(
                    $"{trade.Timestamp:HH:mm:ss}",
                    trade.Symbol,
                    $"[{dirColor}]{trade.Direction}[/]",
                    $"{trade.Quantity}",
                    $"${trade.FillPrice:F2}");
            }

            if (_trades.Count == 0)
            {
                table.AddRow("[grey]--[/]", "[grey]Waiting...[/]", "", "", "");
            }

            return new Panel(table)
                .Header("[bold]Recent Trades[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Yellow);
        }
    }

    public void Dispose()
    {
        _monitor.EquityChanged -= OnEquityChanged;
        _monitor.TradeExecuted -= OnTradeExecuted;
        _monitor.StatusChanged -= OnStatusChanged;
    }
}
