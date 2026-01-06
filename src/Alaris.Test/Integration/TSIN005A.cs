// TSIN005A.cs - Integration Tests for Alaris.Application Services
// Component ID: TSIN005A
//
// Tests for Application service components:
// - APsv001A (Session Lifecycle Management)
// - APmd001A (Session Model)
// - SessionStatus enum
// - SessionStatistics record
//
// Test Approach:
// Uses temporary directories for file operations - no subprocess spawning.
// All tests are self-contained and clean up after themselves.
//
// Mathematical Invariants Tested:
// 1. Session Sequence: Each new session has seq > all previous
// 2. Session ID Format: BT[Sequence][Variant]-[StartDate]-[EndDate]
// 3. Date Constraints: EndDate > StartDate
// 4. CRUD Operations: Create/Read/Update/Delete consistency
//
// References:
//   - Alaris.Governance Session Naming § 2.1

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Alaris.Host.Application.Model;
using Alaris.Host.Application.Service;

namespace Alaris.Test.Integration;

/// <summary>
/// TSIN005A: Integration tests for Alaris.Application services.
/// </summary>
public sealed class TSIN005A : IAsyncLifetime
{
    private string _tempSessionsRoot = null!;
    private APsv001A _sessionService = null!;

    // Test Lifecycle

    public Task InitializeAsync()
    {
        // Create a unique temp directory for each test
        _tempSessionsRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"alaris_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempSessionsRoot);
        _sessionService = new APsv001A(_tempSessionsRoot);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // Clean up temp directory
        if (Directory.Exists(_tempSessionsRoot))
        {
            try
            {
                Directory.Delete(_tempSessionsRoot, recursive: true);
            }
            catch (IOException)
            {
                // Ignore cleanup errors for CI environments
            }
        }
        return Task.CompletedTask;
    }

    // APsv001A.CreateAsync Tests

    /// <summary>
    /// CreateAsync should create a session with valid ID format.
    /// </summary>
    [Fact]
    public async Task APsv001A_CreateAsync_CreatesSessionWithValidId()
    {
        // Arrange
        DateTime startDate = new DateTime(2024, 1, 1);
        DateTime endDate = new DateTime(2024, 3, 31);

        // Act
        APmd001A session = await _sessionService.CreateAsync(startDate, endDate);

        // Assert
        session.Should().NotBeNull();
        session.SessionId.Should().StartWith("BT");
        session.SessionId.Should().MatchRegex(@"BT\d{3}[A-Z]-\d{8}-\d{8}");
    }

    /// <summary>
    /// CreateAsync should create required folder structure.
    /// </summary>
    [Fact]
    public async Task APsv001A_CreateAsync_CreatesFolderStructure()
    {
        // Arrange
        DateTime startDate = new DateTime(2024, 1, 1);
        DateTime endDate = new DateTime(2024, 3, 31);

        // Act
        APmd001A session = await _sessionService.CreateAsync(startDate, endDate);

        // Assert - Check folder structure
        Directory.Exists(session.SessionPath).Should().BeTrue();
        Directory.Exists(System.IO.Path.Combine(session.SessionPath, "universe")).Should().BeTrue();
        Directory.Exists(System.IO.Path.Combine(session.SessionPath, "data", "equity", "usa", "daily")).Should().BeTrue();
        Directory.Exists(System.IO.Path.Combine(session.SessionPath, "results")).Should().BeTrue();
        Directory.Exists(System.IO.Path.Combine(session.SessionPath, "earnings")).Should().BeTrue();
    }

    /// <summary>
    /// CreateAsync should save session.json metadata.
    /// </summary>
    [Fact]
    public async Task APsv001A_CreateAsync_SavesMetadata()
    {
        // Arrange
        DateTime startDate = new DateTime(2024, 1, 1);
        DateTime endDate = new DateTime(2024, 3, 31);

        // Act
        APmd001A session = await _sessionService.CreateAsync(startDate, endDate);

        // Assert
        string metadataPath = System.IO.Path.Combine(session.SessionPath, "session.json");
        File.Exists(metadataPath).Should().BeTrue();
        
        string json = await File.ReadAllTextAsync(metadataPath);
        json.Should().Contain("sessionId");
        json.Should().Contain(session.SessionId);
    }

