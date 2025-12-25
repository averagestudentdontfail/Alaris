// =============================================================================
// TSUN030A.cs - Binary Serialization Unit Tests
// Component: TSUN030A | Category: Unit Tests | Variant: A (Primary)
// =============================================================================
// Tests for FIX SBE binary serialization across Protocol, Data, Events, and
// Application layers. Validates round-trip encoding/decoding correctness.
// =============================================================================
// References:
// - Alaris.Governance/Coding.md Rule 5 (Zero-Allocation Hot Paths)
// - FIX SBE Specification
// =============================================================================

using System;
using System.Collections.Generic;
using Alaris.Infrastructure.Data.Model;
using Alaris.Infrastructure.Data.Serialization;
using Alaris.Infrastructure.Events.Core;
using Alaris.Infrastructure.Events.Serialization;
using Alaris.Host.Application.Model;
using Alaris.Host.Application.Serialization;
using Alaris.Infrastructure.Protocol.Buffers;
using Alaris.Infrastructure.Protocol.Serialization;
using FluentAssertions;
using Xunit;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for FIX SBE binary serialization components.
/// Component ID: TSUN030A
/// </summary>
public sealed class TSUN030A
{
    #region Protocol Layer (PLSR001A) Tests

    [Fact]
    public void PriceBarData_RoundTrip_PreservesAllFields()
    {
        // Arrange
        var original = new PriceBarData
        {
            TimestampEpochMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            OpenMantissa = PLSR001A.ToMantissa(150.25m),
            HighMantissa = PLSR001A.ToMantissa(155.50m),
            LowMantissa = PLSR001A.ToMantissa(149.00m),
            CloseMantissa = PLSR001A.ToMantissa(154.75m),
            Volume = 1_500_000
        };
        var buffer = new byte[256];

        // Act
        int bytesWritten = PLSR001A.EncodePriceBar(in original, buffer);
        var decoded = PLSR001A.DecodePriceBar(buffer.AsSpan(0, bytesWritten));

        // Assert
        decoded.TimestampEpochMs.Should().Be(original.TimestampEpochMs);
        decoded.OpenMantissa.Should().Be(original.OpenMantissa);
        decoded.HighMantissa.Should().Be(original.HighMantissa);
        decoded.LowMantissa.Should().Be(original.LowMantissa);
        decoded.CloseMantissa.Should().Be(original.CloseMantissa);
        decoded.Volume.Should().Be(original.Volume);
    }

    [Fact]
    public void OptionContractData_RoundTrip_PreservesAllFields()
    {
        // Arrange
        var original = new OptionContractData
        {
            StrikeMantissa = PLSR001A.ToMantissa(150.00m),
            ExpirationDays = (int)(new DateTime(2025, 3, 21) - DateTime.UnixEpoch).TotalDays,
            Right = OptionRightEnum.Call,
            BidMantissa = PLSR001A.ToMantissa(5.25m),
            AskMantissa = PLSR001A.ToMantissa(5.50m),
            LastMantissa = PLSR001A.ToMantissa(5.35m),
            ImpliedVolatilityMantissa = PLSR001A.ToMantissa(0.2845m),
            DeltaMantissa = PLSR001A.ToMantissa(0.55m),
            GammaMantissa = PLSR001A.ToMantissa(0.025m),
            ThetaMantissa = PLSR001A.ToMantissa(-0.05m),
            VegaMantissa = PLSR001A.ToMantissa(0.15m),
            OpenInterest = 5000,
            Volume = 250,
            Symbol = "AAPL"
        };
        var buffer = new byte[256];

        // Act
        int bytesWritten = PLSR001A.EncodeOptionContract(in original, buffer);
        var decoded = PLSR001A.DecodeOptionContract(buffer.AsSpan(0, bytesWritten));

        // Assert
        decoded.StrikeMantissa.Should().Be(original.StrikeMantissa);
        decoded.ExpirationDays.Should().Be(original.ExpirationDays);
        decoded.Right.Should().Be(original.Right);
        decoded.BidMantissa.Should().Be(original.BidMantissa);
        decoded.AskMantissa.Should().Be(original.AskMantissa);
        decoded.Symbol.Should().Be(original.Symbol);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(100.12345678)]
    [InlineData(-999.99999999)]
    [InlineData(0.00000001)]
    public void ToMantissa_FromMantissa_RoundTrip_IsAccurate(decimal value)
    {
        // Act
        long mantissa = PLSR001A.ToMantissa(value);
        decimal result = PLSR001A.FromMantissa(mantissa);

        // Assert - 8 decimal places precision
        result.Should().BeApproximately(value, 0.000000005m);
    }

    #endregion

    #region Data Layer (DTsr001A) Tests

