using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Testing;
using Xunit;

namespace HostSample.Tests;

/// <summary>
/// The sign-on between two origins, as a host with a second application meets it: the
/// workshop runs as the authentication application and again, on the same deployment, at
/// an origin of its own as a second application, and a person starting at the second's
/// sign-on signs in at the first and is shown the second's route (LIB-TEST-004, D-229).
/// The person's browser keeps each origin's cookies apart, as a browser does, so the test
/// holds one client at each origin. The test moves the deployment's clock past the
/// shipped send restriction, so it has a deployment of its own.
/// </summary>
/// <param name="host">The deployment.</param>
public sealed class SignOnTests(WorkshopHost host) : IClassFixture<WorkshopHost>
{
    // The second application's client of the provider.
    private const string SecondClient = "kite-shop";

    // The route of the second application's frontend the person meant to reach.
    private const string Route = "/repairs";

    // The origin the second application answers at.
    private static readonly Uri SecondOrigin = new("https://shop.kites.example.test");

    // The shipped email destination restriction, one send a minute to an address, which
    // the test waits out between the registration's messages and the new browser's code.
    private static readonly TimeSpan PastTheRestriction = TimeSpan.FromMinutes(2);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// LIB-TEST-004 AC2, BFF-SESS-006, AUTH-SESS-012 AC10: a person with no session
    /// starts at the second application's sign-on, which finds no session at the first
    /// and sends the browser to the first's sign-in; the person signs in there, from a
    /// browser the account has not used, with the code the new-device check sends, follows
    /// the continuation back to the second's return, and is shown the route the sign-on
    /// began for, where the second application's session is theirs.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_004_AC2_APersonIsSentFromTheSecondApplicationsSignOnToTheSignInAndBackAsync()
    {
        if (!EmailAddress.TryParse("signs.on.at.the.shop@kites.example.test", out EmailAddress email))
        {
            throw new InvalidOperationException("The customer's address does not read as one.");
        }

        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

        await using SecondApplication shop = await host.SecondApplicationAsync(SecondOrigin, SecondClient, Cancellation);
        using TestSession customer = await host.CustomerAsync(email, password, Cancellation);

        host.Clock.Advance(PastTheRestriction);

        // The person's browser at each origin.
        using TestVisitor arriving = shop.Visitor();
        using TestVisitor signingIn = host.Guest();

        Uri started = await NavigatedAsync(arriving, new Uri(arriving.BaseAddress!, "auth/signon?returnTo=" + Uri.EscapeDataString(Route)));
        Uri silent = await NavigatedAsync(signingIn, started);
        Uri pushedAgain = await NavigatedAsync(arriving, silent);
        Uri signIn = await NavigatedAsync(signingIn, pushedAgain);

        int held = host.Notifications.SentTo(email).Count;
        Answered begun = await Exchanges.SendAsync(signingIn, HttpMethod.Post, "auth/begin", new { identifier = email.Value }, Cancellation);
        string challenge = begun.Body!.Value.GetProperty("challengeId").GetString()!;
        Answered checkedBrowser = await Exchanges.SendAsync(
            signingIn,
            HttpMethod.Post,
            "auth/factor",
            new { challengeId = challenge, factor = "password", value = password },
            Cancellation);

        await Exchanges.UntilAsync(() => host.Notifications.SentTo(email).Count > held, Cancellation);

        Answered signedIn = await Exchanges.SendAsync(
            signingIn,
            HttpMethod.Post,
            "auth/device/verify",
            new { challengeId = challenge, code = host.Notifications.SentTo(email)[^1].Values["code"] },
            Cancellation);

        Uri returned = await NavigatedAsync(signingIn, new Uri(Text(signedIn, "continueTo")));
        Uri shown = await NavigatedAsync(arriving, returned);
        Answered session = await Exchanges.SendAsync(arriving, HttpMethod.Get, "auth/session", null, Cancellation);

        Assert.Equal(WorkshopHost.Origin.GetLeftPart(UriPartial.Authority), started.GetLeftPart(UriPartial.Authority));
        Assert.Equal(SecondOrigin.GetLeftPart(UriPartial.Authority), silent.GetLeftPart(UriPartial.Authority));
        Assert.Equal(new Uri(WorkshopHost.Origin, "/signin").AbsoluteUri, signIn.GetLeftPart(UriPartial.Path));
        Assert.Equal("deviceVerificationRequired", Text(checkedBrowser, "status"));
        Assert.Equal("complete", Text(signedIn, "status"));
        Assert.StartsWith(
            WorkshopApplication.Address(SecondOrigin).AbsoluteUri + "/auth/signon/return?",
            returned.AbsoluteUri,
            StringComparison.Ordinal);
        Assert.Equal(new Uri(SecondOrigin, Route), shown);
        Assert.Equal(HttpStatusCode.OK, session.Status);
        Assert.Equal(customer.Subject.Value, session.Body!.Value.GetProperty("subject").GetGuid());
    }

    // A member of an answer's body, or a failure naming what the answer was.
    private static string Text(Answered answered, string member) =>
        answered.Body is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty(member, out JsonElement value)
            ? value.GetString()!
            : throw new InvalidOperationException(
                "The answer " + (int)answered.Status + " carries no " + member + ": " + answered.Body);

    // One navigation of a browser: where the application sends it next, read as the
    // browser reads a redirect's address against the address it asked for.
    private static async Task<Uri> NavigatedAsync(HttpClient browser, Uri address)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        using HttpResponseMessage response = await browser.SendAsync(request, Cancellation);

        return response.StatusCode is HttpStatusCode.Found && response.Headers.Location is Uri location
            ? new Uri(address, location)
            : throw new InvalidOperationException(
                "GET " + address + " answered " + (int)response.StatusCode + " "
                + await response.Content.ReadAsStringAsync(Cancellation));
    }
}
