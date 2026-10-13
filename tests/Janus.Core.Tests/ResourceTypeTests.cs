using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// A resource type as a route or a query carries it (CONV-DESIGN-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class ResourceTypeTests
{
    /// <summary>
    /// CONV-DESIGN-006: the text a route carries reads as the value it names, and text
    /// outside the rule is not read at all.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute()
    {
        Assert.True(Reads("invoice", out ResourceType read));
        Assert.Equal("invoice", read.ToString());
        Assert.Equal(read, Read<ResourceType>("invoice"));
        Assert.False(Reads<ResourceType>("Invoice!", out _));
        Assert.False(Reads<ResourceType>(null, out _));
        Assert.Throws<FormatException>(() => Read<ResourceType>("Invoice!"));
    }

    private static bool Reads<TValue>(string? text, out TValue value)
        where TValue : struct, IParsable<TValue> =>
        TValue.TryParse(text, provider: null, out value);

    private static TValue Read<TValue>(string text)
        where TValue : IParsable<TValue> =>
        TValue.Parse(text, provider: null);
}
