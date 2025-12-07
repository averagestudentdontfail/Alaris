// =============================================================================
// APsv001A.cs - Backtest Session Lifecycle Management
// Component: APsv001A | Category: Services | Variant: A (Primary)
// =============================================================================
// Manages backtest session lifecycle: create, list, run, delete.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Alaris.Application.Model;
using Microsoft.Extensions.Logging;

using System.Text.Json.Serialization;

namespace Alaris.Application.Service;

/// <summary>
/// Service for managing backtest session lifecycle.
/// Component ID: APsv001A
/// </summary>
public sealed class APsv001A
{
    private readonly string _sessionsRoot;
    private readonly string _indexPath;
    private readonly ILogger<APsv001A>? _logger;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Initializes the session service.
    /// </summary>
    /// <param name="sessionsRoot">Root directory for sessions (defaults to Alaris.Sessions/).</param>
    /// <param name="logger">Logger instance.</param>
    public APsv001A(string? sessionsRoot = null, ILogger<APsv001A>? logger = null)
    {
        _sessionsRoot = sessionsRoot ?? FindSessionsRoot();
        _indexPath = System.IO.Path.Combine(_sessionsRoot, "sessions.json");
        _logger = logger;
        
        // Ensure sessions directory exists
        Directory.CreateDirectory(_sessionsRoot);
    }

    /// <summary>
    /// Creates a new backtest session.
    /// </summary>
    /// <param name="startDate">Backtest start date.</param>
    /// <param name="endDate">Backtest end date.</param>
    /// <param name="symbols">Optional list of symbols (null = generate universe).</param>
    /// <returns>Created session.</returns>
    public async Task<APmd001A> CreateAsync(DateTime startDate, DateTime endDate, IEnumerable<string>? symbols = null)
    {
        if (endDate <= startDate)
        {
            throw new ArgumentException("End date must be after start date");
        }

        // Generate session ID
        var sessionId = await GenerateSessionIdAsync(startDate, endDate);
        var sessionPath = System.IO.Path.Combine(_sessionsRoot, sessionId);

        _logger?.LogInformation("Creating session {SessionId} at {Path}", sessionId, sessionPath);

        // Create folder structure
        Directory.CreateDirectory(sessionPath);
        Directory.CreateDirectory(System.IO.Path.Combine(sessionPath, "universe"));
        Directory.CreateDirectory(System.IO.Path.Combine(sessionPath, "data", "equity", "usa", "daily"));
        Directory.CreateDirectory(System.IO.Path.Combine(sessionPath, "results"));
        Directory.CreateDirectory(System.IO.Path.Combine(sessionPath, "earnings"));

        var symbolList = symbols?.ToList() ?? new List<string>();

        var session = new APmd001A
        {
            SessionId = sessionId,
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = SessionStatus.Created,
            SessionPath = sessionPath,
            Symbols = symbolList
        };

        // Save session metadata
        await SaveSessionMetadataAsync(session);

        // Update index
        await AddToIndexAsync(session);

        _logger?.LogInformation("Session {SessionId} created successfully", sessionId);

        return session;
    }

    /// <summary>
    /// Gets a session by ID.
    /// </summary>
    public async Task<APmd001A?> GetAsync(string sessionId)
    {
        var sessionPath = System.IO.Path.Combine(_sessionsRoot, sessionId);
        var metadataPath = System.IO.Path.Combine(sessionPath, "session.json");

        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(metadataPath);
        return JsonSerializer.Deserialize<APmd001A>(json, JsonOptions);
    }

    /// <summary>
    /// Lists all sessions.
    /// </summary>
    public async Task<IReadOnlyList<APmd001A>> ListAsync()
    {
        if (!File.Exists(_indexPath))
        {
            return Array.Empty<APmd001A>();
        }

        var json = await File.ReadAllTextAsync(_indexPath);
        var index = JsonSerializer.Deserialize<SessionIndex>(json, JsonOptions);
        
        if (index?.Sessions == null)
        {
            return Array.Empty<APmd001A>();
        }

        var sessions = new List<APmd001A>();
        foreach (var sessionId in index.Sessions)
        {
            var session = await GetAsync(sessionId);
            if (session != null)
            {
                sessions.Add(session);
            }
        }

        return sessions.OrderByDescending(s => s.CreatedAt).ToList();
    }

    /// <summary>
    /// Updates a session's status and metadata.
    /// </summary>
    public async Task UpdateAsync(APmd001A session)
    {
        var updated = session with { UpdatedAt = DateTime.UtcNow };
        await SaveSessionMetadataAsync(updated);
        _logger?.LogDebug("Session {SessionId} updated to status {Status}", session.SessionId, session.Status);
    }

