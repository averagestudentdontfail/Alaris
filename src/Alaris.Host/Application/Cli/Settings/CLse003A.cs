// CLse003A.cs - Backtest command settings

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;

namespace Alaris.Host.Application.Cli.Settings;

/// <summary>
/// Settings for backtest run command.
/// Component ID: CLse003A
/// </summary>
public sealed class BacktestRunSettings : CLif004A
{
    [CommandArgument(0, "<SESSION_ID>")]
    [Description("Session ID to run backtest for")]
    public string SessionId { get; init; } = string.Empty;

    [CommandOption("--monitor")]
    [Description("Show live monitoring dashboard")]
    [DefaultValue(true)]
    public bool Monitor { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(SessionId))
            return ValidationResult.Error("Session ID is required.");

        return ValidationResult.Success();
    }
}

/// <summary>
/// Settings for backtest analyze command.
/// </summary>
public sealed class BacktestAnalyzeSettings : CLif004A
{
    [CommandArgument(0, "<SESSION_ID>")]
    [Description("Session ID to analyze")]
    public string SessionId { get; init; } = string.Empty;

    [CommandOption("--format <FORMAT>")]
    [Description("Output format: table, json, csv")]
    [DefaultValue("table")]
    public string Format { get; init; } = "table";
}

/// <summary>
/// Settings for backtest list command.
/// </summary>
public sealed class BacktestListSettings : CLif004A
{
    [CommandOption("-n|--limit <COUNT>")]
    [Description("Maximum sessions to show")]
    [DefaultValue(20)]
    public int Limit { get; init; }

    [CommandOption("--all")]
    [Description("Show all sessions including incomplete")]
    [DefaultValue(false)]
    public bool ShowAll { get; init; }
}
