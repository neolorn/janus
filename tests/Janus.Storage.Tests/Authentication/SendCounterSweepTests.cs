using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What the counter sweep reads, which is what the index it relies on is over
/// (AUTH-ABUSE-004).
/// </summary>
/// <remarks>
/// The model is built from the design-time factory, which connects to nothing, so this
/// reads the statement and not a database.
/// </remarks>
[Trait("kind", "unit")]
public sealed class SendCounterSweepTests
{
    /// <summary>
    /// AUTH-ABUSE-004 AC6: the sweep is one indexed delete, which holds only while the
    /// expression it reads is the expression the migration indexed. No model builder
    /// expresses that index, so this pins the pair.
    /// </summary>
    [Fact]
    public void AUTH_ABUSE_004_AC6_TheSweepReadsTheExpressionTheIndexIsOver()
    {
        using StoreContext context = new DesignTimeContextFactory().CreateDbContext([]);

        string swept = context.SendCounters
            .Where(counter => counter.SentAt[counter.SentAt.Length - 1] < DateTimeOffset.UnixEpoch)
            .ToQueryString();

        // The statement aliases the table and the index is over the same expression
        // written without the alias.
        Assert.Contains(
            "sent_at[(cardinality(sent_at) - 1) + 1]",
            swept.Replace("s.", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
    }
}