    /// <summary>
    /// Deletes a session and all its data.
    /// </summary>
    public async Task DeleteAsync(string sessionId)
    {
        var sessionPath = System.IO.Path.Combine(_sessionsRoot, sessionId);

        if (!Directory.Exists(sessionPath))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        _logger?.LogInformation("Deleting session {SessionId}", sessionId);

        // Remove from index first
        await RemoveFromIndexAsync(sessionId);

        // Delete session folder
        Directory.Delete(sessionPath, recursive: true);

        _logger?.LogInformation("Session {SessionId} deleted", sessionId);
    }

    /// <summary>
    /// Gets the data folder path for a session (for LEAN integration).
    /// </summary>
    public string GetDataPath(string sessionId)
    {
        return System.IO.Path.Combine(_sessionsRoot, sessionId, "data");
    }

    /// <summary>
    /// Gets the results folder path for a session.
    /// </summary>
    public string GetResultsPath(string sessionId)
    {
        return System.IO.Path.Combine(_sessionsRoot, sessionId, "results");
    }

    /// <summary>
    /// Gets the next available sequence number for session IDs.
    /// </summary>
    private async Task<int> GetNextSequenceAsync()
    {
        var sessions = await ListAsync();
        if (sessions.Count == 0)
        {
            return 1;
        }

        var maxSequence = sessions
            .Select(s => ParseSequenceFromId(s.SessionId))
            .Max();

        return maxSequence + 1;
    }

    /// <summary>
    /// Generates a session ID following Alaris naming convention.
    /// Format: BT[Sequence][Variant]-[StartDate]-[EndDate]
    /// </summary>
    private async Task<string> GenerateSessionIdAsync(DateTime startDate, DateTime endDate)
    {
        var sequence = await GetNextSequenceAsync();
        var sequenceStr = sequence.ToString("D3"); // 001, 002, etc.
        var variant = "A"; // Primary variant
        var startStr = startDate.ToString("yyyyMMdd");
        var endStr = endDate.ToString("yyyyMMdd");

        return $"BT{sequenceStr}{variant}-{startStr}-{endStr}";
    }

    /// <summary>
    /// Parses the sequence number from a session ID.
    /// </summary>
    private static int ParseSequenceFromId(string sessionId)
    {
        // Format: BT001A-YYYYMMDD-YYYYMMDD
        if (sessionId.Length >= 5 && sessionId.StartsWith("BT", StringComparison.Ordinal))
        {
            var sequenceStr = sessionId.Substring(2, 3);
            if (int.TryParse(sequenceStr, out var sequence))
            {
                return sequence;
            }
        }
        return 0;
    }

    /// <summary>
    /// Saves session metadata to session.json.
    /// </summary>
    private async Task SaveSessionMetadataAsync(APmd001A session)
    {
        var metadataPath = System.IO.Path.Combine(session.SessionPath, "session.json");
        var json = JsonSerializer.Serialize(session, JsonOptions);
        await File.WriteAllTextAsync(metadataPath, json);
    }

    /// <summary>
    /// Adds a session to the index.
    /// </summary>
    private async Task AddToIndexAsync(APmd001A session)
    {
        var index = await LoadIndexAsync();
        if (!index.Sessions.Contains(session.SessionId))
        {
            index.Sessions.Add(session.SessionId);
            await SaveIndexAsync(index);
        }
    }

    /// <summary>
    /// Removes a session from the index.
    /// </summary>
    private async Task RemoveFromIndexAsync(string sessionId)
    {
        var index = await LoadIndexAsync();
        index.Sessions.Remove(sessionId);
        await SaveIndexAsync(index);
    }

    /// <summary>
    /// Loads the sessions index.
    /// </summary>
    private async Task<SessionIndex> LoadIndexAsync()
    {
        if (!File.Exists(_indexPath))
        {
            return new SessionIndex { Sessions = new List<string>() };
        }

        var json = await File.ReadAllTextAsync(_indexPath);
        return JsonSerializer.Deserialize<SessionIndex>(json, JsonOptions) 
               ?? new SessionIndex { Sessions = new List<string>() };
    }

    /// <summary>
    /// Saves the sessions index.
    /// </summary>
    private async Task SaveIndexAsync(SessionIndex index)
    {
        var json = JsonSerializer.Serialize(index, JsonOptions);
        await File.WriteAllTextAsync(_indexPath, json);
    }

    /// <summary>
    /// Finds the sessions root directory.
    /// </summary>
    private static string FindSessionsRoot()
    {
        // Look for Alaris.Sessions in parent directories
        var current = Directory.GetCurrentDirectory();
        
        for (int i = 0; i < 5; i++)
        {
            var candidate = System.IO.Path.Combine(current, "Alaris.Sessions");
            var configPath = System.IO.Path.Combine(current, "config.json");
            
            // If we find config.json, this is likely the project root
            if (File.Exists(configPath))
            {
                return candidate;
            }
            
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        // Default to current directory
        return System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Alaris.Sessions");
    }
}

/// <summary>
/// Index of all sessions.
/// </summary>
internal sealed class SessionIndex
{
    public List<string> Sessions { get; init; } = new();
}