    /// <summary>
    /// CreateAsync should set correct dates.
    /// </summary>
    [Fact]
    public async Task APsv001A_CreateAsync_SetsCorrectDates()
    {
        // Arrange
        DateTime startDate = new DateTime(2024, 6, 1);
        DateTime endDate = new DateTime(2024, 6, 30);

        // Act
        APmd001A session = await _sessionService.CreateAsync(startDate, endDate);

        // Assert
        session.StartDate.Should().Be(startDate);
        session.EndDate.Should().Be(endDate);
        session.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// CreateAsync should set initial status to Created.
    /// </summary>
    [Fact]
    public async Task APsv001A_CreateAsync_SetsStatusToCreated()
    {
        // Arrange
        DateTime startDate = new DateTime(2024, 1, 1);
        DateTime endDate = new DateTime(2024, 3, 31);

        // Act
        APmd001A session = await _sessionService.CreateAsync(startDate, endDate);

        // Assert
        session.Status.Should().Be(SessionStatus.Created);
    }

    /// <summary>
    /// CreateAsync should include symbols when provided.
    /// </summary>
    [Fact]
    public async Task APsv001A_CreateAsync_IncludesSymbols()
    {
        // Arrange
        DateTime startDate = new DateTime(2024, 1, 1);
        DateTime endDate = new DateTime(2024, 3, 31);
        string[] symbols = new[] { "AAPL", "MSFT", "GOOGL" };

        // Act
        APmd001A session = await _sessionService.CreateAsync(startDate, endDate, symbols);

        // Assert
        session.Symbols.Should().HaveCount(3);
        session.Symbols.Should().Contain("AAPL");
        session.Symbols.Should().Contain("MSFT");
        session.Symbols.Should().Contain("GOOGL");
    }

    /// <summary>
    /// CreateAsync should reject invalid date range.
    /// </summary>
    [Fact]
    public async Task APsv001A_CreateAsync_RejectsInvalidDateRange()
    {
        // Arrange - end before start
        DateTime startDate = new DateTime(2024, 6, 30);
        DateTime endDate = new DateTime(2024, 6, 1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _sessionService.CreateAsync(startDate, endDate));
    }

    /// <summary>
    /// CreateAsync should reject same start and end date.
    /// </summary>
    [Fact]
    public async Task APsv001A_CreateAsync_RejectsSameDates()
    {
        // Arrange
        DateTime startDate = new DateTime(2024, 6, 15);
        DateTime endDate = new DateTime(2024, 6, 15);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _sessionService.CreateAsync(startDate, endDate));
    }

    // Session Sequence Numbering Tests

    /// <summary>
    /// Session sequence numbers should be monotonically increasing.
    /// </summary>
    [Fact]
    public async Task APsv001A_CreateMultiple_SequenceNumbersIncrease()
    {
        // Arrange & Act - Create multiple sessions
        APmd001A session1 = await _sessionService.CreateAsync(
            new DateTime(2024, 1, 1), new DateTime(2024, 1, 31));
        APmd001A session2 = await _sessionService.CreateAsync(
            new DateTime(2024, 2, 1), new DateTime(2024, 2, 28));
        APmd001A session3 = await _sessionService.CreateAsync(
            new DateTime(2024, 3, 1), new DateTime(2024, 3, 31));

        // Assert - Extract sequence numbers
        int seq1 = ExtractSequence(session1.SessionId);
        int seq2 = ExtractSequence(session2.SessionId);
        int seq3 = ExtractSequence(session3.SessionId);

        seq1.Should().Be(1);
        seq2.Should().Be(2);
        seq3.Should().Be(3);
    }

    // APsv001A.GetAsync Tests

