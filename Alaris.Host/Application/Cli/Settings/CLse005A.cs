// CLse005A.cs - Strategy command settings

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;

namespace Alaris.Host.Application.Cli.Settings;

/// <summary>
/// Settings for strategy info command.
/// Component ID: CLse005A
/// </summary>
public sealed class StrategyInfoSettings : CLif004A
{
    [CommandOption("-n|--name <NAME>")]
    [Description("Strategy name")]
    [DefaultValue("earnings-vol")]
    public string Name { get; init; } = "earnings-vol";
}

/// <summary>
/// Settings for strategy evaluate command.
/// </summary>
public sealed class StrategyEvaluateSettings : CLif004A
{
    [CommandArgument(0, "<SYMBOL>")]
    [Description("Symbol to evaluate")]
    public string Symbol { get; init; } = string.Empty;

    [CommandOption("--detailed")]
    [Description("Show detailed pipeline output")]
    [DefaultValue(false)]
    public bool Detailed { get; init; }
}
