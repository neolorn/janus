using System;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Core.Tests.Configuration;

/// <summary>
/// A configuration key as a route or a query carries it (CONV-DESIGN-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class ConfigurationKeyTests
{
    /// <summary>
    /// CONV-DESIGN-006: the text a route carries reads as the value it names, and text
    /// outside the rule is not read at all.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute()
    {
        Assert.True(Reads("session.idle.timeout", out ConfigurationKey read));
        Assert.Equal("session.idle.timeout", read.ToString());
        Assert.Equal(read, Read<ConfigurationKey>("session.idle.timeout"));
        Assert.False(Reads<ConfigurationKey>("Session..Idle", out _));
        Assert.False(Reads<ConfigurationKey>(null, out _));
        Assert.Throws<FormatException>(() => Read<ConfigurationKey>("Session..Idle"));
    }

    private static bool Reads<TValue>(string? text, out TValue value)
        where TValue : struct, IParsable<TValue> =>
        TValue.TryParse(text, provider: null, out value);

    private static TValue Read<TValue>(string text)
        where TValue : IParsable<TValue> =>
        TValue.Parse(text, provider: null);
}