    /// <summary>
    /// GetAsync should return session by ID.
    /// </summary>
    [Fact]
    public async Task APsv001A_GetAsync_ReturnsSessionById()
    {
        // Arrange
        APmd001A created = await _sessionService.CreateAsync(
            new DateTime(2024, 1, 1), new DateTime(2024, 3, 31));

        // Act
        APmd001A? retrieved = await _sessionService.GetAsync(created.SessionId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.SessionId.Should().Be(created.SessionId);
        retrieved.StartDate.Should().Be(created.StartDate);
        retrieved.EndDate.Should().Be(created.EndDate);
        retrieved.Status.Should().Be(created.Status);
    }

    /// <summary>
    /// GetAsync should return null for non-existent session.
    /// </summary>
    [Fact]
    public async Task APsv001A_GetAsync_ReturnsNullForNonExistent()
    {
        // Act
        APmd001A? result = await _sessionService.GetAsync("BT999A-20240101-20240331");

        // Assert
        result.Should().BeNull();
    }

    // APsv001A.ListAsync Tests

    /// <summary>
    /// ListAsync should return empty list when no sessions exist.
    /// </summary>
    [Fact]
    public async Task APsv001A_ListAsync_ReturnsEmptyWhenNoSessions()
    {
        // Act
        IReadOnlyList<APmd001A> sessions = await _sessionService.ListAsync();

        // Assert
        sessions.Should().BeEmpty();
    }

    /// <summary>
    /// ListAsync should return all created sessions.
    /// </summary>
    [Fact]
    public async Task APsv001A_ListAsync_ReturnsAllSessions()
    {
        // Arrange - Create 3 sessions
        await _sessionService.CreateAsync(new DateTime(2024, 1, 1), new DateTime(2024, 1, 31));
        await _sessionService.CreateAsync(new DateTime(2024, 2, 1), new DateTime(2024, 2, 28));
        await _sessionService.CreateAsync(new DateTime(2024, 3, 1), new DateTime(2024, 3, 31));

        // Act
        IReadOnlyList<APmd001A> sessions = await _sessionService.ListAsync();

        // Assert
        sessions.Should().HaveCount(3);
    }

    /// <summary>
    /// ListAsync should return sessions ordered by creation date descending.
    /// </summary>
    [Fact]
    public async Task APsv001A_ListAsync_ReturnsOrderedByCreatedAt()
    {
        // Arrange - Create sessions with slight delays
        APmd001A s1 = await _sessionService.CreateAsync(new DateTime(2024, 1, 1), new DateTime(2024, 1, 31));
        await Task.Delay(50);
        APmd001A s2 = await _sessionService.CreateAsync(new DateTime(2024, 2, 1), new DateTime(2024, 2, 28));
        await Task.Delay(50);
        APmd001A s3 = await _sessionService.CreateAsync(new DateTime(2024, 3, 1), new DateTime(2024, 3, 31));

        // Act
        IReadOnlyList<APmd001A> sessions = await _sessionService.ListAsync();

        // Assert - Most recent first
        sessions[0].SessionId.Should().Be(s3.SessionId);
        sessions[1].SessionId.Should().Be(s2.SessionId);
        sessions[2].SessionId.Should().Be(s1.SessionId);
    }

    // APsv001A.UpdateAsync Tests

    /// <summary>
    /// UpdateAsync should persist status changes.
    /// </summary>
    [Fact]
    public async Task APsv001A_UpdateAsync_PersistsStatusChange()
    {
        // Arrange
        APmd001A session = await _sessionService.CreateAsync(
            new DateTime(2024, 1, 1), new DateTime(2024, 3, 31));
        
        // Act
        APmd001A updated = session with { Status = SessionStatus.Running };
        await _sessionService.UpdateAsync(updated);

        // Assert
        APmd001A? retrieved = await _sessionService.GetAsync(session.SessionId);
        retrieved!.Status.Should().Be(SessionStatus.Running);
    }

    /// <summary>
    /// UpdateAsync should update the UpdatedAt timestamp.
    /// </summary>
    [Fact]
    public async Task APsv001A_UpdateAsync_UpdatesTimestamp()
    {
        // Arrange
        APmd001A session = await _sessionService.CreateAsync(
            new DateTime(2024, 1, 1), new DateTime(2024, 3, 31));
        DateTime originalUpdatedAt = session.UpdatedAt;
        await Task.Delay(100);

        // Act
        APmd001A updated = session with { Status = SessionStatus.Completed };
        await _sessionService.UpdateAsync(updated);

        // Assert
        APmd001A? retrieved = await _sessionService.GetAsync(session.SessionId);
        retrieved!.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    // APsv001A.DeleteAsync Tests

    /// <summary>
    /// DeleteAsync should remove session folder.
    /// </summary>
    [Fact]
    public async Task APsv001A_DeleteAsync_RemovesSessionFolder()
    {
        // Arrange
        APmd001A session = await _sessionService.CreateAsync(
            new DateTime(2024, 1, 1), new DateTime(2024, 3, 31));
        string sessionPath = session.SessionPath;
        Directory.Exists(sessionPath).Should().BeTrue();

        // Act
        await _sessionService.DeleteAsync(session.SessionId);

        // Assert
        Directory.Exists(sessionPath).Should().BeFalse();
    }

    /// <summary>
    /// DeleteAsync should remove session from index.
    /// </summary>
    [Fact]
    public async Task APsv001A_DeleteAsync_RemovesFromIndex()
    {
        // Arrange
        APmd001A session = await _sessionService.CreateAsync(
            new DateTime(2024, 1, 1), new DateTime(2024, 3, 31));
        
        // Act
        await _sessionService.DeleteAsync(session.SessionId);

        // Assert
        IReadOnlyList<APmd001A> sessions = await _sessionService.ListAsync();
        sessions.Should().NotContain(s => s.SessionId == session.SessionId);
    }

    /// <summary>
    /// DeleteAsync should throw for non-existent session.
    /// </summary>
    [Fact]
    public async Task APsv001A_DeleteAsync_ThrowsForNonExistent()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _sessionService.DeleteAsync("BT999A-20240101-20240331"));
    }

