using System;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Accounts;

/// <summary>
/// What a browser does to its own standing over the API: the deactivation and the
/// link that reverses it, the deletion window and the link that ends it
/// (IDN-LIFE-013, IDN-LIFE-014, IDN-ACCT-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccountLifecycleFlowTests : IAsyncDisposable
{
    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to send, holding the two notices this exercises.
    /// </summary>
    public AccountLifecycleFlowTests()
    {
        Flow.Prepare(_deployment);

        foreach (MessageKind message in new[]
        {
            MessageKind.DeactivationNotice,
            MessageKind.DeletionNotice,
        })
        {
            foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
            {
                _deployment.Templates.Set(
                    message,
                    kind,
                    "en",
                    new MessageTemplate(kind is SendKind.Email ? "notice" : null, "{token}"));
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// IDN-LIFE-013 AC1: the deactivation is accepted, the notice carries the link,
    /// and the link stands the account back up.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_013_AC1_TheNoticesLinkStandsTheAccountBackUpAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer down = await browser.SendAsync("POST", "/account/deactivate");

        Assert.Equal(StatusCodes.Status202Accepted, down.Status);

        Answer up = await (await ElsewhereAsync()).SendAsync(
            "POST",
            "/account/reactivate",
            ("linkToken", Link()));

        Assert.Equal(StatusCodes.Status204NoContent, up.Status);
    }

    /// <summary>
    /// IDN-LIFE-013: an account an administrator suspended is not stood up by a
    /// link of its own, and is told which of the two it is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_013_AnAdministrativeSuspensionIsAConflictAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _ = await browser.SendAsync("POST", "/account/deactivate");

        _deployment.Accounts.Suspended(Subject(), SuspensionOrigin.Administrator);

        Answer up = await (await ElsewhereAsync()).SendAsync(
            "POST",
            "/account/reactivate",
            ("linkToken", Link()));

        Assert.Equal(StatusCodes.Status409Conflict, up.Status);
        Assert.Equal("identity.account.adminsuspended", up.Text("code"));
    }

    /// <summary>
    /// IDN-LIFE-014: the answer carries when the erasure runs, which is the grace
    /// window the deployment configured.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_014_TheAnswerCarriesWhenTheErasureRunsAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer going = await browser.SendAsync("POST", "/account/delete");

        Assert.Equal(StatusCodes.Status202Accepted, going.Status);

        Assert.Equal(
            _deployment.Clock.GetUtcNow() + Settings.AccountDeletionGrace.Default,
            going.Json().GetProperty("erasesAt").GetDateTimeOffset());

        Assert.Equal(AccountState.Deleting, await StateAsync());
    }

    /// <summary>
    /// IDN-ACCT-007 AC4: the link ends the window and the account is active again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_007_AC4_TheLinkEndsTheWindowAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _ = await browser.SendAsync("POST", "/account/delete");

        Answer stayed = await (await ElsewhereAsync()).SendAsync(
            "POST",
            "/account/delete/cancel",
            ("linkToken", Link()));

        Assert.Equal(StatusCodes.Status204NoContent, stayed.Status);
        Assert.Equal(AccountState.Active, await StateAsync());
    }

    /// <summary>
    /// IDN-ACCT-007 AC4: a window that has run out has nothing left to cancel, and
    /// the answer says so rather than accepting.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_007_AC4_AWindowThatHasRunOutIsRefusedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _ = await browser.SendAsync("POST", "/account/delete");

        string link = Link();

        _deployment.Clock.Advance(Settings.AccountDeletionGrace.Default);

        Answer late = await (await ElsewhereAsync()).SendAsync(
            "POST",
            "/account/delete/cancel",
            ("linkToken", link));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, late.Status);
        Assert.Equal("identity.deletion.windowelapsed", late.Text("code"));
    }

    // The browser the notice's link is opened in, which is never the one that made
    // the request: the operation ended every session the account had.
    private async Task<Browser> ElsewhereAsync()
    {
        var elsewhere = new Browser(_deployment);

        _ = await elsewhere.SendAsync("GET", "/register");

        return elsewhere;
    }

    // The token the notice carried, read off the body the template put it in.
    private string Link() => _deployment.Mail.Taken[^1].Body;

    private SubjectId Subject() => _deployment.Directory.Created[^1].Subject;

    private async Task<AccountState?> StateAsync() =>
        await _deployment.Accounts.StateAsync(Subject(), TestContext.Current.CancellationToken);
}
