using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Sessions;

/// <summary>
/// The two revocations of chapter 09 section 8: one account's sessions, and every
/// session in the deployment (AUTH-SESS-009, AUTH-SESS-011).
/// </summary>
[Trait("kind", "unit")]
public sealed class SessionRevocationEndpointTests : IAsyncDisposable
{
    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private readonly Deployment _deployment = new();
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization.
    /// </summary>
    public SessionRevocationEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _randomness.Dispose();
        await _deployment.DisposeAsync();
    }

    /// <summary>
    /// AUTH-SESS-011 AC1: revoking one account's sessions over the endpoint ends that
    /// account's session and leaves another account's, the caller's own, standing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_011_AC1_OneAccountsSessionsAreRevokedOverTheEndpointAsync()
    {
        Session leaving = await OtherSessionAsync();
        Browser administrator = await AuthorisedAsync(Permissions.SessionRevokeAccount);

        Answer revoked = await administrator.SendAsync(
            "POST",
            $"/admin/accounts/{leaving.Subject.Value}/sessions/revoke");

        Assert.Equal(StatusCodes.Status204NoContent, revoked.Status);
        Assert.NotNull(leaving.EndedAt);
        Assert.Equal(StatusCodes.Status200OK, (await administrator.SendAsync("GET", "/auth/session")).Status);
    }

    /// <summary>
    /// AUTH-SESS-009 AC3: the explicit revocation over the endpoint ends every session,
    /// the caller's own included.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_009_AC3_EverySessionIsRevokedOverTheEndpointAsync()
    {
        Session customer = await OtherSessionAsync();
        Browser administrator = await AuthorisedAsync(Permissions.SessionRevoke);

        Answer revoked = await administrator.SendAsync("POST", "/admin/sessions/revoke-all");

        Assert.Equal(StatusCodes.Status204NoContent, revoked.Status);
        Assert.NotNull(customer.EndedAt);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await administrator.SendAsync("GET", "/auth/session")).Status);
    }

    /// <summary>
    /// AUTH-SESS-011 (D-166): ending another person's sessions is stepped up, judged after
    /// every other refusal: a subject no account bears is answered 404
    /// <c>identity.account.notfound</c>, and once the caller's proof is no longer recent
    /// the endpoint answers 403 <c>auth.stepup.required</c> and ends nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_011_OneAccountsRevocationAsksForStepUpAsync()
    {
        Session leaving = await OtherSessionAsync();
        Browser administrator = await AuthorisedAsync(Permissions.SessionRevokeAccount);

        _deployment.Clock.Advance(Settings.SessionStepUpRecency.Default + TimeSpan.FromMinutes(1));

        Answer unknown = await administrator.SendAsync(
            "POST",
            $"/admin/accounts/{SubjectId.New(_randomness).Value}/sessions/revoke");
        Answer stale = await administrator.SendAsync(
            "POST",
            $"/admin/accounts/{leaving.Subject.Value}/sessions/revoke");

        Assert.Equal(StatusCodes.Status404NotFound, unknown.Status);
        Assert.Equal(ErrorCodes.AccountNotFound.ToString(), unknown.Text("code"));
        Assert.Equal(StatusCodes.Status403Forbidden, stale.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), stale.Text("code"));
        Assert.Null(leaving.EndedAt);
    }

    /// <summary>
    /// AUTH-SESS-009 (D-166): ending every session is stepped up, so once the caller's
    /// proof is no longer recent the endpoint answers 403 <c>auth.stepup.required</c> and
    /// ends nothing, the caller's own session included.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_009_EveryRevocationAsksForStepUpAsync()
    {
        Session customer = await OtherSessionAsync();
        Browser administrator = await AuthorisedAsync(Permissions.SessionRevoke);

        _deployment.Clock.Advance(Settings.SessionStepUpRecency.Default + TimeSpan.FromMinutes(1));

        Answer stale = await administrator.SendAsync("POST", "/admin/sessions/revoke-all");

        Assert.Equal(StatusCodes.Status403Forbidden, stale.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), stale.Text("code"));
        Assert.Null(customer.EndedAt);
        Assert.Equal(StatusCodes.Status200OK, (await administrator.SendAsync("GET", "/auth/session")).Status);
    }

    /// <summary>
    /// AUTHZ-CONCEAL-005 AC1: a caller without the permission is refused forbidden and
    /// nothing ends.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_005_AC1_TheRevocationsAreRefusedWithoutThePermissionAsync()
    {
        Session other = await OtherSessionAsync();
        (Browser customer, _) = await SignedInAsync();

        Answer one = await customer.SendAsync("POST", $"/admin/accounts/{other.Subject.Value}/sessions/revoke");
        Answer every = await customer.SendAsync("POST", "/admin/sessions/revoke-all");

        Assert.Equal(StatusCodes.Status403Forbidden, one.Status);
        Assert.Equal(StatusCodes.Status403Forbidden, every.Status);
        Assert.Null(other.EndedAt);
        Assert.Equal(StatusCodes.Status200OK, (await customer.SendAsync("GET", "/auth/session")).Status);
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction of the administrator committed after the gate
    /// step and before the first write refuses the revoking of one account's sessions
    /// and of every session, and no session is ended.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStepRefusesBothRevocationsAsync()
    {
        Session other = await OtherSessionAsync();
        Browser administrator = await AuthorisedAsync(Permissions.SessionRevokeAccount);

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.SessionRevoke);

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => administrator.SendAsync("POST", $"/admin/accounts/{other.Subject.Value}/sessions/revoke"));
        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => administrator.SendAsync("POST", "/admin/sessions/revoke-all"));

        Assert.Null(other.EndedAt);
        Assert.Equal(StatusCodes.Status200OK, (await administrator.SendAsync("GET", "/auth/session")).Status);
    }

    private async Task<(Browser Browser, SubjectId Subject)> SignedInAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        return (browser, _deployment.Directory.Created[^1].Subject);
    }

    private async Task<Browser> AuthorisedAsync(Permission permission)
    {
        (Browser browser, SubjectId subject) = await SignedInAsync();

        _deployment.Gate.Grant(subject, Administration, permission);

        return browser;
    }

    private async Task<Session> OtherSessionAsync()
    {
        var session = Session.Begin(
            SessionId.New(_deployment.Clock),
            SubjectId.New(_randomness),
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            _deployment.Clock.GetUtcNow(),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        _deployment.Accounts.Stands(session.Subject, AccountState.Active);

        await _deployment.Sessions.AddAsync(
            session,
            OpaqueToken.Draw(_randomness).Fingerprint(),
            OpaqueToken.Draw(_randomness).Fingerprint(),
            TestContext.Current.CancellationToken);

        return session;
    }
}
