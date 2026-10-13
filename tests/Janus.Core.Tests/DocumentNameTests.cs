using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// A legal document's name as a route carries it and as a caller in process gives it
/// (INT-SMS-003, CONV-DESIGN-004, CONV-DESIGN-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class DocumentNameTests
{
    /// <summary>
    /// INT-SMS-003 AC3: a name of 1 to 64 lower-case letters and digits separated by
    /// single full stops, hyphens or underscores is read as it is written.
    /// </summary>
    /// <param name="name">A name inside the rule.</param>
    [Theory]
    [InlineData("a")]
    [InlineData("privacy-notice")]
    [InlineData("terms.of_service-2")]
    [InlineData("a123456789a123456789a123456789a123456789a123456789a123456789a123")]
    public void INT_SMS_003_AC3_ADocumentNameInsideTheRuleIsRead(string name)
    {
        Assert.True(DocumentName.TryParse(name, out DocumentName read));
        Assert.Equal(name, read.ToString());
        Assert.Equal(read, DocumentName.Parse(name));
    }

    /// <summary>
    /// INT-SMS-003 AC3: a name outside the rule makes no document name, so no contract
    /// that takes one can be handed it.
    /// </summary>
    /// <param name="name">A name outside the rule.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Terms")]
    [InlineData("terms of service")]
    [InlineData("terms..of-service")]
    [InlineData("-terms")]
    [InlineData("terms.")]
    [InlineData("termes-et-conditions-générales")]
    [InlineData("a123456789a123456789a123456789a123456789a123456789a123456789a1234")]
    public void INT_SMS_003_AC3_ADocumentNameOutsideTheRuleIsNotRead(string name)
    {
        Assert.False(DocumentName.TryParse(name, out _));
        Assert.False(DocumentName.TryParse(null, out _));
        Assert.Throws<ArgumentException>(() => DocumentName.Parse(name));
    }

    /// <summary>
    /// CONV-DESIGN-006: the text a route carries reads as the value it names, and text
    /// outside the rule is not read at all.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute()
    {
        Assert.True(Reads("terms-of-service", out DocumentName read));
        Assert.Equal("terms-of-service", read.ToString());
        Assert.Equal(read, Read<DocumentName>("terms-of-service"));
        Assert.False(Reads<DocumentName>("Terms Of Service", out _));
        Assert.False(Reads<DocumentName>(null, out _));
        Assert.Throws<FormatException>(() => Read<DocumentName>("Terms Of Service"));
    }

    /// <summary>
    /// CONV-DESIGN-004 AC3: a name that was never read has no text to give.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_004_AC3_AnUnsetDocumentNameGivesNoText() =>
        Assert.Throws<InvalidOperationException>(() => default(DocumentName).ToString());

    private static bool Reads<TValue>(string? text, out TValue value)
        where TValue : struct, IParsable<TValue> =>
        TValue.TryParse(text, provider: null, out value);

    private static TValue Read<TValue>(string text)
        where TValue : IParsable<TValue> =>
        TValue.Parse(text, provider: null);
}
