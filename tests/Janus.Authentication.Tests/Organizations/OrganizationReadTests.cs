using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Authentication.Organizations;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Invitations;
using Janus.Authentication.Tests.Policies;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Organizations;

/// <summary>
/// The organizations a caller reads, one of them, its current members and its standing
/// invitations (IDN-READ-001, IDN-READ-002, IDN-READ-004, D-218, D-219).
/// </summary>
[Trait("kind", "unit")]
public sealed class OrganizationReadTests
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly MembershipLookupInMemory _memberships = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly InvitationStoreInMemory _invitations = new();
    private readonly ReadVolumeInMemory _volume = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly OrganizationsInMemory _organizations;
    private readonly OrganizationId _staff = Fresh();
    private readonly SubjectId _caller = new(Guid.NewGuid());

    /// <summary>
    /// A deployment with its administrative organization.
    /// </summary>
    public OrganizationReadTests()
    {
        _organizations = new OrganizationsInMemory(_memberships);
        _organizations.Seed(_staff, administrative: true);
        _administrative.Organization = _staff;
    }

    private OrganizationReads Reads =>
        new(
            new ReadAdmissionInMemory(_gate, _administrative, _memberships),
            _organizations,
            _invitations,
            _volume,
            _clock);

    private AccessContext Asking => AccessContext.Of(_caller);

    /// <summary>
    /// IDN-READ-001 AC1 (D-218, D-219): a holder of <c>organization:read</c> in the
    /// administrative organization lists every organization the deployment holds but the
    /// erased ones, ordered by identifier, in pages, <c>next</c> naming the last one
    /// answered while more remain.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_001_AC1_AnAdministrativeHolderListsEveryOrganizationButTheErasedInPagesAsync()
    {
        OrganizationId[] held = [Seeded(), Seeded(), _staff];
        OrganizationId erased = Fresh();

        _organizations.Seed(erased, erasedAt: Noon);
        _gate.Grant(_caller, _staff, Permissions.OrganizationRead);

        OrganizationId[] ordered = [.. held.OrderBy(organization => organization.Value)];
        OrganizationPage first = Succeeded(await Reads.ListAsync(Asking, after: null, 2, TestContext.Current.CancellationToken));
        OrganizationPage second = Succeeded(await Reads.ListAsync(Asking, first.Next, 2, TestContext.Current.CancellationToken));

        Assert.Equal(ordered[..2], first.Organizations.Select(organization => organization.Id));
        Assert.Equal(ordered[1], first.Next);
        Assert.Equal([ordered[2]], second.Organizations.Select(organization => organization.Id));
        Assert.Null(second.Next);
        Assert.Equal(
            new OrganizationSummary(_staff, _staff.ToString(), Administrative: true, DeletionRequestedAt: null),
            first.Organizations.Concat(second.Organizations).Single(organization => organization.Id == _staff));
    }

    /// <summary>
    /// IDN-READ-001 AC1 (D-219): a holder of <c>organization:read</c> in another
    /// organization lists that organization alone, not another it is a member of without
    /// holding the permission there.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_001_AC1_AHolderInAnotherOrganizationListsThatOrganizationAloneAsync()
    {
        OrganizationId reading = Seeded();
        OrganizationId member = Seeded();

        _memberships.Place(_caller, reading);
        _memberships.Place(_caller, member);
        _gate.Grant(_caller, reading, Permissions.OrganizationRead);

        OrganizationPage listed = Succeeded(
            await Reads.ListAsync(Asking, after: null, limit: null, TestContext.Current.CancellationToken));

        Assert.Equal([reading], listed.Organizations.Select(organization => organization.Id));
        Assert.Null(listed.Next);
    }

    /// <summary>
    /// IDN-READ-001 (D-219): a caller holding <c>organization:read</c> nowhere is refused
    /// <c>authz.denied</c>, with one refusal recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_001_ACallerHoldingItNowhereIsRefusedOnceAsync()
    {
        _memberships.Place(_caller, Seeded());

        Error refused = Failed(await Reads.ListAsync(Asking, after: null, limit: null, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.Denied, refused.Code);
        Assert.Single(_gate.Refusals);
    }

    /// <summary>
    /// IDN-READ-001, IDN-READ-004 (D-219): a declared job lists its own organization,
    /// and no other.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_004_AJobListsItsOwnOrganizationAsync()
    {
        OrganizationId own = Seeded();
        _ = Seeded();

        OrganizationPage listed = Succeeded(await Reads.ListAsync(
            AccessContext.Of(SystemPrincipal.ForOrganization("digest", "the weekly digest", own)),
            after: null,
            limit: null,
            TestContext.Current.CancellationToken));

        Assert.Equal([own], listed.Organizations.Select(organization => organization.Id));
    }

    /// <summary>
    /// IDN-READ-004 (D-219): a declared job reads its own organization's members, and is
    /// refused another's, even acting for the administrative organization.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_004_AJobReadsItsOwnOrganizationAloneAsync()
    {
        OrganizationId own = Seeded();
        MemberSummary member = Joined(own, invitation: null);

        MemberPage read = Succeeded(await Reads.MembersAsync(
            AccessContext.Of(SystemPrincipal.ForOrganization("digest", "the weekly digest", own)),
            own,
            after: null,
            limit: null,
            TestContext.Current.CancellationToken));
        Error refused = Failed(await Reads.MembersAsync(
            AccessContext.Of(SystemPrincipal.ForOrganization("digest", "the weekly digest", _staff)),
            own,
            after: null,
            limit: null,
            TestContext.Current.CancellationToken));

        Assert.Equal([member], read.Members);
        Assert.Equal(ErrorCodes.Denied, refused.Code);
    }

    /// <summary>
    /// <c>09</c> section 8a: a <c>limit</c> outside 1 to 500 is refused
    /// <c>api.request.malformed</c> naming <c>limit</c>, before anything is asked, and the
    /// bounds themselves are read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_001_ALimitOutsideOneTo500IsMalformedAsync()
    {
        OrganizationId organization = Seeded();

        _gate.Grant(_caller, _staff, Permissions.OrganizationRead);
        _gate.Grant(_caller, _staff, Permissions.MembershipRead);

        Error[] refused =
        [
            Failed(await Reads.ListAsync(Asking, after: null, 0, TestContext.Current.CancellationToken)),
            Failed(await Reads.ListAsync(Asking, after: null, 501, TestContext.Current.CancellationToken)),
            Failed(await Reads.MembersAsync(Asking, organization, after: null, 0, TestContext.Current.CancellationToken)),
        ];

        Assert.All(refused, error => Assert.Equal(
            (ErrorCodes.RequestMalformed, "limit"),
            (error.Code, error.Details["member"].GetString())));
        _ = Succeeded(await Reads.ListAsync(Asking, after: null, 1, TestContext.Current.CancellationToken));
        _ = Succeeded(await Reads.ListAsync(Asking, after: null, 500, TestContext.Current.CancellationToken));
        Assert.Empty(_gate.Refusals);
    }

    /// <summary>
    /// IDN-READ-001 (D-218): an erased organization is read as one the deployment never
    /// held, <c>identity.organization.notfound</c>, before its permission is asked, so
    /// even a caller holding nothing is answered so and no refusal is recorded; one held
    /// is read as listed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_001_AnErasedOrganizationIsReadAsOneNeverHeldAsync()
    {
        OrganizationId erased = Fresh();
        OrganizationId deleting = Fresh();

        _organizations.Seed(erased, deletionRequestedAt: Noon.AddDays(-31), erasedAt: Noon);
        _organizations.Seed(deleting, deletionRequestedAt: Noon, name: "Harbour office");

        Error[] refused =
        [
            Failed(await Reads.ReadAsync(Asking, erased, TestContext.Current.CancellationToken)),
            Failed(await Reads.ReadAsync(Asking, Fresh(), TestContext.Current.CancellationToken)),
            Failed(await Reads.MembersAsync(Asking, erased, after: null, limit: null, TestContext.Current.CancellationToken)),
        ];

        _gate.Grant(_caller, deleting, Permissions.OrganizationRead);

        Assert.All(refused, error => Assert.Equal(ErrorCodes.OrganizationNotFound, error.Code));
        Assert.Empty(_gate.Refusals);
        Assert.Equal(
            new OrganizationSummary(deleting, "Harbour office", Administrative: false, Noon),
            Succeeded(await Reads.ReadAsync(Asking, deleting, TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// IDN-READ-002 AC1 (D-218): a member who joined through an invitation is listed with
    /// it, and one bootstrap attached with none, ordered by subject.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_002_AC1_AMemberWhoJoinedThroughAnInvitationIsListedWithItAsync()
    {
        OrganizationId organization = Seeded();
        MemberSummary invited = Joined(organization, InvitationId.New(_clock));
        MemberSummary founding = Joined(organization, invitation: null);

        _gate.Grant(_caller, organization, Permissions.MembershipRead);

        MemberPage listed = Succeeded(
            await Reads.MembersAsync(Asking, organization, after: null, limit: null, TestContext.Current.CancellationToken));

        Assert.Equal(
            new[] { invited, founding }.OrderBy(member => member.Subject.Value),
            listed.Members);
        Assert.Null(listed.Next);
    }

    /// <summary>
    /// IDN-READ-002 AC2 (D-218): an account whose membership ended answers
    /// <c>identity.membership.notfound</c>, and one whose end is set in advance is listed
    /// and read with it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_002_AC2_AnEndedMembershipIsNotFoundAndOneEndingIsListedWithItsEndAsync()
    {
        OrganizationId organization = Seeded();
        MemberSummary departed = Joined(organization, invitation: null);
        MemberSummary ending = Joined(organization, invitation: null) with { EndsAt = Noon.AddDays(30) };

        _organizations.Join(organization, ending);
        _ = _memberships.Leave(departed.Subject, organization);
        _gate.Grant(_caller, organization, Permissions.MembershipRead);

        MemberPage listed = Succeeded(
            await Reads.MembersAsync(Asking, organization, after: null, limit: null, TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.MembershipNotFound,
            Failed(await Reads.MemberAsync(Asking, organization, departed.Subject, TestContext.Current.CancellationToken)).Code);
        Assert.Equal([ending], listed.Members);
        Assert.Equal(
            ending,
            Succeeded(await Reads.MemberAsync(Asking, organization, ending.Subject, TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// IDN-READ-002 AC1, <c>09</c> section 8a: the members are read in pages after the
    /// subject the last page ended at.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_002_TheMembersAreReadInPagesAsync()
    {
        OrganizationId organization = Seeded();
        SubjectId[] members =
        [
            .. Enumerable.Range(0, 3)
                .Select(_ => Joined(organization, invitation: null).Subject)
                .OrderBy(subject => subject.Value),
        ];

        _gate.Grant(_caller, organization, Permissions.MembershipRead);

        MemberPage first = Succeeded(
            await Reads.MembersAsync(Asking, organization, after: null, 2, TestContext.Current.CancellationToken));
        MemberPage second = Succeeded(
            await Reads.MembersAsync(Asking, organization, first.Next, 2, TestContext.Current.CancellationToken));

        Assert.Equal(members[..2], first.Members.Select(member => member.Subject));
        Assert.Equal(members[1], first.Next);
        Assert.Equal([members[2]], second.Members.Select(member => member.Subject));
        Assert.Null(second.Next);
    }

    /// <summary>
    /// IDN-READ-002 AC3 (D-218): an invitation acknowledged, revoked or past its lifetime
    /// is not listed; one standing is, with its roles, when it was issued and expires,
    /// who issued it and the identifiers it binds.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_002_AC3_AnInvitationAcknowledgedRevokedOrExpiredIsNotListedAsync()
    {
        OrganizationId organization = Seeded();
        SubjectId inviter = new(Guid.NewGuid());
        Invitation standing = Issued(organization, inviter, Noon.AddHours(-1));
        Invitation revoked = Issued(organization, inviter, Noon.AddHours(-1));
        Invitation acknowledged = Issued(organization, inviter, Noon.AddHours(-1));
        Invitation expired = Issued(organization, inviter, Noon.AddDays(-8));

        revoked.Revoke(Noon);
        acknowledged.AttachTo(new SubjectId(Guid.NewGuid()), Noon);
        acknowledged.Acknowledge(Noon);
        _invitations.Held.AddRange([standing, revoked, acknowledged, expired, Issued(Seeded(), inviter, Noon)]);
        _gate.Grant(_caller, organization, Permissions.MembershipRead);

        IReadOnlyList<StandingInvitation> listed = Succeeded(
            await Reads.StandingAsync(Asking, organization, TestContext.Current.CancellationToken));

        Assert.Equal(
            new StandingInvitation(
                standing.Id,
                standing.Roles,
                standing.IssuedAt,
                standing.ExpiresAt,
                inviter,
                "invited@example.test",
                "+441632960011",
                CorporateAddress: null),
            Assert.Single(listed));
    }

    /// <summary>
    /// IDN-READ-004 AC5 (D-219): a holder of <c>membership:read</c> in the administrative
    /// organization lists the members of another organization, and reads one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_004_AC5_AnAdministrativeHolderListsAnotherOrganizationsMembersAsync()
    {
        OrganizationId organization = Seeded();
        MemberSummary member = Joined(organization, invitation: null);

        _gate.Grant(_caller, _staff, Permissions.MembershipRead);

        MemberPage listed = Succeeded(
            await Reads.MembersAsync(Asking, organization, after: null, limit: null, TestContext.Current.CancellationToken));

        Assert.Equal([member], listed.Members);
        Assert.Equal(
            member,
            Succeeded(await Reads.MemberAsync(Asking, organization, member.Subject, TestContext.Current.CancellationToken)));
        Assert.Empty(_gate.Refusals);
    }

    /// <summary>
    /// IDN-READ-004 AC5 (D-219): a holder of <c>membership:read</c> in neither the
    /// organization the path names nor the administrative organization is refused
    /// <c>authz.denied</c> by each read, one refusal recorded for each.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_004_AC5_AHolderInNeitherOrganizationIsRefusedOnceAsync()
    {
        OrganizationId organization = Seeded();
        OrganizationId elsewhere = Seeded();
        MemberSummary member = Joined(organization, invitation: null);

        _gate.Grant(_caller, elsewhere, Permissions.MembershipRead);

        Error[] refused =
        [
            Failed(await Reads.MembersAsync(Asking, organization, after: null, limit: null, TestContext.Current.CancellationToken)),
            Failed(await Reads.MemberAsync(Asking, organization, member.Subject, TestContext.Current.CancellationToken)),
            Failed(await Reads.StandingAsync(Asking, organization, TestContext.Current.CancellationToken)),
        ];

        Assert.All(refused, error => Assert.Equal(ErrorCodes.Denied, error.Code));
        Assert.Equal(3, _gate.Refusals.Count);
        Assert.Empty(_volume.Returned);
    }

    /// <summary>
    /// IDN-READ-004 AC4, OPS-ALERT-005: each member a person reads counts toward their
    /// read volume, a page by the members it answers.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_004_EachMemberAPersonReadsIsCountedAsync()
    {
        OrganizationId organization = Seeded();
        MemberSummary member = Joined(organization, invitation: null);

        _ = Joined(organization, invitation: null);
        _gate.Grant(_caller, organization, Permissions.MembershipRead);

        _ = Succeeded(
            await Reads.MembersAsync(Asking, organization, after: null, limit: null, TestContext.Current.CancellationToken));
        _ = Succeeded(await Reads.MemberAsync(Asking, organization, member.Subject, TestContext.Current.CancellationToken));

        Assert.Equal([2, 1], _volume.Returned.Select(returned => returned.Records));
        Assert.All(_volume.Returned, returned => Assert.Equal(_caller, returned.Context.Effective));
    }

    private static OrganizationId Fresh() => new(Guid.NewGuid());

    private static TValue Succeeded<TValue>(Result<TValue> outcome) =>
        outcome.Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static Error Failed<TValue>(Result<TValue> outcome) =>
        outcome.Match(_ => throw new Xunit.Sdk.XunitException("The read was answered."), error => error);

    private static Invitation Issued(OrganizationId organization, SubjectId inviter, DateTimeOffset issuedAt) =>
        Invitation.Issued(
            InvitationId.New(TimeProvider.System),
            organization,
            inviter,
            new InvitedIdentifiers("invited@example.test", "+441632960011", CorporateEmail: null),
            [RoleName.Parse("clerk")],
            documents: [],
            mailbox: null,
            RandomNumberGenerator.GetBytes(32),
            issuedAt,
            TimeSpan.FromDays(7));

    private OrganizationId Seeded()
    {
        OrganizationId organization = Fresh();

        _organizations.Seed(organization);

        return organization;
    }

    private MemberSummary Joined(OrganizationId organization, InvitationId? invitation)
    {
        var member = new MemberSummary(
            new SubjectId(Guid.NewGuid()),
            new MembershipId(Guid.NewGuid()),
            Noon.AddDays(-1),
            EndsAt: null,
            invitation);

        _organizations.Join(organization, member);

        return member;
    }
}
