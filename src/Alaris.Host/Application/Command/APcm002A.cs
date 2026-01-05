// APcm002A.cs - Configuration command (show/set/validate appsettings)

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Alaris.Host.Application.Command;

/// <summary>
/// Settings for the config command.
/// </summary>
public sealed class ConfigSettings : CommandSettings
{
    /// <summary>
    /// Action to perform: show, set, or validate.
    /// </summary>
    [CommandArgument(0, "<ACTION>")]
    [Description("Action: show, set, validate")]
    public string Action { get; init; } = "show";

    /// <summary>
    /// Configuration key (for set action, e.g. "Polygon.ApiKey").
    /// </summary>
    [CommandArgument(1, "[KEY]")]
    [Description("Configuration key path (e.g. Polygon.ApiKey)")]
    public string? Key { get; init; }

    /// <summary>
    /// Configuration value (for set action).
    /// </summary>
    [CommandArgument(2, "[VALUE]")]
    [Description("Value to set")]
    public string? Value { get; init; }

    /// <summary>
    /// Show only secrets/API keys (for show action).
    /// </summary>
    [CommandOption("--secrets")]
    [Description("Show only API keys and secrets")]
    public bool SecretsOnly { get; init; }
}

/// <summary>
/// APcm002A: Configuration management command for Alaris.
/// </summary>
public sealed class APcm002A : Command<ConfigSettings>
{
    private static readonly string[] ConfigPaths =
    [
        "appsettings.local.jsonc",
        "../appsettings.local.jsonc",
        "../../appsettings.local.jsonc",
        "../../../appsettings.local.jsonc"
    ];

    private static readonly string[] BasePaths =
    [
        "appsettings.jsonc",
        "../appsettings.jsonc",
        "../../appsettings.jsonc",
        "../../../appsettings.jsonc"
    ];

    /// <summary>
    /// Executes the config command.
    /// </summary>
    public override int Execute(CommandContext context, ConfigSettings settings)
    {
        return settings.Action.ToUpperInvariant() switch
        {
            "SHOW" => ShowConfiguration(settings.SecretsOnly),
            "SET" => SetConfiguration(settings.Key, settings.Value),
            "VALIDATE" => ValidateConfiguration(),
            _ => ShowHelp()
        };
    }

    private static int ShowConfiguration(bool secretsOnly)
    {
        AnsiConsole.MarkupLine("[bold cyan]Alaris Configuration[/]");
        AnsiConsole.WriteLine();

        // Find and load base config
        string? basePath = FindConfigFile(BasePaths);
        string? localPath = FindConfigFile(ConfigPaths);

        if (basePath == null && localPath == null)
        {
            AnsiConsole.MarkupLine("[red]No configuration files found.[/]");
            AnsiConsole.MarkupLine("[grey]Create appsettings.jsonc in project root.[/]");
            return 1;
        }

        // Show file locations
        Table table = new Table()
            .Title("[bold]Configuration Files[/]")
            .AddColumn("File")
            .AddColumn("Status");

        table.AddRow(
            "appsettings.jsonc (base)",
            basePath != null ? $"[green]✓ {basePath}[/]" : "[red]✗ Not found[/]");
        table.AddRow(
            "appsettings.local.jsonc (secrets)",
            localPath != null ? $"[green]✓ {localPath}[/]" : "[yellow]○ Not found[/]");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Parse and display config
        JsonNode? baseConfig = basePath != null ? LoadJsonc(basePath) : null;
        JsonNode? localConfig = localPath != null ? LoadJsonc(localPath) : null;

        // Show key configuration values
        Table configTable = new Table()
            .Title("[bold]Settings[/]")
            .AddColumn("Key")
            .AddColumn("Value")
            .AddColumn("Source");

        if (secretsOnly)
        {
            // Show only secrets
            AddConfigRow(configTable, "Polygon.ApiKey", localConfig, baseConfig, true);
            AddConfigRow(configTable, "IBKR.AccountId", localConfig, baseConfig, true);
            AddConfigRow(configTable, "IBKR.ClientId", localConfig, baseConfig, false);
        }
        else
        {
            // Show all main settings
            AddConfigRow(configTable, "Polygon.ApiKey", localConfig, baseConfig, true);
            AddConfigRow(configTable, "IBKR.Host", localConfig, baseConfig, false);
            AddConfigRow(configTable, "IBKR.Port", localConfig, baseConfig, false);
            AddConfigRow(configTable, "IBKR.ClientId", localConfig, baseConfig, false);
            AddConfigRow(configTable, "IBKR.AccountId", localConfig, baseConfig, true);
            AddConfigRow(configTable, "Strategy.MinIVRVRatio", localConfig, baseConfig, false);
            AddConfigRow(configTable, "Logging.LogLevel.Default", localConfig, baseConfig, false);
        }

        AnsiConsole.Write(configTable);

        return 0;
    }

