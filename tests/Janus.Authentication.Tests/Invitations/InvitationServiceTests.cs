using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Invitations;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Organizations;
using Janus.Authentication.Policies;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Mailboxes;
using Janus.Authentication.Tests.Organizations;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Invitations;

/// <summary>
/// Issuing an invitation into an organization and revoking it (IDN-LIFE-009a,
/// REG-INV-001, REG-MAIL-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class InvitationServiceTests : IAsyncDisposable
{
    private const string Source = "198.51.100.7";
    private const string Personal = "person@elsewhere.test";
    private const string Corporate = "person@staff.test";
    private const string Number = "+441632960011";

    private static readonly string[] English = ["en"];

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly SessionOrigin Somewhere = new(Source, new DeviceDescription("Firefox", "Fedora"));

    private static readonly OrganizationId Staff = new(Guid.NewGuid());

    private static readonly OrganizationId Customer = new(Guid.NewGuid());

    private readonly MembershipLookupInMemory _memberships = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new() { Organization = Staff };
    private readonly SessionStoreInMemory _sessions = new();
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly RoleCatalogueInMemory _roles = new();
    private readonly LegalDocumentsInMemory _documents = new();
    private readonly DomainStoreInMemory _domains = new();
    private readonly InvitationStoreInMemory _invitations = new();
    private readonly MailboxStoreInMemory _mailboxes = new();
    private readonly MailServerInMemory _server = new();
    private readonly NotificationHandlerInMemory _notifications = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly OrganizationAuditInMemory _audit = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly OrganizationsInMemory _organizations;
    private readonly SubjectId _inviter;

    /// <summary>
    /// A staff organization whose mail is integrated, a customer organization whose
    /// mail is not, and an administrator of both who may manage their memberships.
    /// </summary>
    public InvitationServiceTests()
    {
        _organizations = new OrganizationsInMemory(_memberships);
        _organizations.Seed(Staff, administrative: true);
        _organizations.Seed(Customer);

        _configuration.Set(Settings.NotificationLanguages, English);
        _configuration.Set(Settings.RegistrationPhone, AttributeRequirement.Optional);

        _inviter = SubjectId.New(_randomness);
        _passwords.Hold(_inviter, Noon);
        _gate.Grant(_inviter, Staff, Permissions.MembershipManage);
        _gate.Grant(_inviter, Customer, Permissions.MembershipManage);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// IDN-LIFE-009a AC2: the link is time-boxed: the invitation expires the
    /// configured lifetime after it is issued, and the organization's record of it
    /// names the invitation and who issued it.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_009a_AC2_AnInvitationExpiresItsLifetimeAfterItIsIssuedAsync()
    {
        _configuration.Set(Settings.LinkInvitationLifetime, TimeSpan.FromDays(3));

        IssuedInvitation issued = Accepted(await IssueAsync(Customer, Request(email: Personal)));

        Assert.Equal(Noon + TimeSpan.FromDays(3), issued.ExpiresAt);

        Invitation held = Assert.Single(_invitations.Held);

        Assert.Equal(issued.Id, held.Id);
        Assert.Equal(_inviter, held.Inviter);

        OrganizationAuditInMemory.OrganizationChange recorded = Assert.Single(_audit.Changes);

        Assert.Equal(AuditActions.InvitationIssued, recorded.Action);
        Assert.Equal(Customer, recorded.Organization);
        Assert.Equal(issued.Id, recorded.Invitation);
    }

    /// <summary>
    /// REG-INV-001: a bound email is where the link goes, and the token is never
    /// answered to the administrator; with no email bound it is answered, once, for
    /// the administrator to hand over, and nothing is sent.
    /// </summary>
    [Fact]
    public async Task REG_INV_001_TheLinkGoesToTheBoundEmailOrToTheAdministratorAsync()
    {
        IssuedInvitation bound = Accepted(await IssueAsync(Customer, Request(email: Personal)));

        SendRequest sent = Assert.Single(_notifications.Mail);

        Assert.Null(bound.Token);
        Assert.Equal(MessageKind.InvitationLink, sent.Message);
        Assert.Equal(Personal, sent.Destination.Canonical);
        Assert.Equal(
            _invitations.Held[0].Token,
            OpaqueToken.Of(sent.Values["token"]).Fingerprint());

        IssuedInvitation open = Accepted(await IssueAsync(Customer, Request(phone: Number)));

        Assert.Single(_notifications.Sent);
        Assert.NotNull(open.Token);
        Assert.Equal(_invitations.Held[1].Token, OpaqueToken.Of(open.Token).Fingerprint());
    }

    /// <summary>
    /// REG-INV-001: what the invitation binds is kept as the administrator entered it,
    /// and the documents are held at the version current when it is issued.
    /// </summary>
    [Fact]
    public async Task REG_INV_001_TheInvitationKeepsWhatItBindsAndTheVersionsShownAsync()
    {
        _documents.Publish("staff-handbook", "1", Noon.AddDays(-2));
        _documents.Publish("staff-handbook", "2", Noon.AddDays(-1));

        _ = Accepted(await IssueAsync(
            Customer,
            Request(email: "  Person@Elsewhere.test ", phone: Number, documents: ["staff-handbook"])));

        Invitation held = Assert.Single(_invitations.Held);

        Assert.Equal(new InvitedIdentifiers("Person@Elsewhere.test", Number, CorporateEmail: null), held.Identifiers);
        Assert.Equal(new InvitationDocument("staff-handbook", "2"), Assert.Single(held.Documents));
    }

    /// <summary>
    /// REG-INV-001 AC4: an invitation into an organization whose mail is integrated
    /// is refused without a personal email, with the corporate address given as the
    /// personal one, and without the corporate address it asserts; nothing is written
    /// and nothing is reserved.
    /// </summary>
    [Fact]
    public async Task REG_INV_001_AC4_AnIntegratedInvitationNeedsAPersonalEmailAsync()
    {
        Error withoutPersonal = Failure(await IssueAsync(Staff, Request(corporate: Corporate)));
        Error same = Failure(await IssueAsync(Staff, Request(email: Corporate, corporate: Corporate)));
        Error withoutCorporate = Failure(await IssueAsync(Staff, Request(email: Personal)));

        Assert.Equal(ErrorCodes.IdentifierInvalid, withoutPersonal.Code);
        Assert.Equal("email", Member(withoutPersonal));
        Assert.Equal((ErrorCodes.IdentifierInvalid, "email"), (same.Code, Member(same)));
        Assert.Equal(ErrorCodes.IdentifierInvalid, withoutCorporate.Code);
        Assert.Equal("corporateEmail", Member(withoutCorporate));
        Assert.Empty(_invitations.Held);
        Assert.Empty(_mailboxes.Held);
        Assert.Empty(_notifications.Sent);
    }

    /// <summary>
    /// REG-MAIL-001 AC1 and IDN-LIFE-009a AC4: sending a staff invitation reserves the
    /// corporate mailbox, which the next pass creates disabled; the link goes to the
    /// personal email.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_001_AC1_SendingAStaffInvitationCreatesADisabledMailboxAsync()
    {
        _ = Accepted(await IssueAsync(Staff, Request(email: Personal, corporate: Corporate)));

        Mailbox reserved = Assert.Single(_mailboxes.Held);

        Assert.Equal(Corporate, reserved.Address);
        Assert.Equal(reserved.Id, _invitations.Held[0].Mailbox);

        _ = await Publisher.PublishAsync(TestContext.Current.CancellationToken);

        MailboxPush pushed = Assert.Single(_server.Applied);

        Assert.Equal(Corporate, pushed.Address);
        Assert.Equal(MailboxState.Disabled, pushed.State);
        Assert.Equal(Personal, Assert.Single(_notifications.Mail).Destination.Canonical);
    }

    /// <summary>
    /// REG-MAIL-001 AC2: the corporate address is never sent anything at issue: the
    /// link goes to the personal email alone.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_001_AC2_TheCorporateAddressIsSentNothingAsync()
    {
        _ = Accepted(await IssueAsync(Staff, Request(email: Personal, corporate: Corporate, phone: Number)));

        Assert.DoesNotContain(_notifications.Sent, sent => sent.Destination.Canonical == Corporate);
        Assert.DoesNotContain(_notifications.Sent, sent => sent.Destination.Canonical == Number);
    }

    /// <summary>
    /// REG-MAIL-001 AC1: an invitation that expires leaves its mailbox reserved and
    /// disabled; inviting the address again replaces the expired invitation and keeps
    /// the one mailbox.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_001_AC1_ExpiryLeavesTheMailboxReservedUntilTheAddressIsInvitedAgainAsync()
    {
        _ = Accepted(await IssueAsync(Staff, Request(email: Personal, corporate: Corporate)));

        _clock.Advance(Settings.LinkInvitationLifetime.Default + TimeSpan.FromMinutes(1));

        Mailbox reserved = Assert.Single(_mailboxes.Held);

        Assert.True(reserved.IsRemovable);
        Assert.Equal(MailboxState.Disabled, reserved.Owed(stands: false));

        _ = Accepted(await IssueAsync(Staff, Request(email: Personal, corporate: Corporate)));

        Assert.Same(reserved, Assert.Single(_mailboxes.Held));
        Assert.True(_invitations.Held[0].IsRevoked);
        Assert.Null(_invitations.Held[0].Identifiers);
        Assert.True(_invitations.Held[1].Stands);
        Assert.Equal(
            [AuditActions.InvitationIssued, AuditActions.InvitationRevoked, AuditActions.InvitationIssued],
            _audit.Changes.Select(change => change.Action));
    }

    /// <summary>
    /// REG-MAIL-001: an address an open invitation already stands over, or one a
    /// member holds, is not reserved a second time.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_001_AnAddressAlreadyTakenIsRefusedAsync()
    {
        _ = Accepted(await IssueAsync(Staff, Request(email: Personal, corporate: Corporate)));

        Error open = Failure(await IssueAsync(Staff, Request(email: "other@elsewhere.test", corporate: Corporate)));

        var held = Mailbox.Reserved("held@staff.test", Noon);

        held.Hold(SubjectId.New(_randomness));
        _mailboxes.Held.Add(held);

        Error member = Failure(await IssueAsync(Staff, Request(email: Personal, corporate: "held@staff.test")));

        Assert.Equal(ErrorCodes.RequestMalformed, open.Code);
        Assert.Equal("corporateEmail", Member(open));
        Assert.Equal(ErrorCodes.RequestMalformed, member.Code);
        Assert.Equal("corporateEmail", Member(member));
        Assert.Single(_invitations.Held);
    }

    /// <summary>
    /// REG-MAIL-001: where the organization runs its own mail there is no corporate
    /// address for the administrator to assert.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_001_ACorporateAddressIsRefusedWhereTheMailIsNotIntegratedAsync()
    {
        Error customer = Failure(await IssueAsync(Customer, Request(email: Personal, corporate: Corporate)));
        Error unserved = Failure(await ServiceWithout.IssueAsync(
            AccessContext.Of(_inviter),
            Stepped(),
            Staff,
            Request(email: Personal, corporate: Corporate),
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal("corporateEmail", Member(customer));
        Assert.Equal(ErrorCodes.RequestMalformed, unserved.Code);
        Assert.Empty(_mailboxes.Held);
    }

    /// <summary>
    /// REG-DOM-001: the address a member will sign in with is judged against the
    /// organization's lock at issue: the corporate address where the mail is
    /// integrated, the bound email otherwise.
    /// </summary>
    [Fact]
    public async Task REG_DOM_001_TheLockJudgesTheAddressTheMemberWillSignInWithAsync()
    {
        await LockedAsync(Customer, "staff.test");
        await LockedAsync(Staff, "staff.test");

        Assert.Equal(
            ErrorCodes.IdentifierDomainNotAllowed,
            Failure(await IssueAsync(Customer, Request(email: Personal))).Code);
        Assert.Equal(
            ErrorCodes.IdentifierDomainNotAllowed,
            Failure(await IssueAsync(Staff, Request(email: Personal, corporate: "person@elsewhere.example"))).Code);

        _ = Accepted(await IssueAsync(Customer, Request(email: Corporate)));
        _ = Accepted(await IssueAsync(Staff, Request(email: Personal, corporate: Corporate)));
    }

    /// <summary>
    /// REG-INV-001: an identifier that does not read, or whose words mix scripts, is
    /// refused naming the member it was entered in.
    /// </summary>
    [Fact]
    public async Task REG_INV_001_AnIdentifierThatDoesNotReadIsRefusedByMemberAsync()
    {
        Error email = Failure(await IssueAsync(Customer, Request(email: "not an address")));
        Error phone = Failure(await IssueAsync(Customer, Request(phone: "+20(100)1234567")));
        Error mixed = Failure(await IssueAsync(Customer, Request(email: "pаypal@example.test")));

        Assert.Equal((ErrorCodes.IdentifierInvalid, "email"), (email.Code, Member(email)));
        Assert.Equal((ErrorCodes.IdentifierInvalid, "phone"), (phone.Code, Member(phone)));
        Assert.Equal((ErrorCodes.IdentifierMixedScript, "email"), (mixed.Code, Member(mixed)));
    }

    /// <summary>
    /// REG-INV-001: a phone the deployment does not collect is not a phone a
    /// registration could take, so it is not bound.
    /// </summary>
    [Fact]
    public async Task REG_INV_001_APhoneIsNotBoundWhereTheDeploymentCollectsNoneAsync()
    {
        _configuration.Set(Settings.RegistrationPhone, AttributeRequirement.Off);

        Error refused = Failure(await IssueAsync(Customer, Request(phone: Number)));

        Assert.Equal((ErrorCodes.RequestMalformed, "phone"), (refused.Code, Member(refused)));
    }

    /// <summary>
    /// REG-INV-001 and AUTHZ-GRANT-004: the roles an invitation attaches ask what a
    /// grant asks: <c>grant:manage</c> in the organization, a role the deployment
    /// defines, and the permission to administer the deployment for a role that
    /// carries it.
    /// </summary>
    [Fact]
    public async Task REG_INV_001_TheRolesAttachedAskWhatAGrantAsksAsync()
    {
        RoleName clerk = _roles.Define("clerk", Permissions.MembershipManage);
        RoleName root = _roles.Define("root", Permissions.SystemAdminister);

        Assert.Equal(
            ErrorCodes.Denied,
            Failure(await IssueAsync(Customer, Request(email: Personal, roles: [clerk]))).Code);

        _gate.Grant(_inviter, Customer, Permissions.GrantManage);

        Error unknown = Failure(await IssueAsync(Customer, Request(email: Personal, roles: [RoleName.Parse("ghost")])));

        Assert.Equal((ErrorCodes.RequestMalformed, "roles"), (unknown.Code, Member(unknown)));
        Assert.Equal(
            ErrorCodes.Denied,
            Failure(await IssueAsync(Customer, Request(email: Personal, roles: [clerk, root]))).Code);

        _gate.Grant(_inviter, Staff, Permissions.SystemAdminister);

        _ = Accepted(await IssueAsync(Customer, Request(email: Personal, roles: [clerk, root, clerk])));

        Assert.Equal([clerk, root], Assert.Single(_invitations.Held).Roles);
    }

    /// <summary>
    /// REG-INV-001: a document the deployment never published cannot be shown.
    /// </summary>
    [Fact]
    public async Task REG_INV_001_AnUnpublishedDocumentIsRefusedAsync()
    {
        Error refused = Failure(await IssueAsync(Customer, Request(email: Personal, documents: ["unwritten"])));

        Assert.Equal((ErrorCodes.RequestMalformed, "documents"), (refused.Code, Member(refused)));
    }

    /// <summary>
    /// IDN-LIFE-009a: issuing is <c>membership:manage</c> in the organization and the
    /// <c>invitation:issue</c> step-up action, and an organization on its way out
    /// takes no invitation.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_009a_IssuingIsGatedAndSteppedUpAsync()
    {
        var elsewhere = new OrganizationId(Guid.NewGuid());
        var closing = new OrganizationId(Guid.NewGuid());

        _organizations.Seed(elsewhere);
        _organizations.Seed(closing, deletionRequestedAt: Noon);
        _gate.Grant(_inviter, closing, Permissions.MembershipManage);

        Assert.Equal(ErrorCodes.Denied, Failure(await IssueAsync(elsewhere, Request(email: Personal))).Code);
        Assert.Equal(
            (ErrorCodes.RequestMalformed, "id"),
            Coded(Failure(await IssueAsync(closing, Request(email: Personal)))));
        Assert.Equal(
            ErrorCodes.StepUpRequired,
            Failure(await Service.IssueAsync(
                AccessContext.Of(_inviter),
                Stale(),
                Customer,
                Request(email: Personal),
                Source,
                TestContext.Current.CancellationToken)).Code);
        Assert.Empty(_invitations.Held);
        Assert.Empty(_notifications.Sent);
    }

    /// <summary>
    /// IDN-LIFE-009a: a link that could not be sent issues no invitation.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_009a_ALinkThatCouldNotBeSentIssuesNothingAsync()
    {
        _notifications.Refusal = Error.From(ErrorCodes.RestrictionExceeded);

        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Failure(await IssueAsync(Staff, Request(email: Personal, corporate: Corporate))).Code);
        Assert.Empty(_invitations.Held);
        Assert.Empty(_mailboxes.Held);
    }

    /// <summary>
    /// REG-MAIL-001: revoking an invitation forgets what it bound and gives up the
    /// mailbox nobody ever held, which the next pass removes; revoking it again
    /// answers the same.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_001_RevokingGivesUpAMailboxNobodyHeldAsync()
    {
        IssuedInvitation issued = Accepted(await IssueAsync(Staff, Request(email: Personal, corporate: Corporate)));

        _ = await Publisher.PublishAsync(TestContext.Current.CancellationToken);

        Accepted(await RevokeAsync(Staff, issued.Id));
        Accepted(await RevokeAsync(Staff, issued.Id));

        Invitation revoked = Assert.Single(_invitations.Held);

        Assert.True(revoked.IsRevoked);
        Assert.Null(revoked.Identifiers);
        Assert.Equal(MailboxState.Removed, _mailboxes.Held[0].Owed(stands: false));

        _ = await Publisher.PublishAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MailboxState.Removed, _server.Applied[^1].State);
        Assert.Equal(
            [AuditActions.InvitationIssued, AuditActions.InvitationRevoked],
            _audit.Changes.Select(change => change.Action));
    }

    /// <summary>
    /// REG-MAIL-001: a mailbox someone has held is never removed by revoking the
    /// invitation that reserved it again; it stays, disabled.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_001_RevokingKeepsAMailboxSomeoneHeldAsync()
    {
        var retired = Mailbox.Reserved(Corporate, Noon.AddYears(-1));

        retired.Hold(SubjectId.New(_randomness));
        retired.Retire(Noon.AddMonths(-1));
        _mailboxes.Held.Add(retired);

        IssuedInvitation issued = Accepted(await IssueAsync(Staff, Request(email: Personal, corporate: Corporate)));

        Assert.Null(retired.Holder);
        Accepted(await RevokeAsync(Staff, issued.Id));

        Assert.Null(retired.ReleasedAt);
        Assert.Equal(MailboxState.Disabled, retired.Owed(stands: false));
    }

    /// <summary>
    /// IDN-LIFE-009a: an invitation the organization did not issue is not one it can
    /// revoke, and an acknowledged one is used.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_009a_OnlyAnUnacknowledgedInvitationOfTheOrganizationIsRevokedAsync()
    {
        IssuedInvitation issued = Accepted(await IssueAsync(Customer, Request(email: Personal)));

        _invitations.Held.Add(Invitation.Existing(
            InvitationId.New(_clock),
            Customer,
            _inviter,
            token: [1, 2, 3],
            identifiers: null,
            roles: [],
            documents: [],
            mailbox: null,
            issuedAt: Noon,
            expiresAt: Noon.AddDays(7),
            session: null,
            invitee: SubjectId.New(_randomness),
            attachedAt: Noon,
            acknowledgedAt: Noon,
            revokedAt: null));

        Assert.Equal(
            (ErrorCodes.RequestMalformed, "invitationId"),
            Coded(Failure(await RevokeAsync(Staff, issued.Id))));
        Assert.Equal(
            (ErrorCodes.RequestMalformed, "invitationId"),
            Coded(Failure(await RevokeAsync(Customer, InvitationId.New(_clock)))));
        Assert.Equal(
            ErrorCodes.InvitationExpired,
            Failure(await RevokeAsync(Customer, _invitations.Held[1].Id)).Code);
        Assert.Equal(ErrorCodes.Denied, Failure(await RevokeAsync(new OrganizationId(Guid.NewGuid()), issued.Id)).Code);
        Assert.True(_invitations.Held[0].Stands);
    }

    /// <summary>
    /// REG-INV-002 AC1 and IDN-LIFE-009a AC2: a link pressed while signed in attaches
    /// its invitation to that account, once; a token that opens nothing, a spent one
    /// and an expired one are refused alike.
    /// </summary>
    [Fact]
    public async Task REG_INV_002_AC1_ALinkPressedWhileSignedInAttachesToThatAccountAsync()
    {
        string token = Accepted(await IssueAsync(Customer, Request(phone: Number))).Token!;
        string lapsed = Accepted(await IssueAsync(Customer, Request(phone: "+441632960012"))).Token!;
        var holder = SubjectId.New(_randomness);

        Accepted(await OpenAsync(holder, token));

        Invitation attached = _invitations.Held[0];

        Assert.Equal(holder, attached.Invitee);
        Assert.Null(attached.Session);
        Assert.Equal(Noon, attached.AttachedAt);

        Assert.Equal(ErrorCodes.InvitationExpired, Failure(await OpenAsync(SubjectId.New(_randomness), token)).Code);
        Assert.Equal(ErrorCodes.InvitationExpired, Failure(await OpenAsync(holder, "no-such-token")).Code);

        _clock.Advance(Settings.LinkInvitationLifetime.Default);

        Assert.Equal(ErrorCodes.InvitationExpired, Failure(await OpenAsync(holder, lapsed)).Code);
    }

    private InvitationService Service => Serving(_server);

    private InvitationService ServiceWithout => Serving(server: null);

    private MailboxPublisher Publisher =>
        new(_mailboxes, _server, _configuration, new EventsInMemory(), _work, _clock, _randomness);

    private static InvitationRequest Request(
        string? email = null,
        string? phone = null,
        string? corporate = null,
        IReadOnlyList<RoleName>? roles = null,
        IReadOnlyList<string>? documents = null) =>
        new(email, phone, corporate, roles ?? [], documents ?? []);

    private static string? Member(Error error) =>
        error.Details.TryGetValue("member", out JsonElement member) ? member.GetString() : null;

    private static (ErrorCode Code, string? Member) Coded(Error error) => (error.Code, Member(error));

    private static TValue Accepted<TValue>(Result<TValue> outcome) =>
        outcome.Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static void Accepted(Result outcome) =>
        outcome.Switch(() => { }, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static Error Failure<TValue>(Result<TValue> outcome) =>
        outcome.Match(_ => throw new Xunit.Sdk.XunitException("The operation was admitted."), error => error);

    private static Error Failure(Result outcome) =>
        outcome.Match(() => throw new Xunit.Sdk.XunitException("The operation was admitted."), error => error);

    private InvitationService Serving(IMailServer? server)
    {
        var policies = new PolicyResolution(_memberships, _configuration, _raises);

        return new(
            _gate,
            new AdministrativeScope(_gate, _administrative),
            new StepUpGuard(_sessions, _authenticators, _passwords, policies, _clock),
            _organizations,
            _roles,
            _documents,
            new DomainLock(_memberships, _configuration, _domains),
            _invitations,
            _mailboxes,
            server,
            _notifications,
            _configuration,
            _audit,
            _work,
            _clock,
            _randomness);
    }

    private ValueTask<Result> OpenAsync(SubjectId holder, string token) =>
        Service.OpenAsync(AccessContext.Of(holder), token, TestContext.Current.CancellationToken);

    private ValueTask<Result<IssuedInvitation>> IssueAsync(OrganizationId organization, InvitationRequest request) =>
        Service.IssueAsync(
            AccessContext.Of(_inviter),
            Stepped(),
            organization,
            request,
            Source,
            TestContext.Current.CancellationToken);

    private ValueTask<Result> RevokeAsync(OrganizationId organization, InvitationId invitation) =>
        Service.RevokeAsync(
            AccessContext.Of(_inviter),
            organization,
            invitation,
            TestContext.Current.CancellationToken);

    // An organization locked to one domain, verified.
    private async Task LockedAsync(OrganizationId organization, string domain)
    {
        _configuration.Set(
            Settings.OrganizationPolicy,
            organization.ToString(),
            PolicyOverride.None with { EmailDomains = [domain] });

        var listed = LockedDomain.Listed(organization, domain, _randomness, Noon);

        listed.Checked(passed: true, Noon);

        await _domains.AddAsync(listed, TestContext.Current.CancellationToken);
    }

    private SessionId Stepped() => Opened(_clock.GetUtcNow());

    private SessionId Stale() => Opened(_clock.GetUtcNow() - Settings.SessionStepUpRecency.Default - TimeSpan.FromMinutes(1));

    private SessionId Opened(DateTimeOffset at)
    {
        var session = Session.Begin(
            SessionId.New(_clock),
            _inviter,
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            Somewhere,
            at,
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            satisfiesEveryGate: false);

        _sessions.AddAsync(session, Drawn(), Drawn(), TestContext.Current.CancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return session.Id;
    }

    private byte[] Drawn()
    {
        byte[] fingerprint = new byte[32];

        _randomness.GetBytes(fingerprint);

        return fingerprint;
    }
}
