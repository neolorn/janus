using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Consents;
using Janus.Privacy.Tests.Documents;
using Xunit;

namespace Janus.Privacy.Tests.Consents;

/// <summary>
/// What a consent record is: one purpose, the notice version displayed, the
/// mechanism, the capture path, and a withdrawal that sets a timestamp rather than
/// deleting anything.
/// </summary>
[Trait("kind", "unit")]
public sealed class ConsentTests : IAsyncDisposable
{
    private const string Recommendations = "recommendations";

    private const string Marketing = "marketing";

    private const string Fulfilment = "fulfilment";

    private const string Security = "security";

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private readonly ConsentStoreInMemory _consents = new();
    private readonly LegalDocumentStoreInMemory _documents = new();
    private readonly EventsInMemory _events = new();
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A deployment whose notice is published and whose purposes span the bases.
    /// </summary>
    public ConsentTests() =>
        _documents.Hold(new DocumentVersion(ConsentService.Notice, "1", "ar", "النص", [], Noon));

    private ConsentService Consents =>
        new(
            _consents,
            _documents,
            Declaration.Processing,
            _events,
            _audit,
            _work,
            _clock);

    private static AccessContext Acting => AccessContext.Of(Ahmed);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// PRIV-CONS-001 AC1, PRIV-CONS-002 AC1: consent for two purposes produces two
    /// records, each naming one purpose.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC1_ConsentForTwoPurposesProducesTwoRecordsAsync()
    {
        await GrantAsync(Recommendations);
        await GrantAsync(Marketing);

        IReadOnlyList<ConsentRecord> held = await HeldAsync();

        Assert.Equal(
            [Recommendations, Marketing],
            held.Select(record => record.Purpose).Order(StringComparer.Ordinal).Reverse());
        Assert.All(held, record => Assert.False(string.IsNullOrEmpty(record.Purpose)));
    }

    /// <summary>
    /// PRIV-CONS-001 AC2: the record names the notice version displayed, which
    /// resolves to the exact text shown.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC2_TheRecordNamesTheNoticeVersionDisplayedAsync()
    {
        await GrantAsync(Recommendations);

        ConsentRecord held = Assert.Single(await HeldAsync());
        DocumentVersion shown = await _documents
            .FindAsync(ConsentService.Notice, held.NoticeVersion, CancellationToken.None)
            ?? throw new InvalidOperationException("The version was not published.");

        Assert.Equal("1", held.NoticeVersion);
        Assert.Equal("النص", shown.Text);
    }

    /// <summary>
    /// PRIV-CONS-001 AC3: withdrawal sets a timestamp rather than deleting the
    /// record, which is the evidence the law asks for.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC3_WithdrawalSetsATimestampRatherThanDeletingAsync()
    {
        await GrantAsync(Recommendations);

        _clock.Advance(TimeSpan.FromDays(30));

        Assert.True(await WithdrawAsync(Recommendations));

        ConsentRecord held = Assert.Single(await HeldAsync());

        Assert.Equal(Noon, held.GrantedAt);
        Assert.Equal(Noon.AddDays(30), held.WithdrawnAt);
        Assert.False(held.Live);
    }

    /// <summary>
    /// PRIV-CONS-001: the record carries where it was given, so a consent taken at
    /// registration is distinguishable from one taken on the dashboard.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC1_TheRecordCarriesTheMechanismAsync()
    {
        await GrantAsync(Recommendations, ConsentMechanism.Registration);

        Assert.Equal(ConsentMechanism.Registration, Assert.Single(await HeldAsync()).Mechanism);
    }

    /// <summary>
    /// PRIV-CONS-004 AC1: the written-consent record is distinguishable from an
    /// ordinary one, because the capture path the basis and the sensitivity derive
    /// is on the record.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_004_AC1_TheWrittenRecordIsDistinguishableFromTheOrdinaryAsync()
    {
        await GrantAsync(Recommendations);
        await GrantAsync(Marketing);

        IReadOnlyList<ConsentRecord> held = await HeldAsync();

        Assert.Equal(
            ConsentKind.Written,
            held.Single(record => record.Purpose == Recommendations).Kind);
        Assert.Equal(
            ConsentKind.Ordinary,
            held.Single(record => record.Purpose == Marketing).Kind);
    }

    /// <summary>
    /// PRIV-CONS-004 AC2: the record is retrievable afterwards, withdrawn or not,
    /// because nothing deletes it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_004_AC2_TheWrittenRecordIsRetrievableAfterwardsAsync()
    {
        await GrantAsync(Recommendations);
        await WithdrawAsync(Recommendations);

        Assert.Equal(ConsentKind.Written, Assert.Single(await HeldAsync()).Kind);
    }

    /// <summary>
    /// PRIV-CONS-008 AC1, AC3: withdrawal is one call as granting is, and neither
    /// waits on a human.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_008_AC1_WithdrawalTakesNoMoreInteractionsThanGrantingAsync()
    {
        await GrantAsync(Recommendations);

        _work.Reset();

        Assert.True(await WithdrawAsync(Recommendations));
        Assert.Equal(1, _work.Opened);
        Assert.Equal(1, _work.Committed);
        Assert.False(Assert.Single(await HeldAsync()).Live);
    }

    /// <summary>
    /// PRIV-CONS-008 AC4: withdrawal announces the change for the purpose, which is
    /// what erases what was held solely for it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_008_AC4_WithdrawalAnnouncesTheChangeForThePurposeAsync()
    {
        await GrantAsync(Recommendations);
        await WithdrawAsync(Recommendations);

        IReadOnlyList<ConsentChanged> raised = _events.Of<ConsentChanged>();
        ConsentChanged announced = raised[^1];

        Assert.Equal(ConsentChange.Withdrawn, announced.Change);
        Assert.Equal(Recommendations, announced.Purpose);
        Assert.Equal(Ahmed, announced.Subject);
    }