    // APsv001A Path Methods Tests

    /// <summary>
    /// GetDataPath should return correct data folder path.
    /// </summary>
    [Fact]
    public async Task APsv001A_GetDataPath_ReturnsCorrectPath()
    {
        // Arrange
        APmd001A session = await _sessionService.CreateAsync(
            new DateTime(2024, 1, 1), new DateTime(2024, 3, 31));

        // Act
        string dataPath = _sessionService.GetDataPath(session.SessionId);

        // Assert
        dataPath.Should().EndWith(System.IO.Path.Combine(session.SessionId, "data"));
        Directory.Exists(dataPath).Should().BeTrue();
    }

    /// <summary>
    /// GetResultsPath should return correct results folder path.
    /// </summary>
    [Fact]
    public async Task APsv001A_GetResultsPath_ReturnsCorrectPath()
    {
        // Arrange
        APmd001A session = await _sessionService.CreateAsync(
            new DateTime(2024, 1, 1), new DateTime(2024, 3, 31));

        // Act
        string resultsPath = _sessionService.GetResultsPath(session.SessionId);

        // Assert
        resultsPath.Should().EndWith(System.IO.Path.Combine(session.SessionId, "results"));
        Directory.Exists(resultsPath).Should().BeTrue();
    }

    // APmd001A Model Tests

    /// <summary>
    /// Session model should be immutable (record type).
    /// </summary>
    [Fact]
    public void APmd001A_IsImmutableRecord()
    {
        // Arrange
        APmd001A session = new APmd001A
        {
            SessionId = "BT001A-20240101-20240331",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 3, 31),
            CreatedAt = DateTime.UtcNow,
            SessionPath = "/tmp/test"
        };

        // Act - Use with expression to create modified copy
        APmd001A modified = session with { Status = SessionStatus.Running };

