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
/// BFF-MACH-001, BFF-ERR-001, BFF-LOG-001 and AUTH-OIDC-006. The request is pushed and
/// the code exchanged from this server to the provider and never from the browser,
/// which carries only the reference the push was answered with; the destination the
/// code returns to is the registered one and never one a request names, and what the
/// exchange hands back is read once and dropped: after it, this application holds a
/// session record and nothing else. The sign-on is a navigation (BFF-ERR-001): a
/// refusal other than its state's returns the browser, to where it was going at the
/// start and to the stored return address at the return, with the code of a session
/// that is not there, and what it carried inside, the provider's error or the
/// library's own code, is recorded beside it; a return whose state is absent, unbound
/// or mismatched is refused and sent nowhere. A fault stays a fault (BFF-ERR-002): no
/// pre-authentication session to bind the start to; a push or an exchange that did not
/// reach the provider, was answered a 5xx or read no answer in its protocol's shape;
/// every error the push reads; every error of the exchange but a 400 naming the grant
/// refused; an authorization response naming the provider's own failure; this
/// application's client in no registry; its secret that cannot be read; the provider's
/// published keys that cannot be read; an identity token that does not hold up under
/// them; and a derivation failing with a code whose row names a fault or that no row
/// names (10 section 6). A return carrying neither a code nor an error, and a token
/// whose session has ended since, are refusals. A fault the push, the exchange or the
/// authorization response answered records, beside its own entry, the status and the
/// error read and nothing else of the answer.
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

    // OpenID Connect Core section 3.1.2.6: what the provider answers a silent request
    // with where the person holds no record there.
    private const string SignInRequired = "login_required";

    // RFC 6749 section 5.2: the error a token endpoint refuses the grant itself with,
    // the code spent, expired or issued to another client.
    private const string CodeRefused = "invalid_grant";

    // RFC 6749 section 4.1.2.1: the errors of an authorization response that are the
    // provider's own failure and nothing it refused.
    private const string ProviderFailed = "server_error";

    private const string ProviderUnavailable = "temporarily_unavailable";

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
        if (string.Equals(error, SignInRequired, StringComparison.Ordinal))
        {
            return await ForwardAsync(context, attempt.ReturnTo, silent: false, cancellationToken)
                .ConfigureAwait(false);
        }

        // BFF-ERR-001 AC5, chapter 09: the provider's own failure is a fault and no
        // refusal of the person's, so the browser is not returned to sign in again.
        if (error is ProviderFailed or ProviderUnavailable)
        {
            BrowserProfileLog.SignOnFaulted(log, context.TraceIdentifier, status: null, error);

            throw new InvalidOperationException("The authentication application failed the authorization request.");
        }

        if (error is { Length: > 0 } refused)
        {
            BrowserProfileLog.SignOnRefused(log, context.TraceIdentifier, refused);

            return Refused(context, attempt.ReturnTo, refused);
        }

        // BFF-ERR-001 AC5, chapter 09: a return carrying neither a code nor an error is
        // a refusal, and what it carried inside is the code of a request that cannot be
        // read.
        return code is not { Length: > 0 } issued
            ? Refused(context, attempt.ReturnTo, ErrorCodes.RequestMalformed.ToString())
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

    // BFF-ERR-001 AC5, chapter 09: the application's client in no registry, and its
    // secret that cannot be read, are the deployment's own state and so faults, never
    // a refusal that would send every person round to sign in again.
    private static InvalidOperationException Unregistered() =>
        new("This application's client is in no registry.");

    private static InvalidOperationException SecretUnread() =>
        new("This application's client secret could not be read.");

    private static string? Text(JsonDocument body, string member) =>
        body.RootElement.ValueKind is JsonValueKind.Object
        && body.RootElement.TryGetProperty(member, out JsonElement held)
        && held.ValueKind is JsonValueKind.String
            ? held.GetString()
            : null;

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

            throw Unregistered();
        }

        // BFF-ERR-002, chapter 09: a browser that reached here holds no session, so it
        // was issued a pre-authentication session unless none could be issued, which
        // is a fault and no refusal of the person's (BFF-CSRF-005a).
        PreAuthentication contact = browser.FirstContact
            ?? throw new InvalidOperationException("No pre-authentication session was issued for the sign-on.");

        var state = OpaqueToken.Draw(randomness);
        string verifier = OpaqueToken.Draw(randomness).Value;

        string reference = await PushedAsync(context, registered, state, verifier, silent, cancellationToken)
            .ConfigureAwait(false);

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
    private async Task<string> PushedAsync(
        HttpContext context,
        OidcClient registered,
        OpaqueToken state,
        [NeverLogged] string verifier,
        bool silent,
        CancellationToken cancellationToken)
    {
        if (await SecretAsync(registered, cancellationToken).ConfigureAwait(false) is not string secret)
        {
            BrowserProfileLog.SignOnPushRejected(log, context.TraceIdentifier);

            throw SecretUnread();
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

        return await AnsweredAsync(context, answered, "request_uri", refusal: null, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The push was answered with no reference.");
    }

    // BFF-ERR-001 AC5, BFF-LOG-001 AC2: a refusal returns the browser with the code of
    // a session that is not there, and what it carried inside, the provider's error or
    // the library's own code, is recorded beside it and never carried to the browser.
    private IResult Refused(HttpContext context, string returnTo, string inside)
    {
        BrowserProfileLog.SignOnReturned(log, context.TraceIdentifier, ErrorCodes.SessionExpired, inside);

        return Expired(returnTo);
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

            throw Unregistered();
        }

        if (await SecretAsync(registered, cancellationToken).ConfigureAwait(false) is not string secret)
        {
            BrowserProfileLog.SignOnExchangeRejected(log, context.TraceIdentifier);

            throw SecretUnread();
        }

        string? identity = await ExchangedAsync(context, registered, secret, attempt, code, cancellationToken)
            .ConfigureAwait(false);

        if (identity is null)
        {
            BrowserProfileLog.SignOnExchangeRejected(log, context.TraceIdentifier);

            return Refused(context, attempt.ReturnTo, CodeRefused);
        }

        SessionId? spine = await RecordAsync(context, identity, cancellationToken).ConfigureAwait(false);

        if (spine is not SessionId named)
        {
            BrowserProfileLog.SignOnExchangeRejected(log, context.TraceIdentifier);

            return Expired(attempt.ReturnTo);
        }

        Result<IssuedSession> derived = await sessions
            .DeriveAsync(named, SessionType.PerApp, RequestOrigin.Of(context.Request), cancellationToken)
            .ConfigureAwait(false);

        // BFF-SESS-006, chapter 09: a session that was not derived is one that is not
        // there, and the code the derivation was refused with inside is recorded and
        // never carried to the browser. A code whose row names a fault, or that no row
        // names, is no refusal: it is answered as it is everywhere (10 section 6).
        if (derived.Match(_ => (Error?)null, failure => failure) is Error underived)
        {
            return ApiStatus.Of(underived) is StatusCodes.Status500InternalServerError
                ? Answers.Refused(underived)
                : Refused(context, attempt.ReturnTo, underived.Code.ToString());
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
    // Nothing comes back where the provider refused the code itself.
    private async Task<string?> ExchangedAsync(
        HttpContext context,
        OidcClient registered,
        [NeverLogged] string secret,
        SignOnAttempt attempt,
        [NeverLogged] string code,
        CancellationToken cancellationToken)
    {
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

        return await AnsweredAsync(context, answered, "id_token", CodeRefused, cancellationToken).ConfigureAwait(false);
    }

    // BFF-ERR-002, BFF-ERR-001 AC5, chapter 09: what the authentication application
    // answered a push or an exchange with. The member asked for is the answer. A 400
    // whose error is the one the caller names as a refusal is none; a push names no
    // such error, so every error it reads is a fault. Every other answer is a fault:
    // any other error, which refuses the deployment's own client or request, a 5xx, a
    // 4xx naming no error, and a body that does not read. Such a fault was answered,
    // so the status and the error read are recorded beside it, and nothing else of the
    // answer.
    private async ValueTask<string?> AnsweredAsync(
        HttpContext context,
        HttpResponseMessage answered,
        string member,
        string? refusal,
        CancellationToken cancellationToken)
    {
        int status = (int)answered.StatusCode;

        using JsonDocument body = Read(
            context,
            status,
            await answered.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

        if (answered.IsSuccessStatusCode && Text(body, member) is { Length: > 0 } value)
        {
            return value;
        }

        string? error = Text(body, "error");

        if (refusal is not null
            && status is StatusCodes.Status400BadRequest
            && string.Equals(error, refusal, StringComparison.Ordinal))
        {
            return null;
        }

        BrowserProfileLog.SignOnFaulted(log, context.TraceIdentifier, status, error);

        throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"The authentication application answered {status} with no answer this application takes."));
    }

    // BFF-ERR-001 AC5: a body that does not read was answered all the same, so its
    // status is recorded beside the fault, which names no error.
    private JsonDocument Read(HttpContext context, int status, string body)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            BrowserProfileLog.SignOnFaulted(log, context.TraceIdentifier, status, error: null);

            throw;
        }
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
    // BFF-ERR-001 AC5, chapter 09: published keys that cannot be read, and a token
    // that does not hold up under them, in its signature, its issuer, its audience or
    // its expiry, are the deployment's own state and so faults.
    private async ValueTask<SessionId?> RecordAsync(
        HttpContext context,
        string identity,
        CancellationToken cancellationToken)
    {
        if ((await oidc.KeysAsync(cancellationToken).ConfigureAwait(false))
                .Match<IReadOnlyList<PublishedSigningKey>?>(keys => keys, _ => null)
            is not IReadOnlyList<PublishedSigningKey> published)
        {
            BrowserProfileLog.SignOnExchangeRejected(log, context.TraceIdentifier);

            throw new InvalidOperationException("The authentication application's published keys could not be read.");
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

        if (!read.IsValid)
        {
            BrowserProfileLog.SignOnExchangeRejected(log, context.TraceIdentifier);

            throw new InvalidOperationException("The identity token the authentication application issued did not hold up.");
        }

        return read.Claims.TryGetValue(OidcClaimNames.Session, out object? named)
            && Guid.TryParse(
                Convert.ToString(named, CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                out Guid record)
                ? new SessionId(record)
                : null;
    }
}
