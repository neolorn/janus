using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Passwords;
using Janus.Authentication.Registration;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Mailboxes;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Authorization;
using Janus.Identity.Identifiers;
using Janus.Privacy.SubjectKeys;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Organizations;

/// <summary>
/// A mailbox given to a member, and one an invitation reserves, as the deployment
/// composes them over the database, where a row a transaction adds is not read back by
/// a query until it is saved (REG-MAIL-001, REG-MAIL-003, D-253, D-255).
/// </summary>
/// <param name="host">The database the cases run against.</param>
[Trait("kind", "integration")]
public sealed class MailboxGivingTests(HostFixture host) : IClassFixture<HostFixture>, IAsyncDisposable
{
    private const string Source = "198.51.100.7";

    private const string Why = "The member joined the staff.";

    private ServiceProvider? _mailed;

    // The deployment over the fixture's database with a mail server of the host's own
    // registered, so that the mail server in use is chosen and mailboxes are given.
    private ServiceProvider Mailed => _mailed ??= Deployed(host);

    /// <summary>
    /// REG-MAIL-001 AC9 (D-253): a current member of the administrative organization is
    /// given a mailbox at an address nobody holds: the address is written as a verified
    /// email and the primary in the transaction that holds the mailbox, and the member's
    /// own email, of another domain, stays beside it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_MAIL_001_AC9_AMemberIsGivenAMailboxAtANewAddressAsync()
    {
        (Staff staff, SubjectId member, IdentifierId personal) = await StaffAsync();
        string corporate = Address("staff.example.test");

        GivenMailbox given = Accepted(await staff.GiveAsync(member, new MailboxGift(corporate, Why)));

        HeldIdentifiers held = await staff.HeldAsync(member);
        HeldIdentifier email = held.OfKind(IdentifierKind.Email).Single(one => one.Canonical == corporate);

        Assert.True(email is { IsVerified: true, IsPrimary: true, IsLocked: false });
        Assert.True(held.Find(personal) is { IsVerified: true, IsPrimary: false });
        Assert.Equal((member.Value, false), await MailboxAsync(given.Id));
    }

    /// <summary>
    /// REG-MAIL-003 AC9 (D-255): a mailbox given under <c>formerMailbox</c> <c>adopt</c>
    /// may adopt the account the mail server holds, and its address is written as the
    /// member's verified primary email all the same.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_MAIL_003_AC9_AMemberIsGivenAMailboxThatAdoptsAsync()
    {
        (Staff staff, SubjectId member, IdentifierId personal) = await StaffAsync();
        string corporate = Address("staff.example.test");

        GivenMailbox given = Accepted(await staff.GiveAsync(
            member,
            new MailboxGift(corporate, Why) { FormerMailbox = FormerMailbox.Adopt }));

        HeldIdentifiers held = await staff.HeldAsync(member);

        Assert.True(held.OfKind(IdentifierKind.Email).Single(one => one.Canonical == corporate)
            is { IsVerified: true, IsPrimary: true });
        Assert.True(held.Find(personal) is { IsVerified: true, IsPrimary: false });
        Assert.Equal((member.Value, true), await MailboxAsync(given.Id));
    }

    /// <summary>
    /// REG-MAIL-001 AC11 (D-253): a mailbox given at an address the member itself holds
    /// writes no second email: the one held, verified, held unverified, or an add begun,
    /// is the mailbox's address, verified and the primary.
    /// </summary>
    /// <param name="holding">How the member holds the address.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("verified")]
    [InlineData("unverified")]
    [InlineData("pending")]
    public async Task REG_MAIL_001_AC11_AnAddressTheMemberHoldsIsTheMailboxsOneEmailAsync(string holding)
    {
        (Staff staff, SubjectId member, IdentifierId personal) = await StaffAsync(personal: holding is "pending");
        string corporate = Address("staff.example.test");

        IdentifierId own = holding is "pending"
            ? await staff.BegunAsync(member, corporate)
            : await staff.EmailAsync(member, corporate, verified: holding is "verified");

        GivenMailbox given = Accepted(await staff.GiveAsync(member, new MailboxGift(corporate, Why)));

        HeldIdentifiers held = await staff.HeldAsync(member);
        HeldIdentifier email = Assert.Single(held.OfKind(IdentifierKind.Email), one => one.Canonical == corporate);

        Assert.Equal(own, email.Id);
        Assert.True(email is { IsVerified: true, IsPrimary: true, IsPending: false });
        Assert.Equal(holding is "pending" ? 2 : 1, held.OfKind(IdentifierKind.Email).Count);
        Assert.Equal(holding is "pending", held.Find(personal) is { IsVerified: true, IsPrimary: false });
        Assert.Equal((member.Value, false), await MailboxAsync(given.Id));
    }

