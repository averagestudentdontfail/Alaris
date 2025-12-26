// CLst001A.cs - Strategy list command

using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;

namespace Alaris.Host.Application.Cli.Commands.Strategy;

/// <summary>
/// Lists available trading strategies.
/// Component ID: CLst001A
/// </summary>
public sealed class CLst001A : Command<CLif004A>
{
    public override int Execute(CommandContext context, CLif004A settings)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title("[bold]Available Strategies[/]");

        table.AddColumn("[grey]Name[/]");
        table.AddColumn("[grey]Description[/]");
        table.AddColumn("[grey]Status[/]");

        table.AddRow(
            "[bold]earnings-vol[/]",
            "Earnings volatility calendar spread strategy",
            "[green]Active[/]");

        table.AddRow(
            "[grey]mean-reversion[/]",
            "Statistical mean reversion (planned)",
            "[grey]Planned[/]");

        table.AddRow(
            "[grey]momentum[/]",
            "Trend following momentum (planned)",
            "[grey]Planned[/]");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        CLif003A.Info("Use 'alaris strategy info --name earnings-vol' for details.");

        return 0;
    }
}
