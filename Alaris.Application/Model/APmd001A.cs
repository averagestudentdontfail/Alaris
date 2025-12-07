// =============================================================================
// APmd001A.cs - Backtest Session Model
// Component: APmd001A | Category: Models | Variant: A (Primary)
// =============================================================================
// Represents a backtest session with isolated data, universe, and results.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Alaris.Application.Model;

/// <summary>
/// Status of a backtest session.
/// </summary>
public enum SessionStatus
{
    /// <summary>Session created, data not yet downloaded.</summary>
    Created,
    
    /// <summary>Data download and preparation in progress.</summary>
    Preparing,
    
    /// <summary>Session ready to run.</summary>
    Ready,
    
    /// <summary>Backtest currently running.</summary>
    Running,
    
    /// <summary>Backtest completed successfully.</summary>
    Completed,
    
    /// <summary>Backtest failed.</summary>
    Failed
}

/// <summary>
/// Represents a backtest session with isolated data and results.
/// Component ID: APmd001A
/// </summary>
/// <remarks>
/// Session naming follows Alaris governance:
/// Format: BT[Sequence][Variant]-[StartDate]-[EndDate]
/// Example: BT001A-20230601-20230630
/// </remarks>
public sealed record APmd001A
{
    /// <summary>
    /// Unique session identifier.
    /// Format: BT[Sequence][Variant]-[StartDate]-[EndDate]
    /// </summary>
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    /// <summary>
    /// Backtest start date.
    /// </summary>
    [JsonPropertyName("startDate")]
    public required DateTime StartDate { get; init; }
    
    /// <summary>
    /// Backtest end date.
    /// </summary>
    [JsonPropertyName("endDate")]
    public required DateTime EndDate { get; init; }
    
    /// <summary>
    /// Session creation timestamp.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// Last update timestamp.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
    
    /// <summary>
    /// Current session status.
    /// </summary>
    [JsonPropertyName("status")]
    public SessionStatus Status { get; init; } = SessionStatus.Created;
    
    /// <summary>
    /// Absolute path to session folder.
    /// </summary>
    [JsonPropertyName("sessionPath")]
    public required string SessionPath { get; init; }
    
    /// <summary>
    /// Symbols included in this session's universe.
    /// </summary>
    [JsonPropertyName("symbols")]
    public IReadOnlyList<string> Symbols { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Exit code from LEAN execution (null if not run).
    /// </summary>
    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; init; }
    
    /// <summary>
    /// Session statistics (populated after completion).
    /// </summary>
    [JsonPropertyName("statistics")]
    public SessionStatistics? Statistics { get; init; }
    
    /// <summary>
    /// Error message if session failed.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Statistics from a completed backtest session.
/// </summary>
public sealed record SessionStatistics
{
    [JsonPropertyName("totalOrders")]
    public int TotalOrders { get; init; }
    
    [JsonPropertyName("netProfit")]
    public decimal NetProfit { get; init; }
    
    [JsonPropertyName("sharpeRatio")]
    public double SharpeRatio { get; init; }
    
    [JsonPropertyName("maxDrawdown")]
    public decimal MaxDrawdown { get; init; }
    
    [JsonPropertyName("winRate")]
    public double WinRate { get; init; }
    
    [JsonPropertyName("startEquity")]
    public decimal StartEquity { get; init; }
    
    [JsonPropertyName("endEquity")]
    public decimal EndEquity { get; init; }
    
    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }
}
