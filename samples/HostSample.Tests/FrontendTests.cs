using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Testing;
using Xunit;

namespace HostSample.Tests;

/// <summary>
/// What the workshop's own frontends meet, called through the library's routes as a
/// browser calls them: a sign-in link pressed in a browser the account has never used,
/// the settings the screens are drawn from, the enrolment screen's document and whether
/// a password is set (LIB-TEST-004, D-224). The test moves the deployment's clock past
/// the shipped send restriction and enables sign-in links in the system policy, so it
/// has a deployment of its own.
/// </summary>
/// <param name="host">The deployment.</param>
public sealed partial class FrontendTests(WorkshopHost host) : IClassFixture<WorkshopHost>
{
    // The shipped email destination restriction, one send a minute to an address, which
    // the test waits out between the registration's message and the next one.
    private static readonly TimeSpan PastTheRestriction = TimeSpan.FromMinutes(2);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// AUTH-FACT-016 AC8, LIB-TEST-004: a sign-in link to the account's primary email,
    /// pressed in a browser the account has never signed in from, completes the sign-in
    /// there and then, and the mailbox receives no second message.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_016_AC8_ALinkPressedInANewBrowserSignsInWithNoSecondMailAsync()
    {
        await LinksEnabledAsync();

        EmailAddress email = Address("signs.in.by.link");
        using TestSession customer = await host.CustomerAsync(email, Cancellation);
        using TestVisitor browser = host.Guest();

        host.Clock.Advance(PastTheRestriction);

        int held = host.Mail.SentTo(email).Count;
        Answered begun = await Exchanges.SendAsync(
            browser,
            HttpMethod.Post,
            "auth/begin",
            new { identifier = email.Value },
            Cancellation);
        Answered asked = await Exchanges.SendAsync(
            browser,
            HttpMethod.Post,
            "auth/link",
            new { identifier = email.Value },
            Cancellation);

        await Exchanges.UntilAsync(() => host.Mail.SentTo(email).Count > held, Cancellation);

        Answered pressed = await Exchanges.SendAsync(
            browser,
            HttpMethod.Post,
            "auth/factor",
            new
            {
                challengeId = begun.Body!.Value.GetProperty("challengeId").GetString(),
                factor = "emailLink",
                linkToken = Token(Sent(email, held), "sign-in"),
                press = true,
            },
            Cancellation);
        Answered session = await Exchanges.SendAsync(browser, HttpMethod.Get, "auth/session", null, Cancellation);

        Assert.Equal(HttpStatusCode.OK, begun.Status);
        Assert.Equal(HttpStatusCode.Accepted, asked.Status);
        Assert.Equal(HttpStatusCode.OK, pressed.Status);
        Assert.Equal("complete", pressed.Body!.Value.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, session.Status);
        Assert.Equal(customer.Subject.Value, session.Body!.Value.GetProperty("subject").GetGuid());
        _ = Assert.Single(Sent(email, held));
    }

    /// <summary>
    /// REG-IDENT-001, AUTH-PRIN-002, LIB-TEST-004: a browser that holds no session reads
    /// the settings the screens are drawn from, the system policy's login factors among
    /// them as the deployment last wrote them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_001_ABrowserWithNoSessionReadsTheSettingsAsync()
    {
        await LinksEnabledAsync();

        using TestVisitor browser = host.Guest();

        Answered read = await Exchanges.SendAsync(browser, HttpMethod.Get, "settings", null, Cancellation);

        Assert.Equal(HttpStatusCode.OK, read.Status);
        Assert.Contains(
            read.Body!.Value.GetProperty("policy").GetProperty("loginFactors").EnumerateArray(),
            factor => factor.GetString() == "emailLink");
        Assert.Equal(JsonValueKind.Object, read.Body!.Value.GetProperty("registration").ValueKind);
    }

