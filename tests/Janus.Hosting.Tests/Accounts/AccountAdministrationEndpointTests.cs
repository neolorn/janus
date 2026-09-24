using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Accounts;

/// <summary>
/// The account endpoints of chapter 09 section 8a: an administrator suspends an account
/// and reactivates it (IDN-LIFE-013, AUTH-SESS-010).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccountAdministrationEndpointTests : IAsyncDisposable
{
    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private readonly Deployment _deployment = new();
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization.
    /// </summary>
    public AccountAdministrationEndpointTests()
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
    /// AUTH-SESS-010 and IDN-LIFE-013 AC1: the suspension ends the account's session in
    /// the operation that suspends it, and the reactivation stands the account back up;
    /// without <c>account:manage</c> neither is done.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_010_AnAccountIsSuspendedAndReactivatedAsync()
    {
        Session member = await MemberAsync();
        Browser browser = await Flow.SignedInAsync(_deployment);
        Answer withheld = await browser.SendAsync("POST", PathOf(member.Subject, "suspend"));

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.AccountManage);

        Answer suspended = await browser.SendAsync("POST", PathOf(member.Subject, "suspend"));
        AccountState? held = await StateAsync(member.Subject);
        Answer reactivated = await browser.SendAsync("POST", PathOf(member.Subject, "reactivate"));
        Answer unknown = await browser.SendAsync("POST", PathOf(SubjectId.New(_randomness), "reactivate"));

        Assert.Equal(StatusCodes.Status403Forbidden, withheld.Status);
        Assert.Equal(StatusCodes.Status204NoContent, suspended.Status);
        Assert.Equal(AccountState.Suspended, held);
        Assert.NotNull(member.EndedAt);
        Assert.Equal(StatusCodes.Status204NoContent, reactivated.Status);
        Assert.Equal(AccountState.Active, await StateAsync(member.Subject));
        Assert.Equal(StatusCodes.Status400BadRequest, unknown.Status);
        Assert.Equal("subject", unknown.Json().GetProperty("details").GetProperty("member").GetString());
    }

    private static string PathOf(SubjectId subject, string operation) =>
        "/admin/accounts/" + subject.Value + "/" + operation;

    private async Task<AccountState?> StateAsync(SubjectId subject) =>
        await _deployment.Accounts.StateAsync(subject, TestContext.Current.CancellationToken);

    private async Task<Session> MemberAsync()
    {
        var session = Session.Begin(
            SessionId.New(_deployment.Clock),
            SubjectId.New(_randomness),
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            _deployment.Clock.GetUtcNow(),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            satisfiesEveryGate: false);

        _deployment.Accounts.Stands(session.Subject, AccountState.Active);

        await _deployment.Sessions.AddAsync(
            session,
            OpaqueToken.Draw(_randomness).Fingerprint(),
            OpaqueToken.Draw(_randomness).Fingerprint(),
            TestContext.Current.CancellationToken);

        return session;
    }
}
