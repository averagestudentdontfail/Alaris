// CLst002A.cs - Strategy info command

using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;
using Alaris.Host.Application.Cli.Settings;

namespace Alaris.Host.Application.Cli.Commands.Strategy;

/// <summary>
/// Shows strategy parameters and thresholds.
/// Component ID: CLst002A
/// </summary>
public sealed class CLst002A : Command<StrategyInfoSettings>
{
    public override int Execute(CommandContext context, StrategyInfoSettings settings)
    {
        if (!string.Equals(settings.Name, "earnings-vol", StringComparison.OrdinalIgnoreCase))
        {
            CLif003A.Error($"Unknown strategy: {settings.Name}");
            return 1;
        }

        CLif003A.WritePanel("Earnings Volatility Strategy",
            "[bold]Type:[/] Calendar Spread (Volatility Arbitrage)\n" +
            "[bold]Asset:[/] Equity Options\n" +
            "[bold]Timeframe:[/] Pre-earnings to post-announcement\n" +
            "[bold]Academic Basis:[/] Earnings Announcement Return Puzzle (Ball & Kothari 1991)");

        AnsiConsole.WriteLine();

        var entryTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title("[bold]Entry Criteria[/]");

        entryTable.AddColumn("[grey]Parameter[/]");
        entryTable.AddColumn("[grey]Threshold[/]");
        entryTable.AddColumn("[grey]Description[/]");

        entryTable.AddRow("IV/RV Ratio", "[blue]> 1.25[/]", "Implied vol exceeds realized vol");
        entryTable.AddRow("Term Structure Slope", "[blue]> 0.02[/]", "Backwardation in vol curve");
        entryTable.AddRow("Days to Earnings", "[blue]3-21[/]", "Entry window");
        entryTable.AddRow("Min Liquidity", "[blue]> 500[/]", "Contracts per day");
        entryTable.AddRow("Bid-Ask Spread", "[blue]< 5%[/]", "Maximum spread cost");

        AnsiConsole.Write(entryTable);
        AnsiConsole.WriteLine();

        var exitTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title("[bold]Exit Rules[/]");

        exitTable.AddColumn("[grey]Condition[/]");
        exitTable.AddColumn("[grey]Action[/]");

        exitTable.AddRow("Earnings Announced", "Close within 1 day");
        exitTable.AddRow("Max Profit", "Take profit at 50% target");
        exitTable.AddRow("Max Loss", "Stop loss at 25% of premium");
        exitTable.AddRow("Days in Trade", "Close after 5 days post-earnings");
        exitTable.AddRow("VIX Spike", "Close if VIX > 30");

        AnsiConsole.Write(exitTable);

        return 0;
    }
}
