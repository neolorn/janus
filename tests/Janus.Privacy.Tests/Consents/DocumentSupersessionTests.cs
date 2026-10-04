using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Consents;
using Xunit;

namespace Janus.Privacy.Tests.Consents;

/// <summary>
/// What a start does to the consents recorded against a document their purpose no
/// longer names: each is stamped superseded and announced once (PRIV-CONS-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class DocumentSupersessionTests : IAsyncDisposable
{
    private const string Recommendations = "recommendations";

    private const string Newsletter = "newsletter";

    private const string Security = "security";

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly SubjectId Mona =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private readonly ConsentStoreInMemory _consents = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon.AddDays(30));
    private readonly EventsInMemory _events;

    /// <summary>
    /// A deployment whose events are written in the start's transaction.
    /// </summary>
    public DocumentSupersessionTests() => _events = new EventsInMemory { Work = _work };

    private DocumentSupersession Start =>
        new(_consents, Declaration.Processing, _events, _work, _clock);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _work.DisposeAsync();

    /// <summary>
    /// PRIV-CONS-007 AC5: a live consent recorded against a document its purpose no
    /// longer names is stamped superseded at the start, and announced in the same
    /// transaction.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC5_AStartStampsAConsentAgainstAnotherDocumentAndAnnouncesItAsync()
    {
        _consents.Keep(Ahmed, Held(Newsletter, ConsentService.Notice));

        int ended = await Start.SupersededAsync(CancellationToken.None);

        ConsentRecord held = Assert.Single(await _consents.ConsentsAsync(Ahmed, CancellationToken.None));
        ConsentChanged raised = Assert.Single(_events.Of<ConsentChanged>());

        Assert.Equal(1, ended);
        Assert.Equal(Noon.AddDays(30), held.SupersededAt);
        Assert.Null(held.WithdrawnAt);
        Assert.Equal((Ahmed, Newsletter, ConsentChange.Superseded), (raised.Subject, raised.Purpose, raised.Change));
        Assert.Equal(_events.Published, _events.PublishedInTransaction);
        Assert.Equal((1, 1, 0), (_work.Opened, _work.Committed, _work.RolledBack));
    }

    /// <summary>
    /// PRIV-CONS-007 AC5: a second start finds the consent stamped and neither stamps
    /// nor announces it again.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC5_ASecondStartStampsAndAnnouncesNothingAsync()
    {
        _consents.Keep(Ahmed, Held(Newsletter, ConsentService.Notice));

        _ = await Start.SupersededAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(1));

        int again = await Start.SupersededAsync(CancellationToken.None);

        ConsentRecord held = Assert.Single(await _consents.ConsentsAsync(Ahmed, CancellationToken.None));

        Assert.Equal(0, again);
        Assert.Equal(Noon.AddDays(30), held.SupersededAt);
        Assert.Single(_events.Of<ConsentChanged>());
    }

    /// <summary>
    /// PRIV-CONS-007 AC2, AC5: a consent recorded against the document its purpose
    /// names, whichever that is, stands, as does one already withdrawn or superseded
    /// and one of a purpose resting on another basis.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC5_AStartTouchesNoConsentAgainstTheDocumentItsPurposeNamesAsync()
    {
        _consents.Keep(Ahmed, Held(Recommendations, ConsentService.Notice));
        _consents.Keep(Ahmed, Held(Newsletter, Declaration.Newsletter));
        _consents.Keep(Ahmed, Held(Security, "another-document"));
        _consents.Keep(Mona, Held(Newsletter, ConsentService.Notice) with { WithdrawnAt = Noon.AddDays(1) });
        _consents.Keep(Mona, Held(Recommendations, "another-document") with { SupersededAt = Noon.AddDays(2) });

        int ended = await Start.SupersededAsync(CancellationToken.None);

        Assert.Equal(0, ended);
        Assert.All(
            await _consents.ConsentsAsync(Ahmed, CancellationToken.None),
            held => Assert.True(held.Live));
        Assert.Equal(
            [Noon.AddDays(2), null],
            (await _consents.ConsentsAsync(Mona, CancellationToken.None))
                .OrderByDescending(held => held.Purpose, StringComparer.Ordinal)
                .Select(held => held.SupersededAt));
        Assert.Empty(_events.Published);
    }

    /// <summary>
    /// PRIV-CONS-007 AC5, CONV-DESIGN-003: a consent that cannot be announced leaves
    /// the start's transaction rolled back, and the start fails naming the code.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC5_AConsentThatIsNotAnnouncedRollsTheStartBackAsync()
    {
        _consents.Keep(Ahmed, Held(Newsletter, ConsentService.Notice));
        _events.Refusal = Error.From(ErrorCodes.SystemFault);

        InvalidOperationException fault = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Start.SupersededAsync(CancellationToken.None));

        Assert.Equal(ErrorCodes.SystemFault.ToString(), fault.Message);
        Assert.Equal((1, 0, 1), (_work.Opened, _work.Committed, _work.RolledBack));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC7: the start returns no result, so a transaction that will not
    /// begin, or will not commit, is thrown as a fault naming the failure's code.
    /// </summary>
    /// <param name="atCommit">Whether the commit, and not the beginning, fails.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CONV_DESIGN_003_AC7_ATransactionThatFailsIsAFaultNamingItsCodeAsync(bool atCommit)
    {
        if (atCommit)
        {
            _work.RefusesCommit = Error.From(ErrorCodes.SystemFault);
        }
        else
        {
            _work.RefusesBegin = Error.From(ErrorCodes.SystemFault);
        }

        InvalidOperationException fault = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Start.SupersededAsync(CancellationToken.None));

        Assert.Equal(ErrorCodes.SystemFault.ToString(), fault.Message);
    }

    private static ConsentRecord Held(string purpose, string document) =>
        new(
            purpose,
            document,
            "1",
            ConsentMechanism.Dashboard,
            ConsentKind.Ordinary,
            Noon,
            WithdrawnAt: null,
            SupersededAt: null);
}
