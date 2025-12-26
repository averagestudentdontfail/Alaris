// STDT002A.cs - SoA (Struct of Arrays) memory layout for SIMD-friendly option chain data

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Alaris.Strategy.Core.DataTypes;

/// <summary>
/// SoA (Struct of Arrays) memory layout for option chain data.
/// Component ID: STDT002A
/// </summary>
/// <remarks>
/// <para>
/// SoA layout enables efficient SIMD processing by keeping related data in contiguous memory:
/// <list type="bullet">
///   <item><description>Strikes - all strike prices contiguous</description></item>
///   <item><description>Expiries - all expiry times contiguous</description></item>
///   <item><description>IVs - all implied volatilities contiguous</description></item>
///   <item><description>Deltas - all deltas contiguous</description></item>
/// </list>
/// </para>
/// <para>
/// This is more cache-efficient than AoS (Array of Structs) for vectorized loops
/// that process one field at a time across all options.
/// </para>
/// </remarks>
[SuppressMessage("Performance", "CA1819:Properties should not return arrays", 
    Justification = "SoA pattern requires direct array access for SIMD processing. Arrays are internal storage.")]
public sealed class OptionChainSoA
{
    private readonly double[] _strikes;
    private readonly double[] _taus;
    private readonly double[] _ivs;
    private readonly bool[] _isCalls;
    private readonly double[] _prices;
    private readonly double[] _deltas;
    private readonly double[] _gammas;
    private readonly double[] _vegas;

    /// <summary>Number of contracts in the chain.</summary>
    public int Count { get; }

    /// <summary>
    /// Creates a new SoA option chain with the specified capacity.
    /// </summary>
    /// <param name="count">Number of contracts.</param>
    public OptionChainSoA(int count)
    {
        Count = count;
        _strikes = new double[count];
        _taus = new double[count];
        _ivs = new double[count];
        _isCalls = new bool[count];
        _prices = new double[count];
        _deltas = new double[count];
        _gammas = new double[count];
        _vegas = new double[count];
    }

    /// <summary>Gets a span over strike prices.</summary>
    public Span<double> Strikes => _strikes.AsSpan();

    /// <summary>Gets a span over times to expiry.</summary>
    public Span<double> Taus => _taus.AsSpan();

    /// <summary>Gets a span over implied volatilities.</summary>
    public Span<double> IVs => _ivs.AsSpan();

    /// <summary>Gets a span over call/put flags.</summary>
    public Span<bool> IsCalls => _isCalls.AsSpan();

    /// <summary>Gets a span over computed prices.</summary>
    public Span<double> Prices => _prices.AsSpan();

    /// <summary>Gets a span over computed deltas.</summary>
    public Span<double> Deltas => _deltas.AsSpan();

    /// <summary>Gets a span over computed gammas.</summary>
    public Span<double> Gammas => _gammas.AsSpan();

    /// <summary>Gets a span over computed vegas.</summary>
    public Span<double> Vegas => _vegas.AsSpan();

    /// <summary>
    /// Creates a SoA option chain from contract arrays.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OptionChainSoA FromArrays(
        double[] strikes,
        double[] taus,
        double[] ivs,
        bool[] isCalls)
    {
        ArgumentNullException.ThrowIfNull(strikes);
        ArgumentNullException.ThrowIfNull(taus);
        ArgumentNullException.ThrowIfNull(ivs);
        ArgumentNullException.ThrowIfNull(isCalls);

        int count = strikes.Length;
        OptionChainSoA chain = new(count);
        Array.Copy(strikes, chain._strikes, count);
        Array.Copy(taus, chain._taus, count);
        Array.Copy(ivs, chain._ivs, count);
        Array.Copy(isCalls, chain._isCalls, count);
        return chain;
    }
}

/// <summary>
/// SoA layout for market data snapshots.
/// </summary>
[SuppressMessage("Performance", "CA1819:Properties should not return arrays",
    Justification = "SoA pattern requires direct array access for SIMD processing.")]
public sealed class MarketDataSoA
{
    private readonly string[] _symbols;
    private readonly double[] _bids;
    private readonly double[] _asks;
    private readonly int[] _bidSizes;
    private readonly int[] _askSizes;
    private readonly double[] _lasts;

    /// <summary>Number of instruments.</summary>
    public int Count { get; }

    /// <summary>
    /// Creates a new SoA market data container.
    /// </summary>
    public MarketDataSoA(int count)
    {
        Count = count;
        _symbols = new string[count];
        _bids = new double[count];
        _asks = new double[count];
        _bidSizes = new int[count];
        _askSizes = new int[count];
        _lasts = new double[count];
    }

    /// <summary>Gets a span over ticker symbols.</summary>
    public Span<string> Symbols => _symbols.AsSpan();

    /// <summary>Gets a span over bid prices.</summary>
    public Span<double> Bids => _bids.AsSpan();

    /// <summary>Gets a span over ask prices.</summary>
    public Span<double> Asks => _asks.AsSpan();

    /// <summary>Gets a span over bid sizes.</summary>
    public Span<int> BidSizes => _bidSizes.AsSpan();

    /// <summary>Gets a span over ask sizes.</summary>
    public Span<int> AskSizes => _askSizes.AsSpan();

    /// <summary>Gets a span over last trade prices.</summary>
    public Span<double> Lasts => _lasts.AsSpan();

    /// <summary>
    /// Gets mid prices using vectorized computation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ComputeMidPrices(Span<double> mids)
    {
        for (int i = 0; i < Count; i++)
        {
            mids[i] = (_bids[i] + _asks[i]) * 0.5;
        }
    }

    /// <summary>
    /// Gets bid-ask spreads.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ComputeSpreads(Span<double> spreads)
    {
        for (int i = 0; i < Count; i++)
        {
            spreads[i] = _asks[i] - _bids[i];
        }
    }
}
