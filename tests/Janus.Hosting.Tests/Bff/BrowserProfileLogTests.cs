using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Janus.Hosting.Bff;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// The entries the browser profile's log declares (CONV-LOG-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class BrowserProfileLogTests
{
    /// <summary>
    /// CONV-LOG-001: an entry whose last call was removed is declared no longer, and
    /// neither its event identifier nor its event name is given to another entry of
    /// the log.
    /// </summary>
    /// <param name="identifier">The event identifier the removed entry held.</param>
    /// <param name="name">The event name the removed entry held.</param>
    [Theory]
    [InlineData(10, "SignOnRefused")]
    [InlineData(11, "SignOnExchangeRejected")]
    [InlineData(14, "SignOnPushRejected")]
    public void CONV_LOG_001_ARemovedEntrysIdentifierAndNameAreGivenToNoOther(int identifier, string name)
    {
        IReadOnlyList<(int EventId, string EventName)> declared = Declared();

        Assert.NotEmpty(declared);
        Assert.DoesNotContain(declared, entry => entry.EventId == identifier);
        Assert.DoesNotContain(declared, entry => string.Equals(entry.EventName, name, StringComparison.Ordinal));
    }

    /// <summary>
    /// CONV-LOG-001: no two entries of the log share an event identifier or an event
    /// name, so a query kept on either matches one event.
    /// </summary>
    [Fact]
    public void CONV_LOG_001_NoTwoEntriesShareAnIdentifierOrAName()
    {
        IReadOnlyList<(int EventId, string EventName)> declared = Declared();

        Assert.Equal(declared.Count, declared.Select(entry => entry.EventId).Distinct().Count());
        Assert.Equal(
            declared.Count,
            declared.Select(entry => entry.EventName).Distinct(StringComparer.Ordinal).Count());
    }

    // Every entry the log declares, by the identifier and the name its generated
    // method fixes: the name is the method's where the declaration gives none.
    private static IReadOnlyList<(int EventId, string EventName)> Declared() =>
        [
            .. typeof(BrowserProfileLog)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(method => (Method: method, Entry: method.GetCustomAttribute<LoggerMessageAttribute>()))
                .Where(found => found.Entry is not null)
                .Select(found => (found.Entry!.EventId, found.Entry.EventName ?? found.Method.Name)),
        ];
}