    private static int SetConfiguration(string? key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Key is required for set action.");
            AnsiConsole.MarkupLine("Usage: alaris config set <KEY> <VALUE>");
            return 1;
        }

        if (value == null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Value is required for set action.");
            AnsiConsole.MarkupLine("Usage: alaris config set <KEY> <VALUE>");
            return 1;
        }

        // Find or create local config file
        string? localPath = FindConfigFile(ConfigPaths);
        string targetPath = localPath ?? "appsettings.local.jsonc";

        JsonNode? config = localPath != null ? LoadJsonc(localPath) : new JsonObject();
        config ??= new JsonObject();

        // Parse key path (e.g. "Polygon.ApiKey" -> ["Polygon", "ApiKey"])
        string[] keyParts = key.Split('.');
        JsonNode current = config;

        // Navigate/create path
        for (int i = 0; i < keyParts.Length - 1; i++)
        {
            if (current is JsonObject obj)
            {
                if (!obj.ContainsKey(keyParts[i]))
                {
                    obj[keyParts[i]] = new JsonObject();
                }
                current = obj[keyParts[i]]!;
            }
        }

        // Set the value
        if (current is JsonObject finalObj)
        {
            // Try to preserve type (number vs string)
            if (double.TryParse(value, out double numValue))
            {
                finalObj[keyParts[^1]] = numValue;
            }
            else if (bool.TryParse(value, out bool boolValue))
            {
                finalObj[keyParts[^1]] = boolValue;
            }
            else
            {
                finalObj[keyParts[^1]] = value;
            }
        }

        // Save file
        JsonSerializerOptions options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        string json = config.ToJsonString(options);
        File.WriteAllText(targetPath, json);

        AnsiConsole.MarkupLine($"[green]✓[/] Set [cyan]{key}[/] = [yellow]{MaskSensitive(key, value)}[/]");
        AnsiConsole.MarkupLine($"[grey]Saved to: {targetPath}[/]");

