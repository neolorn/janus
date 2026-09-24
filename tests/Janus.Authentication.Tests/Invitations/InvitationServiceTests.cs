using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Invitations;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Organizations;
using Janus.Authentication.Policies;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Identifiers;
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
/// Issuing an invitation into an organization, revoking it, and acknowledging it into a
/// membership (IDN-LIFE-009a, REG-INV-001, REG-INV-002, REG-MAIL-001).
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
    private readonly AccountDirectoryInMemory _accounts = new(PreferenceDeclarations.None);
    private readonly MailboxStoreInMemory _mailboxes = new();
    private readonly MailServerInMemory _server = new();
    private readonly NotificationHandlerInMemory _notifications = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly OrganizationAuditInMemory _audit = new();
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly EventsInMemory _events = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly OrganizationsInMemory _organizations;
    private readonly MembershipAttachmentInMemory _attachments;
    private readonly SubjectId _inviter;

    /// <summary>
    /// A staff organization whose mail is integrated, a customer organization whose
    /// mail is not, and an administrator of both who may manage their memberships.
    /// </summary>
    public InvitationServiceTests()
    {
        _organizations = new OrganizationsInMemory(_memberships);
        _attachments = new MembershipAttachmentInMemory(_memberships);
        _organizations.Seed(Staff, administrative: true);
        _organizations.Seed(Customer, name: "Northern branch");

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

    /// <summary>
    /// REG-INV-002: the membership step reads the standing invitation the account
    /// opened last: the organization by its name, who invited them by the name that
    /// account shows and nothing else of theirs, the roles and the documents at their
    /// versions; an account nothing stands attached to reads that none is.
    /// </summary>
    [Fact]
    public async Task REG_INV_002_TheMembershipStepReadsTheInvitationOpenedLastAsync()
    {
        RoleName clerk = _roles.Define("clerk", Permissions.MembershipManage);

        _gate.Grant(_inviter, Customer, Permissions.GrantManage);
        _documents.Publish("staff-handbook", "2", Noon.AddDays(-1));
        _accounts.Holds(_inviter, new HeldProfile(Named("Ada"), LegalName: null, DateOfBirth: null, PhotoUpdatedAt: null));

        string earlier = Accepted(await IssueAsync(Customer, Request(phone: Number))).Token!;
        string later = Accepted(await IssueAsync(
            Customer,
            Request(phone: "+441632960012", roles: [clerk], documents: ["staff-handbook"]))).Token!;
        var holder = SubjectId.New(_randomness);

        Assert.Equal(ErrorCodes.InvitationNotFound, Failure(await AttachedAsync(holder)).Code);

        Accepted(await OpenAsync(holder, earlier));
        _clock.Advance(TimeSpan.FromMinutes(1));
        Accepted(await OpenAsync(holder, later));

        AttachedInvitation read = Accepted(await AttachedAsync(holder));

        Assert.Equal(_invitations.Held[1].Id, read.Id);
        Assert.Equal(Customer, read.Organization);
        Assert.Equal("Northern branch", read.OrganizationName);
        Assert.Equal("Ada", read.InvitedBy);
        Assert.Equal([clerk], read.Roles);
        Assert.Equal([new InvitationDocument("staff-handbook", "2")], read.Documents);
        Assert.Equal(_invitations.Held[1].ExpiresAt, read.ExpiresAt);

        Accepted(await RevokeAsync(Customer, read.Id));
        _accounts.Holds(_inviter, new HeldProfile(DisplayName: null, LegalName: null, DateOfBirth: null, PhotoUpdatedAt: null));

        AttachedInvitation remaining = Accepted(await AttachedAsync(holder));

        Assert.Equal(_invitations.Held[0].Id, remaining.Id);
        Assert.Null(remaining.InvitedBy);
        Assert.Equal(
            ErrorCodes.InvitationNotFound,
            Failure(await AttachedAsync(SubjectId.New(_randomness))).Code);
    }

    /// <summary>
    /// REG-INV-001 AC2 and AC3, IDN-LIFE-009a AC1: until the person acknowledges, the
    /// account holds no membership of the organization and no grant; acknowledging
    /// attaches the membership carrying the documents at the versions shown, grants the
    /// roles across the organization as given by who invited them, announces it and
    /// writes it down; the invitation forgets what it bound and stands no longer.
    /// </summary>
    [Fact]
    public async Task REG_INV_001_AC3_AcknowledgingAttachesTheMembershipThatWasShownAsync()
    {
        RoleName clerk = _roles.Define("clerk", Permissions.MembershipManage);

        _gate.Grant(_inviter, Customer, Permissions.GrantManage);
        _documents.Publish("staff-handbook", "2", Noon.AddDays(-1));

        string token = Accepted(await IssueAsync(
            Customer,
            Request(phone: Number, roles: [clerk], documents: ["staff-handbook"]))).Token!;
        SubjectId holder = Holder();
        Invitation invitation = _invitations.Held[0];

        _ = _identifiers.Verified(holder, IdentifierKind.Phone, Number);
        Accepted(await OpenAsync(holder, token));

        Assert.Empty(_attachments.Attached);
        Assert.Empty(await _memberships.OfAsync(holder, TestContext.Current.CancellationToken));

        _clock.Advance(TimeSpan.FromMinutes(5));
        Accepted(await AcknowledgeAsync(holder, invitation.Id));

        AttachedMembership attached = Assert.Single(_attachments.Attached);
        MembershipChanged began = Assert.Single(_events.Of<MembershipChanged>());
        OrganizationAuditInMemory.OrganizationChange recorded = _audit.Changes[^1];

        Assert.Equal((holder, Customer), (attached.Subject, attached.Organization));
        Assert.Equal([new InvitationDocument("staff-handbook", "2")], attached.Acknowledged);
        Assert.Equal([clerk], attached.Roles);
        Assert.Equal(_inviter, attached.GrantedBy);
        Assert.Equal($"invitation:{invitation.Id.Value}", attached.Reason);
        Assert.Equal(Noon.AddMinutes(5), attached.At);
        Assert.Equal((attached.Id, Customer, MembershipChange.Began), (began.Membership, began.Organization, began.Change));
        Assert.Equal(holder, began.Subject);
        Assert.Empty(_events.Of<IdentifierPrimaryChanged>());
        Assert.Equal(AuditActions.InvitationAcknowledged, recorded.Action);
        Assert.Equal((Customer, holder), (recorded.Organization, recorded.Actor));
        Assert.Equal(invitation.Id, recorded.Invitation);
        Assert.Equal(Noon.AddMinutes(5), invitation.AcknowledgedAt);
        Assert.Null(invitation.Identifiers);
        Assert.Equal(ErrorCodes.InvitationNotFound, Failure(await AttachedAsync(holder)).Code);
        Assert.Equal(ErrorCodes.InvitationExpired, Failure(await AcknowledgeAsync(holder, invitation.Id)).Code);
    }

    /// <summary>
    /// REG-INV-001 AC4, REG-MAIL-001 AC1 and AC5, IDN-LIFE-009a AC4: acknowledging an
    /// invitation into an organization whose mail is integrated makes the corporate
    /// address the primary email, verified and locked, keeps the personal email
    /// verified beside it as the membership's, gives the person the mailbox, which is
    /// then owed enabled, and tells the set as it stood of the address once.
    /// </summary>
    [Fact]
    public async Task REG_INV_001_AC4_TheCorporateAddressBecomesPrimaryBesideThePersonalEmailAsync()
    {
        _ = Accepted(await IssueAsync(Staff, Request(email: Personal, corporate: Corporate)));

        string token = _notifications.Mail[^1].Values["token"];
        SubjectId holder = Holder();
        IdentifierId personal = _identifiers.Verified(holder, IdentifierKind.Email, Personal);
        Invitation invitation = _invitations.Held[0];
        Mailbox reserved = Assert.Single(_mailboxes.Held);

        _authenticators.Hold(Passkey(holder));
        Accepted(await OpenAsync(holder, token));
        _notifications.Sent.Clear();

        Accepted(await AcknowledgeAsync(holder, invitation.Id));

        HeldIdentifiers held = await _identifiers.HeldAsync(holder, TestContext.Current.CancellationToken);
        HeldIdentifier corporate = held.OfKind(IdentifierKind.Email).Single(email => email.Canonical == Corporate);
        HeldIdentifier kept = held.Find(personal)!;
        IdentifierPrimaryChanged promoted = Assert.Single(_events.Of<IdentifierPrimaryChanged>());
        SendRequest told = Assert.Single(_notifications.Sent);

        Assert.True(corporate is { IsVerified: true, IsPrimary: true, IsLocked: true, IsPersonal: false });
        Assert.True(kept is { IsVerified: true, IsPrimary: false, IsPersonal: true });
        Assert.Equal((corporate.Id, IdentifierKind.Email), (promoted.Identifier, promoted.Kind));
        Assert.Equal(holder, promoted.Subject);
        Assert.Equal((Personal, MessageKind.IdentifierAdded), (told.Destination.Canonical, told.Message));
        Assert.Equal(holder, reserved.Holder);
        Assert.Equal(MailboxState.Enabled, reserved.Owed(stands: true));
        Assert.Equal(Staff, Assert.Single(_attachments.Attached).Organization);
    }

    /// <summary>
    /// REG-IDENT-002 and REG-MAIL-001: the corporate address counts against
    /// <c>identifiers.email.max</c> as any added email does, so an account already
    /// holding as many as it may is refused and nothing attaches.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_001_TheCorporateAddressCountsAgainstTheEmailMaximumAsync()
    {
        _configuration.Set(Settings.IdentifiersEmailMax, 1);

        _ = Accepted(await IssueAsync(Staff, Request(email: Personal, corporate: Corporate)));

        string token = _notifications.Mail[^1].Values["token"];
        SubjectId holder = Holder();

        _ = _identifiers.Verified(holder, IdentifierKind.Email, Personal);
        _authenticators.Hold(Passkey(holder));
        Accepted(await OpenAsync(holder, token));

        Assert.Equal(
            ErrorCodes.IdentifierMaximum,
            Failure(await AcknowledgeAsync(holder, _invitations.Held[0].Id)).Code);
        Assert.Empty(_attachments.Attached);
        Assert.Null(Assert.Single(_mailboxes.Held).Holder);
    }

    /// <summary>
    /// REG-INV-002 AC2 and IDN-LIFE-009b: an account below the organization's required
    /// assurance, counting only the factors that organization permits, is sent to
    /// enrol and nothing attaches; once it holds a permitted factor that reaches the
    /// level, the membership attaches, and the policy it then holds no longer counts
    /// what the organization does not permit.
    /// </summary>
    [Fact]
    public async Task REG_INV_002_AC2_AnAccountBelowTheRequiredAssuranceIsHeldAtEnrolmentAsync()
    {
        _configuration.Set(
            Settings.OrganizationPolicy,
            Customer.ToString(),
            PolicyOverride.None with
            {
                RequiredAssurance = AssuranceLevel.Aal2,
                LoginFactors = new HashSet<Factor> { Factor.Passkey, Factor.Totp },
            });

        string token = Accepted(await IssueAsync(Customer, Request(phone: Number))).Token!;
        SubjectId holder = Holder();
        Invitation invitation = _invitations.Held[0];

        _ = _identifiers.Verified(holder, IdentifierKind.Phone, Number);
        Accepted(await OpenAsync(holder, token));

        Error held = Failure(await AcknowledgeAsync(holder, invitation.Id));

        Assert.Equal(ErrorCodes.StepUpRequired, held.Code);
        Assert.Equal("enrol", held.Details["outcome"].GetString());
        Assert.Equal("requiredAssurance", held.Details["field"].GetString());
        Assert.Equal("aal2", held.Details["value"].GetString());
        Assert.Empty(_attachments.Attached);
        Assert.Null(invitation.AcknowledgedAt);

        _authenticators.Hold(Passkey(holder));
        Accepted(await AcknowledgeAsync(holder, invitation.Id));

        Policy joined = Accepted(await Policies.ForAsync(holder, TestContext.Current.CancellationToken));

        Assert.Single(_attachments.Attached);
        Assert.DoesNotContain(Factor.Password, joined.LoginFactors);
    }

    /// <summary>
    /// REG-INV-002: where the organization enforces credential redundancy, an account
    /// with one credential that is not backed up is sent to enrol another.
    /// </summary>
    [Fact]
    public async Task REG_INV_002_EnforcedRedundancyAsksForASecondCredentialAsync()
    {
        _configuration.Set(
            Settings.OrganizationPolicy,
            Customer.ToString(),
            PolicyOverride.None with { CredentialRedundancy = CredentialRedundancy.Enforced });

        string token = Accepted(await IssueAsync(Customer, Request(phone: Number))).Token!;
        SubjectId holder = Holder();

        _ = _identifiers.Verified(holder, IdentifierKind.Phone, Number);
        _authenticators.Hold(Passkey(holder, backedUp: false));
        Accepted(await OpenAsync(holder, token));

        Error held = Failure(await AcknowledgeAsync(holder, _invitations.Held[0].Id));

        Assert.Equal(ErrorCodes.StepUpRequired, held.Code);
        Assert.Equal("credentialRedundancy", held.Details["field"].GetString());
        Assert.Equal("enforced", held.Details["value"].GetString());
        Assert.Empty(_attachments.Attached);
    }

    /// <summary>
    /// REG-INV-002: every identifier the invitation binds is one the accepting account
    /// holds verified, and a corporate address another account holds cannot be taken
    /// on; either is the mismatch, and nothing attaches.
    /// </summary>
    [Fact]
    public async Task REG_INV_002_AnIdentifierTheInvitationBindsIsVerifiedOnTheAccountAsync()
    {
        string token = Accepted(await IssueAsync(Customer, Request(phone: Number))).Token!;
        SubjectId holder = Holder();
        SubjectId other = Holder();

        _ = _identifiers.Verified(other, IdentifierKind.Phone, Number);
        Accepted(await OpenAsync(holder, token));

        Assert.Equal(
            ErrorCodes.InvitationIdentifierMismatch,
            Failure(await AcknowledgeAsync(holder, _invitations.Held[0].Id)).Code);

        _ = Accepted(await IssueAsync(Staff, Request(email: Personal, corporate: Corporate)));

        string staff = _notifications.Mail[^1].Values["token"];
        SubjectId member = Holder();

        _ = _identifiers.Verified(member, IdentifierKind.Email, Personal);
        _ = _identifiers.Verified(other, IdentifierKind.Email, Corporate);
        _authenticators.Hold(Passkey(member));
        Accepted(await OpenAsync(member, staff));

        Assert.Equal(
            ErrorCodes.InvitationIdentifierMismatch,
            Failure(await AcknowledgeAsync(member, _invitations.Held[1].Id)).Code);
        Assert.Empty(_attachments.Attached);
        Assert.Null(Assert.Single(_mailboxes.Held).Holder);
    }

    /// <summary>
    /// REG-INV-002 and IDN-LIFE-009a AC2: an invitation attached to another account, or
    /// to none, is answered as one that does not exist; one that expired, was revoked,
    /// or whose organization is on its way out no longer stands.
    /// </summary>
    [Fact]
    public async Task REG_INV_002_OnlyAStandingInvitationOfTheAccountIsAcknowledgedAsync()
    {
        string token = Accepted(await IssueAsync(Customer, Request(phone: Number))).Token!;
        string revoked = Accepted(await IssueAsync(Customer, Request(phone: "+441632960012"))).Token!;
        SubjectId holder = Holder();
        Invitation invitation = _invitations.Held[0];

        _ = _identifiers.Verified(holder, IdentifierKind.Phone, Number);
        _ = _identifiers.Verified(holder, IdentifierKind.Phone, "+441632960012");

        Assert.Equal(ErrorCodes.InvitationNotFound, Failure(await AcknowledgeAsync(holder, invitation.Id)).Code);

        Accepted(await OpenAsync(holder, token));
        Accepted(await OpenAsync(holder, revoked));
        Accepted(await RevokeAsync(Customer, _invitations.Held[1].Id));

        Assert.Equal(
            ErrorCodes.InvitationNotFound,
            Failure(await AcknowledgeAsync(Holder(), invitation.Id)).Code);
        Assert.Equal(
            ErrorCodes.InvitationNotFound,
            Failure(await AcknowledgeAsync(holder, new InvitationId(Guid.NewGuid()))).Code);
        Assert.Equal(
            ErrorCodes.InvitationExpired,
            Failure(await AcknowledgeAsync(holder, _invitations.Held[1].Id)).Code);

        _organizations.Seed(Customer, deletionRequestedAt: Noon, name: "Northern branch");

        Assert.Equal(ErrorCodes.InvitationExpired, Failure(await AcknowledgeAsync(holder, invitation.Id)).Code);

        _organizations.Seed(Customer, name: "Northern branch");
        _clock.Advance(Settings.LinkInvitationLifetime.Default);

        Assert.Equal(ErrorCodes.InvitationExpired, Failure(await AcknowledgeAsync(holder, invitation.Id)).Code);
        Assert.Equal(
            ErrorCodes.Denied,
            Failure(await Service.AcknowledgeAsync(
                AccessContext.Of(SystemPrincipal.ForOrganization("sweep", "expiry", Customer)),
                invitation.Id,
                Source,
                TestContext.Current.CancellationToken)).Code);
        Assert.Empty(_attachments.Attached);
    }

    /// <summary>
    /// IDN-MEM-002: an account that may hold no further membership is refused with the
    /// code, and nothing of the invitation is spent.
    /// </summary>
    [Fact]
    public async Task IDN_MEM_002_AnAccountAtItsMembershipLimitIsRefusedAsync()
    {
        string token = Accepted(await IssueAsync(Customer, Request(phone: Number))).Token!;
        SubjectId holder = Holder();

        _ = _identifiers.Verified(holder, IdentifierKind.Phone, Number);
        _memberships.Place(holder, Staff);
        _authenticators.Hold(Passkey(holder));
        Accepted(await OpenAsync(holder, token));

        Assert.Equal(
            ErrorCodes.MembershipLimitReached,
            Failure(await AcknowledgeAsync(holder, _invitations.Held[0].Id)).Code);
        Assert.True(_invitations.Held[0].Stands);
        Assert.Empty(_events.Published);
    }

    /// <summary>
    /// PRIV-RIGHT-005a and REG-MAIL-001 AC1: the sweep forgets what an invitation that
    /// expired unused bound, in one transaction, and leaves its mailbox reserved; one
    /// still in time keeps what it binds.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_001_AC1_TheSweepForgetsWhatAnExpiredInvitationBoundAsync()
    {
        _ = Accepted(await IssueAsync(Staff, Request(email: Personal, corporate: Corporate)));
        _clock.Advance(TimeSpan.FromDays(1));
        _ = Accepted(await IssueAsync(Customer, Request(phone: Number)));
        _clock.Advance(Settings.LinkInvitationLifetime.Default - TimeSpan.FromDays(1));
        _work.Reset();

        Assert.Equal(1, await Service.SweepAsync(TestContext.Current.CancellationToken));
        Assert.Null(_invitations.Held[0].Identifiers);
        Assert.NotNull(_invitations.Held[1].Identifiers);
        Assert.True(Assert.Single(_mailboxes.Held).IsRemovable);
        Assert.Equal((1, 1), (_work.Opened, _work.Committed));
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

    private PolicyResolution Policies => new(_memberships, _configuration, _raises);

    private InvitationService Serving(IMailServer? server)
    {
        PolicyResolution policies = Policies;

        return new(
            _gate,
            new AdministrativeScope(_gate, _administrative),
            new StepUpGuard(_sessions, _authenticators, _passwords, policies, _clock),
            _organizations,
            _roles,
            _documents,
            new DomainLock(_memberships, _configuration, _domains),
            _invitations,
            _accounts,
            new InvitationAcknowledgement(
                _invitations,
                _organizations,
                _identifiers,
                _authenticators,
                _passwords,
                policies,
                _attachments,
                _mailboxes,
                _notifications,
                _events,
                _configuration,
                _audit,
                _work,
                _clock),
            _mailboxes,
            server,
            _notifications,
            _configuration,
            _audit,
            _work,
            _clock,
            _randomness);
    }

    private static DisplayName Named(string name) =>
        DisplayName.TryParse(name, out DisplayName named)
            ? named
            : throw new InvalidOperationException("The name does not parse.");

    // An account holding a password, which is what reaches the system policy's floor.
    private SubjectId Holder()
    {
        var holder = SubjectId.New(_randomness);

        _passwords.Hold(holder, Noon);

        return holder;
    }

    private Authenticator Passkey(SubjectId holder, bool backedUp = true) =>
        Authenticator.WebAuthnCredential(
            AuthenticatorId.New(_clock),
            holder,
            Factor.Passkey,
            CredentialLabel.TryParse("This laptop", out CredentialLabel label)
                ? label
                : throw new InvalidOperationException("The label does not parse."),
            new WebAuthnMaterial(
                new byte[] { 1, 2, 3 },
                new byte[] { 4, 5, 6 },
                Algorithm: -7,
                "example.test",
                Counter: 0,
                BackupEligible: backedUp,
                BackupState: backedUp),
            Noon);

    private ValueTask<Result> AcknowledgeAsync(SubjectId holder, InvitationId invitation) =>
        Service.AcknowledgeAsync(
            AccessContext.Of(holder),
            invitation,
            Source,
            TestContext.Current.CancellationToken);

    private ValueTask<Result<AttachedInvitation>> AttachedAsync(SubjectId holder) =>
        Service.AttachedAsync(AccessContext.Of(holder), TestContext.Current.CancellationToken);

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
