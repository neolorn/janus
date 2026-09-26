using System;
using Janus.Core;
using Janus.Privacy.Erasures;
using Xunit;

namespace Janus.Privacy.Tests.Erasures;

/// <summary>
/// The one form a line of the off-host erasure ledger takes, and the reading of it the
/// replay relies on (DR-016).
/// </summary>
[Trait("kind", "unit")]
public sealed class ErasureLedgerLineTests
{
    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    /// <summary>
    /// DR-016: every reason is written in the spelling of chapter 10 section 5.12a and
    /// read back as the erasure it was written for.
    /// </summary>
    /// <param name="reason">Why the erasure happened.</param>
    /// <param name="spelled">The reason as the line holds it.</param>
    [Theory]
    [InlineData(ErasureReason.ErasureRequest, "erasure-request")]
    [InlineData(ErasureReason.MinorTakedown, "minor-takedown")]
    [InlineData(ErasureReason.OrganizationErasure, "organization-erasure")]
    public void DR_016_ALineIsReadAsTheErasureItWasWrittenFor(ErasureReason reason, string spelled)
    {
        var line = new ErasureLedgerLine(
            new DateTimeOffset(2026, 8, 14, 9, 22, 5, TimeSpan.Zero),
            Ahmed,
            reason);

        Assert.Equal("2026-08-14T09:22:05Z 11111111-1111-4111-8111-111111111111 " + spelled, line.Written());
        Assert.Equal(line, ErasureLedgerLine.Read(line.Written()));
    }

    /// <summary>
    /// DR-016: a line in any form but the one written, the minute-precision and
    /// double-spaced form of the chapter's illustration among them, is not read, so a
    /// ledger edited by hand is refused rather than half understood.
    /// </summary>
    /// <param name="line">The line.</param>
    [Theory]
    [InlineData("2026-08-14T09:22Z  11111111-1111-4111-8111-111111111111  erasure-request")]
    [InlineData("2026-08-14T09:22Z 11111111-1111-4111-8111-111111111111 erasure-request")]
    [InlineData("2026-08-14T09:22:05Z 11111111-1111-4111-8111-111111111111 erasure-request ")]
    [InlineData("2026-08-14T09:22:05.250Z 11111111-1111-4111-8111-111111111111 erasure-request")]
    [InlineData("2026-08-14T11:22:05+02:00 11111111-1111-4111-8111-111111111111 erasure-request")]
    [InlineData("2026-08-14T09:22:05Z 11111111-1111-4111-8111-11111111111A erasure-request")]
    [InlineData("2026-08-14T09:22:05Z {11111111-1111-4111-8111-111111111111} erasure-request")]
    [InlineData("2026-08-14T09:22:05Z 11111111-1111-4111-8111-111111111111 ErasureRequest")]
    [InlineData("2026-08-14T09:22:05Z 11111111-1111-4111-8111-111111111111 0")]
    [InlineData("2026-08-14T09:22:05Z 11111111-1111-4111-8111-111111111111")]
    [InlineData("2026-08-14T09:22:05Z\t11111111-1111-4111-8111-111111111111\terasure-request")]
    [InlineData("")]
    public void DR_016_ALineInAnyOtherFormIsNotRead(string line) =>
        Assert.Null(ErasureLedgerLine.Read(line));
}
