using System;

namespace Alaris.Data.Model;

/// <summary>
/// Calendar spread quote (combined front and back month).
/// </summary>
public sealed record DTmd002A
{
    /// <summary>Gets the underlying symbol.</summary>
    public required string UnderlyingSymbol { get; init; }

    /// <summary>Gets the strike price.</summary>
    public required decimal Strike { get; init; }

    /// <summary>Gets the front month quote.</summary>
    public required OptionContract FrontLeg { get; init; }
    
    /// <summary>Gets the back month quote.</summary>
    public required OptionContract BackLeg { get; init; }
    
    /// <summary>Gets the spread bid (buy back at ask, sell front at bid).</summary>
    public decimal SpreadBid => BackLeg.Ask - FrontLeg.Bid;
    
    /// <summary>Gets the spread ask (buy back at ask, sell front at bid).</summary>
    public decimal SpreadAsk => BackLeg.Ask - FrontLeg.Bid;
    
    /// <summary>Gets the spread mid price.</summary>
    public decimal SpreadMid => (SpreadBid + SpreadAsk) / 2m;
    
    /// <summary>Gets the spread width.</summary>
    public decimal SpreadWidth => SpreadAsk - SpreadBid;
    
    /// <summary>Gets the quote timestamp.</summary>
    public DateTime Timestamp { get; init; }
}
