using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// The registration a test is not about, run the way a browser runs it, so that a
/// test that needs a signed-in browser says only that.
/// </summary>
internal static class Flow
{
    /// <summary>
    /// The address the flow registers.
    /// </summary>
    public const string Address = "person@example.test";

    /// <summary>
    /// The number the flow registers.
    /// </summary>
    public const string Number = "+441632960011";

    /// <summary>
    /// The password the flow sets, which stands alone at the floor.
    /// </summary>
    public const string Password = "orangemarmalade";

    private const string Client = "web";
    private const string Language = "en";

    /// <summary>
    /// Makes a deployment able to send: the templates carry the code and the link
    /// token, and the balance floor is out of the way.
    /// </summary>
    /// <param name="deployment">What to prepare.</param>
    public static void Prepare(Deployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        deployment.Configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);
        deployment.Configuration.Set(Settings.NotificationLanguages, [Language]);

        foreach (MessageKind message in new[] { MessageKind.VerificationCode, MessageKind.AccountExists })
        {
            foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
            {
                deployment.Templates.Set(
                    message,
                    kind,
                    Language,
                    new MessageTemplate(kind is SendKind.Email ? "code" : null, "{code} {token}"));
            }
        }
    }

    /// <summary>
    /// A browser that has begun a registration, which is a first contact plus the
    /// session the endpoint carried onto it.
    /// </summary>
    /// <param name="deployment">What it talks to.</param>
    /// <returns>The browser.</returns>
    public static async Task<Browser> BegunAsync(Deployment deployment)
    {
        var browser = new Browser(deployment);

        _ = await browser.SendAsync("GET", "/register");
        _ = await browser.SendAsync("POST", "/register", ("clientId", Client));

        return browser;
    }

    /// <summary>
    /// A browser that has answered the age step and staged an address.
    /// </summary>
    /// <param name="deployment">What it talks to.</param>
    /// <returns>The browser.</returns>
    public static async Task<Browser> AwaitingAsync(Deployment deployment)
    {
        Browser browser = await BegunAsync(deployment);

        _ = await browser.SendAsync("PUT", "/register/age", ("dateOfBirth", "1990-01-01"));
        _ = await browser.SendAsync("PUT", "/register/email", ("value", Address));

        return browser;
    }

    /// <summary>
    /// A browser that has reached the terms step: both identifiers verified, the
    /// confirm step passed and a password that stands alone set.
    /// </summary>
    /// <param name="deployment">What it talks to.</param>
    /// <returns>The browser.</returns>
    public static async Task<Browser> SecuredAsync(Deployment deployment)
    {
        Browser browser = await AwaitingAsync(deployment);

        await VerifiedAsync(deployment, browser, IdentifierKind.Email);

        _ = await browser.SendAsync("PUT", "/register/phone", ("value", Number));

        await VerifiedAsync(deployment, browser, IdentifierKind.Phone);

        Assert.Equal(
            StatusCodes.Status200OK,
            (await browser.SendAsync("POST", "/register/confirm")).Status);

        Assert.Equal(
            StatusCodes.Status200OK,
            (await browser.SendAsync("PUT", "/register/security", ("password", Password))).Status);

        return browser;
    }

    /// <summary>
    /// A browser holding the session a completed registration signed it in on.
    /// </summary>
    /// <param name="deployment">What it talks to.</param>
    /// <returns>The browser.</returns>
    public static async Task<Browser> SignedInAsync(Deployment deployment)
    {
        Browser browser = await SecuredAsync(deployment);

        Answer completed = await browser.SendAsync(
            "POST",
            "/register/terms",
            ("termsVersion", "terms-3"),
            ("noticeVersion", "notice-2"));

        Assert.Equal(StatusCodes.Status201Created, completed.Status);

        Carried(deployment);

        return browser;
    }

    // One table holds the accounts and their identifiers in a deployment; here the
    // area that created them and the area that reads them keep their own, so what
    // registration wrote is carried across once.
    private static void Carried(Deployment deployment)
    {
        NewAccount created = deployment.Directory.Created[^1];

        deployment.Accounts.Stands(created.Subject, AccountState.Active);

        foreach (NewIdentifier identifier in created.Identifiers)
        {
            _ = deployment.Identifiers.Verified(created.Subject, identifier.Kind, identifier.Canonical);
        }
    }

    /// <summary>
    /// Verifies the identifier of a kind that is waiting, by the code the message
    /// carried.
    /// </summary>
    /// <param name="deployment">What it talks to.</param>
    /// <param name="browser">Whose registration.</param>
    /// <param name="kind">Which kind.</param>
    /// <returns>The work of verifying it.</returns>
    public static async Task VerifiedAsync(Deployment deployment, Browser browser, IdentifierKind kind)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        ArgumentNullException.ThrowIfNull(browser);

        Answer state = await browser.SendAsync("GET", "/register");

        Answer verified = await browser.SendAsync(
            "POST",
            "/register/verify/" + Waiting(state, kind),
            ("code", Code(deployment, kind)));

        Assert.Equal(StatusCodes.Status204NoContent, verified.Status);
    }

    /// <summary>
    /// The code the last message of a kind carried.
    /// </summary>
    /// <param name="deployment">Where the messages went.</param>
    /// <param name="kind">Which kind.</param>
    /// <returns>The code.</returns>
    public static string Code(Deployment deployment, IdentifierKind kind)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        return Sent(deployment, kind).Split(' ')[0];
    }

    /// <summary>
    /// The link token the last message of a kind carried, which never touches the
    /// state document.
    /// </summary>
    /// <param name="deployment">Where the messages went.</param>
    /// <param name="kind">Which kind.</param>
    /// <returns>The token.</returns>
    public static string Token(Deployment deployment, IdentifierKind kind)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        return Sent(deployment, kind).Split(' ')[1];
    }

    /// <summary>
    /// The identifier of a kind that is still waiting for its code.
    /// </summary>
    /// <param name="state">The state document.</param>
    /// <param name="kind">Which kind.</param>
    /// <returns>Its identifier.</returns>
    public static string Waiting(Answer state, IdentifierKind kind)
    {
        ArgumentNullException.ThrowIfNull(state);

        foreach (JsonElement staged in state.Json().GetProperty("identifiers").EnumerateArray())
        {
            if (string.Equals(staged.GetProperty("kind").GetString(), Named(kind), StringComparison.Ordinal)
                && !staged.GetProperty("verified").GetBoolean())
            {
                return staged.GetProperty("id").GetString()!;
            }
        }

        throw new InvalidOperationException("Nothing of that kind is waiting.");
    }

    private static string Named(IdentifierKind kind) => kind is IdentifierKind.Email ? "email" : "phone";

    // The last message written from the verification template, which is the one
    // carrying a code and a token; the notices that follow a change carry neither.
    private static string Sent(Deployment deployment, IdentifierKind kind)
    {
        IEnumerable<string> written = kind is IdentifierKind.Email
            ? deployment.Mail.Taken.Select(sent => sent.Body)
            : deployment.Sms.Taken.Select(sent => sent.Text);

        foreach (string body in written.Reverse())
        {
            if (body.Split(' ') is [{ Length: 6 } code, { Length: > 0 }] && code.All(char.IsAsciiDigit))
            {
                return body;
            }
        }

        throw new InvalidOperationException("No message carrying a code went out.");
    }
}
