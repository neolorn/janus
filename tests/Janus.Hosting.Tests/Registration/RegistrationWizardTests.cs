using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Registration;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Registration;

/// <summary>
/// The server half of the wizard: the order the steps are reachable in, what a
/// correction does, where the state is held, and what a landing changes
/// (FE-REG-001 to FE-REG-005, FE-VER-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class RegistrationWizardTests : IAsyncDisposable
{
    private const string Short = "tenletters12";
    private const string Second = "other@example.test";
    private const string Terms = "terms-3";
    private const string Notice = "notice-2";

    // The names a destination travels under where an API takes one.
    private static readonly string[] Destinations =
        ["DESTINATION", "REDIRECT", "REDIRECTURI", "RETURNTO", "RETURNURL", "NEXT", "URL", "CONTINUE"];

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment whose messages carry the code and the link token.
    /// </summary>
    public RegistrationWizardTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// FE-REG-001 AC2: no request of the flow takes a destination and no answer of it
    /// forwards one, so there is nowhere for a link to put one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_REG_001_AC2_NoStepTakesOrForwardsADestinationAsync()
    {
        Assert.All(
            Fields(),
            named => Assert.DoesNotContain(
                named.ToUpperInvariant(),
                Destinations,
                StringComparer.Ordinal));

        Browser browser = await Flow.SecuredAsync(_deployment);

        Answer completed = await browser.SendAsync(
            "POST",
            "/register/terms",
            ("termsVersion", Terms),
            ("noticeVersion", Notice));

        Assert.Equal(StatusCodes.Status201Created, completed.Status);
        Assert.Null(completed.Location);
    }

    /// <summary>
    /// FE-REG-002 AC1: a password at the floor that is on no list is taken, so
    /// nothing but the floor and the list stands between the person and the step.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_REG_002_AC1_APasswordAtTheFloorAndOnNoListIsTakenAsync()
    {
        Browser browser = await Flow.SecuredAsync(_deployment);

        Answer state = await browser.SendAsync("GET", "/register");

        Assert.True(state.Json().GetProperty("security").GetProperty("password").GetBoolean());
        Assert.Equal("terms", state.Text("step"));
    }

    /// <summary>
    /// FE-REG-003 AC2: a password below fifteen leaves the step where it was, and the
    /// step after it refuses, so nothing provisional exists.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_REG_003_AC2_AShortPasswordDoesNotCompleteTheStepAsync()
    {
        Browser browser = await ConfirmedAsync();

        Answer set = await browser.SendAsync("PUT", "/register/security", ("password", Short));

        Assert.Equal(StatusCodes.Status200OK, set.Status);
        Assert.Equal("security", set.Text("step"));

        Answer terms = await browser.SendAsync(
            "POST",
            "/register/terms",
            ("termsVersion", Terms),
            ("noticeVersion", Notice));

        Assert.Equal(ErrorCodes.RegistrationIncomplete.ToString(), terms.Text("code"));
    }

    /// <summary>
    /// FE-REG-003 AC3: lengthening the password on the same step lifts the second
    /// step the short one made mandatory, without anything else being sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_REG_003_AC3_LengtheningThePasswordLiftsTheSecondStepAsync()
    {
        Browser browser = await ConfirmedAsync();

        _ = await browser.SendAsync("PUT", "/register/security", ("password", Short));

        Answer lengthened = await browser.SendAsync(
            "PUT",
            "/register/security",
            ("password", Flow.Password));

        Assert.Equal("terms", lengthened.Text("step"));
    }

    /// <summary>
    /// FE-REG-004 AC1: a corrected address is a different destination, so the send
    /// the mistyped one used does not stand in the way of it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_REG_004_AC1_ACorrectedAddressIsSentToAtOnceAsync()
    {
        Browser browser = await Flow.AwaitingAsync(_deployment);

        Answer state = await browser.SendAsync("GET", "/register");

        Answer corrected = await browser.SendAsync(
            "PUT",
            "/register/identifiers/" + Flow.Waiting(state, IdentifierKind.Email),
            ("value", Second));

        Assert.Equal(StatusCodes.Status202Accepted, corrected.Status);
        Assert.Equal(2, _deployment.Mail.Taken.Count);
        Assert.Equal(Second, _deployment.Mail.Taken[^1].Destination.ToString());
    }

    /// <summary>
    /// FE-REG-004 AC2: the restriction counts per destination, so a correction that
    /// arrives back at an address already written to is refused with the instant it
    /// lifts.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_REG_004_AC2_TheSameDestinationStaysRestrictedAsync()
    {
        Browser browser = await Flow.AwaitingAsync(_deployment);

        string staged = Flow.Waiting(await browser.SendAsync("GET", "/register"), IdentifierKind.Email);

        _ = await browser.SendAsync("PUT", "/register/identifiers/" + staged, ("value", Second));

        Answer back = await browser.SendAsync(
            "PUT",
            "/register/identifiers/" + staged,
            ("value", Flow.Address));

        Assert.Equal(StatusCodes.Status429TooManyRequests, back.Status);
        Assert.Equal(ErrorCodes.RestrictionExceeded.ToString(), back.Text("code"));
        Assert.True(back.Json().GetProperty("details").TryGetProperty("retryAt", out _));
    }

    /// <summary>
    /// FE-REG-005 AC1: a step whose predecessor has not completed is refused, so the
    /// order of REG-SESS-002 holds however the frontend arranges its screens.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_REG_005_AC1_NoStepIsReachableBeforeItsPredecessorAsync()
    {
        Browser browser = await Flow.BegunAsync(_deployment);

        Answer early = await browser.SendAsync("PUT", "/register/email", ("value", Flow.Address));

        Assert.Equal(ErrorCodes.AffirmationRequired.ToString(), early.Text("code"));

        _ = await browser.SendAsync("PUT", "/register/age", ("dateOfBirth", "1990-01-01"));
        _ = await browser.SendAsync("PUT", "/register/email", ("value", Flow.Address));

        Answer unconfirmed = await browser.SendAsync("POST", "/register/confirm");

        Assert.Equal(ErrorCodes.RegistrationIncomplete.ToString(), unconfirmed.Text("code"));

        Answer unsecured = await browser.SendAsync(
            "PUT",
            "/register/security",
            ("password", Flow.Password));

        Assert.Equal(ErrorCodes.RegistrationIncomplete.ToString(), unsecured.Text("code"));
    }

    /// <summary>
    /// FE-REG-005 AC2: an unlocked identifier is changed and loses its verification
    /// with the value; a locked one is not changed at all.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_REG_005_AC2_OnlyAnUnlockedIdentifierChangesAsync()
    {
        Browser browser = await Flow.AwaitingAsync(_deployment);

        await Flow.VerifiedAsync(_deployment, browser, IdentifierKind.Email);

        string staged = Staged(await browser.SendAsync("GET", "/register"), IdentifierKind.Email);

        Answer changed = await browser.SendAsync(
            "PUT",
            "/register/identifiers/" + staged,
            ("value", Second));

        Assert.Equal(StatusCodes.Status202Accepted, changed.Status);
        Assert.False(Proved(await browser.SendAsync("GET", "/register"), IdentifierKind.Email));

        Answer locked = await browser.SendAsync(
            "PUT",
            "/register/identifiers/" + Locked(),
            ("value", Second));

        Assert.Equal(ErrorCodes.IdentifierLocked.ToString(), locked.Text("code"));
    }

    /// <summary>
    /// FE-REG-005 AC3: the confirm step waits for every staged identifier, an extra
    /// one is taken up to the maximum, and an unverified extra is discarded again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_REG_005_AC3_ConfirmWaitsForEveryIdentifierAsync()
    {
        _deployment.Configuration.Set(Janus.Core.Configuration.Settings.IdentifiersEmailMax, 2);

        Browser browser = await Flow.AwaitingAsync(_deployment);

        await Flow.VerifiedAsync(_deployment, browser, IdentifierKind.Email);

        _ = await browser.SendAsync("PUT", "/register/phone", ("value", Flow.Number));

        Answer waiting = await browser.SendAsync("POST", "/register/confirm");

        Assert.Equal(ErrorCodes.RegistrationIncomplete.ToString(), waiting.Text("code"));

        await Flow.VerifiedAsync(_deployment, browser, IdentifierKind.Phone);

        Answer added = await browser.SendAsync(
            "POST",
            "/register/identifiers",
            ("kind", "email"),
            ("value", Second));

        Assert.Equal(StatusCodes.Status202Accepted, added.Status);

        Answer beyond = await browser.SendAsync(
            "POST",
            "/register/identifiers",
            ("kind", "email"),
            ("value", "third@example.test"));

        Assert.Equal(ErrorCodes.IdentifierMaximum.ToString(), beyond.Text("code"));

        Answer discarded = await browser.SendAsync(
            "DELETE",
            "/register/identifiers/" + Staged(await browser.SendAsync("GET", "/register"), IdentifierKind.Email, Second));

        Assert.Equal(StatusCodes.Status200OK, discarded.Status);
        Assert.Equal(StatusCodes.Status200OK, (await browser.SendAsync("POST", "/register/confirm")).Status);
    }

    /// <summary>
    /// FE-REG-005 AC4: the state is the server's. Two reads of it agree, and what the
    /// browser holds is a token that says nothing about where the session stands.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_REG_005_AC4_TheStateIsReadBackFromTheServerAsync()
    {
        Browser browser = await Flow.AwaitingAsync(_deployment);

        Answer first = await browser.SendAsync("GET", "/register");
        Answer again = await browser.SendAsync("GET", "/register");

        Assert.Equal(first.Body, again.Body);

        foreach (string held in browser.Cookies.Values)
        {
            Assert.DoesNotContain(Flow.Address, held, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("email", held, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(first.Text("step"), held, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// FE-REG-005 AC5: the end of the flow leaves a session the browser holds and
    /// names no address for it to go to.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_REG_005_AC5_TheEndLeavesASessionAndNoAddressAsync()
    {
        Browser browser = await Flow.SecuredAsync(_deployment);

        Answer completed = await browser.SendAsync(
            "POST",
            "/register/terms",
            ("termsVersion", Terms),
            ("noticeVersion", Notice));

        Assert.Equal(StatusCodes.Status201Created, completed.Status);
        Assert.Empty(completed.Body);
        Assert.Null(completed.Location);
        Assert.True(browser.Cookies.ContainsKey("__Host-janus-session"));
    }

    /// <summary>
    /// FE-VER-001 AC1: a landing that is merely loaded changes nothing, which is what
    /// stops a scanner's prefetch from completing it; the press changes it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_VER_001_AC1_TheLandingChangesNothingUntilItIsPressedAsync()
    {
        Browser browser = await Flow.AwaitingAsync(_deployment);

        string token = Flow.Token(_deployment, IdentifierKind.Email);
        string staged = Flow.Waiting(await browser.SendAsync("GET", "/register"), IdentifierKind.Email);

        Answer loaded = await browser.SendAsync(
            "POST",
            "/register/verify/" + staged,
            ("linkToken", token),
            ("press", false));

        Assert.Equal(StatusCodes.Status200OK, loaded.Status);
        Assert.True(loaded.Json().GetProperty("sameBrowser").GetBoolean());
        Assert.False(Proved(await browser.SendAsync("GET", "/register"), IdentifierKind.Email));

        Answer pressed = await browser.SendAsync(
            "POST",
            "/register/verify/" + staged,
            ("linkToken", token),
            ("press", true));

        Assert.Equal(StatusCodes.Status204NoContent, pressed.Status);
        Assert.True(Proved(await browser.SendAsync("GET", "/register"), IdentifierKind.Email));
    }

    /// <summary>
    /// FE-VER-001 AC3: the browser the link was not sent from is given the code to
    /// type and the means to end the attempt, and ending it ends the registration.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_VER_001_AC3_ElsewhereShowsTheCodeAndEndsTheAttemptAsync()
    {
        Browser browser = await Flow.AwaitingAsync(_deployment);

        string token = Flow.Token(_deployment, IdentifierKind.Email);
        string staged = Flow.Waiting(await browser.SendAsync("GET", "/register"), IdentifierKind.Email);

        var elsewhere = new Browser(_deployment);

        _ = await elsewhere.SendAsync("GET", "/register");

        Answer landed = await elsewhere.SendAsync(
            "POST",
            "/register/verify/" + staged,
            ("linkToken", token),
            ("press", true));

        Assert.False(landed.Json().GetProperty("sameBrowser").GetBoolean());
        Assert.Equal(Flow.Code(_deployment, IdentifierKind.Email), landed.Text("code"));
        Assert.False(Proved(await browser.SendAsync("GET", "/register"), IdentifierKind.Email));

        Answer ended = await elsewhere.SendAsync("POST", "/register/abandon", ("linkToken", token));

        Assert.Equal(StatusCodes.Status204NoContent, ended.Status);
        Assert.Empty(_deployment.Registrations.All);
        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            (await browser.SendAsync("GET", "/register")).Status);
    }

    // Every field of every request the registration endpoints read a body into.
    private static List<string> Fields()
    {
        var named = new List<string>();

        foreach (Type request in typeof(JanusEndpoints).Assembly.GetTypes())
        {
            if (request.Namespace is not "Janus.Hosting.Registration"
                || !request.Name.EndsWith("Request", StringComparison.Ordinal))
            {
                continue;
            }

            named.AddRange(request.GetProperties().Select(property => property.Name));
        }

        return named;
    }

    // The identifier of a staged one of a kind, whatever its verification state.
    private static string Staged(Answer state, IdentifierKind kind, string? value = null)
    {
        foreach (JsonElement staged in state.Json().GetProperty("identifiers").EnumerateArray())
        {
            if (!string.Equals(staged.GetProperty("kind").GetString(), Named(kind), StringComparison.Ordinal))
            {
                continue;
            }

            if (value is null || string.Equals(staged.GetProperty("value").GetString(), value, StringComparison.Ordinal))
            {
                return staged.GetProperty("id").GetString()!;
            }
        }

        throw new InvalidOperationException("Nothing of that kind is staged.");
    }

    // Whether the staged identifier of a kind stands verified.
    private static bool Proved(Answer state, IdentifierKind kind)
    {
        foreach (JsonElement staged in state.Json().GetProperty("identifiers").EnumerateArray())
        {
            if (string.Equals(staged.GetProperty("kind").GetString(), Named(kind), StringComparison.Ordinal))
            {
                return staged.GetProperty("verified").GetBoolean();
            }
        }

        throw new InvalidOperationException("Nothing of that kind is staged.");
    }

    private static string Named(IdentifierKind kind) => kind is IdentifierKind.Email ? "email" : "phone";

    // An invitation is what locks an identifier, and no endpoint of this phase issues
    // one, so the session is given the locked identity directly.
    private string Locked()
    {
        RegistrationSession session = _deployment.Registrations.All.Single();
        var id = IdentifierId.New(_deployment.Clock);

        session.Stage(StagedIdentity.Of(id, IdentifierKind.Phone, Flow.Number, Flow.Number, isLocked: true));

        return id.Value.ToString();
    }

    // A browser that has verified its address and passed the confirm step, which is
    // where the security step becomes reachable.
    private async Task<Browser> ConfirmedAsync()
    {
        Browser browser = await Flow.AwaitingAsync(_deployment);

        await Flow.VerifiedAsync(_deployment, browser, IdentifierKind.Email);

        _ = await browser.SendAsync("PUT", "/register/phone", ("value", Flow.Number));

        await Flow.VerifiedAsync(_deployment, browser, IdentifierKind.Phone);

        Assert.Equal(
            StatusCodes.Status200OK,
            (await browser.SendAsync("POST", "/register/confirm")).Status);

        return browser;
    }
}
