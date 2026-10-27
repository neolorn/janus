using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Organizations;

/// <summary>
/// The organizations, their current members and their standing invitations, read over
/// <c>/admin/organizations</c> of chapter 09 section 8a (IDN-READ-001, IDN-READ-002,
/// IDN-READ-004, D-218, D-219).
/// </summary>
[Trait("kind", "unit")]
public sealed class OrganizationReadEndpointTests : IAsyncDisposable
{
    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Branch =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private readonly Janus.Hosting.Tests.Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization and
    /// holding a second.
    /// </summary>
    public OrganizationReadEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
        _deployment.Organizations.Seed(Branch, name: "Harbour office");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// IDN-READ-001 AC1, 09 section 8a: a holder of <c>organization:read</c> in the
    /// administrative organization lists the organizations in pages ordered by
    /// identifier, <c>next</c> null once none remain, and reads one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_001_AC1_TheOrganizationsAreListedInPagesAndOneReadAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.OrganizationRead);

        Answer first = await browser.SendAsync("GET", "/admin/organizations?limit=1");
        Answer second = await browser.SendAsync("GET", $"/admin/organizations?after={Administration.Value}&limit=1");
        Answer read = await browser.SendAsync("GET", $"/admin/organizations/{Branch.Value}");

        Assert.Equal((200, 200, 200), (first.Status, second.Status, read.Status));
        Assert.Equal(
            [Administration.Value],
            first.Json().GetProperty("organizations").EnumerateArray().Select(organization => organization.GetProperty("id").GetGuid()));
        Assert.Equal(Administration.Value, first.Json().GetProperty("next").GetGuid());
        Assert.Equal(
            [Branch.Value],
            second.Json().GetProperty("organizations").EnumerateArray().Select(organization => organization.GetProperty("id").GetGuid()));
        Assert.Equal(JsonValueKind.Null, second.Json().GetProperty("next").ValueKind);
        Assert.Equal(
            (Branch.Value, "Harbour office", false, JsonValueKind.Null),
            (
                read.Json().GetProperty("id").GetGuid(),
                read.Text("name"),
                read.Json().GetProperty("administrative").GetBoolean(),
                read.Json().GetProperty("deletionRequestedAt").ValueKind));
    }

    /// <summary>
    /// 09 section 8a: an <c>after</c> or a <c>limit</c> that does not read, and a
    /// <c>limit</c> outside 1 to 500, are refused 400 <c>api.request.malformed</c> naming
    /// the member.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_001_AnUnreadablePageOrALimitOutOfBoundsIsMalformedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.OrganizationRead);
        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.MembershipRead);

        Answer[] refused =
        [
            await browser.SendAsync("GET", "/admin/organizations?after=one"),
            await browser.SendAsync("GET", "/admin/organizations?limit=many"),
            await browser.SendAsync("GET", "/admin/organizations?limit=501"),
            await browser.SendAsync("GET", $"/admin/organizations/{Branch.Value}/members?after=one"),
            await browser.SendAsync("GET", $"/admin/organizations/{Branch.Value}/members?limit=0"),
        ];

        Assert.Equal(
            [
                ("after", 400),
                ("limit", 400),
                ("limit", 400),
                ("after", 400),
                ("limit", 400),
            ],
            refused.Select(answer => (answer.Json().GetProperty("details").GetProperty("member").GetString(), answer.Status)));
        Assert.All(refused, answer => Assert.Equal(ErrorCodes.RequestMalformed.ToString(), answer.Text("code")));
    }

    /// <summary>
    /// IDN-READ-001, 09 section 8a: an organization the deployment does not hold is
    /// answered 404 <c>identity.organization.notfound</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_001_AnOrganizationNotHeldIsNotFoundAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.OrganizationRead);

        Answer missing = await browser.SendAsync("GET", $"/admin/organizations/{Guid.NewGuid()}");

        Assert.Equal(StatusCodes.Status404NotFound, missing.Status);
        Assert.Equal(ErrorCodes.OrganizationNotFound.ToString(), missing.Text("code"));
    }

    /// <summary>
    /// IDN-READ-002, 09 section 8a: a holder of <c>membership:read</c> in the
    /// organization reads its current members, one of them, and its standing invitations
    /// with what each binds; an account holding no membership is answered 404
    /// <c>identity.membership.notfound</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_002_TheMembersAndStandingInvitationsAreReadAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        var invitation = InvitationId.New(TimeProvider.System);
        var member = new MemberSummary(
            new SubjectId(Guid.NewGuid()),
            new MembershipId(Guid.NewGuid()),
            _deployment.Clock.GetUtcNow().AddDays(-1),
            _deployment.Clock.GetUtcNow().AddDays(30),
            invitation);
        var standing = Invitation.Issued(
            InvitationId.New(TimeProvider.System),
            Branch,
            _deployment.Directory.Created[^1].Subject,
            new InvitedIdentifiers("joiner@example.test", Phone: null, CorporateEmail: null),
            [RoleName.Parse("clerk")],
            documents: [],
            mailbox: null,
            RandomNumberGenerator.GetBytes(32),
            _deployment.Clock.GetUtcNow(),
            TimeSpan.FromDays(7));

        _deployment.Organizations.Join(Branch, member);
        _deployment.Invitations.Held.Add(standing);
        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Branch, Permissions.MembershipRead);

        Answer listed = await browser.SendAsync("GET", $"/admin/organizations/{Branch.Value}/members");
        Answer read = await browser.SendAsync("GET", $"/admin/organizations/{Branch.Value}/members/{member.Subject.Value}");
        Answer none = await browser.SendAsync("GET", $"/admin/organizations/{Branch.Value}/members/{Guid.NewGuid()}");
        Answer invitations = await browser.SendAsync("GET", $"/admin/organizations/{Branch.Value}/invitations");

        JsonElement first = Assert.Single(listed.Json().GetProperty("members").EnumerateArray().ToArray());
        JsonElement issued = Assert.Single(invitations.Json().EnumerateArray().ToArray());

        Assert.Equal((200, 200, 404, 200), (listed.Status, read.Status, none.Status, invitations.Status));
        Assert.Equal(
            (member.Subject.Value, member.Membership.Value, invitation.Value, member.EndsAt),
            (
                first.GetProperty("subject").GetGuid(),
                first.GetProperty("membership").GetGuid(),
                first.GetProperty("invitation").GetGuid(),
                first.GetProperty("endsAt").GetDateTimeOffset()));
        Assert.Equal(member.Subject.Value, read.Json().GetProperty("subject").GetGuid());
        Assert.Equal(ErrorCodes.MembershipNotFound.ToString(), none.Text("code"));
        Assert.Equal(
            (standing.Id.Value, "clerk", "joiner@example.test", JsonValueKind.Null),
            (
                issued.GetProperty("id").GetGuid(),
                Assert.Single(issued.GetProperty("roles").EnumerateArray().ToArray()).GetString(),
                issued.GetProperty("email").GetString(),
                issued.GetProperty("corporateAddress").ValueKind));
    }

    /// <summary>
    /// IDN-READ-004 AC5 (D-219): a holder of <c>membership:read</c> in the administrative
    /// organization reads another organization's members over the endpoint; one holding
    /// it in neither is refused 403 <c>authz.denied</c>, and so is a list of the
    /// organizations by a caller holding <c>organization:read</c> nowhere.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_004_AC5_TheAdministrativeOrganizationReachesAnotherOrganizationsMembersAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer refused = await browser.SendAsync("GET", $"/admin/organizations/{Branch.Value}/members");
        Answer unlisted = await browser.SendAsync("GET", "/admin/organizations");

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.MembershipRead);

        Answer admitted = await browser.SendAsync("GET", $"/admin/organizations/{Branch.Value}/members");

        Assert.Equal((403, 403, 200), (refused.Status, unlisted.Status, admitted.Status));
        Assert.Equal(ErrorCodes.Denied.ToString(), refused.Text("code"));
        Assert.Equal(ErrorCodes.Denied.ToString(), unlisted.Text("code"));
    }
}
