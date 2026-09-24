using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Oidc;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// What a first-party client of the provider does, written once, so that a test of the
/// provider says only what it asks and what it expects.
/// </summary>
internal static class RelyingParty
{
    /// <summary>
    /// A browser application's own layer, which exchanges one code and holds nothing.
    /// </summary>
    public const string Application = "browser-app";

    /// <summary>
    /// A protocol client, which holds access and refresh tokens.
    /// </summary>
    public const string Protocol = "mail-server";

    /// <summary>
    /// The one destination every client here registers.
    /// </summary>
    public const string Destination = "https://app.example.test/signin/callback";

    /// <summary>
    /// The secret every client here authenticates with.
    /// </summary>
    public const string Secret = "a-secret-the-deployment-set";

    /// <summary>
    /// The proof key every request here is made with.
    /// </summary>
    public const string Verifier = "a-verifier-of-at-least-forty-three-characters-long";

    /// <summary>
    /// The challenge the proof key answers, by the one method the provider takes.
    /// </summary>
    public static string Challenge =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));

    /// <summary>
    /// Registers a browser application and a protocol client, each with the one
    /// destination and the scopes a client may be registered for.
    /// </summary>
    /// <param name="deployment">Where they are registered.</param>
    /// <returns>The work of registering them.</returns>
    public static async Task RegisteredAsync(Deployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        foreach ((string clientId, OidcClientKind kind) in new[]
        {
            (Application, OidcClientKind.BrowserApplication),
            (Protocol, OidcClientKind.Protocol),
        })
        {
            await deployment.Clients.RecordAsync(
                new OidcClient(
                    clientId,
                    clientId,
                    kind,
                    Destination,
                    ["openid", "email", "offline_access"]),
                OpaqueToken.Of(Secret).Fingerprint(),
                TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Registers the clients and signs a browser in at the authentication application.
    /// </summary>
    /// <param name="deployment">Where it happens.</param>
    /// <returns>The browser holding the session record.</returns>
    public static async Task<Browser> PreparedAsync(Deployment deployment)
    {
        await RegisteredAsync(deployment);

        Flow.Prepare(deployment);

        return await Flow.SignedInAsync(deployment);
    }

    /// <summary>
    /// The fields of an authorization request, as a client pushes them.
    /// </summary>
    /// <param name="clientId">The client asking.</param>
    /// <param name="silent">Whether it asks not to be shown anything.</param>
    /// <param name="redirect">Where it asks the code to go.</param>
    /// <param name="scope">What it asks for.</param>
    /// <returns>The fields.</returns>
    public static (string Name, string? Value)[] Request(
        string clientId,
        bool silent,
        string redirect,
        string scope) =>
    [
        ("response_type", "code"),
        ("client_id", clientId),
        ("client_secret", Secret),
        ("redirect_uri", redirect),
        ("scope", scope),
        ("state", "the-state"),
        ("code_challenge", Challenge),
        ("code_challenge_method", "S256"),
        ("prompt", silent ? "none" : null),
    ];

    /// <summary>
    /// The same fields with one of them set otherwise, or left out where it is set to
    /// nothing.
    /// </summary>
    /// <param name="fields">The fields.</param>
    /// <param name="name">Which one.</param>
    /// <param name="value">What it is set to.</param>
    /// <returns>The fields as changed.</returns>
    public static (string Name, string? Value)[] With(
        (string Name, string? Value)[] fields,
        string name,
        string? value) =>
        [
            .. fields.Where(field => !string.Equals(field.Name, name, StringComparison.Ordinal)),
            (name, value),
        ];

    /// <summary>
    /// Pushes an authorization request on the back channel.
    /// </summary>
    /// <param name="deployment">What it is pushed to.</param>
    /// <param name="fields">What it carries.</param>
    /// <returns>What came back.</returns>
    public static Task<Answer> PushAsync(Deployment deployment, (string Name, string? Value)[] fields) =>
        new Machine(deployment).PostAsync("/oidc/par", fields);

    /// <summary>
    /// Pushes an authorization request on the back channel.
    /// </summary>
    /// <param name="deployment">What it is pushed to.</param>
    /// <param name="clientId">The client asking.</param>
    /// <param name="silent">Whether it asks not to be shown anything.</param>
    /// <param name="redirect">Where it asks the code to go.</param>
    /// <param name="scope">What it asks for.</param>
    /// <returns>What came back.</returns>
    public static Task<Answer> PushAsync(
        Deployment deployment,
        string clientId,
        bool silent,
        string redirect,
        string scope) =>
        PushAsync(deployment, Request(clientId, silent, redirect, scope));

    /// <summary>
    /// Pushes an authorization request and gives back where the browser is sent with
    /// the reference it was answered with (AUTH-OIDC-006 AC2).
    /// </summary>
    /// <param name="deployment">What it is pushed to.</param>
    /// <param name="clientId">The client asking.</param>
    /// <param name="silent">Whether it asks not to be shown anything.</param>
    /// <param name="redirect">Where it asks the code to go.</param>
    /// <returns>The address the browser is sent to.</returns>
    public static Task<string> AuthorizeAsync(
        Deployment deployment,
        string clientId,
        bool silent,
        string redirect = Destination) =>
        AuthorizeAsync(
            deployment,
            clientId,
            silent,
            redirect,
            // 09 section 9: what may be asked for is what the kind of client may hold,
            // and a browser application's own layer holds nothing after the exchange.
            string.Equals(clientId, Protocol, StringComparison.Ordinal)
                ? "openid email offline_access"
                : "openid email");

    /// <summary>
    /// Pushes an authorization request and gives back where the browser is sent with
    /// the reference it was answered with (AUTH-OIDC-006 AC2).
    /// </summary>
    /// <param name="deployment">What it is pushed to.</param>
    /// <param name="clientId">The client asking.</param>
    /// <param name="silent">Whether it asks not to be shown anything.</param>
    /// <param name="redirect">Where it asks the code to go.</param>
    /// <param name="scope">What it asks for.</param>
    /// <returns>The address the browser is sent to.</returns>
    public static async Task<string> AuthorizeAsync(
        Deployment deployment,
        string clientId,
        bool silent,
        string redirect,
        string scope)
    {
        Answer pushed = await PushAsync(deployment, clientId, silent, redirect, scope);

        Assert.Equal(StatusCodes.Status201Created, pushed.Status);

        return Authorization(clientId, pushed.Text("request_uri"));
    }

    /// <summary>
    /// Where the browser is sent with a reference: the client and the reference and
    /// nothing else.
    /// </summary>
    /// <param name="clientId">The client.</param>
    /// <param name="reference">The reference the push was answered with.</param>
    /// <returns>The address.</returns>
    public static string Authorization(string clientId, string reference) =>
        "/oidc/authorize?client_id=" + Uri.EscapeDataString(clientId)
        + "&request_uri=" + Uri.EscapeDataString(reference);

    /// <summary>
    /// Takes a code for a browser holding a live session.
    /// </summary>
    /// <param name="deployment">What issues it.</param>
    /// <param name="browser">The browser.</param>
    /// <param name="clientId">The client it is issued to.</param>
    /// <returns>The code.</returns>
    public static async Task<string> CodeAsync(Deployment deployment, Browser browser, string clientId)
    {
        ArgumentNullException.ThrowIfNull(browser);

        Answer answered = await browser.SendAsync(
            "GET",
            await AuthorizeAsync(deployment, clientId, silent: true));

        Assert.Equal(StatusCodes.Status302Found, answered.Status);

        return Returned(answered, "code");
    }

    /// <summary>
    /// The fields of the exchange of a code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="clientId">The client exchanging it.</param>
    /// <returns>The fields.</returns>
    public static (string Name, string? Value)[] Code(string code, string clientId) =>
    [
        ("grant_type", "authorization_code"),
        ("code", code),
        ("client_id", clientId),
        ("client_secret", Secret),
        ("redirect_uri", Destination),
        ("code_verifier", Verifier),
    ];

    /// <summary>
    /// Where an answer forwarded the browser.
    /// </summary>
    /// <param name="answered">The answer.</param>
    /// <returns>The address.</returns>
    /// <exception cref="InvalidOperationException">It forwarded nowhere.</exception>
    public static string Where(Answer answered) =>
        answered?.Location ?? throw new InvalidOperationException("The answer forwarded nowhere.");

    /// <summary>
    /// One parameter of the address an answer forwarded the browser to.
    /// </summary>
    /// <param name="answered">The answer.</param>
    /// <param name="name">Which parameter.</param>
    /// <returns>Its value, or empty where the address carries none.</returns>
    public static string Returned(Answer answered, string name) =>
        Parameters(Where(answered)).TryGetValue(name, out string? held) ? held : string.Empty;

    private static Dictionary<string, string> Parameters(string where)
    {
        var read = new Dictionary<string, string>(StringComparer.Ordinal);
        int at = where.IndexOf('?', StringComparison.Ordinal);

        foreach (string pair in where[(at + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);

            read[pair[..equals]] = Uri.UnescapeDataString(pair[(equals + 1)..]);
        }

        return read;
    }
}
