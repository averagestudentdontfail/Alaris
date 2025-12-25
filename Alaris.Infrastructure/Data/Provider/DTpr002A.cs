using System;
using System.Threading;
using System.Threading.Tasks;
using Alaris.Infrastructure.Data.Model;

namespace Alaris.Infrastructure.Data.Provider;

/// <summary>
/// Interface for execution quote providers (IBKR snapshots).
/// </summary>
public interface DTpr002A : IDisposable
{
    /// <summary>
    /// Gets a single option quote.
    /// </summary>
    Task<OptionContract> GetSnapshotQuoteAsync(
        string underlyingSymbol,
        decimal strike,
        DateTime expiration,
        OptionRight right,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a calendar spread quote (front and back months).
    /// </summary>
    Task<DTmd002A> GetDTmd002AAsync(
        string underlyingSymbol,
        decimal strike,
        DateTime frontExpiration,
        DateTime backExpiration,
        OptionRight right,
        CancellationToken cancellationToken = default);
}
