// CLse004A.cs - Trade command settings

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;

namespace Alaris.Host.Application.Cli.Settings;

/// <summary>
/// Settings for trade status command.
/// Component ID: CLse004A
/// </summary>
public sealed class TradeStatusSettings : CLif004A
{
    [CommandOption("--positions")]
    [Description("Show only positions")]
    [DefaultValue(false)]
    public bool PositionsOnly { get; init; }
}

/// <summary>
/// Settings for trade signals command.
/// </summary>
public sealed class TradeSignalsSettings : CLif004A
{
    [CommandOption("--pending")]
    [Description("Show only pending signals")]
    [DefaultValue(false)]
    public bool PendingOnly { get; init; }

    [CommandOption("-s|--symbol <SYMBOL>")]
    [Description("Filter by symbol")]
    public string? Symbol { get; init; }
}
