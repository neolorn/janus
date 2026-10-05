using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Oidc;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Oidc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Hosting.Bff;

/// <summary>
/// This application's half of the sign-on: a confidential client of the provider the
/// authentication application holds, which trades one code for the session it keeps
/// and keeps nothing else.
/// </summary>
/// <param name="client">Which client of the provider this application is.</param>
/// <param name="secrets">What it authenticates itself with at the provider, read from the registry.</param>
/// <param name="addresses">Where the provider answers.</param>
/// <param name="clients">Where the one destination a code returns to is registered.</param>
/// <param name="contacts">Where the sign-on in flight is bound to the browser.</param>
/// <param name="sessions">What derives the per-application session from the record.</param>
/// <param name="oidc">Where the keys a token is judged against are read.</param>
/// <param name="cookies">Where the session's two values are written.</param>
/// <param name="browser">What the request arrived carrying.</param>
/// <param name="channel">Where the back-channel request is made from.</param>
/// <param name="randomness">What the state and the proof key are drawn from.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="log">Where a refused return is recorded.</param>
/// <remarks>
/// Implements BFF-SESS-006, BFF-SESS-003, BFF-SESS-004, BFF-OWN-001, BFF-CSRF-005a,
/// BFF-MACH-001 and AUTH-OIDC-006. The request is pushed and the code exchanged from
/// this server to the provider and never from the browser, which carries only the
/// reference the push was answered with; the destination the code returns to is the
/// registered one and never one a request names, and what the exchange hands back is
/// read once and dropped: after it, this application holds a session record and nothing
/// else. The sign-on is a navigation (BFF-ERR-001): a failure other than its state
/// returns the browser, to where it was going at the start and to the stored return
/// address at the return, with the code of a session that is not there, and a return
/// whose state is absent, unbound or mismatched is refused and sent nowhere. A fault
/// stays a fault (BFF-ERR-002): no pre-authentication session to bind the start to, or
/// a push or an exchange that did not reach the provider, was answered a 5xx or read
/// no answer in its protocol's shape.
/// </remarks>
internal sealed class SignOn(
    SignOnClient client,
    RegisteredSecrets secrets,
    AuthenticationAddresses addresses,
    IOidcClientStore clients,
    PreAuthenticationService contacts,
    SessionService sessions,
    IOidc oidc,
    BrowserSessionCookies cookies,
    RequestSession browser,
    IHttpClientFactory channel,
    RandomNumberGenerator randomness,
    TimeProvider time,
    ILogger<SignOn> log)
{
    /// <summary>
    /// The client the back-channel request is made with, which a host configures the
    /// way it configures every other client of the framework's factory.
    /// </summary>
    public const string Channel = "identity-signon";

    /// <summary>
    /// Where a browser is sent to establish this application's session.
    /// </summary>
    public const string StartPath = "/auth/signon";

    /// <summary>
    /// Where the provider returns the browser, which is the value the deployment
    /// registers as this client's one destination (API-REDIR-001).
    /// </summary>
    public const string ReturnPath = "/auth/signon/return";

    /// <summary>
    /// Starts the flow for a browser that holds no session here.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="returnTo">Where on this application the browser was going.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The forwarding, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public async Task<IResult> StartAsync(
        HttpContext context,
        string? returnTo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // BFF-SESS-006 AC1: a browser that already holds one here has nothing to
        // establish, so the flow is not started and it goes where it was going.
        if (browser.Live is not null)
        {
            return Results.Redirect(Local(returnTo));
        }

        return await ForwardAsync(context, Local(returnTo), silent: true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Completes the flow when the provider returns the browser.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="code">The code the provider issued, where it issued one.</param>
    /// <param name="state">What the browser was sent out with.</param>
    /// <param name="error">What the provider refused with, where it refused.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The forwarding, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public async Task<IResult> ReturnAsync(
        HttpContext context,
        [NeverLogged] string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (browser.FirstContactSecret is not OpaqueToken carried
            || browser.FirstContact is not PreAuthentication contact
            || contact.SignOn is not SignOnAttempt attempt)
        {
            BrowserProfileLog.SignOnUnbound(log, context.TraceIdentifier);

            return Answers.Refused(ErrorCodes.SessionCsrfInvalid);
        }

        // BFF-SESS-006 AC3: the return is judged once, so the attempt is forgotten
        // whichever way it goes and a second return of the same code is unbound.
        await contacts.AbandonAsync(contact, cancellationToken).ConfigureAwait(false);

        if (state is not { Length: > 0 } presented
            || !CryptographicOperations.FixedTimeEquals(
                OpaqueToken.Of(presented).Fingerprint(),
                attempt.StateFingerprint))
        {
            BrowserProfileLog.SignOnStateRejected(log, context.TraceIdentifier);

            return Answers.Refused(ErrorCodes.SessionCsrfInvalid);
        }

        // AUTH-SESS-012 AC3: `login_required` answers the silent attempt and nothing
        // else, so the second attempt asks the provider to sign the person in and the
        // provider forwards them rather than refusing again.
        if (string.Equals(error, "login_required", StringComparison.Ordinal))
        {
            return await ForwardAsync(context, attempt.ReturnTo, silent: false, cancellationToken)
                .ConfigureAwait(false);
        }

        if (error is { Length: > 0 } refused)
        {
            BrowserProfileLog.SignOnRefused(log, context.TraceIdentifier, refused);

            return Expired(attempt.ReturnTo);
        }

        return code is not { Length: > 0 } issued
            ? Expired(attempt.ReturnTo)
            : await RedeemAsync(context, carried, attempt, issued, cancellationToken)
                .ConfigureAwait(false);
    }

    // BFF-SESS-006, chapter 09: a failure other than the state returns the browser to
    // where it was going with the code of a session that is not there.
    private static IResult Expired(string returnTo) =>
        Results.Redirect(NavigationReturn.Refused(returnTo, Error.From(ErrorCodes.SessionExpired)));

    // BFF-SESS-006: the registered destination is the only one, so what the browser is
    // sent back to is read from the registry and never from the request that asked.
    private static string Destination(OidcClient registered) => registered.Redirect;

    private static string Challenge([NeverLogged] string verifier) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    // BFF-SESS-006: the browser is sent back onto this application and nowhere else, so
    // anything that is not one absolute path of this origin becomes the root.
    private static string Local(string? returnTo) =>
        returnTo is { Length: > 1 }
            && returnTo[0] is '/'
            && returnTo[1] is not ('/' or '\\')
            && !returnTo.Contains('\\', StringComparison.Ordinal)
                ? returnTo
                : "/";

    private static string Address(string provider, string route) =>
        provider.TrimEnd('/') + route;

    // BFF-ERR-002, chapter 09: what the authentication application answered a push or
    // an exchange with. The member asked for is the answer, and a refusal in its
    // protocol's shape, a 4xx naming its error (RFC 6749 section 5.2), is none. A 5xx,
    // or anything that is neither, is a fault, as a body that does not read is.
    private static async ValueTask<string?> AnsweredAsync(
        HttpResponseMessage answered,
        string member,
        CancellationToken cancellationToken)
    {
        using var body = JsonDocument.Parse(
            await answered.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

        if (answered.IsSuccessStatusCode && Text(body, member) is { Length: > 0 } value)
        {
            return value;
        }

        return (int)answered.StatusCode is >= StatusCodes.Status400BadRequest and < StatusCodes.Status500InternalServerError
            && Text(body, "error") is { Length: > 0 }
                ? null
                : throw new InvalidOperationException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The authentication application answered {(int)answered.StatusCode} outside its protocol's shape."));
    }

    private static string? Text(JsonDocument body, string member) =>
        body.RootElement.ValueKind is JsonValueKind.Object
        && body.RootElement.TryGetProperty(member, out JsonElement held)
        && held.ValueKind is JsonValueKind.String
            ? held.GetString()
            : null;

    private static IReadOnlyList<PublishedSigningKey> Withheld(
        Error error,
        ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private async Task<IResult> ForwardAsync(
        HttpContext context,
        string returnTo,
        bool silent,
        CancellationToken cancellationToken)
    {
        if (await clients.FindAsync(client.ClientId, cancellationToken).ConfigureAwait(false)
            is not OidcClient registered)
        {
            BrowserProfileLog.SignOnUnregistered(log, context.TraceIdentifier);

            return Expired(returnTo);
        }

        // BFF-ERR-002, chapter 09: a browser that reached here holds no session, so it
        // was issued a pre-authentication session unless none could be issued, which
        // is a fault and no refusal of the person's (BFF-CSRF-005a).
        PreAuthentication contact = browser.FirstContact
            ?? throw new InvalidOperationException("No pre-authentication session was issued for the sign-on.");

        var state = OpaqueToken.Draw(randomness);
        string verifier = OpaqueToken.Draw(randomness).Value;

        if (await PushedAsync(registered, state, verifier, silent, cancellationToken)
                .ConfigureAwait(false)
            is not string reference)
        {
            BrowserProfileLog.SignOnPushRejected(log, context.TraceIdentifier);

            return Expired(returnTo);
        }

        await contacts
            .CarryAsync(
                contact,
                new SignOnAttempt(state.Fingerprint(), verifier, returnTo),
                cancellationToken)
            .ConfigureAwait(false);

        return Results.Redirect(Authorization(registered, reference));
    }

    // AUTH-OIDC-006 AC2: the browser carries the client and the reference the push was
    // answered with, and nothing of the request itself.
    private string Authorization(OidcClient registered, string reference) =>
        Address(addresses.Provider, "/oidc/authorize")
        + "?client_id=" + Uri.EscapeDataString(registered.ClientId)
        + "&request_uri=" + Uri.EscapeDataString(reference);

    // AUTH-OIDC-006 AC2: the request is pushed on this server's own connection,
    // authenticated as the exchange is, and answered with the reference alone.
    private async Task<string?> PushedAsync(
        OidcClient registered,
        OpaqueToken state,
        [NeverLogged] string verifier,
        bool silent,
        CancellationToken cancellationToken)
    {
        if (await SecretAsync(registered, cancellationToken).ConfigureAwait(false) is not string secret)
        {
            return null;
        }

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["response_type"] = "code",
            ["client_id"] = registered.ClientId,
            ["client_secret"] = secret,
            ["redirect_uri"] = Destination(registered),
            ["scope"] = "openid",
            ["state"] = state.Value,
            ["code_challenge"] = Challenge(verifier),
            ["code_challenge_method"] = "S256",
        };

        if (silent)
        {
            parameters["prompt"] = "none";
        }

        using HttpClient requests = channel.CreateClient(Channel);
        using var form = new FormUrlEncodedContent(parameters);
        using HttpResponseMessage answered = await requests
            .PostAsync(new Uri(Address(addresses.Provider, "/oidc/par")), form, cancellationToken)
            .ConfigureAwait(false);

        return await AnsweredAsync(answered, "request_uri", cancellationToken).ConfigureAwait(false);
    }

    private async Task<IResult> RedeemAsync(
        HttpContext context,
        OpaqueToken carried,
        SignOnAttempt attempt,
        [NeverLogged] string code,
        CancellationToken cancellationToken)
    {
        if (await clients.FindAsync(client.ClientId, cancellationToken).ConfigureAwait(false)
            is not OidcClient registered)
        {
            BrowserProfileLog.SignOnUnregistered(log, context.TraceIdentifier);

            return Expired(attempt.ReturnTo);
        }

        string? identity = await ExchangedAsync(registered, attempt, code, cancellationToken)
            .ConfigureAwait(false);

        if (identity is null)
        {
            BrowserProfileLog.SignOnExchangeRejected(log, context.TraceIdentifier);

            return Expired(attempt.ReturnTo);
        }

        SessionId? spine = await RecordAsync(identity, cancellationToken).ConfigureAwait(false);

        if (spine is not SessionId named)
        {
            BrowserProfileLog.SignOnExchangeRejected(log, context.TraceIdentifier);

            return Expired(attempt.ReturnTo);
        }

        Result<IssuedSession> derived = await sessions
            .DeriveAsync(named, SessionType.PerApp, RequestOrigin.Of(context.Request), cancellationToken)
            .ConfigureAwait(false);

        // A record that has ended since the code was issued is a session that is not
        // there; anything else that kept the session from being derived is a fault.
        if (derived.Match(_ => (Error?)null, failure => failure) is Error unestablished)
        {
            return unestablished.Code == ErrorCodes.SessionExpired
                ? Expired(attempt.ReturnTo)
                : Answers.Refused(unestablished);
        }

        // BFF-SESS-004, BFF-CSRF-005a AC3: the pair the browser carries is written
        // again and what it carried before the session existed is ended rather than
        // left beside it.
        cookies.Write(context.Response, derived.Match(issued => issued, _ => default!));
        cookies.ClearFirstContact(context.Response);

        await contacts.RotateAsync(carried, cancellationToken).ConfigureAwait(false);

        return Results.Redirect(attempt.ReturnTo);
    }

    // BFF-SESS-006 AC2 and AC4: the code is traded on this server's own connection,
    // and what comes back is read for the one claim that names the record and dropped.
    private async Task<string?> ExchangedAsync(
        OidcClient registered,
        SignOnAttempt attempt,
        [NeverLogged] string code,
        CancellationToken cancellationToken)
    {
        if (await SecretAsync(registered, cancellationToken).ConfigureAwait(false) is not string secret)
        {
            return null;
        }

        using HttpClient requests = channel.CreateClient(Channel);
        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = Destination(registered),
                ["client_id"] = registered.ClientId,
                ["client_secret"] = secret,
                ["code_verifier"] = attempt.Verifier,
            });

        using HttpResponseMessage answered = await requests
            .PostAsync(new Uri(Address(addresses.Provider, "/oidc/token")), form, cancellationToken)
            .ConfigureAwait(false);

        return await AnsweredAsync(answered, "id_token", cancellationToken).ConfigureAwait(false);
    }

    // OPS-SEC-002: the secret is read from the registry at each request and held
    // nowhere, so one rotated since the last request is the one presented.
    private async ValueTask<string?> SecretAsync(OidcClient registered, CancellationToken cancellationToken)
    {
        Result<byte[]> read = await secrets.CurrentAsync(registered.ClientId, cancellationToken).ConfigureAwait(false);

        return read.Match<string?>(
            current =>
            {
                try
                {
                    return Encoding.UTF8.GetString(current);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(current);
                }
            },
            _ => null);
    }

    // AUTH-KEY-001 AC2: the identity token is judged against the set the deployment
    // publishes, for this client and no other, before a claim of it is believed.
    private async ValueTask<SessionId?> RecordAsync(
        string identity,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        IReadOnlyList<PublishedSigningKey> published = (await oidc
                .KeysAsync(cancellationToken)
                .ConfigureAwait(false))
            .Match(keys => keys, error => Withheld(error, ref failure));

        if (failure is not null)
        {
            return null;
        }

        string provider = addresses.Provider.TrimEnd('/');
        var parameters = new TokenValidationParameters
        {
            IssuerSigningKeys = [.. published.Select(PublishedKeys.Of)],
            ValidIssuers = [provider, provider + "/"],
            ValidAudience = client.ClientId,
            LifetimeValidator = (before, expires, _, _) =>
                (before is null || before <= time.GetUtcNow().UtcDateTime)
                && (expires is null || expires > time.GetUtcNow().UtcDateTime),
        };

        TokenValidationResult read = await new JsonWebTokenHandler()
            .ValidateTokenAsync(identity, parameters)
            .ConfigureAwait(false);

        return read.IsValid
            && read.Claims.TryGetValue(OidcClaimNames.Session, out object? named)
            && Guid.TryParse(
                Convert.ToString(named, CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                out Guid record)
                ? new SessionId(record)
                : null;
    }
}
