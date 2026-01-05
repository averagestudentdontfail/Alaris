// CROP002ATests.cs - Unit tests for VanillaPayoff
// Tests payoff calculations for call and put options

using Alaris.Core.Options;
using Xunit;

namespace Alaris.Test.Unit.Core.Options;

public class VanillaPayoffTests
{
    #region Call Payoff Tests

    [Fact]
    public void CallPayoff_ITM_ReturnsPositive()
    {
        var payoff = new VanillaPayoff(OptionType.Call, 100.0);
        
        double result = payoff.Payoff(110.0);
        
        Assert.Equal(10.0, result);
    }

    [Fact]
    public void CallPayoff_ATM_ReturnsZero()
    {
        var payoff = new VanillaPayoff(OptionType.Call, 100.0);
        
        double result = payoff.Payoff(100.0);
        
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void CallPayoff_OTM_ReturnsZero()
    {
        var payoff = new VanillaPayoff(OptionType.Call, 100.0);
        
        double result = payoff.Payoff(90.0);
        
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void CallPayoff_DeepITM_ReturnsLargeValue()
    {
        var payoff = new VanillaPayoff(OptionType.Call, 100.0);
        
        double result = payoff.Payoff(200.0);
        
        Assert.Equal(100.0, result);
    }

    #endregion

    #region Put Payoff Tests

    [Fact]
    public void PutPayoff_ITM_ReturnsPositive()
    {
        var payoff = new VanillaPayoff(OptionType.Put, 100.0);
        
        double result = payoff.Payoff(90.0);
        
        Assert.Equal(10.0, result);
    }

    [Fact]
    public void PutPayoff_ATM_ReturnsZero()
    {
        var payoff = new VanillaPayoff(OptionType.Put, 100.0);
        
        double result = payoff.Payoff(100.0);
        
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void PutPayoff_OTM_ReturnsZero()
    {
        var payoff = new VanillaPayoff(OptionType.Put, 100.0);
        
        double result = payoff.Payoff(110.0);
        
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void PutPayoff_DeepITM_ReturnsLargeValue()
    {
        var payoff = new VanillaPayoff(OptionType.Put, 100.0);
        
        double result = payoff.Payoff(20.0);
        
        Assert.Equal(80.0, result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Payoff_ZeroSpot_PutReturnsStrike()
    {
        var payoff = new VanillaPayoff(OptionType.Put, 100.0);
        
        double result = payoff.Payoff(0.0);
        
        Assert.Equal(100.0, result);
    }

    [Fact]
    public void Strike_Property_ReturnsCorrectValue()
    {
        double expectedStrike = 123.45;
        var payoff = new VanillaPayoff(OptionType.Call, expectedStrike);
        
        Assert.Equal(expectedStrike, payoff.Strike);
    }

    [Fact]
    public void Type_Property_ReturnsCorrectValue()
    {
        var callPayoff = new VanillaPayoff(OptionType.Call, 100.0);
        var putPayoff = new VanillaPayoff(OptionType.Put, 100.0);
        
        Assert.Equal(OptionType.Call, callPayoff.Type);
        Assert.Equal(OptionType.Put, putPayoff.Type);
    }

    [Fact]
    public void PayoffSigned_MatchesPayoff()
    {
        var callPayoff = new VanillaPayoff(OptionType.Call, 100.0);
        var putPayoff = new VanillaPayoff(OptionType.Put, 100.0);
        
        Assert.Equal(callPayoff.Payoff(110.0), callPayoff.PayoffSigned(110.0));
        Assert.Equal(putPayoff.Payoff(90.0), putPayoff.PayoffSigned(90.0));
    }

    #endregion
}

public class OptionTypeTests
{
    [Fact]
    public void Sign_Call_ReturnsPositiveOne()
    {
        Assert.Equal(1, OptionType.Call.Sign());
    }

    [Fact]
    public void Sign_Put_ReturnsNegativeOne()
    {
        Assert.Equal(-1, OptionType.Put.Sign());
    }

    [Fact]
    public void IsCall_Call_ReturnsTrue()
    {
        Assert.True(OptionType.Call.IsCall());
        Assert.False(OptionType.Call.IsPut());
    }

    [Fact]
    public void IsPut_Put_ReturnsTrue()
    {
        Assert.True(OptionType.Put.IsPut());
        Assert.False(OptionType.Put.IsCall());
    }
}
