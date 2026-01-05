// CLse002A.cs - Earnings command settings

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;

namespace Alaris.Host.Application.Cli.Settings;

/// <summary>
/// Settings for earnings bootstrap command.
/// Component ID: CLse002A
/// </summary>
public sealed class EarningsBootstrapSettings : CLif004A
{
    [CommandOption("-s|--start <DATE>")]
    [Description("Start date (yyyy-MM-dd)")]
    public string? StartDate { get; init; }

    [CommandOption("-e|--end <DATE>")]
    [Description("End date (yyyy-MM-dd)")]
    public string? EndDate { get; init; }

    [CommandOption("-o|--output <PATH>")]
    [Description("Output directory for cached data")]
    public string? OutputPath { get; init; }

    [CommandOption("--force")]
    [Description("Re-download existing cached dates")]
    [DefaultValue(false)]
    public bool Force { get; init; }

    public override ValidationResult Validate()
    {
        if (!string.IsNullOrEmpty(StartDate) && !DateTime.TryParse(StartDate, out _))
            return ValidationResult.Error("Invalid start date format. Use yyyy-MM-dd.");

        if (!string.IsNullOrEmpty(EndDate) && !DateTime.TryParse(EndDate, out _))
            return ValidationResult.Error("Invalid end date format. Use yyyy-MM-dd.");

        return ValidationResult.Success();
    }
}

/// <summary>
/// Settings for earnings upcoming command.
/// </summary>
public sealed class EarningsUpcomingSettings : CLif004A
{
    [CommandOption("-d|--days <DAYS>")]
    [Description("Number of days to look ahead")]
    [DefaultValue(7)]
    public int Days { get; init; }

    [CommandOption("-s|--symbol <SYMBOL>")]
    [Description("Filter by symbol")]
    public string? Symbol { get; init; }
}

/// <summary>
/// Settings for earnings check command.
/// </summary>
public sealed class EarningsCheckSettings : CLif004A
{
    [CommandArgument(0, "<SESSION_ID>")]
    [Description("Session ID to check coverage for")]
    public string SessionId { get; init; } = string.Empty;
}