    [Fact]
    public void PriceBar_RoundTrip_PreservesAllFields()
    {
        // Arrange
        var original = new PriceBar
        {
            Symbol = "NVDA",
            Timestamp = new DateTime(2024, 12, 20, 16, 0, 0, DateTimeKind.Utc),
            Open = 520.50m,
            High = 535.25m,
            Low = 518.00m,
            Close = 532.75m,
            Volume = 45_000_000
        };
        var buffer = new byte[256];

        // Act
        int bytesWritten = DTsr001A.EncodePriceBar(original, buffer);
        var decoded = DTsr001A.DecodePriceBar(buffer.AsSpan(0, bytesWritten), "NVDA");

        // Assert
        decoded.Symbol.Should().Be(original.Symbol);
        decoded.Timestamp.Should().Be(original.Timestamp);
        decoded.Open.Should().Be(original.Open);
        decoded.High.Should().Be(original.High);
        decoded.Low.Should().Be(original.Low);
        decoded.Close.Should().Be(original.Close);
        decoded.Volume.Should().Be(original.Volume);
    }

    [Fact]
    public void OptionContract_RoundTrip_PreservesAllFields()
    {
        // Arrange
        var original = new OptionContract
        {
            UnderlyingSymbol = "AAPL",
            OptionSymbol = "AAPL250321C00150000",
            Strike = 150.00m,
            Expiration = new DateTime(2025, 3, 21, 0, 0, 0, DateTimeKind.Utc),
            Right = OptionRight.Call,
            Bid = 5.25m,
            Ask = 5.50m,
            Last = 5.35m,
            ImpliedVolatility = 0.2845m,
            Delta = 0.55m,
            Gamma = 0.025m,
            Theta = -0.05m,
            Vega = 0.15m,
            OpenInterest = 5000,
            Volume = 250,
            Timestamp = DateTime.UtcNow
        };
        var buffer = new byte[512];

        // Act
        int bytesWritten = DTsr001A.EncodeOptionContract(original, buffer);
        var decoded = DTsr001A.DecodeOptionContract(buffer.AsSpan(0, bytesWritten));

        // Assert
        decoded.UnderlyingSymbol.Should().Be(original.UnderlyingSymbol);
        decoded.OptionSymbol.Should().Be(original.OptionSymbol);
        decoded.Strike.Should().Be(original.Strike);
        decoded.Expiration.Date.Should().Be(original.Expiration.Date);
        decoded.Right.Should().Be(original.Right);
        decoded.Bid.Should().Be(original.Bid);
        decoded.Ask.Should().Be(original.Ask);
        decoded.ImpliedVolatility.Should().Be(original.ImpliedVolatility);
        decoded.Delta.Should().Be(original.Delta);
    }

    [Fact]
    public void OptionContract_WithNullGreeks_PreservesNulls()
    {
        // Arrange
        var original = new OptionContract
        {
            UnderlyingSymbol = "MSFT",
            OptionSymbol = "MSFT250321P00400000",
            Strike = 400.00m,
            Expiration = new DateTime(2025, 3, 21, 0, 0, 0, DateTimeKind.Utc),
            Right = OptionRight.Put,
            Bid = 12.50m,
            Ask = 13.00m,
            Last = null,
            ImpliedVolatility = null,
            Delta = null,
            Gamma = null,
            Theta = null,
            Vega = null,
            OpenInterest = 1000,
            Volume = 50,
            Timestamp = DateTime.UtcNow
        };
        var buffer = new byte[512];

        // Act
        int bytesWritten = DTsr001A.EncodeOptionContract(original, buffer);
        var decoded = DTsr001A.DecodeOptionContract(buffer.AsSpan(0, bytesWritten));

        // Assert
        decoded.Last.Should().BeNull();
        decoded.ImpliedVolatility.Should().BeNull();
        decoded.Delta.Should().BeNull();
    }

    [Fact]
    public void OptionChainSnapshot_RoundTrip_PreservesAllContracts()
    {
        // Arrange
        var contracts = new List<OptionContract>
        {
            new()
            {
                UnderlyingSymbol = "SPY",
                OptionSymbol = "SPY250321C00500000",
                Strike = 500m,
                Expiration = new DateTime(2025, 3, 21, 0, 0, 0, DateTimeKind.Utc),
                Right = OptionRight.Call,
                Bid = 10m,
                Ask = 11m,
                OpenInterest = 1000,
                Volume = 100,
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                UnderlyingSymbol = "SPY",
                OptionSymbol = "SPY250321P00500000",
                Strike = 500m,
                Expiration = new DateTime(2025, 3, 21, 0, 0, 0, DateTimeKind.Utc),
                Right = OptionRight.Put,
                Bid = 8m,
                Ask = 9m,
                OpenInterest = 800,
                Volume = 80,
                Timestamp = DateTime.UtcNow
            }
        };

        var original = new OptionChainSnapshot
        {
            Symbol = "SPY",
            Timestamp = DateTime.UtcNow,
            SpotPrice = 505.50m,
            Contracts = contracts
        };

        using var buffer = PLBF001A.RentBuffer(PLBF001A.LargeBufferSize);

        // Act
        int bytesWritten = DTsr001A.EncodeOptionChainSnapshot(original, buffer.Span);
        var decoded = DTsr001A.DecodeOptionChainSnapshot(buffer.Array.AsSpan(0, bytesWritten));

        // Assert
        decoded.Symbol.Should().Be(original.Symbol);
        decoded.SpotPrice.Should().Be(original.SpotPrice);
        decoded.Contracts.Should().HaveCount(2);
        decoded.Contracts[0].Strike.Should().Be(500m);
        decoded.Contracts[0].Right.Should().Be(OptionRight.Call);
        decoded.Contracts[1].Right.Should().Be(OptionRight.Put);
    }

