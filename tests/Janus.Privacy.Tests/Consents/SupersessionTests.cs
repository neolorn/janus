using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Consents;
using Janus.Privacy.Documents;
using Janus.Privacy.Policies;
using Janus.Privacy.Tests.Documents;
using Xunit;

namespace Janus.Privacy.Tests.Consents;

/// <summary>
/// What publishing a revised privacy notice does to the consents already given: a
/// material revision ends them and the subject is asked again, a revision that is
/// not touches nothing (PRIV-CONS-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class SupersessionTests : IAsyncDisposable
{
    private const string Recommendations = "recommendations";

    private const string Marketing = "marketing";

    private const string Security = "security";

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly SubjectId Mona =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private static readonly SubjectId Officer =
        new(Guid.Parse("22222222-2222-4222-8222-222222222222"));

    private static readonly OrganizationId Deployment =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private readonly ConsentStoreInMemory _consents = new();
    private readonly LegalDocumentStoreInMemory _documents = new();
    private readonly EventsInMemory _events = new();
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly PrivacyAlertsInMemory _alerts = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A deployment with a notice published and an officer who may publish another.
    /// </summary>
    public SupersessionTests()
    {
        _documents.Hold(new DocumentVersion(ConsentService.Notice, "1", "ar", "النص", [], Noon));
        _memberships.Add(Officer, Deployment);
        _gate.Grant(Officer, Deployment, Permissions.NoticePublish);
        _configuration.Set(Settings.LegalGoverningLanguage, "ar");
    }

    private ConsentService Consents =>
        new(_consents, _documents, Declaration.Processing, _events, _audit, _work, _clock);

    private LegalDocumentService Documents =>
        new(
            _documents,
            new AdministrativeScope(_gate, _memberships),
            new Supersession(_consents, _events),
            _configuration,
            _audit,
            _alerts,
            _work,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// PRIV-CONS-007 AC1: a material change identifies which subjects require
    /// re-asking, and it is every one holding a live consent against an earlier
    /// version.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC1_AMaterialChangeIdentifiesWhoMustBeAskedAgainAsync()
    {
        await GrantAsync(Ahmed, Recommendations);
        await GrantAsync(Mona, Marketing);

        _clock.Advance(TimeSpan.FromDays(60));

        await PublishAsync(material: true);

        Assert.All(
            await HeldAsync(Ahmed),
            record => Assert.Equal(Noon.AddDays(60), record.SupersededAt));
        Assert.All(
            await HeldAsync(Mona),
            record => Assert.Equal(Noon.AddDays(60), record.SupersededAt));
    }

    /// <summary>
    /// PRIV-CONS-007 AC3, AC4: a superseded consent prompts and never blocks, so the
    /// record stays and is not withdrawn; the subject is asked again because the
    /// record says it was superseded.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC4_ASupersededConsentPromptsRatherThanWithdrawsAsync()
    {
        await GrantAsync(Ahmed, Recommendations);
        await PublishAsync(material: true);

        ConsentRecord held = Assert.Single(await HeldAsync(Ahmed));

        Assert.Null(held.WithdrawnAt);
        Assert.NotNull(held.SupersededAt);
        Assert.False(held.Live);
        Assert.Equal(
            ConsentChange.Superseded,
            Assert.Single(_events.Of<ConsentChanged>(), raised => raised.Subject == Ahmed
                && raised.Change is ConsentChange.Superseded).Change);
    }

    /// <summary>
    /// PRIV-CONS-007: a revision the person publishing called immaterial touches no
    /// consent, which is what keeps a paragraph edit from stopping a storefront.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC3_AnImmaterialRevisionTouchesNoConsentAsync()
    {
        await GrantAsync(Ahmed, Recommendations);
        await PublishAsync(material: false);

        ConsentRecord held = Assert.Single(await HeldAsync(Ahmed));

        Assert.True(held.Live);
        Assert.DoesNotContain(
            _events.Of<ConsentChanged>(),
            raised => raised.Change is ConsentChange.Superseded);
    }

    /// <summary>
    /// PRIV-CONS-007 AC2: only the consent-based purposes are suspended; an
    /// objection recorded on another basis is untouched by a notice revision.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC2_OnlyTheConsentBasedPurposesAreSuspendedAsync()
    {
        await GrantAsync(Ahmed, Recommendations);

        _ = await Consents.ObjectAsync(
            AccessContext.Of(Ahmed),
            Security,
            ConsentMechanism.Dashboard,
            CancellationToken.None);

        await PublishAsync(material: true);

        IReadOnlyList<ObjectionRecord> objections =
            (await Consents.ObjectionsAsync(AccessContext.Of(Ahmed), CancellationToken.None))
            .Match(records => records, _ => []);

        Assert.False(Assert.Single(await HeldAsync(Ahmed)).Live);
        Assert.True(Assert.Single(objections).Standing);
    }

    /// <summary>
    /// PRIV-CONS-007: a consent given against the version just published is already
    /// current, so publishing it does not end it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC1_AConsentAgainstTheCurrentVersionSurvivesAsync()
    {
        await PublishAsync(material: true);
        await GrantAsync(Ahmed, Recommendations);

        ConsentRecord held = Assert.Single(await HeldAsync(Ahmed));

        Assert.Equal("2", held.NoticeVersion);
        Assert.True(held.Live);
    }

    /// <summary>
    /// PRIV-CONS-007: the audit record of the publication carries how many consents
    /// the answer on materiality ended.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC1_ThePublicationRecordsHowManyConsentsItEndedAsync()
    {
        await GrantAsync(Ahmed, Recommendations);
        await GrantAsync(Mona, Marketing);
        await PublishAsync(material: true);

        PrivacyAuditEntry published = _audit.Entries
            .Last(entry => entry.Action.ToString() == "privacy.document.published");

        Assert.Equal(2, published.Details["superseded"].GetInt32());
    }

    /// <summary>
    /// PRIV-CONS-007: a material revision of another document ends no consent,
    /// because a consent record names the version of the notice and of nothing else.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC2_AMaterialRevisionOfAnotherDocumentEndsNoConsentAsync()
    {
        await GrantAsync(Ahmed, Recommendations);

        _ = await Documents.PublishAsync(
            AccessContext.Of(Officer),
            new DocumentPublication("terms-of-service", "The terms", "en", [], Material: true),
            CancellationToken.None);

        Assert.True(Assert.Single(await HeldAsync(Ahmed)).Live);
    }

    private async Task GrantAsync(SubjectId subject, string purpose) =>
        _ = await Consents.GrantAsync(
            AccessContext.Of(subject),
            purpose,
            ConsentMechanism.Dashboard,
            CancellationToken.None);

    private async Task PublishAsync(bool material) =>
        _ = await Documents.PublishAsync(
            AccessContext.Of(Officer),
            new DocumentPublication(ConsentService.Notice, "النص الجديد", "ar", [], material),
            CancellationToken.None);

    private async Task<IReadOnlyList<ConsentRecord>> HeldAsync(SubjectId subject) =>
        (await Consents.ReadAsync(AccessContext.Of(subject), CancellationToken.None))
        .Match(records => records, _ => []);
}
