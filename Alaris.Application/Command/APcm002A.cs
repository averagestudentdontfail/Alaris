// =============================================================================
// APcm002A.cs - Config Command
// Component: APcm002A | Category: Commands | Variant: A (Primary)
// =============================================================================
// Placeholder for Config command.
// =============================================================================

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Alaris.Application.Command;

public sealed class ConfigSettings : CommandSettings
{
    [CommandArgument(0, "<ACTION>")]
    [Description("Action: show, set")]
    public string Action { get; init; } = "show";
}

public sealed class APcm002A : Command<ConfigSettings>
{
    public override int Execute(CommandContext context, ConfigSettings settings)
    {
        AnsiConsole.MarkupLine("[yellow]Config command placeholder restored.[/]");
        return 0;
    }
}
