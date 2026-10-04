using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// A mail app password's identifier, which the mail server draws and the library keeps
/// in the server's form (CONV-DESIGN-004, CONV-DESIGN-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class AppPasswordIdTests
{
    /// <summary>
    /// CONV-DESIGN-004: 1 to 255 octets of the URL and filename safe base64 alphabet
    /// read as an identifier, as the server wrote them.
    /// </summary>
    /// <param name="id">An identifier inside the form.</param>
    [Theory]
    [InlineData("a")]
    [InlineData("p0001")]
    [InlineData("Ab-9_z")]
    [InlineData("-")]
    public void CONV_DESIGN_004_AnIdentifierInsideItsFormIsRead(string id)
    {
        Assert.True(AppPasswordId.TryParse(id, out AppPasswordId read));
        Assert.Equal(id, read.ToString());
        Assert.Equal(read, AppPasswordId.Parse(id));
        Assert.True(AppPasswordId.TryParse(new string('a', 255), out _));
    }

    /// <summary>
    /// CONV-DESIGN-004 AC3: text outside the form makes no identifier: nothing, more than
    /// 255 octets, the pad, and any character outside the alphabet.
    /// </summary>
    /// <param name="id">Text outside the form.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("p0001=")]
    [InlineData("p+0001")]
    [InlineData("p/0001")]
    [InlineData("p.0001")]
    [InlineData("p 0001")]
    [InlineData("pé0001")]
    public void CONV_DESIGN_004_AC3_TextOutsideTheFormMakesNoIdentifier(string id)
    {
        Assert.False(AppPasswordId.TryParse(id, out _));
        Assert.False(AppPasswordId.TryParse(null, out _));
        Assert.False(AppPasswordId.TryParse(new string('a', 256), out _));
        Assert.Throws<ArgumentException>(() => AppPasswordId.Parse(id));
    }

    /// <summary>
    /// CONV-DESIGN-006: the text a route carries reads as the value it names, and text
    /// outside the form is not read at all.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute()
    {
        Assert.True(Reads("p0001", out AppPasswordId read));
        Assert.Equal("p0001", read.ToString());
        Assert.Equal(read, Read<AppPasswordId>("p0001"));
        Assert.False(Reads<AppPasswordId>("p0001=", out _));
        Assert.False(Reads<AppPasswordId>(null, out _));
        Assert.Throws<FormatException>(() => Read<AppPasswordId>("p0001="));
    }

    /// <summary>
    /// CONV-DESIGN-004 AC3: an identifier that was never read has no text to give.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_004_AC3_AnUnsetIdentifierGivesNoText() =>
        Assert.Throws<InvalidOperationException>(() => default(AppPasswordId).ToString());

    private static bool Reads<TValue>(string? text, out TValue value)
        where TValue : struct, IParsable<TValue> =>
        TValue.TryParse(text, provider: null, out value);

    private static TValue Read<TValue>(string text)
        where TValue : IParsable<TValue> =>
        TValue.Parse(text, provider: null);
}
