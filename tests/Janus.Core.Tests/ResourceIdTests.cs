using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// A resource identifier as a route or a query carries it (CONV-DESIGN-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class ResourceIdTests
{
    /// <summary>
    /// CONV-DESIGN-006: the text a route carries reads as the value it names, and text
    /// outside the rule is not read at all.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute()
    {
        Assert.True(Reads("INV-0042", out ResourceId read));
        Assert.Equal("INV-0042", read.ToString());
        Assert.Equal(read, Read<ResourceId>("INV-0042"));
        Assert.False(Reads<ResourceId>(" ", out _));
        Assert.False(Reads<ResourceId>(null, out _));
        Assert.Throws<FormatException>(() => Read<ResourceId>(" "));
    }

    private static bool Reads<TValue>(string? text, out TValue value)
        where TValue : struct, IParsable<TValue> =>
        TValue.TryParse(text, provider: null, out value);

    private static TValue Read<TValue>(string text)
        where TValue : IParsable<TValue> =>
        TValue.Parse(text, provider: null);
}
