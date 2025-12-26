// CLse001A.cs - Data command settings

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;

namespace Alaris.Host.Application.Cli.Settings;

/// <summary>
/// Settings for data bootstrap command.
/// Component ID: CLse001A
/// </summary>
public sealed class DataBootstrapSettings : CLif004A
{
    [CommandArgument(0, "<SESSION_ID>")]
    [Description("Session ID to download data for")]
    public string SessionId { get; init; } = string.Empty;

    [CommandOption("--symbols <SYMBOLS>")]
    [Description("Comma-separated list of symbols (overrides session symbols)")]
    public string? Symbols { get; init; }

    [CommandOption("--skip-options")]
    [Description("Skip options chain download")]
    [DefaultValue(false)]
    public bool SkipOptions { get; init; }

    [CommandOption("--skip-rates")]
    [Description("Skip interest rate download")]
    [DefaultValue(false)]
    public bool SkipRates { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(SessionId))
            return ValidationResult.Error("Session ID is required.");

        return ValidationResult.Success();
    }
}

/// <summary>
/// Settings for data status command.
/// </summary>
public sealed class DataStatusSettings : CLif004A
{
    [CommandOption("-s|--session <SESSION>")]
    [Description("Session ID to check (default: all sessions)")]
    public string? SessionId { get; init; }
}

/// <summary>
/// Settings for data validate command.
/// </summary>
public sealed class DataValidateSettings : CLif004A
{
    [CommandArgument(0, "<SESSION_ID>")]
    [Description("Session ID to validate")]
    public string SessionId { get; init; } = string.Empty;

    [CommandOption("--fix")]
    [Description("Attempt to fix minor issues")]
    [DefaultValue(false)]
    public bool Fix { get; init; }
}
