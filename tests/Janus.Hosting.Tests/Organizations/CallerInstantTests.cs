using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Tests.Authorization;
using Janus.Privacy.SubjectKeys;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using Xunit.Sdk;

namespace Janus.Hosting.Tests.Organizations;

/// <summary>
/// The instants a caller sends at an offset other than 0, the end of a grant, of a
/// membership and of an invitation's grants, recorded and answered as the same instants
/// in UTC over the database (API-CONV-002, D-264).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class CallerInstantTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Source = "198.51.100.7";

    private const string Why = "The contract ends.";

    // An instant as a frontend two hours ahead of UTC writes it, and the same instant in UTC.
    private static readonly DateTimeOffset Sent = new(2026, 12, 1, 0, 0, 0, TimeSpan.FromHours(2));

    private static readonly DateTimeOffset Held = new(2026, 11, 30, 22, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// API-CONV-002 AC5 (D-264): a grant's <c>expiresAt</c> sent at +02:00 is recorded,
    /// and answered among the holder's grants, as the same instant in UTC.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task API_CONV_002_AC5_AGrantsEndSentAtAnOffsetIsHeldInUtcAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Authorization.Deployment(host);

        RoleName listed = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);
        RoleName managing = await deployment.RoleAsync([Permissions.GrantManage, Permissions.GrantRead], cancellationToken);
        SubjectId administrator = await deployment.AccountAsync(cancellationToken);
        SubjectId holder = await deployment.AccountAsync(cancellationToken);

        await deployment.GrantAsync(
            GrantSubject.Of(administrator), managing, null, false, null, deployment.Organization, cancellationToken);
        await deployment.HandsOutAsync(managing, [listed], cancellationToken);

        SessionId session = await SteppedUpAsync(administrator);
        GrantId granted;
        HeldGrant answered;

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            IGrants grants = scope.ServiceProvider.GetRequiredService<IGrants>();

            granted = Accepted(await grants.GrantAsync(
                AccessContext.Of(administrator),
                session,
                new GrantRequest(
                    GrantSubject.Of(holder),
                    listed,
                    new ResourceReference(ResourceType.Parse("organization"), ResourceId.Parse(deployment.Organization.ToString())),
                    false,
                    Sent,
                    Why),
                cancellationToken));
            answered = Assert.Single(Accepted(await grants.HeldAsync(
                AccessContext.Of(administrator),
                deployment.Organization,
                GrantSubject.Of(holder),
                cancellationToken)));
        }

        Assert.Equal(Held.UtcDateTime, await RecordedAsync("SELECT expires_at FROM identity.grants WHERE id = @id;", granted.Value));
        Assert.Equal(((DateTimeOffset?)Held, TimeSpan.Zero), (answered.ExpiresAt, answered.ExpiresAt?.Offset));
    }

    /// <summary>
    /// API-CONV-002 AC5 (D-264): a membership's <c>endsAt</c> sent at +02:00 is recorded,
    /// and answered on the member, as the same instant in UTC.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task API_CONV_002_AC5_AMembershipsEndSentAtAnOffsetIsHeldInUtcAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Authorization.Deployment(host);

        RoleName managing = await deployment.BeginAsync(
            [Permissions.MembershipManage, Permissions.MembershipRead],
            cancellationToken);
        OrganizationId organization = deployment.Organization;
        SubjectId administrator = await deployment.AccountAsync(cancellationToken);
        SubjectId member = await deployment.AccountAsync(cancellationToken);

        await deployment.MemberAsync(administrator, organization, cancellationToken);
        await deployment.MemberAsync(member, organization, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(administrator), managing, null, false, null, organization, cancellationToken);

        SessionId session = await SteppedUpAsync(administrator);
        MemberSummary answered;

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            IInvitations invitations = scope.ServiceProvider.GetRequiredService<IInvitations>();

            Result set = await invitations.SetMembershipEndAsync(
                AccessContext.Of(administrator),
                session,
                organization,
                member,
                Sent,
                Why,
                cancellationToken);

            Assert.True(set.Match(() => true, _ => false));

            answered = Accepted(await invitations.MemberAsync(
                AccessContext.Of(administrator),
                organization,
                member,
                cancellationToken));
        }

        await using NpgsqlConnection connection = await host.OpenAsync();

        DateTime? recorded = await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            "SELECT ends_at FROM identity.memberships WHERE subject = @member AND organization = @organization;",
            new { member = member.Value, organization = organization.Value },
            cancellationToken: cancellationToken));

        Assert.Equal(Held.UtcDateTime, recorded);
        Assert.Equal(((DateTimeOffset?)Held, TimeSpan.Zero), (answered.EndsAt, answered.EndsAt?.Offset));
    }

    /// <summary>
    /// API-CONV-002 AC5 (D-264): an invitation's <c>grantEndsAt</c> sent at +02:00 is
    /// recorded, and answered among the standing invitations, as the same instant in UTC.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task API_CONV_002_AC5_AnInvitationsGrantEndSentAtAnOffsetIsHeldInUtcAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Authorization.Deployment(host);

        RoleName ending = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);
        RoleName managing = await deployment.RoleAsync(
            [Permissions.MembershipManage, Permissions.MembershipRead, Permissions.GrantManage],
            cancellationToken);
        OrganizationId organization = deployment.Organization;
        SubjectId administrator = await deployment.AccountAsync(cancellationToken);

        await deployment.MemberAsync(administrator, organization, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(administrator), managing, null, false, null, organization, cancellationToken);
        await deployment.HandsOutAsync(managing, [ending], cancellationToken);
        await EndingAsync(ending);

        SessionId session = await SteppedUpAsync(administrator);
        IssuedInvitation issued;
        StandingInvitation answered;

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            IInvitations invitations = scope.ServiceProvider.GetRequiredService<IInvitations>();

            issued = Accepted(await invitations.IssueAsync(
                AccessContext.Of(administrator),
                session,
                organization,
                new InvitationRequest("invitee@example.test", Phone: null, CorporateEmail: null, [ending], Documents: [])
                {
                    GrantEndsAt = Sent,
                },
                Source,
                cancellationToken));
            answered = Assert.Single(Accepted(await invitations.StandingAsync(
                AccessContext.Of(administrator),
                organization,
                cancellationToken)));
        }

        Assert.Equal(Held.UtcDateTime, await RecordedAsync("SELECT grant_ends_at FROM identity.invitations WHERE id = @id;", issued.Id.Value));
        Assert.Equal(((DateTimeOffset?)Held, TimeSpan.Zero), (answered.GrantEndsAt, answered.GrantEndsAt?.Offset));
    }

    private static T Accepted<T>(Result<T> answered) =>
        answered.Match(value => value, error => throw new XunitException(error.Code.ToString()));

    // The instant one row holds, read without the library, in UTC.
    private async Task<DateTime?> RecordedAsync(string statement, Guid id)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            statement,
            new { id },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // AUTHZ-GRANT-003: a role whose grants must end, so an invitation naming it carries
    // the end of its grants.
    private async Task EndingAsync(RoleName role)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE identity.roles SET grants_must_end = TRUE WHERE name = @role;",
            new { role = role.ToString() },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // A session of the account's own that proved a phishing-resistant second factor just
    // now, which granting, issuing an invitation and setting an end ask for as their
    // step-up.
    private async Task<SessionId> SteppedUpAsync(SubjectId account)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;

        var session = Session.Begin(
            SessionId.New(TimeProvider.System),
            account,
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            new SessionOrigin(Source, new DeviceDescription("Firefox", "Linux")),
            services.GetRequiredService<TimeProvider>().GetUtcNow(),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        // A session is kept under its person's key, which an account written directly
        // does not have until its first session asks for it.
        ISubjectKeyStore keys = services.GetRequiredService<ISubjectKeyStore>();

        if (await keys.FindBySubjectAsync(account, cancellationToken) is null)
        {
            await keys.CreateAsync(account, cancellationToken);
        }

        await services.GetRequiredService<ISessionStore>().AddAsync(
            session,
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(32),
            cancellationToken);
        await work.CommitAsync(cancellationToken);

        return session.Id;
    }
}