        // Assert - Original unchanged
        session.Status.Should().Be(SessionStatus.Created);
        modified.Status.Should().Be(SessionStatus.Running);
    }

    /// <summary>
    /// Session statistics should have all required fields.
    /// </summary>
    [Fact]
    public void SessionStatistics_ContainsAllFields()
    {
        // Arrange
        SessionStatistics stats = new SessionStatistics
        {
            TotalOrders = 100,
            NetProfit = 5000m,
            SharpeRatio = 1.5,
            MaxDrawdown = 0.10m,
            WinRate = 0.65,
            StartEquity = 100000m,
            EndEquity = 105000m,
            DurationSeconds = 3600
        };

        // Assert
        stats.TotalOrders.Should().Be(100);
        stats.NetProfit.Should().Be(5000m);
        stats.SharpeRatio.Should().Be(1.5);
        stats.MaxDrawdown.Should().Be(0.10m);
        stats.WinRate.Should().Be(0.65);
        stats.StartEquity.Should().Be(100000m);
        stats.EndEquity.Should().Be(105000m);
        stats.DurationSeconds.Should().Be(3600);
    }

    // SessionStatus Enum Tests

    /// <summary>
    /// SessionStatus should have all expected values.
    /// </summary>
    [Theory]
    [InlineData(SessionStatus.Created)]
    [InlineData(SessionStatus.Preparing)]
    [InlineData(SessionStatus.Ready)]
    [InlineData(SessionStatus.Running)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Failed)]
    public void SessionStatus_HasExpectedValue(SessionStatus status)
    {
        // Assert
        Enum.IsDefined(status).Should().BeTrue();
    }

    /// <summary>
    /// SessionStatus should have exactly 6 values.
    /// </summary>
    [Fact]
    public void SessionStatus_HasSixValues()
    {
        // Assert
        Enum.GetValues<SessionStatus>().Should().HaveCount(6);
    }

    // Session ID Format Tests

    /// <summary>
    /// Session ID should contain correct dates.
    /// </summary>
    [Fact]
    public async Task SessionId_ContainsCorrectDates()
    {
        // Arrange
        DateTime startDate = new DateTime(2024, 7, 15);
        DateTime endDate = new DateTime(2024, 8, 20);

        // Act
        APmd001A session = await _sessionService.CreateAsync(startDate, endDate);

        // Assert
        session.SessionId.Should().Contain("20240715");
        session.SessionId.Should().Contain("20240820");
    }

    /// <summary>
    /// Session ID should have variant suffix.
    /// </summary>
    [Fact]
    public async Task SessionId_HasVariantSuffix()
    {
        // Act
        APmd001A session = await _sessionService.CreateAsync(
            new DateTime(2024, 1, 1), new DateTime(2024, 1, 31));

        // Assert - Should have variant 'A'
        session.SessionId.Should().MatchRegex(@"BT\d{3}A-");
    }

    // Concurrent Operations Tests

    /// <summary>
    /// Creating multiple sessions concurrently should work correctly.
    /// </summary>
    [Fact]
    public async Task APsv001A_ConcurrentCreation_AllSessionsCreated()
    {
        // Arrange
        List<Task<APmd001A>> tasks = new List<Task<APmd001A>>();

        // Act - Create 5 sessions concurrently
        for (int i = 0; i < 5; i++)
        {
            int month = i + 1;
            tasks.Add(_sessionService.CreateAsync(
                new DateTime(2024, month, 1),
                new DateTime(2024, month, 28)));
        }

        APmd001A[] sessions = await Task.WhenAll(tasks);

        // Assert - All sessions created with unique IDs
        HashSet<string> uniqueSessionIds = new HashSet<string>();
        for (int i = 0; i < sessions.Length; i++)
        {
            uniqueSessionIds.Add(sessions[i].SessionId);
        }
        uniqueSessionIds.Count.Should().Be(5);
    }

    // Helper Methods

    private static int ExtractSequence(string sessionId)
    {
        // Format: BT001A-YYYYMMDD-YYYYMMDD
        if (sessionId.Length >= 5 && sessionId.StartsWith("BT", StringComparison.Ordinal))
        {
            string sequenceStr = sessionId.Substring(2, 3);
            if (int.TryParse(sequenceStr, out int sequence))
            {
                return sequence;
            }
        }
        return 0;
    }
}