    /// <summary>
    /// PRIV-SENS-002a AC4, PRIV-CONS-008a AC3: a purpose resting on another basis
    /// takes no consent record, so withdrawing one leaves it running.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_002a_AC4_APurposeOnAnotherBasisTakesNoConsentAsync()
    {
        Assert.False(await GrantAsync(Fulfilment));

        await GrantAsync(Recommendations);
        await WithdrawAsync(Recommendations);

        Assert.DoesNotContain(await HeldAsync(), record => record.Purpose == Fulfilment);
    }

    /// <summary>
    /// PRIV-RIGHT-001a AC1, AC2: a purpose on an objectable basis takes an objection
    /// record, and objecting announces it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001a_AC2_ObjectingWritesARecordAndAnnouncesItAsync()
    {
        Assert.True(await ObjectAsync(Security));

        ObjectionRecord held = Assert.Single(await ObjectionsAsync());
        ObjectionChanged announced = Assert.Single(_events.Of<ObjectionChanged>());

        Assert.Equal(Security, held.Purpose);
        Assert.Equal("1", held.NoticeVersion);
        Assert.Equal(Noon, held.RecordedAt);
        Assert.True(held.Standing);
        Assert.True(announced.Objecting);
        Assert.Equal(1, _work.Committed);
    }

    /// <summary>
    /// PRIV-RIGHT-001a AC5: a purpose whose basis is not objectable is refused with
    /// the code that says so.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001a_AC5_ANonObjectablePurposeIsRefusedAsync()
    {
        Result refused = await Consents.ObjectAsync(
            Acting,
            Recommendations,
            ConsentMechanism.Dashboard,
            CancellationToken.None);

        Assert.Equal(
            ErrorCodes.PurposeNotObjectable,
            refused.Match(() => default, error => error.Code));
        Assert.Empty(await ObjectionsAsync());
    }

    /// <summary>
    /// PRIV-RIGHT-001a AC4: withdrawing an objection takes effect at once and
    /// announces that processing may resume.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001a_AC4_WithdrawingAnObjectionTakesEffectWithoutApprovalAsync()
    {
        await ObjectAsync(Security);

        _clock.Advance(TimeSpan.FromDays(1));

        Result withdrawn = await Consents.WithdrawObjectionAsync(
            Acting,
            Security,
            CancellationToken.None);

        ObjectionRecord held = Assert.Single(await ObjectionsAsync());

        Assert.Null(withdrawn.Match(() => (Error?)null, error => error));
        Assert.Equal(Noon.AddDays(1), held.WithdrawnAt);
        Assert.False(held.Standing);
        IReadOnlyList<ObjectionChanged> raised = _events.Of<ObjectionChanged>();

        Assert.False(raised[^1].Objecting);
    }

    /// <summary>
    /// PRIV-CONS-011 AC1, AC2: every record the subject holds is theirs to read and
    /// theirs to withdraw, with nobody else in the way.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_011_AC1_EveryRecordHeldIsVisibleToItsSubjectAsync()
    {
        await GrantAsync(Recommendations);
        await GrantAsync(Marketing);
        await ObjectAsync(Security);

        Assert.Equal(2, (await HeldAsync()).Count);
        Assert.Single(await ObjectionsAsync());
    }

    /// <summary>
    /// PRIV-CONS-011: records belong to the subject, so a caller who is nobody reads
    /// none.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_011_AC1_ACallerWithNoSubjectReadsNothingAsync()
    {
        Result<IReadOnlyList<ConsentRecord>> refused = await Consents.ReadAsync(
            AccessContext.Of(SystemPrincipal.ForDeployment(
                "records",
                "Generating the records of processing.",
                SystemOperation.RecordsOfProcessing)),
            CancellationToken.None);

        Assert.Equal(ErrorCodes.Denied, refused.Match(_ => default, error => error.Code));
    }

    /// <summary>
    /// PRIV-CONS-001: every change is written to the audit trail, and the entry
    /// carries codes and references rather than a sentence.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC1_EveryChangeIsAuditedByCodeAsync()
    {
        await GrantAsync(Recommendations);
        await WithdrawAsync(Recommendations);

        Assert.Equal(
            ["privacy.consent.granted", "privacy.consent.withdrawn"],
            _audit.Entries.Select(entry => entry.Action.ToString()));
        Assert.All(
            _audit.Entries,
            entry => Assert.Equal(Recommendations, entry.Details["purpose"].GetString()));
    }

    private async Task<bool> GrantAsync(
        string purpose,
        ConsentMechanism mechanism = ConsentMechanism.Dashboard) =>
        (await Consents.GrantAsync(Acting, purpose, mechanism, CancellationToken.None))
        .Match(() => true, _ => false);

    private async Task<bool> WithdrawAsync(string purpose) =>
        (await Consents.WithdrawAsync(Acting, purpose, CancellationToken.None))
        .Match(() => true, _ => false);

    private async Task<bool> ObjectAsync(string purpose) =>
        (await Consents.ObjectAsync(Acting, purpose, ConsentMechanism.Dashboard, CancellationToken.None))
        .Match(() => true, _ => false);

    private async Task<IReadOnlyList<ConsentRecord>> HeldAsync() =>
        (await Consents.ReadAsync(Acting, CancellationToken.None))
        .Match(records => records, _ => []);

    private async Task<IReadOnlyList<ObjectionRecord>> ObjectionsAsync() =>
        (await Consents.ObjectionsAsync(Acting, CancellationToken.None))
        .Match(records => records, _ => []);
}
