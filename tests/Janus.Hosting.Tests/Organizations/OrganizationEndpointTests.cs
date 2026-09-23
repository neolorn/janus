using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Organizations;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Organizations;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Organizations;

/// <summary>
/// The organization lifecycle over <c>/admin/organizations</c> of chapter 09 section 8a,
/// behind <c>organization:manage</c> in the administrative organization, with a deletion
/// request stepped up and ending every member session (IDN-ORG-002 to IDN-ORG-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class OrganizationEndpointTests : IAsyncDisposable
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
    public OrganizationEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
        _deployment.Organizations.Seed(Branch);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// IDN-ORG-002: an organization is created under a name with its policy key holding
    /// no override, answered with its identifier, and written down with who and why.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_002_AnOrganizationIsCreatedAsync()
    {
        (Browser administrator, SubjectId actor) = await AuthorisedAsync(Administration);

        Answer created = await administrator.SendAsync(
            "POST",
            "/admin/organizations",
            ("name", "  Northern branch  "),
            ("reason", "  Opening a branch.  "));

        Assert.Equal(StatusCodes.Status201Created, created.Status);

        OrganizationId organization = new(Guid.Parse(created.Text("id")));
        OrganizationAuditInMemory.OrganizationChange change = Assert.Single(_deployment.OrganizationChanges.Changes);

        Assert.Equal("Northern branch", _deployment.Organizations.NameOf(organization));
        Assert.Equal(PolicyOverride.None, await PolicyAsync(organization));
        Assert.Equal(AuditActions.OrganizationCreated, change.Action);
        Assert.Equal(organization, change.Organization);
        Assert.Equal("Opening a branch.", change.Reason);
        Assert.Equal(actor, change.Actor);
    }

    /// <summary>
    /// IDN-ORG-003 AC1: a deletion request suspends the organization and ends every
    /// session of its members, so the first request on any of them is refused; the
    /// request is written down.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_003_AC1_ADeletionRequestEndsEveryMemberSessionAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Administration);
        Session member = await SessionOfAsync(new SubjectId(Guid.NewGuid()));
        Session outsider = await SessionOfAsync(new SubjectId(Guid.NewGuid()));

        _deployment.Memberships.Place(member.Subject, Branch);

        Answer requested = await RequestedAsync(administrator, Branch);
        OrganizationStanding standing = await StandingAsync(Branch);

        Assert.Equal(StatusCodes.Status204NoContent, requested.Status);
        Assert.Equal(_deployment.Clock.GetUtcNow(), standing.DeletionRequestedAt);
        Assert.Equal(_deployment.Clock.GetUtcNow(), member.EndedAt);
        Assert.Null(outsider.EndedAt);
        Assert.Equal(StatusCodes.Status200OK, (await administrator.SendAsync("GET", "/auth/session")).Status);
        Assert.Equal(
            AuditActions.OrganizationDeletionRequested,
            Assert.Single(_deployment.OrganizationChanges.Changes).Action);
    }

    /// <summary>
    /// IDN-ORG-003: a request for an organization whose deletion is already requested
    /// changes nothing and records nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_003_ASecondRequestChangesNothingAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Administration);

        _ = await RequestedAsync(administrator, Branch);
        _deployment.Clock.Advance(TimeSpan.FromMinutes(1));

        Answer again = await RequestedAsync(administrator, Branch);
        OrganizationStanding standing = await StandingAsync(Branch);

        Assert.Equal(StatusCodes.Status204NoContent, again.Status);
        Assert.Equal(_deployment.Clock.GetUtcNow() - TimeSpan.FromMinutes(1), standing.DeletionRequestedAt);
        Assert.Single(_deployment.OrganizationChanges.Changes);
    }

    /// <summary>
    /// IDN-ORG-002: the administrative organization cannot be deleted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_002_TheAdministrativeOrganizationIsProtectedAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Administration);

        Answer refused = await RequestedAsync(administrator, Administration);
        OrganizationStanding standing = await StandingAsync(Administration);

        Assert.Equal(StatusCodes.Status409Conflict, refused.Status);
        Assert.Equal(ErrorCodes.OrganizationProtected.ToString(), refused.Text("code"));
        Assert.Null(standing.DeletionRequestedAt);
        Assert.Empty(_deployment.OrganizationChanges.Changes);
    }

    /// <summary>
    /// AUTH-STEP-001 and 09 section 8a: a deletion request and its cancellation are the
    /// <c>organization:delete</c> step-up action, so a session whose proof is no longer
    /// recent changes neither; creating an organization, which touches no account, is
    /// not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_001_ADeletionAndItsCancellationAreAStepUpActionAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Administration);
        OrganizationId other = new(Guid.NewGuid());

        _deployment.Organizations.Seed(other, deletionRequestedAt: _deployment.Clock.GetUtcNow());
        _deployment.Clock.Advance(TimeSpan.FromMinutes(16));

        Answer requested = await RequestedAsync(administrator, Branch);
        Answer cancelled = await CancelledAsync(administrator, other);
        Answer created = await administrator.SendAsync(
            "POST",
            "/admin/organizations",
            ("name", "Southern branch"),
            ("reason", "Opening a branch."));
        OrganizationStanding standing = await StandingAsync(Branch);

        Assert.Equal(StatusCodes.Status403Forbidden, requested.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), requested.Text("code"));
        Assert.Null(standing.DeletionRequestedAt);
        Assert.Equal(StatusCodes.Status403Forbidden, cancelled.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), cancelled.Text("code"));
        Assert.NotNull((await StandingAsync(other)).DeletionRequestedAt);
        Assert.Equal(StatusCodes.Status201Created, created.Status);
        Assert.Equal(
            [AuditActions.OrganizationCreated],
            _deployment.OrganizationChanges.Changes.Select(change => change.Action));
    }

    /// <summary>
    /// IDN-ORG-004: a deletion request is cancelled within the grace window, which
    /// restores the organization and is written down; once the window has elapsed it
    /// cannot be.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_004_ARequestIsCancelledOnlyWithinTheWindowAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Administration);
        OrganizationId late = new(Guid.NewGuid());
        DateTimeOffset now = _deployment.Clock.GetUtcNow();

        _deployment.Organizations.Seed(Branch, deletionRequestedAt: now - TimeSpan.FromDays(29));
        _deployment.Organizations.Seed(late, deletionRequestedAt: now - TimeSpan.FromDays(30));

        Answer cancelled = await CancelledAsync(administrator, Branch);
        Answer elapsed = await CancelledAsync(administrator, late);
        Answer idle = await CancelledAsync(administrator, Administration);

        Assert.Equal(StatusCodes.Status204NoContent, cancelled.Status);
        Assert.Null((await StandingAsync(Branch)).DeletionRequestedAt);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, elapsed.Status);
        Assert.Equal(ErrorCodes.DeletionWindowElapsed.ToString(), elapsed.Text("code"));
        Assert.NotNull((await StandingAsync(late)).DeletionRequestedAt);
        Assert.Equal(StatusCodes.Status204NoContent, idle.Status);
        Assert.Equal(
            [AuditActions.OrganizationDeletionCancelled],
            _deployment.OrganizationChanges.Changes.Select(change => change.Action));
    }

    /// <summary>
    /// IDN-ORG-002 and 10: the lifecycle is <c>organization:manage</c> in the
    /// administrative organization; the permission held in the organization itself,
    /// whose grants a suspension silences, is not enough.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_002_TheLifecycleIsGovernedFromTheAdministrativeOrganizationAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Branch);

        Answer created = await administrator.SendAsync(
            "POST",
            "/admin/organizations",
            ("name", "Southern branch"),
            ("reason", "Opening a branch."));
        Answer requested = await RequestedAsync(administrator, Branch);
        Answer cancelled = await CancelledAsync(administrator, Branch);

        Assert.Equal(StatusCodes.Status403Forbidden, created.Status);
        Assert.Equal(StatusCodes.Status403Forbidden, requested.Status);
        Assert.Equal(StatusCodes.Status403Forbidden, cancelled.Status);
        Assert.Empty(_deployment.OrganizationChanges.Changes);
    }

    /// <summary>
    /// IDN-ORG-002 and API-CONV-002: a change names a name and a reason of 1 to 1024
    /// characters and an organization the deployment holds; anything else is a
    /// malformed request naming the field.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_002_WhatAChangeNamesMustBeReadableAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Administration);

        Answer unnamed = await administrator.SendAsync(
            "POST",
            "/admin/organizations",
            ("name", " "),
            ("reason", "Opening a branch."));
        Answer overlong = await administrator.SendAsync(
            "POST",
            "/admin/organizations",
            ("name", new string('n', 1025)),
            ("reason", "Opening a branch."));
        Answer unreasoned = await administrator.SendAsync(
            "POST",
            "/admin/organizations",
            ("name", "Southern branch"));
        Answer silent = await RequestedAsync(administrator, Branch, reason: " ");
        Answer unheld = await RequestedAsync(administrator, new OrganizationId(Guid.NewGuid()));
        Answer unknown = await CancelledAsync(administrator, new OrganizationId(Guid.NewGuid()));

        Assert.Equal("name", Member(unnamed));
        Assert.Equal("name", Member(overlong));
        Assert.Equal("reason", Member(unreasoned));
        Assert.Equal("reason", Member(silent));
        Assert.Equal("id", Member(unheld));
        Assert.Equal("id", Member(unknown));
        Assert.Empty(_deployment.OrganizationChanges.Changes);
    }

    private static string Member(Answer answer)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, answer.Status);

        return answer.Json().GetProperty("details").GetProperty("member").GetString()!;
    }

    private static Task<Answer> RequestedAsync(
        Browser administrator,
        OrganizationId organization,
        string reason = "Closing the branch.") =>
        administrator.SendAsync("POST", "/admin/organizations/" + organization + "/delete", ("reason", reason));

    private static Task<Answer> CancelledAsync(Browser administrator, OrganizationId organization) =>
        administrator.SendAsync(
            "POST",
            "/admin/organizations/" + organization + "/delete/cancel",
            ("reason", "Kept open."));

    // A session of a principal other than the one the browser signed in, begun as a
    // sign-in would.
    private async Task<Session> SessionOfAsync(SubjectId subject)
    {
        using var randomness = RandomNumberGenerator.Create();

        var session = Session.Begin(
            SessionId.New(_deployment.Clock),
            subject,
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            _deployment.Clock.GetUtcNow(),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            satisfiesEveryGate: false);

        await _deployment.Sessions.AddAsync(
            session,
            OpaqueToken.Draw(randomness).Fingerprint(),
            OpaqueToken.Draw(randomness).Fingerprint(),
            TestContext.Current.CancellationToken);

        return session;
    }

    // The policy key the deployment holds for the organization, where one was written.
    private async Task<PolicyOverride?> PolicyAsync(OrganizationId organization) =>
        (await _deployment.Configuration.ReadWrittenAsync(Settings.OrganizationPolicy, CancellationToken.None))
            .Match(written => written.GetValueOrDefault(organization.ToString()), _ => null);

    private async Task<OrganizationStanding> StandingAsync(OrganizationId organization) =>
        (await _deployment.Organizations.FindAsync(organization, CancellationToken.None))!;

    private async Task<(Browser Browser, SubjectId Subject)> SignedInAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        return (browser, _deployment.Directory.Created[^1].Subject);
    }

    private async Task<(Browser Browser, SubjectId Subject)> AuthorisedAsync(OrganizationId organization)
    {
        (Browser browser, SubjectId subject) = await SignedInAsync();

        _deployment.Gate.Grant(subject, organization, Permissions.OrganizationManage);

        return (browser, subject);
    }
}
