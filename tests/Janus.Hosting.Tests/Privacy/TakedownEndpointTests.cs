using System;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Privacy;

/// <summary>
/// The takedown endpoints of chapter 09 section 8a: the trigger, the reading of what
/// the hosts have confirmed, and the reversal (IDN-LIFE-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class TakedownEndpointTests : IAsyncDisposable
{
    private static readonly OrganizationId Company =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, holding one customer's account.
    /// </summary>
    public TakedownEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.AccountStates.Hold(Ahmed, AccountState.Active);
    }

    private static string Takedown => $"/admin/accounts/{Ahmed.Value}/takedown";

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// 09 section 8a: the trigger is answered 202 with the takedown and when its
    /// erasure runs, and the progress read back is that takedown's.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_TheTriggerIsAnsweredWithTheTakedownAndItsErasureDueAsync()
    {
        Browser browser = await AuthorisedAsync();

        Answer triggered = await browser.SendAsync(
            "POST",
            Takedown,
            ("trigger", "staff-report"),
            ("reason", "seen at the counter with a school card"));

        JsonElement takedown = triggered.Json();
        JsonElement progress = (await browser.SendAsync("GET", Takedown)).Json();

        Assert.Equal(StatusCodes.Status202Accepted, triggered.Status);
        Assert.Equal(
            _deployment.Clock.GetUtcNow() + Settings.TakedownGrace.Default,
            takedown.GetProperty("erasureDue").GetDateTimeOffset());
        Assert.Equal(
            takedown.GetProperty("takedownId").GetGuid(),
            progress.GetProperty("takedownId").GetGuid());
        Assert.Equal("awaiting-subscribers", progress.GetProperty("status").GetString());
        Assert.Equal(AccountState.Deleting, _deployment.AccountStates.Of(Ahmed));
    }

    /// <summary>
    /// 10 section 5.12d: a trigger outside the four spellings is malformed and takes
    /// nothing down.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_ATriggerOutsideTheFourSpellingsIsMalformedAsync()
    {
        Browser browser = await AuthorisedAsync();

        Answer refused = await browser.SendAsync(
            "POST",
            Takedown,
            ("trigger", "rumour"),
            ("reason", "somebody said so"));

        Assert.Equal(StatusCodes.Status400BadRequest, refused.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), refused.Text("code"));
        Assert.Equal(AccountState.Active, _deployment.AccountStates.Of(Ahmed));
    }

    /// <summary>
    /// 09 section 8a: the reversal inside the window is answered 204, and one after it
    /// 422 <c>identity.takedown.windowelapsed</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AC5_TheReversalIsAnsweredInsideAndAfterItsWindowAsync()
    {
        Browser browser = await AuthorisedAsync();

        _ = await browser.SendAsync(
            "POST",
            Takedown,
            ("trigger", "customer-report"),
            ("reason", "a parent wrote in"));

        Answer reversed = await browser.SendAsync(
            "POST",
            $"{Takedown}/reverse",
            ("reason", "an adult, misjudged"));

        Assert.Equal(StatusCodes.Status204NoContent, reversed.Status);
        Assert.Equal(AccountState.Active, _deployment.AccountStates.Of(Ahmed));

        _ = await browser.SendAsync(
            "POST",
            Takedown,
            ("trigger", "customer-report"),
            ("reason", "a second letter"));

        _deployment.Clock.Advance(Settings.TakedownGrace.Default);

        // A session a week old proves nothing to step-up, so the reversal is asked
        // for by a member of staff who has just signed in.
        browser = await AuthorisedAsync();

        Answer elapsed = await browser.SendAsync(
            "POST",
            $"{Takedown}/reverse",
            ("reason", "too late"));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, elapsed.Status);
        Assert.Equal(ErrorCodes.TakedownWindowElapsed.ToString(), elapsed.Text("code"));
    }

    /// <summary>
    /// AUTHZ-CONCEAL-005: a customer holding nothing but their own session is refused
    /// every one of the three.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_TheEndpointsAreRefusedToACustomerAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer triggered = await browser.SendAsync(
            "POST",
            Takedown,
            ("trigger", "staff-report"),
            ("reason", "no permission to do this"));

        Assert.Equal(StatusCodes.Status403Forbidden, triggered.Status);
        Assert.Equal(StatusCodes.Status403Forbidden, (await browser.SendAsync("GET", Takedown)).Status);
        Assert.Equal(
            StatusCodes.Status403Forbidden,
            (await browser.SendAsync("POST", $"{Takedown}/reverse", ("reason", "none"))).Status);
        Assert.Equal(AccountState.Active, _deployment.AccountStates.Of(Ahmed));
    }

    private async Task<Browser> AuthorisedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _deployment.PrivacyMemberships.Add(subject, Company);
        _deployment.Gate.Grant(subject, Company, Permissions.TakedownExecute);

        return browser;
    }
}
