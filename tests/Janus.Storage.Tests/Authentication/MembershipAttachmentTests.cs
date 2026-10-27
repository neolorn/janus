using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Events;
using Janus.Authorization.Grants;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Identity.Audit;
using Janus.Identity.Organizations;
using Janus.Storage.Authentication.Events;
using Janus.Storage.Authentication.Invitations;
using Janus.Storage.Authorization.Grants;
using Janus.Storage.Authorization.Roles;
using Janus.Storage.Identity.Audit;
using Janus.Storage.Identity.Organizations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The membership an acknowledged invitation attaches, over the rows (REG-INV-001,
/// IDN-LIFE-009a, IDN-MEM-002).
/// </summary>
[Trait("kind", "integration")]
public sealed class MembershipAttachmentTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string Member =
        """
        INSERT INTO identity.memberships (id, subject, organization, created_at, ended_at)
        VALUES (@id, @subject, @organization, @at, @ended);
        """;

    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly RoleName Clerk = RoleName.Parse("clerk");

    // How long a case waits for the second attachment to reach the lock, or to finish,
    // before it fails rather than hangs.
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// REG-INV-001 AC3: the membership reads back carrying the documents at the
    /// versions acknowledged and when; each role is granted once across the
    /// organization, as given by who invited them, however often the invitation names
    /// it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_INV_001_AC3_TheMembershipCarriesTheAcknowledgementAndTheGrantsAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        SubjectId inviter = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        InvitationDocument[] documents = [new("staff-handbook", "3"), new("conduct", "1")];

        await RoleAsync();

        MembershipId attached = await AttachAsync(
            subject,
            organization,
            documents,
            [Clerk, Clerk],
            inviter,
            multiple: false);

        await using StoreContext reading = database.Context();

        Membership membership = Assert.Single(await new MembershipStore(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken));
        Grant grant = Assert.Single(await new GrantStore(reading, new DataConnections(reading)).HeldByAsync(
            [GrantSubject.Of(subject)],
            organization,
            Noon,
            TestContext.Current.CancellationToken));

        Assert.Equal(attached, membership.Id);
        Assert.Equal(documents, membership.Acknowledgement?.Documents);
        Assert.Equal(Noon, membership.Acknowledgement?.At);
        Assert.Equal((Clerk, GrantKind.Stored, false), (grant.Role, grant.Kind, grant.Deny));
        Assert.Equal((inviter, Noon, "invitation:reason"), (grant.GrantedBy, grant.GrantedAt, grant.Reason));
        Assert.Null(grant.ResourceType);
        Assert.Null(grant.ExpiresAt);
    }

    /// <summary>
    /// AUTHZ-GRANT-003 AC6: each grant an acknowledgement writes is recorded once as
    /// <c>authz.grant.created</c> in its transaction, naming the person acknowledging as
    /// actor and the holder as subject, with the grant, its role and the reason.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_003_AC6_EachGrantAnAcknowledgementWritesIsRecordedCreatedAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        SubjectId inviter = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);

        await RoleAsync();

        _ = await AttachAsync(subject, organization, [], [Clerk, Clerk], inviter, multiple: false);

        await using StoreContext reading = database.Context();

        Grant grant = Assert.Single(await new GrantStore(reading, new DataConnections(reading)).HeldByAsync(
            [GrantSubject.Of(subject)],
            organization,
            Noon,
            TestContext.Current.CancellationToken));
        AuditRecord read = Assert.Single(
            await new AuditStore(reading, new DataConnections(reading), _deployment.Ring, _deployment.Randomness)
                .FindNamingAsync(subject, TestContext.Current.CancellationToken),
            record => record.Action == AuditActions.GrantCreated);

        Assert.Equal((subject, subject), (read.ActingSubject, read.Subject));
        Assert.Equal(organization, read.Organization);
        Assert.Equal(grant.Id.ToString(), read.Details["grant"].GetString());
        Assert.Equal("clerk", read.Details["role"].GetString());
        Assert.Equal("invitation:reason", read.Details["reason"].GetString());
    }

    /// <summary>
    /// LIB-API-001, chapter 10 section 5b (D-218): each grant an acknowledgement writes is
    /// announced once as <c>GrantChanged</c> <c>made</c> in its transaction, across the
    /// organization, naming the holder as subject and the person acknowledging as actor.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task LIB_API_001_EachGrantAnAcknowledgementWritesIsAnnouncedAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        SubjectId inviter = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);

        await RoleAsync();

        _ = await AttachAsync(subject, organization, [], [Clerk, Clerk], inviter, multiple: false);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(
            ("made", "clerk", "organization", organization.ToString(), subject.Value),
            await connection.QuerySingleAsync<(string, string, string, string, Guid)>(new CommandDefinition(
                """
                SELECT payload->>'Change', payload->>'Role', payload->>'ResourceType', payload->>'ResourceId',
                       CAST(payload->'Actor'->>'Value' AS uuid)
                FROM identity.events
                WHERE kind = 'GrantChanged' AND payload->>'SubjectId' = @subject
                  AND CAST(payload->'Subject'->>'Value' AS uuid) = @holder
                """,
                new { subject = subject.Value.ToString(), holder = subject.Value },
                cancellationToken: TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REG-INV-001: a role the account already holds across the organization is not
    /// granted a second time; the grant it held stands as it was.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_INV_001_AGrantTheAccountHoldsIsNotWrittenAgainAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);

        await RoleAsync();

        GrantId held = await GrantedAsync(subject, organization);

        _ = await AttachAsync(subject, organization, [], [Clerk], subject, multiple: false);

        await using StoreContext reading = database.Context();

        Grant grant = Assert.Single(await new GrantStore(reading, new DataConnections(reading)).HeldByAsync(
            [GrantSubject.Of(subject)],
            organization,
            Noon,
            TestContext.Current.CancellationToken));

        Assert.Equal((held, "held before"), (grant.Id, grant.Reason));
    }

    /// <summary>
    /// REG-INV-001: a grant of the role that expires does not stand in for the
    /// permanent one the invitation grants, so the permanent one is written beside it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_INV_001_AnExpiringGrantDoesNotStandInForThePermanentOneAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);

        await RoleAsync();

        GrantId expiring = await GrantedAsync(subject, organization, Noon.AddDays(30));

        _ = await AttachAsync(subject, organization, [], [Clerk], subject, multiple: false);

        await using StoreContext reading = database.Context();

        IReadOnlyList<Grant> held = await new GrantStore(reading, new DataConnections(reading)).HeldByAsync(
            [GrantSubject.Of(subject)],
            organization,
            Noon,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, held.Count);
        Assert.Contains(held, grant => grant.Id == expiring);
        Assert.Contains(held, grant => grant.Role == Clerk && grant.ExpiresAt is null && grant.Reason == "invitation:reason");
    }

    /// <summary>
    /// IDN-MEM-002: an account that may hold no further membership is refused with the
    /// code, and nothing is written.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_MEM_002_AnAccountThatMayHoldNoMoreIsRefusedAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId first = await _deployment.OrganizationAsync(Noon);
        OrganizationId second = await _deployment.OrganizationAsync(Noon);

        _ = await AttachAsync(subject, first, [], [], subject, multiple: false);

        await using StoreContext writing = database.Context();
        await using UnitOfWork work = new(writing);

        await work.BeginAsync(TestContext.Current.CancellationToken);

        Result<MembershipId> refused = await Attachment(writing, work).AttachAsync(
            subject,
            second,
            [],
            [],
            subject,
            "invitation:reason",
            AccessContext.Of(subject),
            multiple: false,
            Noon,
            TestContext.Current.CancellationToken);

        await work.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.MembershipLimitReached,
            refused.Match(_ => throw new Xunit.Sdk.XunitException("The membership attached."), error => error.Code));
        Assert.Equal(
            1,
            await writing.Memberships.CountAsync(row => row.Subject == subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-MEM-002 AC6, X3 of D-166: two acknowledgements for one account, made
    /// together, leave one membership. The second waits on the account's row the first
    /// holds, and decides only once the first has committed, on the membership it made.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_MEM_002_AC6_TwoAttachmentsTogetherLeaveOneMembershipAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId first = await _deployment.OrganizationAsync(Noon);
        OrganizationId second = await _deployment.OrganizationAsync(Noon);

        await using StoreContext holding = database.Context();
        await using StoreContext racing = database.Context();
        await using UnitOfWork held = new(holding);
        await using UnitOfWork raced = new(racing);

        await held.BeginAsync(cancellationToken);
        await raced.BeginAsync(cancellationToken);

        Result<MembershipId> attached = await Attachment(holding, held).AttachAsync(
            subject,
            first,
            [],
            [],
            subject,
            "invitation:reason",
            AccessContext.Of(subject),
            multiple: false,
            Noon,
            cancellationToken);

        Task<Result<MembershipId>> waiting = Attachment(racing, raced)
            .AttachAsync(
                subject,
                second,
                [],
                [],
                subject,
                "invitation:reason",
                AccessContext.Of(subject),
                multiple: false,
                Noon,
                cancellationToken)
            .AsTask();

        await BlockedAsync(cancellationToken);

        Assert.False(waiting.IsCompleted);

        await held.CommitAsync(cancellationToken);

        Result<MembershipId> refused = await waiting.WaitAsync(Bound, cancellationToken);

        await raced.CommitAsync(cancellationToken);

        await using StoreContext reading = database.Context();

        Assert.True(attached.Match(_ => true, _ => false));
        Assert.Equal(
            ErrorCodes.MembershipLimitReached,
            refused.Match(_ => throw new Xunit.Sdk.XunitException("The second membership attached."), error => error.Code));
        Assert.Equal(
            1,
            await reading.Memberships.CountAsync(
                row => row.Subject == subject && row.EndedAt == null,
                cancellationToken));
    }

    /// <summary>
    /// IDN-MEM-002, X3 of D-166: the database holds one current membership of an
    /// organization for an account, whatever writes the rows; one that has ended leaves
    /// room for the next.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_MEM_002_TheDatabaseHoldsOneCurrentMembershipOfAnOrganizationAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);

        await using NpgsqlConnection connection = await database.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            Member,
            Row(subject, organization, Noon),
            cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            Member,
            Row(subject, organization, ended: null),
            cancellationToken: cancellationToken));

        PostgresException refusal = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync(new CommandDefinition(
                Member,
                Row(subject, organization, ended: null),
                cancellationToken: cancellationToken)));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, refusal.SqlState);
        Assert.Equal("ux_memberships_current", refusal.ConstraintName);
    }

    /// <summary>
    /// IDN-ORG-001 AC2: the data model holds one account as a current member of two
    /// organizations at once; whether a deployment lets it is the setting's to say, not
    /// the schema's.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_ORG_001_AC2_OneAccountIsACurrentMemberOfTwoOrganizationsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId first = await _deployment.OrganizationAsync(Noon);
        OrganizationId second = await _deployment.OrganizationAsync(Noon);

        await using NpgsqlConnection connection = await database.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            Member,
            Row(subject, first, ended: null),
            cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            Member,
            Row(subject, second, ended: null),
            cancellationToken: cancellationToken));

        await using StoreContext reading = database.Context();

        List<OrganizationId> held = await reading.Memberships
            .Where(row => row.Subject == subject && row.EndedAt == null)
            .Select(row => row.Organization)
            .ToListAsync(cancellationToken);

        Assert.Equal(2, held.Count);
        Assert.Contains(first, held);
        Assert.Contains(second, held);
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private MembershipAttachment Attachment(StoreContext context, UnitOfWork work) =>
        new(
            new MembershipStore(context),
            new GrantStore(context, new DataConnections(context)),
            new GrantAudit(
                new AuditStore(context, new DataConnections(context), _deployment.Ring, _deployment.Randomness),
                TimeProvider.System),
            new EventOutbox(new PendingEvents(context), work),
            new DataConnections(context),
            TimeProvider.System);

    private static object Row(SubjectId subject, OrganizationId organization, DateTimeOffset? ended) =>
        new { id = Guid.NewGuid(), subject = subject.Value, organization = organization.Value, at = Noon, ended };

    // The second attachment is waiting on the row lock the first holds, as the database
    // itself reports it, so the case commits the first only once the second has read
    // nothing yet.
    private async Task BlockedAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(Bound);

        while (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                   "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() "
                       + "AND wait_event_type = 'Lock' AND query LIKE '%FOR UPDATE%'",
                   cancellationToken: bounded.Token)) == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), bounded.Token);
        }
    }

    private async Task<MembershipId> AttachAsync(
        SubjectId subject,
        OrganizationId organization,
        IReadOnlyList<InvitationDocument> documents,
        IReadOnlyList<RoleName> roles,
        SubjectId inviter,
        bool multiple)
    {
        await using StoreContext writing = database.Context();
        await using UnitOfWork work = new(writing);

        await work.BeginAsync(TestContext.Current.CancellationToken);

        MembershipId attached = (await Attachment(writing, work).AttachAsync(
                subject,
                organization,
                documents,
                roles,
                inviter,
                "invitation:reason",
                AccessContext.Of(subject),
                multiple,
                Noon,
                TestContext.Current.CancellationToken))
            .Match(made => made, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        await work.CommitAsync(TestContext.Current.CancellationToken);

        return attached;
    }

    private async Task<GrantId> GrantedAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset? expiresAt = null)
    {
        Grant grant = Grant.Create(
                GrantId.New(TimeProvider.System),
                GrantSubject.Of(subject),
                Clerk,
                organization,
                on: null,
                deny: false,
                GrantKind.Stored,
                expiresAt,
                subject,
                Noon.AddDays(-1),
                "held before")
            .Match(
                written => written,
                error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        await using StoreContext writing = database.Context();

        await new GrantStore(writing, new DataConnections(writing))
            .CreateAsync(grant, TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return grant.Id;
    }

    private async Task RoleAsync()
    {
        await using StoreContext writing = database.Context();

        if (!await writing.Roles.AnyAsync(row => row.Name == Clerk, TestContext.Current.CancellationToken))
        {
            await new RoleStore(writing).CreateAsync(
                Role.Of(Clerk, [Permissions.GrantRead]),
                TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }
}
