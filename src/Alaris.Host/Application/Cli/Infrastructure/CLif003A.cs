// CLif003A.cs - Rich console output utilities

using Spectre.Console;
using Spectre.Console.Rendering;

namespace Alaris.Host.Application.Cli.Infrastructure;

/// <summary>
/// Rich console output utilities for consistent CLI appearance.
/// Component ID: CLif003A
/// </summary>
public static class CLif003A
{
    private const string Version = "2.0.0";

    /// <summary>
    /// Writes the Alaris banner.
    /// </summary>
    public static void WriteBanner()
    {
        AnsiConsole.Write(new FigletText("Alaris")
            .LeftJustified()
            .Color(Color.Blue));

        AnsiConsole.MarkupLine("[grey]Quantitative Earnings Volatility Trading System[/]");
        AnsiConsole.MarkupLine($"[grey]Version {Version}[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Writes a success message.
    /// </summary>
    public static void Success(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes an error message.
    /// </summary>
    public static void Error(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes a warning message.
    /// </summary>
    public static void Warning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]![/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes an info message.
    /// </summary>
    public static void Info(string message)
    {
        AnsiConsole.MarkupLine($"[blue]ℹ[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Creates a status context for long-running operations.
    /// </summary>
    public static async Task<T> WithStatusAsync<T>(string message, Func<StatusContext, Task<T>> action)
    {
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync(message, action);
    }

    /// <summary>
    /// Creates a progress context for multi-step operations.
    /// </summary>
    public static async Task WithProgressAsync(
        string description,
        Func<ProgressContext, Task> action)
    {
        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(action);
    }

    /// <summary>
    /// Writes a key-value table.
    /// </summary>
    public static void WriteKeyValueTable(string title, IEnumerable<(string Key, string Value)> items)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title($"[bold]{title}[/]")
            .AddColumn(new TableColumn("[grey]Property[/]").LeftAligned())
            .AddColumn(new TableColumn("[grey]Value[/]").LeftAligned());

        foreach ((string key, string value) in items)
        {
            table.AddRow(key, value);
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Writes a data table with custom columns.
    /// </summary>
    public static void WriteTable<T>(
        string title,
        IEnumerable<T> items,
        params (string Header, Func<T, string> Selector)[] columns)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title($"[bold]{title}[/]");

        foreach ((string header, _) in columns)
        {
            table.AddColumn(new TableColumn($"[grey]{header}[/]"));
        }

        foreach (T item in items)
        {
            string[] values = columns.Select(c => c.Selector(item)).ToArray();
            table.AddRow(values);
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Prompts for confirmation.
    /// </summary>
    public static bool Confirm(string prompt, bool defaultValue = false)
    {
        return AnsiConsole.Confirm(prompt, defaultValue);
    }

    /// <summary>
    /// Writes a panel with content.
    /// </summary>
    public static void WritePanel(string title, string content, Color? borderColor = null)
    {
        var panel = new Panel(content)
            .Header($"[bold]{title}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(borderColor ?? Color.Blue)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Writes a rule (horizontal line with optional title).
    /// </summary>
    public static void WriteRule(string? title = null)
    {
        if (title != null)
        {
            AnsiConsole.Write(new Rule($"[grey]{title}[/]").LeftJustified().RuleStyle("grey"));
        }
        else
        {
            AnsiConsole.Write(new Rule().RuleStyle("grey"));
        }
    }
}