    /// <summary>
    /// FE-SEC-001, FE-PM-002, AUTH-RECOV-002, LIB-TEST-004: the account read says a
    /// password is set; a re-enrolment the administrator approves opens, in a browser of
    /// its own, an enrolment session whose document names the account's primary email
    /// as the username, says a password is set and offers the password, and
    /// <c>GET /enrol</c> answers the same document.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_SEC_001_AnEnrolmentSessionReadsItsScreensDocumentAsync()
    {
        EmailAddress email = Address("re.enrols");
        using TestSession customer = await host.CustomerAsync(email, Cancellation);

        Answered account = await Exchanges.SendAsync(customer, HttpMethod.Get, "account", null, Cancellation);

        host.Clock.Advance(PastTheRestriction);

        int held = host.Mail.SentTo(email).Count;
        Answered approved = await Exchanges.SteppedAsync(
            host.Administrator,
            HttpMethod.Post,
            "admin/recovery/approve",
            new { subject = customer.Subject.Value, reason = "The customer lost every factor.", channelUsed = email.Value },
            Cancellation);

        await Exchanges.UntilAsync(() => host.Mail.SentTo(email).Count > held, Cancellation);

        using TestVisitor enrolling = host.Guest();
        Answered begun = await Exchanges.SendAsync(
            enrolling,
            HttpMethod.Post,
            "enrol/begin",
            new { token = Token(Sent(email, held), "enrolment") },
            Cancellation);
        Answered read = await Exchanges.SendAsync(enrolling, HttpMethod.Get, "enrol", null, Cancellation);

        Assert.Equal(HttpStatusCode.OK, account.Status);
        Assert.True(account.Body!.Value.GetProperty("password").GetProperty("set").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, approved.Status);
        Assert.Equal(HttpStatusCode.OK, begun.Status);
        Assert.Equal(email.Value, begun.Body!.Value.GetProperty("username").GetString());
        Assert.True(begun.Body!.Value.GetProperty("password").GetProperty("set").GetBoolean());
        Assert.Contains(
            begun.Body!.Value.GetProperty("offers").EnumerateArray(),
            offer => offer.GetString() == "password");
        Assert.Equal(HttpStatusCode.OK, read.Status);
        Assert.Equal(email.Value, read.Body!.Value.GetProperty("username").GetString());
    }

    private static EmailAddress Address(string local) =>
        EmailAddress.TryParse(local + "@kites.example.test", out EmailAddress email)
            ? email
            : throw new InvalidOperationException("The address " + local + " does not read as one.");

    // The token of the last link of a kind the texts carry, in the form chapter 10
    // section 5.43 gives every link the library sends: the landing route, the kind, a
    // dot and the token, in the fragment.
    private static string Token(string[] texts, string kind) =>
        texts
            .SelectMany(text => Landed().Matches(text))
            .LastOrDefault(link => link.Groups["kind"].Value == kind)?
            .Groups["token"].Value
            ?? throw new InvalidOperationException("No message carried a link of the kind " + kind + ".");

    [GeneratedRegex("/link#(?<kind>[a-z-]+)\\.(?<token>[A-Za-z0-9_-]+)")]
    private static partial Regex Landed();

    // The texts of the messages the address received after the count given.
    private string[] Sent(EmailAddress email, int after) =>
        [.. host.Mail.SentTo(email).Skip(after).Select(mail => mail.Body)];

    // AUTH-FACT-002, D-146: sign-in links are off by default and a host enables them in
    // the system policy, which the first administrator writes, stepped up, as an
    // operator does.
    private async Task LinksEnabledAsync()
    {
        Answered read = await Exchanges.SendAsync(
            host.Administrator,
            HttpMethod.Get,
            "admin/config/policy.default",
            null,
            Cancellation);

        JsonObject policy = JsonNode.Parse(read.Body!.Value.GetProperty("value").GetRawText())!.AsObject();
        JsonArray factors = policy["loginFactors"]!.AsArray();

        if (factors.Any(factor => factor!.GetValue<string>() == "emailLink"))
        {
            return;
        }

        factors.Add("emailLink");

        Answered written = await Exchanges.SteppedAsync(
            host.Administrator,
            HttpMethod.Put,
            "admin/config/policy.default",
            new JsonObject { ["value"] = policy, ["reason"] = "The workshop signs its customers in by email links too." },
            Cancellation);

        if (written.Status is not (HttpStatusCode.OK or HttpStatusCode.NoContent))
        {
            throw new InvalidOperationException("PUT admin/config/policy.default answered " + (int)written.Status + " " + written.Code + ".");
        }
    }
}
