// CLif004A.cs - Global settings base class for all CLI commands

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Alaris.Host.Application.Cli.Infrastructure;

/// <summary>
/// Global settings inherited by all CLI commands.
/// Component ID: CLif004A
/// </summary>
public class CLif004A : CommandSettings
{
    [CommandOption("-v|--verbose")]
    [Description("Enable verbose output")]
    [DefaultValue(false)]
    public bool Verbose { get; init; }

    [CommandOption("--json")]
    [Description("Output in JSON format (for scripting)")]
    [DefaultValue(false)]
    public bool JsonOutput { get; init; }

    [CommandOption("--no-color")]
    [Description("Disable colored output")]
    [DefaultValue(false)]
    public bool NoColor { get; init; }
}
