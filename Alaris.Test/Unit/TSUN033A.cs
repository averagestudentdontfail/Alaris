// TSUN033A.cs - Unit Tests for Persistent Event Stores (EVIF001B, EVIF002B)
// Component ID: TSUN033A
//
// Coverage:
// - EVIF001B file-based event store persistence
// - EVIF002B file-based audit logger persistence
// - Crash recovery and sequence persistence
//

using System.IO;
using Xunit;
using FluentAssertions;
using Alaris.Infrastructure.Events.Core;
using Alaris.Infrastructure.Events.Infrastructure;

namespace Alaris.Test.Unit;

/// <summary>
/// TSUN033A: Unit tests for persistent event stores (EVIF001B, EVIF002B).
/// </summary>
public sealed class TSUN033A : IDisposable
{
    private readonly string _testStoragePath;
    private readonly string _eventsStoragePath;
    private readonly string _auditStoragePath;
    private bool _disposed;

    public TSUN033A()
    {
        // Create unique test directories
        _testStoragePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"alaris-test-{Guid.NewGuid():N}");
        _eventsStoragePath = System.IO.Path.Combine(_testStoragePath, "events");
        _auditStoragePath = System.IO.Path.Combine(_testStoragePath, "audit");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing && System.IO.Directory.Exists(_testStoragePath))
        {
            System.IO.Directory.Delete(_testStoragePath, recursive: true);
        }
        _disposed = true;
    }


    /// <summary>
    /// Append and retrieve event successfully.
    /// </summary>
    [Fact]
    public async Task EVIF001B_AppendAsync_PersistsEvent()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE
        // ═══════════════════════════════════════════════════════════
        using EVIF001B store = new(_eventsStoragePath);
        TestEvent testEvent = new() { Message = "Test message" };

        // ═══════════════════════════════════════════════════════════
        // ACT
        // ═══════════════════════════════════════════════════════════
        EVCR003A envelope = await store.AppendAsync(
            testEvent,
            aggregateId: "test-aggregate-1",
            aggregateType: "TestAggregate");

        // ═══════════════════════════════════════════════════════════
        // ASSERT
        // ═══════════════════════════════════════════════════════════
        envelope.Should().NotBeNull();
        envelope.SequenceNumber.Should().Be(1);
        envelope.AggregateId.Should().Be("test-aggregate-1");
    }

    /// <summary>
    /// Sequence numbers increment correctly.
    /// </summary>
    [Fact]
    public async Task EVIF001B_AppendAsync_IncrementsSequence()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE
        // ═══════════════════════════════════════════════════════════
        using EVIF001B store = new(_eventsStoragePath);

        // ═══════════════════════════════════════════════════════════
        // ACT
        // ═══════════════════════════════════════════════════════════
        EVCR003A first = await store.AppendAsync(new TestEvent { Message = "First" });
        EVCR003A second = await store.AppendAsync(new TestEvent { Message = "Second" });
        EVCR003A third = await store.AppendAsync(new TestEvent { Message = "Third" });

        // ═══════════════════════════════════════════════════════════
        // ASSERT
        // ═══════════════════════════════════════════════════════════
        first.SequenceNumber.Should().Be(1);
        second.SequenceNumber.Should().Be(2);
        third.SequenceNumber.Should().Be(3);
    }

    /// <summary>
    /// Get events by aggregate ID.
    /// </summary>
    [Fact]
    public async Task EVIF001B_GetEventsForAggregateAsync_FiltersCorrectly()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE
        // ═══════════════════════════════════════════════════════════
        using EVIF001B store = new(_eventsStoragePath);
        await store.AppendAsync(new TestEvent { Message = "A1" }, aggregateId: "agg-a");
        await store.AppendAsync(new TestEvent { Message = "B1" }, aggregateId: "agg-b");
        await store.AppendAsync(new TestEvent { Message = "A2" }, aggregateId: "agg-a");

        // ═══════════════════════════════════════════════════════════
        // ACT
        // ═══════════════════════════════════════════════════════════
        IReadOnlyList<EVCR003A> aggAEvents = await store.GetEventsForAggregateAsync("agg-a");

        // ═══════════════════════════════════════════════════════════
        // ASSERT
        // ═══════════════════════════════════════════════════════════
        aggAEvents.Should().HaveCount(2);
        aggAEvents.Should().AllSatisfy(e => e.AggregateId.Should().Be("agg-a"));
    }

    /// <summary>
    /// Sequence persists across store instances (crash recovery).
    /// </summary>
    [Fact]
    public async Task EVIF001B_RecoverSequence_AfterRestart()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE: Append events, then "restart" by creating new instance
        // ═══════════════════════════════════════════════════════════
        using (EVIF001B store1 = new(_eventsStoragePath))
        {
            await store1.AppendAsync(new TestEvent { Message = "1" });
            await store1.AppendAsync(new TestEvent { Message = "2" });
            await store1.AppendAsync(new TestEvent { Message = "3" });
        }

        // ═══════════════════════════════════════════════════════════
        // ACT: Create new instance (simulates restart)
        // ═══════════════════════════════════════════════════════════
        using EVIF001B store2 = new(_eventsStoragePath);
        long recoveredSequence = await store2.GetCurrentSequenceNumberAsync();
        EVCR003A nextEvent = await store2.AppendAsync(new TestEvent { Message = "4" });

        // ═══════════════════════════════════════════════════════════
        // ASSERT
        // ═══════════════════════════════════════════════════════════
        recoveredSequence.Should().Be(3);
        nextEvent.SequenceNumber.Should().Be(4);
    }

    /// <summary>
    /// Get events from sequence number.
    /// </summary>
    [Fact]
    public async Task EVIF001B_GetEventsFromSequenceAsync_ReturnsFromSequence()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE
        // ═══════════════════════════════════════════════════════════
        using EVIF001B store = new(_eventsStoragePath);
        await store.AppendAsync(new TestEvent { Message = "1" });
        await store.AppendAsync(new TestEvent { Message = "2" });
        await store.AppendAsync(new TestEvent { Message = "3" });
        await store.AppendAsync(new TestEvent { Message = "4" });

        // ═══════════════════════════════════════════════════════════
        // ACT
        // ═══════════════════════════════════════════════════════════
        IReadOnlyList<EVCR003A> fromSeq2 = await store.GetEventsFromSequenceAsync(2);

        // ═══════════════════════════════════════════════════════════
        // ASSERT
        // ═══════════════════════════════════════════════════════════
        fromSeq2.Should().HaveCount(3);
        fromSeq2[0].SequenceNumber.Should().Be(2);
    }



    /// <summary>
    /// Log and retrieve audit entry.
    /// </summary>
    [Fact]
    public async Task EVIF002B_LogAsync_PersistsEntry()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE
        // ═══════════════════════════════════════════════════════════
        using EVIF002B logger = new(_auditStoragePath);
        AuditEntry entry = CreateAuditEntry("Order", "order-123", "Created", "system");

        // ═══════════════════════════════════════════════════════════
        // ACT
        // ═══════════════════════════════════════════════════════════
        await logger.LogAsync(entry);
        IReadOnlyList<AuditEntry> entries = await logger.GetEntriesForEntityAsync("Order", "order-123");

        // ═══════════════════════════════════════════════════════════
        // ASSERT
        // ═══════════════════════════════════════════════════════════
        entries.Should().HaveCount(1);
        entries[0].EntityId.Should().Be("order-123");
        entries[0].Action.Should().Be("Created");
    }

    /// <summary>
    /// Filter by initiator.
    /// </summary>
    [Fact]
    public async Task EVIF002B_GetEntriesByInitiatorAsync_FiltersCorrectly()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE
        // ═══════════════════════════════════════════════════════════
        using EVIF002B logger = new(_auditStoragePath);
        await logger.LogAsync(CreateAuditEntry("Order", "1", "Create", "user-a"));
        await logger.LogAsync(CreateAuditEntry("Order", "2", "Create", "user-b"));
        await logger.LogAsync(CreateAuditEntry("Order", "3", "Create", "user-a"));

        // ═══════════════════════════════════════════════════════════
        // ACT
        // ═══════════════════════════════════════════════════════════
        IReadOnlyList<AuditEntry> userAEntries = await logger.GetEntriesByInitiatorAsync("user-a");

        // ═══════════════════════════════════════════════════════════
        // ASSERT
        // ═══════════════════════════════════════════════════════════
        userAEntries.Should().HaveCount(2);
        userAEntries.Should().AllSatisfy(e => e.InitiatedBy.Should().Be("user-a"));
    }

    /// <summary>
    /// Filter by time range.
    /// </summary>
    [Fact]
    public async Task EVIF002B_GetEntriesByTimeRangeAsync_FiltersCorrectly()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE
        // ═══════════════════════════════════════════════════════════
        using EVIF002B logger = new(_auditStoragePath);
        DateTime now = DateTime.UtcNow;
        
        await logger.LogAsync(CreateAuditEntry("Order", "1", "Create", "system"));
        await logger.LogAsync(CreateAuditEntry("Order", "2", "Update", "system"));

        // ═══════════════════════════════════════════════════════════
        // ACT
        // ═══════════════════════════════════════════════════════════
        IReadOnlyList<AuditEntry> entries = await logger.GetEntriesByTimeRangeAsync(
            now.AddMinutes(-1), now.AddMinutes(1));

        // ═══════════════════════════════════════════════════════════
        // ASSERT
        // ═══════════════════════════════════════════════════════════
        entries.Should().HaveCount(2);
    }

    /// <summary>
    /// Guard clause: null entry throws.
    /// </summary>
    [Fact]
    public async Task EVIF002B_LogAsync_NullEntry_Throws()
    {
        // ═══════════════════════════════════════════════════════════
        // ARRANGE
        // ═══════════════════════════════════════════════════════════
        using EVIF002B logger = new(_auditStoragePath);

        // ═══════════════════════════════════════════════════════════
        // ACT & ASSERT
        // ═══════════════════════════════════════════════════════════
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => logger.LogAsync(null!));
    }



    private static AuditEntry CreateAuditEntry(
        string entityType, string entityId, string action, string initiatedBy)
    {
        return new AuditEntry
        {
            AuditId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            InitiatedBy = initiatedBy,
            Description = $"{action} {entityType} {entityId}"
        };
    }



    /// <summary>
    /// Test event for unit tests.
    /// </summary>
    private sealed record TestEvent : EVCR001A
    {
        public required string Message { get; init; }
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
        public string EventType => nameof(TestEvent);
        public string? CorrelationId => null;
    }

}
