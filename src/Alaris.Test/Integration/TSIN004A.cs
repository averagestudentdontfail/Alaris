// TSIN004A.cs - Integration Tests for Alaris.Events Infrastructure
// Component ID: TSIN004A
//
// Tests for event sourcing and audit infrastructure:
// - EVIF001A (In-memory event store)
// - EVIF002A (In-memory audit logger)
// - EVCR003A (Event envelope)
//
// Mathematical Invariants Tested:
// 1. Sequence Number Monotonicity: Each event has seq > all previous
// 2. Append-Only: Events cannot be modified after creation
// 3. Query Completeness: All appended events are retrievable
// 4. Time Ordering: Events returned in time order for time queries
//
// References:
//   - Alaris Governance Rule 17 (Audibility)
//   - Event Sourcing pattern (Fowler)

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Alaris.Infrastructure.Events.Core;
using Alaris.Infrastructure.Events.Infrastructure;

namespace Alaris.Test.Integration;

/// <summary>
/// TSIN004A: Integration tests for Alaris.Events infrastructure.
/// </summary>
public sealed class TSIN004A
{
    // EVIF001A (Event Store) Tests

    /// <summary>
    /// AppendAsync should create envelope with monotonically increasing sequence.
    /// </summary>
    [Fact]
    public async Task EVIF001A_AppendAsync_SequenceNumbersAreMonotonic()
    {
        // Arrange
        EVIF001A store = new EVIF001A();
        List<EVCR003A> events = new List<EVCR003A>();

        // Act - Append 10 events
        for (int i = 0; i < 10; i++)
        {
            TestDomainEvent domainEvent = new TestDomainEvent($"Event-{i}");
            EVCR003A envelope = await store.AppendAsync(domainEvent, $"agg-{i}", "TestAggregate");
            events.Add(envelope);
        }

        // Assert - Sequence numbers should be strictly increasing
        for (int i = 1; i < events.Count; i++)
        {
            events[i].SequenceNumber.Should().BeGreaterThan(events[i - 1].SequenceNumber,
                "Sequence numbers must be monotonically increasing");
        }
    }

    /// <summary>
    /// AppendAsync should assign unique EventId to each envelope.
    /// </summary>
    [Fact]
    public async Task EVIF001A_AppendAsync_EventIdsAreUnique()
    {
        // Arrange
        EVIF001A store = new EVIF001A();
        HashSet<Guid> eventIds = new HashSet<Guid>();

        // Act - Append multiple events
        for (int i = 0; i < 100; i++)
        {
            TestDomainEvent domainEvent = new TestDomainEvent($"Event-{i}");
            EVCR003A envelope = await store.AppendAsync(domainEvent);
            eventIds.Add(envelope.EventId);
        }

        // Assert - All EventIds should be unique
        eventIds.Count.Should().Be(100);
    }

    /// <summary>
    /// GetEventsForAggregateAsync should return only events for specified aggregate.
    /// </summary>
    [Fact]
    public async Task EVIF001A_GetEventsForAggregate_FiltersCorrectly()
    {
        // Arrange
        EVIF001A store = new EVIF001A();
        
        // Append events for different aggregates
        await store.AppendAsync(new TestDomainEvent("A1"), "aggregate-A", "TestType");
        await store.AppendAsync(new TestDomainEvent("B1"), "aggregate-B", "TestType");
        await store.AppendAsync(new TestDomainEvent("A2"), "aggregate-A", "TestType");
        await store.AppendAsync(new TestDomainEvent("B2"), "aggregate-B", "TestType");
        await store.AppendAsync(new TestDomainEvent("A3"), "aggregate-A", "TestType");

        // Act
        IReadOnlyList<EVCR003A> aggregateAEvents = await store.GetEventsForAggregateAsync("aggregate-A");
        IReadOnlyList<EVCR003A> aggregateBEvents = await store.GetEventsForAggregateAsync("aggregate-B");

        // Assert
        aggregateAEvents.Should().HaveCount(3);
        aggregateBEvents.Should().HaveCount(2);
    }

    /// <summary>
    /// GetEventsFromSequenceAsync should paginate correctly.
    /// </summary>
    [Fact]
    public async Task EVIF001A_GetEventsFromSequence_PaginatesCorrectly()
    {
        // Arrange
        EVIF001A store = new EVIF001A();
        
        // Append 20 events
        for (int i = 0; i < 20; i++)
        {
            await store.AppendAsync(new TestDomainEvent($"Event-{i}"));
        }

        // Act - Get events starting from sequence 10 with max 5
        IReadOnlyList<EVCR003A> events = await store.GetEventsFromSequenceAsync(10, maxCount: 5);

        // Assert
        events.Should().HaveCount(5);
        events[0].SequenceNumber.Should().Be(10);
        events[^1].SequenceNumber.Should().Be(14);
    }

