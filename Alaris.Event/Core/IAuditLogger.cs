// IDE0130: Namespace does not match folder structure - suppressed because "Event" is a reserved keyword (CA1716)
namespace Alaris.Eventing.Core;

/// <summary>
/// Interface for writing immutable audit logs.
/// Audit logs provide a human-readable trail of all system activities.
/// </summary>
/// <remarks>
/// Rule 17 (Audibility): Audit logs are append-only and never modified.
/// They complement the event store with human-readable information.
/// </remarks>
public interface IAuditLogger
{
    /// <summary>
    /// Logs an audit entry for a system action.
    /// </summary>
    /// <param name="entry">The audit entry to log.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves audit entries for a specific entity.
    /// </summary>
    /// <param name="entityType">The type of entity.</param>
    /// <param name="entityId">The entity identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All audit entries for the entity.</returns>
    public Task<IReadOnlyList<AuditEntry>> GetEntriesForEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves audit entries initiated by a specific user or system.
    /// </summary>
    /// <param name="initiatedBy">The user or system identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All audit entries initiated by the specified actor.</returns>
    public Task<IReadOnlyList<AuditEntry>> GetEntriesByInitiatorAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves audit entries within a time range.
    /// </summary>
    /// <param name="fromUtc">Start time (inclusive).</param>
    /// <param name="toUtc">End time (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Audit entries within the time range.</returns>
    public Task<IReadOnlyList<AuditEntry>> GetEntriesByTimeRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Immutable audit entry representing a single auditable action.
/// </summary>
public sealed record AuditEntry
{
    /// <summary>
    /// Gets the unique identifier for this audit entry.
    /// </summary>
    public required Guid AuditId { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when this action occurred.
    /// </summary>
    public required DateTime OccurredAtUtc { get; init; }

    /// <summary>
    /// Gets the type of action that was performed.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Gets the type of entity this action was performed on.
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// Gets the identifier of the entity this action was performed on.
    /// </summary>
    public required string EntityId { get; init; }

    /// <summary>
    /// Gets the user or system that initiated this action.
    /// </summary>
    public required string InitiatedBy { get; init; }

    /// <summary>
    /// Gets the description of what happened.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the severity level of this audit entry.
    /// </summary>
    public AuditSeverity Severity { get; init; } = AuditSeverity.Information;

    /// <summary>
    /// Gets the outcome of the action.
    /// </summary>
    public AuditOutcome Outcome { get; init; } = AuditOutcome.Success;

    /// <summary>
    /// Gets the state before the action (if applicable).
    /// </summary>
    public string? StateBefore { get; init; }

    /// <summary>
    /// Gets the state after the action (if applicable).
    /// </summary>
    public string? StateAfter { get; init; }

    /// <summary>
    /// Gets the correlation ID for tracing related actions.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets additional contextual data as key-value pairs.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalData { get; init; }
}

/// <summary>
/// Severity level for audit entries.
/// </summary>
public enum AuditSeverity
{
    /// <summary>
    /// Informational audit entry.
    /// </summary>
    Information = 0,

    /// <summary>
    /// Warning-level audit entry.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error-level audit entry.
    /// </summary>
    Error = 2,

    /// <summary>
    /// Critical audit entry requiring immediate attention.
    /// </summary>
    Critical = 3
}

/// <summary>
/// Outcome of an audited action.
/// </summary>
public enum AuditOutcome
{
    /// <summary>
    /// The action succeeded.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The action failed.
    /// </summary>
    Failure = 1,

    /// <summary>
    /// The action was partially successful.
    /// </summary>
    Partial = 2
}
