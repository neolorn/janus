using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Invitations;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Organizations;

/// <summary>
/// The invitations over <c>/admin/organizations/{id}/invitations</c> of chapter 09
/// section 8a: issuing a time-boxed link and revoking an unused one, and the end of a
/// membership over <c>/admin/organizations/{id}/memberships</c> (IDN-LIFE-009a,
/// IDN-MEM-001, REG-INV-001, REG-MAIL-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class InvitationEndpointTests : IAsyncDisposable
{
    private const string Personal = """{"email":"invited@elsewhere.test"}""";

    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Branch =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private readonly Janus.Hosting.Tests.Deployment _deployment = new();

    /// <summary>
    /// A deployment able to send, administered by one organization whose mail is
    /// integrated, and holding a second that runs its own.
    /// </summary>
    public InvitationEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
        _deployment.Organizations.Seed(Branch);
        _deployment.Templates.Set(
            MessageKind.InvitationLink,
            SendKind.Email,
            "en",
            new MessageTemplate("invitation", "{token}"));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// IDN-LIFE-009a and REG-INV-001: an invitation binding an email answers
    /// <c>201</c> with when it expires, sends the link there, and answers no token;
    /// one binding no email answers the token for the administrator to hand over.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_009a_AnInvitationIsIssuedAndItsLinkSentAsync()
    {
        Browser administrator = await AuthorisedAsync();

        Answer bound = await administrator.SendAsync("POST", PathOf(Branch), Personal);
        Answer open = await administrator.SendAsync("POST", PathOf(Branch), """{"phone":"+441632960099"}""");

        Assert.Equal(StatusCodes.Status201Created, bound.Status);
        Assert.Equal(
            _deployment.Invitations.Held[0].Id.ToString(),
            bound.Text("id"));
        Assert.Equal(
            _deployment.Clock.GetUtcNow() + Settings.LinkInvitationLifetime.Default,
            bound.Json().GetProperty("expiresAt").GetDateTimeOffset());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, bound.Json().GetProperty("token").ValueKind);
        Assert.Equal(
            _deployment.Invitations.Held[0].Token,
            OpaqueToken.Of(_deployment.Mail.Taken[^1].Body.Trim()).Fingerprint());

        Assert.Equal(StatusCodes.Status201Created, open.Status);
        Assert.Equal(
            _deployment.Invitations.Held[1].Token,
            OpaqueToken.Of(open.Text("token")).Fingerprint());
    }

    /// <summary>
    /// 09 section 8a and REG-INV-001 AC4: an integrated-mail invitation without a
    /// personal email is a validation error, <c>422</c>, naming the member.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_INV_001_AC4_AnIntegratedInvitationWithoutAPersonalEmailIsRefusedAsync()
    {
        Browser administrator = await AuthorisedAsync();

        Answer refused = await administrator.SendAsync(
            "POST",
            PathOf(Administration),
            """{"corporateEmail":"invited@example.test"}""");

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal(ErrorCodes.IdentifierInvalid.ToString(), refused.Text("code"));
        Assert.Equal("email", refused.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Empty(_deployment.Invitations.Held);
        Assert.Empty(_deployment.Mailboxes.Held);
    }

    /// <summary>
    /// REG-MAIL-001 AC1: an integrated-mail invitation reserves the corporate mailbox
    /// and sends the link to the personal email alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_MAIL_001_AC1_AStaffInvitationReservesTheMailboxAsync()
    {
        Browser administrator = await AuthorisedAsync();

        Answer issued = await administrator.SendAsync(
            "POST",
            PathOf(Administration),
            """{"email":"invited@elsewhere.test","corporateEmail":"invited@example.test"}""");

        Assert.Equal(StatusCodes.Status201Created, issued.Status);
        Assert.Equal("invited@example.test", Assert.Single(_deployment.Mailboxes.Held).Address.Value);
        Assert.Equal("invited@elsewhere.test", _deployment.Mail.Taken[^1].Destination.Value);
        Assert.DoesNotContain(_deployment.Mail.Taken, mail => mail.Destination.Value == "invited@example.test");
    }

    /// <summary>
    /// API-CONV-002: a member the endpoint cannot take is a <c>400</c> naming it: a
    /// role name that does not read, and a corporate address where the organization
    /// runs its own mail.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_009a_WhatAnInvitationNamesMustBeReadableAsync()
    {
        Browser administrator = await AuthorisedAsync();

        Answer role = await administrator.SendAsync("POST", PathOf(Branch), """{"roles":["Not A Role"]}""");
        Answer corporate = await administrator.SendAsync(
            "POST",
            PathOf(Branch),
            """{"email":"invited@elsewhere.test","corporateEmail":"invited@example.test"}""");

        Assert.Equal("roles", Member(role));
        Assert.Equal("corporateEmail", Member(corporate));
    }

    /// <summary>
    /// 09 section 8a: issuing needs <c>membership:manage</c> in the organization and
    /// is the <c>invitation:issue</c> step-up action.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_009a_IssuingIsGatedAndSteppedUpAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        Answer withheld = await browser.SendAsync("POST", PathOf(Branch), Personal);

        _deployment.Gate.Grant(subject, Branch, Permissions.MembershipManage);
        _deployment.Clock.Advance(TimeSpan.FromMinutes(16));

        Answer stale = await browser.SendAsync("POST", PathOf(Branch), Personal);

        Assert.Equal(StatusCodes.Status403Forbidden, withheld.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), withheld.Text("code"));
        Assert.Equal(StatusCodes.Status403Forbidden, stale.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), stale.Text("code"));
        Assert.Empty(_deployment.Invitations.Held);
    }

    /// <summary>
    /// BFF-STEP-001 AC2: an invitation refused for step-up is issued when the same
    /// request, carrying the same body, is sent again once the session has stepped up,
    /// so nothing the administrator entered is entered again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_STEP_001_AC2_ARequestRefusedForStepUpSucceedsWhenRetriedAfterItAsync()
    {
        Browser administrator = await AuthorisedAsync();

        _deployment.Clock.Advance(TimeSpan.FromMinutes(16));

        Answer refused = await administrator.SendAsync("POST", PathOf(Branch), Personal);
        Answer began = await administrator.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        Answer stepped = await administrator.SendAsync(
            "POST",
            "/auth/step-up",
            ("challengeId", began.Text("challengeId")),
            ("factor", "password"),
            ("value", Flow.Password));
        Answer retried = await administrator.SendAsync("POST", PathOf(Branch), Personal);

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), refused.Text("code"));
        Assert.Equal(StatusCodes.Status200OK, stepped.Status);
        Assert.Equal("complete", stepped.Text("status"));
        Assert.Equal(StatusCodes.Status201Created, retried.Status);

        Invitation issued = Assert.Single(_deployment.Invitations.Held);

        Assert.Equal(Branch, issued.Organization);
        Assert.Equal("invited@elsewhere.test", issued.Identifiers?.Email);
    }

    /// <summary>
    /// 09 section 8a: revoking an unused invitation answers <c>204</c>, and again;
    /// one the organization never issued is a <c>400</c> naming it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_009a_AnUnusedInvitationIsRevokedAsync()
    {
        Browser administrator = await AuthorisedAsync();

        string id = (await administrator.SendAsync("POST", PathOf(Branch), Personal)).Text("id");

        Answer revoked = await administrator.SendAsync("DELETE", PathOf(Branch) + "/" + id);
        Answer again = await administrator.SendAsync("DELETE", PathOf(Branch) + "/" + id);
        Answer unknown = await administrator.SendAsync("DELETE", PathOf(Branch) + "/" + Guid.NewGuid());

        Assert.Equal(StatusCodes.Status204NoContent, revoked.Status);
        Assert.Equal(StatusCodes.Status204NoContent, again.Status);
        Assert.Equal("invitationId", Member(unknown));
        Assert.True(Assert.Single(_deployment.Invitations.Held).IsRevoked);
        Assert.Equal(
            [AuditActions.InvitationIssued, AuditActions.InvitationRevoked],
            _deployment.OrganizationChanges.Changes.Select(change => change.Action));
    }

    /// <summary>
    /// 09 section 8a and IDN-MEM-001: ending a membership answers <c>204</c> and writes
    /// it down; an account holding no current membership of the organization is a
    /// <c>400</c> naming it, and one without <c>membership:manage</c> is refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_MEM_001_AMembershipIsEndedAsync()
    {
        var member = new SubjectId(Guid.NewGuid());

        _deployment.Memberships.Place(member, Branch);

        Browser browser = await Flow.SignedInAsync(_deployment);
        Answer withheld = await browser.SendAsync("DELETE", MembershipOf(Branch, member));

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Branch, Permissions.MembershipManage);

        Answer ended = await browser.SendAsync("DELETE", MembershipOf(Branch, member));
        Answer again = await browser.SendAsync("DELETE", MembershipOf(Branch, member));

        Assert.Equal(StatusCodes.Status403Forbidden, withheld.Status);
        Assert.Equal(StatusCodes.Status204NoContent, ended.Status);
        Assert.Equal("subject", Member(again));
        Assert.Equal((member, Branch), (Assert.Single(_deployment.Endings.Ended).Subject, _deployment.Endings.Ended[0].Organization));
        Assert.Equal(AuditActions.MembershipEnded, Assert.Single(_deployment.OrganizationChanges.Changes).Action);
    }

    private static string MembershipOf(OrganizationId organization, SubjectId member) =>
        "/admin/organizations/" + organization + "/memberships/" + member;

    private static string PathOf(OrganizationId organization) =>
        "/admin/organizations/" + organization + "/invitations";

    private static string Member(Answer answer)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, answer.Status);

        return answer.Json().GetProperty("details").GetProperty("member").GetString()!;
    }

    private async Task<Browser> AuthorisedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _deployment.Gate.Grant(subject, Administration, Permissions.MembershipManage);
        _deployment.Gate.Grant(subject, Branch, Permissions.MembershipManage);
        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));

        return browser;
    }
}