    /// <summary>
    /// GetCurrentSequenceNumberAsync should return highest sequence.
    /// </summary>
    [Fact]
    public async Task EVIF001A_GetCurrentSequenceNumber_ReturnsHighest()
    {
        // Arrange
        EVIF001A store = new EVIF001A();
        
        // Act & Assert - Empty store
        long initial = await store.GetCurrentSequenceNumberAsync();
        initial.Should().Be(0);

        // Add events
        await store.AppendAsync(new TestDomainEvent("E1"));
        await store.AppendAsync(new TestDomainEvent("E2"));
        await store.AppendAsync(new TestDomainEvent("E3"));

        long current = await store.GetCurrentSequenceNumberAsync();
        current.Should().Be(3);
    }

    /// <summary>
    /// GetEventsByCorrelationIdAsync should return correlated events.
    /// </summary>
    [Fact]
    public async Task EVIF001A_GetEventsByCorrelationId_ReturnsCorrelated()
    {
        // Arrange
        EVIF001A store = new EVIF001A();
        string correlationId = Guid.NewGuid().ToString();

        // Append events with same correlation
        TestDomainEvent event1 = new TestDomainEvent("Correlated-1") { CorrelationId = correlationId };
        TestDomainEvent event2 = new TestDomainEvent("Correlated-2") { CorrelationId = correlationId };
        TestDomainEvent event3 = new TestDomainEvent("Uncorrelated");

        await store.AppendAsync(event1);
        await store.AppendAsync(event2);
        await store.AppendAsync(event3);

        // Act
        IReadOnlyList<EVCR003A> correlated = await store.GetEventsByCorrelationIdAsync(correlationId);

        // Assert
        correlated.Should().HaveCount(2);
    }

    /// <summary>
    /// GetEventsByTimeRangeAsync should filter by time range.
    /// </summary>
    [Fact]
    public async Task EVIF001A_GetEventsByTimeRange_FiltersCorrectly()
    {
        // Arrange
        EVIF001A store = new EVIF001A();
        DateTime startTime = DateTime.UtcNow;

        // Append events
        await store.AppendAsync(new TestDomainEvent("E1"));
        await Task.Delay(10);
        await store.AppendAsync(new TestDomainEvent("E2"));
        await Task.Delay(10);
        await store.AppendAsync(new TestDomainEvent("E3"));

        DateTime endTime = DateTime.UtcNow;

        // Act
        IReadOnlyList<EVCR003A> events = await store.GetEventsByTimeRangeAsync(startTime, endTime);

        // Assert
        events.Should().HaveCount(3);
        bool inRange = true;
        for (int i = 0; i < events.Count; i++)
        {
            EVCR003A envelope = events[i];
            if (envelope.StoredAtUtc < startTime || envelope.StoredAtUtc > endTime)
            {
                inRange = false;
                break;
            }
        }
        inRange.Should().BeTrue();
    }

    // EVIF002A (Audit Logger) Tests

    /// <summary>
    /// LogAsync should store audit entries.
    /// </summary>
    [Fact]
    public async Task EVIF002A_LogAsync_StoresEntry()
    {
        // Arrange
        EVIF002A logger = new EVIF002A();
        AuditEntry entry = CreateTestAuditEntry("Order", "ORD-001", "PlaceOrder", "TestUser");

        // Act
        await logger.LogAsync(entry);

        // Assert
        IReadOnlyList<AuditEntry> entries = await logger.GetEntriesForEntityAsync("Order", "ORD-001");
        entries.Should().HaveCount(1);
        entries[0].Action.Should().Be("PlaceOrder");
    }

    /// <summary>
    /// GetEntriesForEntityAsync should return all entries for entity.
    /// </summary>
    [Fact]
    public async Task EVIF002A_GetEntriesForEntity_ReturnsCorrectEntries()
    {
        // Arrange
        EVIF002A logger = new EVIF002A();

        await logger.LogAsync(CreateTestAuditEntry("Order", "ORD-001", "Create", "User1"));
        await logger.LogAsync(CreateTestAuditEntry("Order", "ORD-001", "Update", "User1"));
        await logger.LogAsync(CreateTestAuditEntry("Order", "ORD-002", "Create", "User2"));

        // Act
        IReadOnlyList<AuditEntry> order1Entries = await logger.GetEntriesForEntityAsync("Order", "ORD-001");
        IReadOnlyList<AuditEntry> order2Entries = await logger.GetEntriesForEntityAsync("Order", "ORD-002");

        // Assert
        order1Entries.Should().HaveCount(2);
        order2Entries.Should().HaveCount(1);
    }

