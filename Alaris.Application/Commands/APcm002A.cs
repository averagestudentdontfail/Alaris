// =============================================================================
// APcm002A.cs - Config Command
// Component: APcm002A | Category: Commands | Variant: A (Primary)
// =============================================================================
// Implements 'alaris config show' and 'alaris config set <key> <value>' commands.
// =============================================================================

using System.ComponentModel;
using System.IO;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Alaris.Application.Commands;

/// <summary>
/// Config Command settings.
/// </summary>
public sealed class ConfigSettings : CommandSettings
{
    [CommandArgument(0, "[action]")]
    [Description("Action: show or set")]
    [DefaultValue("show")]
    public string Action { get; init; } = "show";

    [CommandArgument(1, "[key]")]
    [Description("Configuration key (for set action)")]
    public string? Key { get; init; }

    [CommandArgument(2, "[value]")]
    [Description("Configuration value (for set action)")]
    public string? Value { get; init; }
}

/// <summary>
/// Alaris Config Command - view/modify configuration.
/// Component ID: APcm002A
/// </summary>
public sealed class APcm002A : Command<ConfigSettings>
{
    private const string ConfigFileName = "config.json";

    public override int Execute(CommandContext context, ConfigSettings settings)
    {
        var configPath = FindConfigPath();
        if (configPath is null)
        {
            AnsiConsole.MarkupLine("[red]Could not find config.json[/]");
            return 1;
        }

        return settings.Action.ToLowerInvariant() switch
        {
            "show" => ShowConfig(configPath),
            "set" when settings.Key is not null && settings.Value is not null =>
                SetConfig(configPath, settings.Key, settings.Value),
            "set" => InvalidSetUsage(),
            _ => InvalidAction(settings.Action)
        };
    }

    private static int ShowConfig(string configPath)
    {
        AnsiConsole.MarkupLine($"[grey]Configuration file:[/] {configPath}");
        AnsiConsole.WriteLine();

        var content = File.ReadAllText(configPath);

        // Parse JSON (skip comments)
        var jsonOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        try
        {
            using var doc = JsonDocument.Parse(content, jsonOptions);
            var root = doc.RootElement;

            // Show key configuration items
            var table = new Table();
            table.AddColumn("[blue]Setting[/]");
            table.AddColumn("[green]Value[/]");

            AddRow(table, root, "environment");
            AddRow(table, root, "algorithm-type-name");
            AddRow(table, root, "algorithm-location");
            AddRow(table, root, "data-folder");
            AddRow(table, root, "ib-account");
            AddRow(table, root, "ib-trading-mode");
            AddRow(table, root, "polygon-api-key", masked: true);

            AnsiConsole.Write(table);
        }
        catch (JsonException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error parsing config: {ex.Message}[/]");
            return 1;
        }

        return 0;
    }

    private static void AddRow(Table table, JsonElement root, string key, bool masked = false)
    {
        if (root.TryGetProperty(key, out var prop))
        {
            var value = prop.ToString();
            if (masked && !string.IsNullOrEmpty(value))
            {
                value = "****" + value[Math.Max(0, value.Length - 4)..];
            }

            table.AddRow(key, value ?? "[grey](empty)[/]");
        }
    }

    private static int SetConfig(string configPath, string key, string value)
    {
        AnsiConsole.MarkupLine($"[yellow]Setting {key} = {value}[/]");
        AnsiConsole.MarkupLine("[grey]Config modification not yet implemented.[/]");
        AnsiConsole.MarkupLine("[grey]Please edit config.json directly.[/]");
        return 0;
    }

    private static int InvalidSetUsage()
    {
        AnsiConsole.MarkupLine("[red]Usage: alaris config set <key> <value>[/]");
        return 1;
    }

    private static int InvalidAction(string action)
    {
        AnsiConsole.MarkupLine($"[red]Unknown action: {action}. Use 'show' or 'set'.[/]");
        return 1;
    }

    private static string? FindConfigPath()
    {
        var paths = new[]
        {
            ConfigFileName,
            $"../{ConfigFileName}",
            $"../../{ConfigFileName}",
            System.IO.Path.Combine(AppContext.BaseDirectory, ConfigFileName)
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                return System.IO.Path.GetFullPath(path);
            }
        }

        return null;
    }
}
