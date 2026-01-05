// CLer003A.cs - Earnings check command

using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;
using Alaris.Host.Application.Cli.Settings;
using Alaris.Host.Application.Model;
using Alaris.Host.Application.Service;

namespace Alaris.Host.Application.Cli.Commands.Earnings;

/// <summary>
/// Checks earnings data coverage for a session.
/// Component ID: CLer003A
/// </summary>
public sealed class CLer003A : AsyncCommand<EarningsCheckSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, EarningsCheckSettings settings)
    {
        CLif003A.Info($"Checking earnings coverage for session: {settings.SessionId}");
        AnsiConsole.WriteLine();

        APsv001A sessionService = new APsv001A();
        APmd001A? session = await sessionService.GetAsync(settings.SessionId);

        if (session == null)
        {
            CLif003A.Error($"Session not found: {settings.SessionId}");
            return 1;
        }

        string dataPath = sessionService.GetDataPath(session.SessionId);
        string nasdaqPath = System.IO.Path.Combine(dataPath, "earnings", "nasdaq");

        // Calculate required dates
        int totalDays = (session.EndDate - session.StartDate).Days + 1;
        int weekdays = 0;
        for (DateTime date = session.StartDate; date <= session.EndDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
            {
                weekdays++;
            }
        }

        // Count cached dates
        int cachedDates = 0;
        List<DateTime> missingDates = new List<DateTime>();

        if (Directory.Exists(nasdaqPath))
        {
            string[] cachedFiles = Directory.GetFiles(nasdaqPath, "*.json");
            HashSet<DateTime> cachedDatesSet = new HashSet<DateTime>();
            foreach (string file in cachedFiles)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                if (DateTime.TryParse(name, out DateTime parsed))
                {
                    cachedDatesSet.Add(parsed.Date);
                }
            }

            for (DateTime date = session.StartDate; date <= session.EndDate; date = date.AddDays(1))
            {
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                if (cachedDatesSet.Contains(date.Date))
                    cachedDates++;
                else
                    missingDates.Add(date);
            }
        }
        else
        {
            // All dates missing
            for (DateTime date = session.StartDate; date <= session.EndDate; date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                    missingDates.Add(date);
            }
        }

        double coverage = weekdays > 0 ? (double)cachedDates / weekdays * 100 : 0;
        string coverageColor = coverage >= 90 ? "green" : coverage >= 50 ? "yellow" : "red";

        CLif003A.WriteKeyValueTable("Earnings Coverage", new[]
        {
            ("Session", session.SessionId),
            ("Date Range", $"{session.StartDate:yyyy-MM-dd} → {session.EndDate:yyyy-MM-dd}"),
            ("Required Days", $"{weekdays}"),
            ("Cached Days", $"{cachedDates}"),
            ("Coverage", $"[{coverageColor}]{coverage:F1}%[/]")
        });

        if (missingDates.Count > 0 && settings.Verbose)
        {
            AnsiConsole.WriteLine();
            CLif003A.Warning($"{missingDates.Count} missing dates. Sample:");
            
            int sampleCount = missingDates.Count > 5 ? 5 : missingDates.Count;
            for (int i = 0; i < sampleCount; i++)
            {
                AnsiConsole.MarkupLine($"  [grey]• {missingDates[i]:yyyy-MM-dd}[/]");
            }
            
            if (missingDates.Count > 5)
            {
                AnsiConsole.MarkupLine($"  [grey]... and {missingDates.Count - 5} more[/]");
            }
        }

        if (coverage < 90)
        {
            AnsiConsole.WriteLine();
            CLif003A.Info("Run 'alaris earnings bootstrap' to download missing dates.");
        }

        return coverage >= 90 ? 0 : 1;
    }
}