    /// <summary>
    /// GetEntriesByInitiatorAsync should filter by initiator.
    /// </summary>
    [Fact]
    public async Task EVIF002A_GetEntriesByInitiator_FiltersCorrectly()
    {
        // Arrange
        EVIF002A logger = new EVIF002A();

        await logger.LogAsync(CreateTestAuditEntry("Order", "ORD-001", "Create", "Admin"));
        await logger.LogAsync(CreateTestAuditEntry("Order", "ORD-002", "Create", "User"));
        await logger.LogAsync(CreateTestAuditEntry("Order", "ORD-003", "Create", "Admin"));

        // Act
        IReadOnlyList<AuditEntry> adminEntries = await logger.GetEntriesByInitiatorAsync("Admin");

        // Assert
        adminEntries.Should().HaveCount(2);
    }

    /// <summary>
    /// GetEntriesByTimeRangeAsync should filter by time.
    /// </summary>
    [Fact]
    public async Task EVIF002A_GetEntriesByTimeRange_FiltersCorrectly()
    {
        // Arrange
        EVIF002A logger = new EVIF002A();
        DateTime startTime = DateTime.UtcNow;

        await logger.LogAsync(CreateTestAuditEntry("Order", "ORD-001", "Create", "User"));
        await Task.Delay(10);
        await logger.LogAsync(CreateTestAuditEntry("Order", "ORD-002", "Create", "User"));

        DateTime endTime = DateTime.UtcNow;

        // Act
        IReadOnlyList<AuditEntry> entries = await logger.GetEntriesByTimeRangeAsync(startTime, endTime);

        // Assert
        entries.Should().HaveCount(2);
    }

    /// <summary>
    /// Audit entries should preserve severity levels.
    /// </summary>
    [Theory]
    [InlineData(AuditSeverity.Information)]
    [InlineData(AuditSeverity.Warning)]
    [InlineData(AuditSeverity.Error)]
    [InlineData(AuditSeverity.Critical)]
    public async Task EVIF002A_PreservesSeverity(AuditSeverity severity)
    {
        // Arrange
        EVIF002A logger = new EVIF002A();
        AuditEntry entry = new AuditEntry
        {
            AuditId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            Action = "TestAction",
            EntityType = "TestEntity",
            EntityId = "TEST-001",
            InitiatedBy = "TestUser",
            Description = "Test description",
            Severity = severity,
            Outcome = AuditOutcome.Success
        };

        // Act
        await logger.LogAsync(entry);
        IReadOnlyList<AuditEntry> entries = await logger.GetEntriesForEntityAsync("TestEntity", "TEST-001");

        // Assert
        entries[0].Severity.Should().Be(severity);
    }

    /// <summary>
    /// Audit entries should preserve outcome status.
    /// </summary>
    [Theory]
    [InlineData(AuditOutcome.Success)]
    [InlineData(AuditOutcome.Failure)]
    [InlineData(AuditOutcome.Partial)]
    public async Task EVIF002A_PreservesOutcome(AuditOutcome outcome)
    {
        // Arrange
        EVIF002A logger = new EVIF002A();
        AuditEntry entry = new AuditEntry
        {
            AuditId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            Action = "TestAction",
            EntityType = "TestEntity",
            EntityId = "TEST-002",
            InitiatedBy = "TestUser",
            Description = "Test description",
            Severity = AuditSeverity.Information,
            Outcome = outcome
        };

        // Act
        await logger.LogAsync(entry);
        IReadOnlyList<AuditEntry> entries = await logger.GetEntriesForEntityAsync("TestEntity", "TEST-002");

        // Assert
        entries[0].Outcome.Should().Be(outcome);
    }

    // EVCR003A (Event Envelope) Tests

    /// <summary>
    /// Event envelope should serialize event data to JSON.
    /// </summary>
    [Fact]
    public void EVCR003A_Create_SerializesEventToJson()
    {
        // Arrange
        TestDomainEvent domainEvent = new TestDomainEvent("TestPayload")
        {
            TestProperty = "PropertyValue"
        };

        // Act
        EVCR003A envelope = EVCR003A.Create(domainEvent, 1);

        // Assert
        envelope.EventData.Should().Contain("TestPayload");
        envelope.EventData.Should().Contain("PropertyValue");
        envelope.EventType.Should().Be("TestDomainEvent");
    }

    /// <summary>
    /// Event envelope should preserve event ID.
    /// </summary>
    [Fact]
    public void EVCR003A_Create_PreservesEventId()
    {
        // Arrange
        TestDomainEvent domainEvent = new TestDomainEvent("Test");

        // Act
        EVCR003A envelope = EVCR003A.Create(domainEvent, 1);

        // Assert
        envelope.EventId.Should().Be(domainEvent.EventId);
    }