        return 0;
    }

    private static int ValidateConfiguration()
    {
        AnsiConsole.MarkupLine("[bold cyan]Validating Configuration[/]");
        AnsiConsole.WriteLine();

        bool hasErrors = false;
        bool hasWarnings = false;

        // Check for base config
        string? basePath = FindConfigFile(BasePaths);
        if (basePath == null)
        {
            AnsiConsole.MarkupLine("[red]✗ FAIL[/] appsettings.jsonc not found");
            hasErrors = true;
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓ PASS[/] Base config: {basePath}");
        }

        // Check for local config (secrets)
        string? localPath = FindConfigFile(ConfigPaths);
        if (localPath == null)
        {
            AnsiConsole.MarkupLine("[yellow]○ WARN[/] appsettings.local.jsonc not found (API keys not configured)");
            hasWarnings = true;
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓ PASS[/] Local config: {localPath}");

            // Validate API key is present
            JsonNode? localConfig = LoadJsonc(localPath);
            string? apiKey = GetNestedValue(localConfig, "Polygon", "ApiKey");

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "<YOUR_KEY_HERE>")
            {
                AnsiConsole.MarkupLine("[yellow]○ WARN[/] Polygon.ApiKey not configured");
                hasWarnings = true;
            }
            else
            {
                AnsiConsole.MarkupLine("[green]✓ PASS[/] Polygon.ApiKey configured");
            }
        }

        AnsiConsole.WriteLine();
        if (hasErrors)
        {
            AnsiConsole.MarkupLine("[red bold]Validation failed with errors.[/]");
            return 1;
        }
        else if (hasWarnings)
        {
            AnsiConsole.MarkupLine("[yellow bold]Validation passed with warnings.[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine("[green bold]Validation passed.[/]");
            return 0;
        }
    }

    private static int ShowHelp()
    {
        AnsiConsole.MarkupLine("[bold]Usage:[/] alaris config <ACTION> [KEY] [VALUE]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Actions:[/]");
        AnsiConsole.MarkupLine("  [cyan]show[/]     Display current configuration");
        AnsiConsole.MarkupLine("  [cyan]set[/]      Set a configuration value");
        AnsiConsole.MarkupLine("  [cyan]validate[/] Validate configuration files");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Examples:[/]");
        AnsiConsole.MarkupLine("  alaris config show           # Show all settings");
        AnsiConsole.MarkupLine("  alaris config show --secrets # Show only API keys");
        AnsiConsole.MarkupLine("  alaris config set Polygon.ApiKey <KEY>");
        AnsiConsole.MarkupLine("  alaris config validate");
        return 0;
    }

    private static string? FindConfigFile(string[] paths)
    {
        foreach (string path in paths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }
        return null;
    }

    private static JsonNode? LoadJsonc(string path)
    {
        try
        {
            string content = File.ReadAllText(path);
            // Strip C-style comments
            content = StripJsonComments(content);
            return JsonNode.Parse(content);
        }
        catch
        {
            return null;
        }
    }

    private static string StripJsonComments(string json)
    {
        System.Text.StringBuilder result = new System.Text.StringBuilder();
        bool inString = false;
        bool inLineComment = false;
        bool inBlockComment = false;

        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            char next = i + 1 < json.Length ? json[i + 1] : '\0';

            if (inLineComment)
            {
                if (c == '\n')
                {
                    inLineComment = false;
                    result.Append(c);
                }
                continue;
            }

            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++; // Skip /
                }
                continue;
            }

            if (inString)
            {
                result.Append(c);
                if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                {
                    inString = false;
                }
                continue;
            }

            if (c == '"')
            {
                inString = true;
                result.Append(c);
                continue;
            }

            if (c == '/' && next == '/')
            {
                inLineComment = true;
                continue;
            }

            if (c == '/' && next == '*')
            {
                inBlockComment = true;
                i++; // Skip *
                continue;
            }

            result.Append(c);
        }

        return result.ToString();
    }

    private static void AddConfigRow(Table table, string key, JsonNode? local, JsonNode? baseConfig, bool isSecret)
    {
        string[] parts = key.Split('.');
        string? localValue = GetNestedValue(local, parts);
        string? baseValue = GetNestedValue(baseConfig, parts);

        string value = localValue ?? baseValue ?? "[grey]not set[/]";
        string source = localValue != null ? "[blue]local[/]" : (baseValue != null ? "[grey]base[/]" : "");

        if (isSecret && value != "[grey]not set[/]")
        {
            value = MaskSensitive(key, value);
        }

        table.AddRow(key, value, source);
    }

    private static string? GetNestedValue(JsonNode? node, params string[] path)
    {
        if (node == null)
        {
            return null;
        }

        JsonNode? current = node;
        foreach (string part in path)
        {
            if (current is JsonObject obj && obj.TryGetPropertyValue(part, out JsonNode? next))
            {
                current = next;
            }
            else
            {
                return null;
            }
        }

        return current?.ToString();
    }

    private static string MaskSensitive(string key, string value)
    {
        if (key.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Account", StringComparison.OrdinalIgnoreCase))
        {
            if (value.Length <= 4)
            {
                return "****";
            }
            return value[..4] + new string('*', Math.Min(8, value.Length - 4));
        }
        return value;
    }
}
