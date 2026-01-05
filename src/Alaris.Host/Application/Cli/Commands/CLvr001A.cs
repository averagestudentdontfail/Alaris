// CLvr001A.cs - Version command showing system information

using System.Reflection;
using System.Runtime.InteropServices;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;

namespace Alaris.Host.Application.Cli.Commands;

/// <summary>
/// Displays version and system information.
/// Component ID: CLvr001A
/// </summary>
public sealed class CLvr001A : Command<CLif004A>
{
    public override int Execute(CommandContext context, CLif004A settings)
    {
        CLif003A.WriteBanner();

        Assembly assembly = Assembly.GetExecutingAssembly();
        string version = assembly.GetName().Version?.ToString() ?? "2.0.0";

        CLif003A.WriteKeyValueTable("System Information", new[]
        {
            ("Alaris Version", $"[blue]{version}[/]"),
            ("Runtime", RuntimeInformation.FrameworkDescription),
            ("OS", RuntimeInformation.OSDescription),
            ("Architecture", RuntimeInformation.OSArchitecture.ToString()),
            ("Working Directory", Environment.CurrentDirectory),
            ("Config Path", System.IO.Path.Combine(Environment.CurrentDirectory, "appsettings.jsonc"))
        });

        return 0;
    }
}
