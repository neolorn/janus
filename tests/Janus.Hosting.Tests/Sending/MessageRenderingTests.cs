using System;
using System.Collections.Generic;
using Janus.Hosting.Sending;
using Xunit;

namespace Janus.Hosting.Tests.Sending;

/// <summary>
/// Putting the library's values into the places the deployment's template leaves for
/// them, and touching nothing else (CONV-CONTENT-001, AUTH-ABUSE-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class MessageRenderingTests
{
    /// <summary>
    /// CONV-CONTENT-001 AC1: the words around the places are the deployment's and
    /// come through as they were written.
    /// </summary>
    [Fact]
    public void CONV_CONTENT_001_AC1_TheNamedPlacesAreFilledAndTheWordsAreNotTouched()
    {
        string filled = MessageRendering.Fill(
            "your code is {code}, asked for {identifier}",
            new Dictionary<string, string>(capacity: 2, StringComparer.Ordinal)
            {
                ["code"] = "123456",
                ["identifier"] = "you@example.test",
            });

        Assert.Equal("your code is 123456, asked for you@example.test", filled);
    }

    /// <summary>
    /// AUTH-ABUSE-005: a place the values do not name is left as it stands, so a
    /// message that supplies nothing is never silently emptied of its meaning, and
    /// nothing the caller did not supply can appear in it.
    /// </summary>
    [Fact]
    public void Fill_APlaceTheValuesDoNotName_IsLeftAsItStands()
    {
        string filled = MessageRendering.Fill(
            "asked from {source} for {identifier}",
            new Dictionary<string, string>(capacity: 0, StringComparer.Ordinal));

        Assert.Equal("asked from {source} for {identifier}", filled);
    }
}