    /// <summary>
    /// REG-MAIL-003 AC9 and REG-MAIL-001 (D-255): an invitation issued under
    /// <c>formerMailbox</c> <c>adopt</c> reserves a mailbox that may adopt, and its
    /// acknowledgement makes the corporate address the invitee's verified primary email
    /// beside the personal one and the mailbox theirs.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_MAIL_003_AC9_AnInvitationUnderAdoptIsAcknowledgedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (Staff staff, _, _) = await StaffAsync();
        SubjectId invitee = await new Authorization.Deployment(host).AccountAsync(cancellationToken);
        string address = Address("home.example.test");
        string corporate = Address("staff.example.test");
        IdentifierId personal = await staff.EmailAsync(invitee, address, verified: true);

        await staff.PasswordAsync(invitee);

        IssuedInvitation issued = Accepted(await staff.IssueAsync(
            new InvitationRequest(address, Phone: null, corporate, Roles: [], Documents: [])
            {
                FormerMailbox = FormerMailbox.Adopt,
                Reason = Why,
            }));

        await AttachedAsync(issued.Id, invitee);

        Result acknowledged = await staff.AcknowledgeAsync(invitee, issued.Id);

        HeldIdentifiers held = await staff.HeldAsync(invitee);

        Assert.True(acknowledged.Match(() => true, error => throw new InvalidOperationException(error.Code.ToString())));
        Assert.True(held.OfKind(IdentifierKind.Email).Single(one => one.Canonical == corporate)
            is { IsVerified: true, IsPrimary: true, IsLocked: true });
        Assert.True(held.Find(personal) is { IsVerified: true, IsPrimary: false });
        Assert.Equal((invitee.Value, true), await MailboxAsync(await ReservedAsync(issued.Id)));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_mailed is not null)
        {
            await _mailed.DisposeAsync();
        }
    }

    private static TValue Accepted<TValue>(Result<TValue> outcome) =>
        outcome.Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

    // An address nobody else's case names, at the domain given.
    private static string Address(string domain) => "person" + Guid.NewGuid().ToString("n")[..12] + "@" + domain;

    // The administrative organization, an administrator who may manage its members with a
    // step-up of their own, and a member with a verified email of another domain, which
    // is the primary.
    private async Task<(Staff Staff, SubjectId Member, IdentifierId Personal)> StaffAsync(bool personal = true)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Authorization.Deployment(host);

        RoleName managing = await deployment.BeginAsync([Permissions.MembershipManage], cancellationToken);
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        SubjectId administrator = await deployment.AccountAsync(cancellationToken);
        SubjectId member = await deployment.AccountAsync(cancellationToken);

        await deployment.MemberAsync(administrator, administrative, cancellationToken);
        await deployment.MemberAsync(member, administrative, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(administrator),
            managing,
            null,
            false,
            null,
            administrative,
            cancellationToken);

        var staff = new Staff(Mailed, administrative, administrator, await SteppedUpAsync(administrator));

        IdentifierId held = personal
            ? await staff.EmailAsync(member, Address("home.example.test"), verified: true)
            : default;

        return (staff, member, held);
    }

    // A session of the account's own that proved a phishing-resistant second factor just
    // now, which giving a mailbox and issuing an invitation ask for as their step-up.
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

    // The link of the invitation opened by the account, as the account's press of it
    // while signed in leaves it.
    private async Task AttachedAsync(InvitationId invitation, SubjectId invitee)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE identity.invitations SET invitee = @invitee, attached_at = @at WHERE id = @id;",
            new { invitee = invitee.Value, at = Authorization.Deployment.Noon, id = invitation.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // The mailbox the invitation reserved.
    private async Task<MailboxId> ReservedAsync(InvitationId invitation)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return new MailboxId(await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "SELECT mailbox FROM identity.invitations WHERE id = @id;",
            new { id = invitation.Value },
            cancellationToken: TestContext.Current.CancellationToken)));
    }

    // Who holds the mailbox's row, and whether it may adopt the mail server's account.
    private async Task<(Guid? Holder, bool MayAdopt)> MailboxAsync(MailboxId mailbox)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<(Guid?, bool)>(new CommandDefinition(
            "SELECT holder, may_adopt FROM identity.mailboxes WHERE id = @id;",
            new { id = mailbox.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private static ServiceProvider Deployed(HostFixture host)
    {
        var services = new ServiceCollection();

        services.AddSingleton<TimeProvider>(new FixedTime(Authorization.Deployment.Noon));
        services.AddSingleton<ISecretSource>(HostFixture.Secrets(host.MaintenanceConnectionString));
        services.AddSingleton<INotificationHandler>(new Janus.Testing.InMemoryNotifications());
        services.AddSingleton<IAlertHandler>(new Janus.Testing.InMemoryAlerts());
        services.AddSingleton(new LandingOrigins("https://identity.example.test", "https://accounts.example.test"));
        HostFixture.Sourced(services, host.ConnectionString);
        services.AddJanus(host.ConnectionString, HostFixture.Declaration(), ApplicationKind.Public);
        services.AddSingleton<IMailServer>(new MailServerInMemory());

        return HostFixture.Started(services.BuildServiceProvider());
    }

    // What the administrator asks of the deployment, and what the case writes through
    // the deployment's own stores.
    private sealed class Staff(ServiceProvider deployed, OrganizationId organization, SubjectId administrator, SessionId session)
    {

        public async Task<Result<GivenMailbox>> GiveAsync(SubjectId member, MailboxGift gift)
        {
            await using AsyncServiceScope scope = deployed.CreateAsyncScope();

            return await scope.ServiceProvider.GetRequiredService<IInvitations>().GiveMailboxAsync(
                AccessContext.Of(administrator),
                session,
                organization,
                member,
                gift,
                Source,
                TestContext.Current.CancellationToken);
        }

        public async Task<Result<IssuedInvitation>> IssueAsync(InvitationRequest request)
        {
            await using AsyncServiceScope scope = deployed.CreateAsyncScope();

            return await scope.ServiceProvider.GetRequiredService<IInvitations>().IssueAsync(
                AccessContext.Of(administrator),
                session,
                organization,
                request,
                Source,
                TestContext.Current.CancellationToken);
        }

        public async Task<Result> AcknowledgeAsync(SubjectId invitee, InvitationId invitation)
        {
            await using AsyncServiceScope scope = deployed.CreateAsyncScope();

            return await scope.ServiceProvider.GetRequiredService<IInvitations>().AcknowledgeAsync(
                AccessContext.Of(invitee),
                invitation,
                Source,
                TestContext.Current.CancellationToken);
        }

        public async Task<HeldIdentifiers> HeldAsync(SubjectId subject)
        {
            await using AsyncServiceScope scope = deployed.CreateAsyncScope();

            return await scope.ServiceProvider.GetRequiredService<IIdentifierDirectory>()
                .HeldAsync(subject, TestContext.Current.CancellationToken);
        }

        // An email of the account's, written through the deployment's own store: verified,
        // and so the primary where none stands, or held unverified.
        public async Task<IdentifierId> EmailAsync(SubjectId subject, string entered, bool verified)
        {
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;
            Assert.True(EmailAddress.TryParse(entered, out EmailAddress address));

            var id = IdentifierId.New(TimeProvider.System);

            await using AsyncServiceScope scope = deployed.CreateAsyncScope();
            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            IIdentifierStore identifiers = scope.ServiceProvider.GetRequiredService<IIdentifierStore>();

            await work.BeginAsync(cancellationToken);

            IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken);

            set.Add(Identifier.Email(id, subject, address, entered, Authorization.Deployment.Noon), maximum: 5);

            if (verified)
            {
                set.Verify(id, Authorization.Deployment.Noon);
            }

            await identifiers.RecordAsync(set, cancellationToken);
            await work.CommitAsync(cancellationToken);

            return id;
        }

        // An add of the address the account began and has not verified.
        public async Task<IdentifierId> BegunAsync(SubjectId subject, string address)
        {
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;
            var id = IdentifierId.New(TimeProvider.System);

            await using AsyncServiceScope scope = deployed.CreateAsyncScope();
            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await work.BeginAsync(cancellationToken);
            await scope.ServiceProvider.GetRequiredService<IPendingVerificationStore>().AddAsync(
                PendingVerification.ToAdd(
                    subject,
                    browser: null,
                    StagedIdentity.Of(id, IdentifierKind.Email, address, address),
                    Authorization.Deployment.Noon),
                cancellationToken);
            await work.CommitAsync(cancellationToken);

            return id;
        }

        // A password of the account's, which meets the credential policy an organization
        // holds by default.
        public async Task PasswordAsync(SubjectId subject)
        {
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;

            await using AsyncServiceScope scope = deployed.CreateAsyncScope();
            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            using var randomness = RandomNumberGenerator.Create();

            await work.BeginAsync(cancellationToken);
            await scope.ServiceProvider.GetRequiredService<IPasswordStore>().SetAsync(
                Password.Set(
                    subject,
                    new Argon2idHasher(randomness).Hash(
                        Encoding.UTF8.GetBytes("a horse outstanding in its field"),
                        new Argon2StrengthClass(Settings.PasswordArgon2Memory.Default, Settings.PasswordArgon2Iterations.Default),
                        parallelism: 1),
                    meetsSingleFactorFloor: true,
                    Authorization.Deployment.Noon - TimeSpan.FromDays(30)),
                cancellationToken);
            await work.CommitAsync(cancellationToken);
        }
    }
}