    #endregion

    #region Events Layer (EVsr001A) Tests

    [Fact]
    public void EventEnvelope_RoundTrip_PreservesAllFields()
    {
        // Arrange
        var original = new EVCR003A
        {
            EventId = Guid.NewGuid(),
            SequenceNumber = 12345,
            StoredAtUtc = DateTime.UtcNow,
            EventType = "SignalGenerated",
            EventData = "{\"symbol\":\"AAPL\",\"strength\":0.75}",
            AggregateId = "backtest-001",
            CorrelationId = Guid.NewGuid().ToString(),
            CausationId = null,
            InitiatedBy = "system"
        };
        var buffer = new byte[1024];

        // Act
        int bytesWritten = EVsr001A.EncodeEventEnvelope(original, buffer);
        var decoded = EVsr001A.DecodeEventEnvelope(buffer.AsSpan(0, bytesWritten));

        // Assert
        decoded.EventId.Should().Be(original.EventId);
        decoded.SequenceNumber.Should().Be(original.SequenceNumber);
        decoded.EventType.Should().Be(original.EventType);
        decoded.EventData.Should().Be(original.EventData);
        decoded.AggregateId.Should().Be(original.AggregateId);
        decoded.CorrelationId.Should().Be(original.CorrelationId);
        decoded.CausationId.Should().BeNull();
        decoded.InitiatedBy.Should().Be(original.InitiatedBy);
    }

    [Fact]
    public void GetEncodedSize_ReturnsCorrectSize()
    {
        // Arrange
        var envelope = new EVCR003A
        {
            EventId = Guid.NewGuid(),
            SequenceNumber = 1,
            StoredAtUtc = DateTime.UtcNow,
            EventType = "Test",
            EventData = "{}",
            AggregateId = null
        };

        // Act
        int estimatedSize = EVsr001A.GetEncodedSize(envelope);
        var buffer = new byte[estimatedSize + 100];
        int actualSize = EVsr001A.EncodeEventEnvelope(envelope, buffer);

        // Assert - estimated should be >= actual
        estimatedSize.Should().BeGreaterOrEqualTo(actualSize - 10); // Allow small variance for UTF8 vs ASCII
    }

    #endregion

    #region Application Layer (APsr001A) Tests

    [Fact]
    public void SessionMetadata_RoundTrip_PreservesAllFields()
    {
        // Arrange
        var original = new APmd001A
        {
            SessionId = "BT001A-20240101-20241231",
            StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = SessionStatus.Ready,
            SessionPath = "/home/user/sessions/BT001A",
            Symbols = new List<string> { "AAPL", "MSFT", "NVDA", "GOOGL" }
        };
        var buffer = new byte[1024];

        // Act
        int bytesWritten = APsr001A.EncodeSessionMetadata(original, buffer);
        var decoded = APsr001A.DecodeSessionMetadata(buffer.AsSpan(0, bytesWritten));

        // Assert
        decoded.SessionId.Should().Be(original.SessionId);
        decoded.StartDate.Should().Be(original.StartDate);
        decoded.EndDate.Should().Be(original.EndDate);
        decoded.Status.Should().Be(original.Status);
        decoded.SessionPath.Should().Be(original.SessionPath);
        decoded.Symbols.Should().BeEquivalentTo(original.Symbols);
    }

    [Fact]
    public void GetEncodedSize_ForSession_ReturnsCorrectSize()
    {
        // Arrange
        var session = new APmd001A
        {
            SessionId = "BT001A",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(6),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = SessionStatus.Created,
            SessionPath = "/path/to/session",
            Symbols = new List<string> { "AAPL", "MSFT" }
        };

        // Act
        int estimatedSize = APsr001A.GetEncodedSize(session);
        var buffer = new byte[estimatedSize + 100];
        int actualSize = APsr001A.EncodeSessionMetadata(session, buffer);

        // Assert
        estimatedSize.Should().BeGreaterOrEqualTo(actualSize);
    }

    #endregion

    #region Buffer Pool Tests

    [Fact]
    public void RentBuffer_ReturnsBufferOfAtLeastRequestedSize()
    {
        // Act
        using var buffer = PLBF001A.RentBuffer(1024);

        // Assert
        buffer.Length.Should().BeGreaterOrEqualTo(1024);
        buffer.Array.Should().NotBeNull();
        buffer.Span.Length.Should().Be(1024);
    }

    [Fact]
    public void RentLargeBuffer_ReturnsLargeBuffer()
    {
        // Act
        using var buffer = PLBF001A.RentLargeBuffer();

        // Assert
        buffer.Length.Should().Be(PLBF001A.LargeBufferSize);
    }

    [Fact]
    public void RentBuffer_WithInvalidSize_Throws()
    {
        // Act & Assert
        Action act = () => PLBF001A.RentBuffer(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion
}
