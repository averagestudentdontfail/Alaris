// CLtr002A.cs - Trade status command

using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;
using Alaris.Host.Application.Cli.Settings;

namespace Alaris.Host.Application.Cli.Commands.Trade;

/// <summary>
/// Shows current trading state and positions.
/// Component ID: CLtr002A
/// </summary>
public sealed class CLtr002A : Command<TradeStatusSettings>
{
    public override int Execute(CommandContext context, TradeStatusSettings settings)
    {
        CLif003A.Info("Checking trading status...");
        AnsiConsole.WriteLine();

        // Check if trading process is running
        bool isRunning = false; // Would check via IPC or process detection
        
        if (!isRunning)
        {
            CLif003A.WriteKeyValueTable("Trading Status", new[]
            {
                ("Status", "[grey]Not Running[/]"),
                ("Mode", "[grey]--[/]"),
                ("Started", "[grey]--[/]"),
                ("Positions", "[grey]0[/]")
            });

            AnsiConsole.WriteLine();
            CLif003A.Info("Start trading with: alaris trade start --paper");
            return 0;
        }

        // Would show real-time positions from LEAN/IBKR
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title("[bold]Open Positions[/]");

        table.AddColumn("[grey]Symbol[/]");
        table.AddColumn("[grey]Type[/]");
        table.AddColumn("[grey]Qty[/]");
        table.AddColumn("[grey]Entry[/]");
        table.AddColumn("[grey]Current[/]");
        table.AddColumn("[grey]P&L[/]");

        // Placeholder - would query live positions
        AnsiConsole.Write(table);

        return 0;
    }
}
