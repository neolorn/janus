using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// A sending restriction's name as a route carries it and as a caller in process gives it
/// (INT-SMS-003, CONV-DESIGN-004, CONV-DESIGN-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class RestrictionNameTests
{
    /// <summary>
    /// INT-SMS-003 AC3: a name of 1 to 64 lower-case letters and digits separated by
    /// single full stops, hyphens or underscores is read as it is written.
    /// </summary>
    /// <param name="name">A name inside the rule.</param>
    [Theory]
    [InlineData("a")]
    [InlineData("sms.destination")]
    [InlineData("email.account_2-day")]
    [InlineData("a123456789a123456789a123456789a123456789a123456789a123456789a123")]
    public void INT_SMS_003_AC3_ARestrictionNameInsideTheRuleIsRead(string name)
    {
        Assert.True(RestrictionName.TryParse(name, out RestrictionName read));
        Assert.Equal(name, read.ToString());
        Assert.Equal(read, RestrictionName.Parse(name));
    }

    /// <summary>
    /// INT-SMS-003 AC3: a name outside the rule makes no restriction name, so no contract
    /// that takes one can be handed it.
    /// </summary>
    /// <param name="name">A name outside the rule.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Sms")]
    [InlineData("sms destination")]
    [InlineData("sms..destination")]
    [InlineData("-sms")]
    [InlineData("sms.")]
    [InlineData("sms.déstination")]
    [InlineData("a123456789a123456789a123456789a123456789a123456789a123456789a1234")]
    public void INT_SMS_003_AC3_ARestrictionNameOutsideTheRuleIsNotRead(string name)
    {
        Assert.False(RestrictionName.TryParse(name, out _));
        Assert.False(RestrictionName.TryParse(null, out _));
        Assert.Throws<ArgumentException>(() => RestrictionName.Parse(name));
    }

    /// <summary>
    /// CONV-DESIGN-006: the text a route carries reads as the value it names, and text
    /// outside the rule is not read at all.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute()
    {
        Assert.True(Reads("sms.destination", out RestrictionName read));
        Assert.Equal("sms.destination", read.ToString());
        Assert.Equal(read, Read<RestrictionName>("sms.destination"));
        Assert.False(Reads<RestrictionName>("SMS Destination", out _));
        Assert.False(Reads<RestrictionName>(null, out _));
        Assert.Throws<FormatException>(() => Read<RestrictionName>("SMS Destination"));
    }

    /// <summary>
    /// CONV-DESIGN-004 AC3: a name that was never read has no text to give.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_004_AC3_AnUnsetRestrictionNameGivesNoText() =>
        Assert.Throws<InvalidOperationException>(() => default(RestrictionName).ToString());

    private static bool Reads<TValue>(string? text, out TValue value)
        where TValue : struct, IParsable<TValue> =>
        TValue.TryParse(text, provider: null, out value);

    private static TValue Read<TValue>(string text)
        where TValue : IParsable<TValue> =>
        TValue.Parse(text, provider: null);
}