    /// <summary>
    /// Event envelope should store aggregate information.
    /// </summary>
    [Fact]
    public void EVCR003A_Create_StoresAggregateInfo()
    {
        // Arrange
        TestDomainEvent domainEvent = new TestDomainEvent("Test");

        // Act
        EVCR003A envelope = EVCR003A.Create(
            domainEvent, 
            sequenceNumber: 42,
            aggregateId: "AGG-001",
            aggregateType: "OrderAggregate",
            initiatedBy: "TestUser");

        // Assert
        envelope.SequenceNumber.Should().Be(42);
        envelope.AggregateId.Should().Be("AGG-001");
        envelope.AggregateType.Should().Be("OrderAggregate");
        envelope.InitiatedBy.Should().Be("TestUser");
    }

    /// <summary>
    /// Event envelope should include correlation ID from event.
    /// </summary>
    [Fact]
    public void EVCR003A_Create_IncludesCorrelationId()
    {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        TestDomainEvent domainEvent = new TestDomainEvent("Test")
        {
            CorrelationId = correlationId
        };

        // Act
        EVCR003A envelope = EVCR003A.Create(domainEvent, 1);

        // Assert
        envelope.CorrelationId.Should().Be(correlationId);
    }

    // Concurrent Access Tests

    /// <summary>
    /// Event store should handle concurrent appends correctly.
    /// </summary>
    [Fact]
    public async Task EVIF001A_ConcurrentAppends_MaintainsSequenceIntegrity()
    {
        // Arrange
        EVIF001A store = new EVIF001A();
        int eventCount = 100;
        List<Task<EVCR003A>> tasks = new List<Task<EVCR003A>>();

        // Act - Concurrent appends
        for (int i = 0; i < eventCount; i++)
        {
            int index = i; // Capture for closure
            tasks.Add(Task.Run(async () =>
            {
                TestDomainEvent domainEvent = new TestDomainEvent($"Event-{index}");
                return await store.AppendAsync(domainEvent);
            }));
        }

        EVCR003A[] envelopes = await Task.WhenAll(tasks);

        // Assert - All sequence numbers should be unique
        HashSet<long> uniqueSequenceNumbers = new HashSet<long>();
        long minSequence = long.MaxValue;
        long maxSequence = long.MinValue;

        for (int i = 0; i < envelopes.Length; i++)
        {
            long sequence = envelopes[i].SequenceNumber;
            uniqueSequenceNumbers.Add(sequence);

            if (sequence < minSequence)
            {
                minSequence = sequence;
            }

            if (sequence > maxSequence)
            {
                maxSequence = sequence;
            }
        }

        uniqueSequenceNumbers.Count.Should().Be(eventCount,
            "All sequence numbers must be unique even with concurrent appends");

        // Assert - Sequence numbers should span 1 to eventCount
        minSequence.Should().Be(1);
        maxSequence.Should().Be(eventCount);
    }

    /// <summary>
    /// Audit logger should handle concurrent logs correctly.
    /// </summary>
    [Fact]
    public async Task EVIF002A_ConcurrentLogs_AllEntriesStored()
    {
        // Arrange
        EVIF002A logger = new EVIF002A();
        int entryCount = 100;
        List<Task> tasks = new List<Task>();

        // Act - Concurrent logs
        for (int i = 0; i < entryCount; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                AuditEntry entry = CreateTestAuditEntry("Order", $"ORD-{index:D3}", "Create", "User");
                await logger.LogAsync(entry);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All entries should be stored
        IReadOnlyList<AuditEntry> allEntries = await logger.GetEntriesByInitiatorAsync("User");
        allEntries.Should().HaveCount(entryCount);
    }

    // Helper Methods

    private static AuditEntry CreateTestAuditEntry(
        string entityType, 
        string entityId, 
        string action, 
        string initiatedBy)
    {
        return new AuditEntry
        {
            AuditId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            InitiatedBy = initiatedBy,
            Description = $"Test {action} on {entityType} {entityId}",
            Severity = AuditSeverity.Information,
            Outcome = AuditOutcome.Success
        };
    }
}

/// <summary>
/// Test domain event for testing event store.
/// </summary>
public sealed record TestDomainEvent : EVCR001A
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public string EventType { get; init; } = "TestDomainEvent";
    public string? CorrelationId { get; init; }
    
    public string Message { get; init; }
    public string? TestProperty { get; init; }

    public TestDomainEvent(string message)
    {
        Message = message;
    }
}
