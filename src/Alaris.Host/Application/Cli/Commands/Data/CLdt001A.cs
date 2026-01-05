// CLdt001A.cs - Data bootstrap command (Polygon prices + options)

using Spectre.Console;
using Spectre.Console.Cli;
using Alaris.Host.Application.Cli.Infrastructure;
using Alaris.Host.Application.Cli.Settings;
using Alaris.Host.Application.Service;
using Alaris.Host.Application.Command;

namespace Alaris.Host.Application.Cli.Commands.Data;

/// <summary>
/// Downloads market data (prices, options, rates) for a session.
/// Component ID: CLdt001A
/// </summary>
public sealed class CLdt001A : AsyncCommand<DataBootstrapSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DataBootstrapSettings settings)
    {
        CLif003A.Info($"Downloading data for session: {settings.SessionId}");
        AnsiConsole.WriteLine();

        // Load session
        APsv001A sessionService = new APsv001A();
        APmd001A? session = await sessionService.GetAsync(settings.SessionId);

        if (session == null)
        {
            CLif003A.Error($"Session not found: {settings.SessionId}");
            return 1;
        }

        // Resolve symbols
        IReadOnlyList<string> symbols = !string.IsNullOrEmpty(settings.Symbols)
            ? settings.Symbols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : session.Symbols;

        CLif003A.WriteKeyValueTable("Session Details", new[]
        {
            ("Session", session.SessionId),
            ("Date Range", $"{session.StartDate:yyyy-MM-dd} → {session.EndDate:yyyy-MM-dd}"),
            ("Symbols", $"{symbols.Count} symbols"),
            ("Data Path", sessionService.GetDataPath(session.SessionId))
        });
        AnsiConsole.WriteLine();

        try
        {
            // Create download service
            using APsv002A downloadService = DependencyFactory.CreateAPsv002A();
            string dataPath = sessionService.GetDataPath(session.SessionId);

            await downloadService.DownloadEquityDataAsync(
                dataPath,
                symbols,
                session.StartDate,
                session.EndDate);

            CLif003A.Success("Data download complete.");
            
            if (settings.Verbose)
            {
                CLif003A.Info("Note: Earnings data must be bootstrapped separately via 'alaris earnings bootstrap'");
            }

            return 0;
        }
        catch (Exception ex)
        {
            CLif003A.Error($"Download failed: {ex.Message}");
            return 1;
        }
    }
}
