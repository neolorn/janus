using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Consents;
using Janus.Privacy.Policies;
using Janus.Privacy.Tests.Documents;
using Janus.Privacy.Tests.Outbox;
using Janus.Privacy.Tests.Requests;
using Xunit;

namespace Janus.Privacy.Tests.Consents;

/// <summary>
/// What a host reads of where another person stands: the account's state, the consents
/// and the objections, asked in an organization and reaching the people tied to it
/// (IDN-READ-003, IDN-READ-004, D-218).
/// </summary>
[Trait("kind", "unit")]
public sealed class StandingTests : IAsyncDisposable
{
    private const string Recommendations = "recommendations";

    private const string Security = "security";

    private static readonly DateTimeOffset Noon = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrganizationId Deployment =
        new(Guid.Parse("0a0a0a0a-0a0a-4a0a-8a0a-0a0a0a0a0a0a"));

    private static readonly OrganizationId Shop =
        new(Guid.Parse("0b0b0b0b-0b0b-4b0b-8b0b-0b0b0b0b0b0b"));

    private static readonly OrganizationId Elsewhere =
        new(Guid.Parse("0c0c0c0c-0c0c-4c0c-8c0c-0c0c0c0c0c0c"));

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly SubjectId Officer =
        new(Guid.Parse("22222222-2222-4222-8222-222222222222"));

    private static readonly SubjectId Clerk =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly SubjectId Nobody =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private readonly ConsentStoreInMemory _consents = new();
    private readonly LegalDocumentStoreInMemory _documents = new();
    private readonly EventsInMemory _events = new();
    private readonly OutboxStoreInMemory _outbox = new();
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly ReachedSubjectsInMemory _reached = new();
    private readonly AccountStatesInMemory _states = new();
    private readonly ReadVolumeInMemory _volume = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A deployment whose notice is published, an officer holding <c>consent:read</c> in
    /// its administrative organization, and a clerk holding it in a shop that reaches
    /// Ahmed.
    /// </summary>
    public StandingTests()
    {
        _documents.Hold(new DocumentVersion(ConsentService.Notice, "1", "ar", "النص", [], Noon));
        _administrative.Organization = Deployment;
        _reached.Hold(Deployment);
        _reached.Hold(Shop);
        _gate.Grant(Officer, Deployment, Permissions.ConsentRead);
        _gate.Grant(Clerk, Shop, Permissions.ConsentRead);
        _states.Hold(Ahmed, AccountState.Active);
        _states.Hold(Officer, AccountState.Active);
        _states.Hold(Clerk, AccountState.Active);
        _reached.Tie(Ahmed, Shop);
    }

    private ConsentService Consents =>
        new(
            _consents,
            _documents,
            Declaration.Processing,
            _events,
            _outbox,
            _audit,
            new SubjectReach(_gate, _administrative, _reached),
            _states,
            _volume,
            _work,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// IDN-READ-003 AC4: the standing read answers a withdrawn consent as withdrawn and a
    /// restricted account as <c>restricted</c>, beside the objections the person holds.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_READ_003_AC4_AWithdrawnConsentReadsWithdrawnAndARestrictedAccountRestrictedAsync()
    {
        var ahmed = AccessContext.Of(Ahmed);
        Assert.Null(Refusal(await Consents
            .GrantAsync(ahmed, Recommendations, ConsentMechanism.Dashboard, CancellationToken.None)));
        Assert.Null(Refusal(await Consents.WithdrawAsync(ahmed, Recommendations, CancellationToken.None)));
        Assert.Null(Refusal(await Consents
            .ObjectAsync(ahmed, Security, ConsentMechanism.Dashboard, CancellationToken.None)));
        _states.Hold(Ahmed, AccountState.Restricted);

        SubjectStanding standing = (await Consents.StandingAsync(
                AccessContext.Of(Officer),
                Ahmed,
                null,
                CancellationToken.None))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal((SubjectKind.Account, AccountState.Restricted), (standing.Kind, standing.State));
        ConsentRecord consent = Assert.Single(standing.Consents);
        Assert.Equal((Recommendations, true), (consent.Purpose, consent.WithdrawnAt is not null));
        ObjectionRecord objection = Assert.Single(standing.Objections);
        Assert.Equal((Security, false), (objection.Purpose, objection.WithdrawnAt is not null));
        Assert.Equal(Officer, Assert.Single(_volume.Returned).Context.Acting);
    }

    /// <summary>
    /// REC-ADM-001: the standing read answers a person record's subject as an account's,
    /// naming its kind <c>record</c>.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_ADM_001_AStandingOfARecordNamesItsKindAsync()
    {
        SubjectId record = new(Guid.Parse("77777777-7777-4777-8777-777777777777"));
        _states.HoldRecord(record, AccountState.Deleting);

        SubjectStanding standing = (await Consents.StandingAsync(
                AccessContext.Of(Officer),
                record,
                null,
                CancellationToken.None))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal((SubjectKind.Record, AccountState.Deleting), (standing.Kind, standing.State));
    }

    /// <summary>
    /// IDN-READ-004: a holder in an organization reads the standing of a person tied to
    /// it, and is answered <c>identity.account.notfound</c> for a person tied elsewhere
    /// alone, as for a subject no account bears and an account deleted.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_READ_004_AStandingBeyondTheReachReadsAsAnAccountNoPersonBearsAsync()
    {
        SubjectId tiedElsewhere = new(Guid.Parse("55555555-5555-4555-8555-555555555555"));
        SubjectId deleted = new(Guid.Parse("66666666-6666-4666-8666-666666666666"));
        _states.Hold(tiedElsewhere, AccountState.Active);
        _states.Hold(deleted, AccountState.Deleted);
        _reached.Hold(Elsewhere);
        _reached.Tie(tiedElsewhere, Elsewhere);
        _reached.Tie(deleted, Shop);

        Assert.Null(await RefusedAsync(Clerk, Ahmed, Shop));
        Assert.Equal(ErrorCodes.AccountNotFound, await RefusedAsync(Clerk, tiedElsewhere, Shop));
        Assert.Equal(ErrorCodes.AccountNotFound, await RefusedAsync(Clerk, Nobody, Shop));
        Assert.Equal(ErrorCodes.AccountNotFound, await RefusedAsync(Clerk, deleted, Shop));
        Assert.Null(await RefusedAsync(Officer, tiedElsewhere, null));
        Assert.Single(_volume.Returned, counted => counted.Context.Acting == Clerk);
    }

    /// <summary>
    /// IDN-READ-004: an organization the deployment does not hold is answered for what
    /// it is, and a person without the permission in the organization named is refused
    /// by the gate; neither is counted toward the read volume.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_READ_004_AStandingAskedOutsideTheHoldersOrganizationIsRefusedAsync()
    {
        Assert.Equal(ErrorCodes.OrganizationNotFound, await RefusedAsync(Clerk, Ahmed, Elsewhere));
        Assert.Equal(ErrorCodes.Denied, await RefusedAsync(Clerk, Ahmed, null));
        Assert.Equal(ErrorCodes.Denied, await RefusedAsync(Officer, Ahmed, Shop));
        Assert.Empty(_volume.Returned);
    }

    private static ErrorCode? Refusal(Result result) =>
        result.Match(() => default(ErrorCode?), error => error.Code);

    private async Task<ErrorCode?> RefusedAsync(SubjectId reader, SubjectId subject, OrganizationId? organization) =>
        (await Consents.StandingAsync(AccessContext.Of(reader), subject, organization, CancellationToken.None))
        .Match(_ => default(ErrorCode?), error => error.Code);
}
