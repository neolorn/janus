using System;
using System.Buffers.Text;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Credentials;
using Janus.Authentication.Registration;
using Janus.Authentication.Sessions;
using Janus.Authentication.SignIn;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Janus.Hosting.Credentials;

/// <summary>
/// This application as a client of the social providers the deployment declares: it
/// sends a browser to sign in at one, trades the code the browser comes back with for
/// an identity token on this server's own connection, and acts on the identity the
/// token names by what the round trip was started for.
/// </summary>
/// <param name="providers">What each declared provider publishes.</param>
/// <param name="attempts">Where the round trip in flight is bound to the browser.</param>
/// <param name="authentication">What signs in the account an identity is linked to.</param>
/// <param name="registration">What a registration in progress is handed the identity by.</param>
/// <param name="credentials">What links an identity to the account signed in.</param>
/// <param name="contacts">Where the pre-authentication session is ended.</param>
/// <param name="cookies">Where a session begun here is written.</param>
/// <param name="browser">What the request arrived carrying.</param>
/// <param name="channel">Where the back-channel request is made from.</param>
/// <param name="randomness">What the state, the nonce and the proof key are drawn from.</param>
/// <param name="log">Where a refused round trip is recorded.</param>
/// <remarks>
/// Implements IDN-ACCT-001, IDN-LIFE-012, REG-IDENT-008, BFF-CSRF-005a and
/// BFF-OWN-001. The round trip is bound to what the browser already carries, its
/// pre-authentication session or its session, and what it was sent out with is held
/// beside that as fingerprints; the code is traded here and never in the browser; the
/// identity is the provider's <c>sub</c>, and the address the token names is never a
/// key. A browser that left for the provider comes back to the path it started from,
/// carrying the code of any refusal and never its words (CONV-CONTENT-001).
/// </remarks>
internal sealed class ProviderSignIn(
    ProviderKeys providers,
    ProviderAttempts attempts,
    AuthenticationService authentication,
    RegistrationService registration,
    CredentialService credentials,
    PreAuthenticationService contacts,
    BrowserSessionCookies cookies,
    RequestSession browser,
    IHttpClientFactory channel,
    RandomNumberGenerator randomness,
    ILogger<ProviderSignIn> log)
{
    // What a round trip is started for, as the start names it.
    private static readonly FrozenDictionary<string, ProviderIntent> Intents =
        new Dictionary<string, ProviderIntent>(StringComparer.Ordinal)
        {
            ["signin"] = ProviderIntent.SignIn,
            ["register"] = ProviderIntent.Register,
            ["link"] = ProviderIntent.Link,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Sends the browser to sign in at a provider.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="provider">Which social provider.</param>
    /// <param name="intent">What the round trip is for: to sign in, to register or to link.</param>
    /// <param name="returnTo">Where on this application the browser comes back to.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The forwarding, to the provider or back with the refusal's code.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public async Task<IResult> StartAsync(
        HttpContext context,
        Factor provider,
        string? intent,
        string? returnTo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        string destination = Local(returnTo);

        if (intent is null || !Intents.TryGetValue(intent, out ProviderIntent intended))
        {
            return Back(destination, ErrorCodes.RequestMalformed);
        }

        // A browser already signed in has nothing to sign in to, and goes where it was
        // going, as it does from the sign-on (BFF-SESS-006 AC1).
        if (intended is ProviderIntent.SignIn && browser.Live is not null)
        {
            return Results.Redirect(destination);
        }

        Error? failure = null;

        ProviderBinding binding = (await BindingAsync(context, provider, intended, cancellationToken)
                .ConfigureAwait(false))
            .Match(bound => bound, error => Withheld<ProviderBinding>(error, ref failure));

        if (failure is not null)
        {
            return Back(destination, failure.Code);
        }

        if (providers.Of(provider) is not SocialProvider declared
            || await providers.SignInAsync(provider, cancellationToken).ConfigureAwait(false)
                is not { Authorization: Uri authorize } configured)
        {
            BrowserProfileLog.ProviderUnavailable(log, context.TraceIdentifier, provider);

            return Back(destination, ErrorCodes.FactorNotPermitted);
        }

        var state = OpaqueToken.Draw(randomness);
        var nonce = OpaqueToken.Draw(randomness);
        string? verifier = configured.ProofKey ? OpaqueToken.Draw(randomness).Value : null;

        await attempts
            .BindAsync(
                binding,
                new ProviderAttempt(
                    provider,
                    intended,
                    state.Fingerprint(),
                    nonce.Fingerprint(),
                    verifier,
                    destination),
                cancellationToken)
            .ConfigureAwait(false);

        return Results.Redirect(Authorization(declared, authorize, configured, state, nonce, verifier));
    }

    /// <summary>
    /// Completes the round trip when the provider has returned the browser.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="provider">Which social provider the browser comes back from.</param>
    /// <param name="code">The code the provider issued, where it issued one.</param>
    /// <param name="state">What the browser was sent out with.</param>
    /// <param name="error">What the provider refused with, where it refused.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The forwarding, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public async Task<IResult> ReturnAsync(
        HttpContext context,
        Factor provider,
        [NeverLogged] string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        ProviderBinding? binding = browser.Live is Session live
            ? ProviderBinding.Within(live.Id)
            : browser.FirstContact is PreAuthentication contact
                ? ProviderBinding.Before(contact.Fingerprint)
                : null;

        // BFF-CSRF-005a: the round trip is judged once, so it is forgotten whichever
        // way it goes and a second return of the same code finds nothing bound.
        ProviderAttempt? attempt = binding is null
            ? null
            : await attempts.TakeAsync(binding, cancellationToken).ConfigureAwait(false);

        if (attempt is null || attempt.Provider != provider)
        {
            BrowserProfileLog.ProviderUnbound(log, context.TraceIdentifier);

            return Answers.Refused(ErrorCodes.SessionCsrfInvalid);
        }

        if (state is not { Length: > 0 } presented
            || !CryptographicOperations.FixedTimeEquals(
                OpaqueToken.Of(presented).Fingerprint(),
                attempt.StateFingerprint))
        {
            BrowserProfileLog.ProviderStateRejected(log, context.TraceIdentifier);

            return Answers.Refused(ErrorCodes.SessionCsrfInvalid);
        }

        if (error is { Length: > 0 } || code is not { Length: > 0 } issued)
        {
            BrowserProfileLog.ProviderRefused(log, context.TraceIdentifier, provider);

            return Back(attempt.ReturnTo, ErrorCodes.FactorRejected);
        }

        // AUTH-ABUSE-001: an address that has earned a delay is sent back before the
        // code is traded, so this server makes no call to the provider on its behalf.
        if (await authentication
                .ExchangeDelayedAsync(RequestOrigin.Source(context.Request), cancellationToken)
                .ConfigureAwait(false)
            is Error delayed)
        {
            return Back(attempt.ReturnTo, delayed.Code);
        }

        if (await IdentityAsync(provider, attempt, issued, cancellationToken).ConfigureAwait(false)
            is not JsonWebToken identity
            || identity.Subject is not { Length: > 0 } subject)
        {
            BrowserProfileLog.ProviderExchangeRejected(log, context.TraceIdentifier, provider);

            // CONV-LOG-005, AUTH-ABUSE-001: an identity that did not hold up is a
            // refused factor, recorded and counted against the source behind its delay.
            Error refused = await authentication
                .ProviderRefusedAsync(provider, RequestOrigin.Source(context.Request), cancellationToken)
                .ConfigureAwait(false);

            return Back(attempt.ReturnTo, refused.Code);
        }

        return attempt.Intent switch
        {
            ProviderIntent.Register => await RegisteredAsync(
                    context,
                    provider,
                    subject,
                    identity,
                    attempt.ReturnTo,
                    cancellationToken)
                .ConfigureAwait(false),
            ProviderIntent.Link => await LinkedAsync(
                    context,
                    provider,
                    subject,
                    attempt.ReturnTo,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => await SignedInAsync(
                    context,
                    await authentication
                        .DelegatedAsync(provider, subject, RequestOrigin.Of(context.Request), cancellationToken)
                        .ConfigureAwait(false),
                    attempt.ReturnTo,
                    cancellationToken)
                .ConfigureAwait(false),
        };
    }

    // BFF-SESS-006: the browser is sent back onto this application and nowhere else, so
    // anything that is not one absolute path of this origin becomes the root.
    private static string Local(string? returnTo) =>
        returnTo is { Length: > 1 }
            && returnTo[0] is '/'
            && returnTo[1] is not ('/' or '\\')
            && !returnTo.Contains('\\', StringComparison.Ordinal)
                ? returnTo
                : "/";

    // CONV-CONTENT-001: the browser comes back to where it started with the code of
    // what refused it, and the frontend says what that means.
    private static IResult Back(string destination, ErrorCode code)
    {
        int fragment = destination.IndexOf('#', StringComparison.Ordinal);
        string path = fragment < 0 ? destination : destination[..fragment];
        string rest = fragment < 0 ? string.Empty : destination[fragment..];
        char separator = path.Contains('?', StringComparison.Ordinal) ? '&' : '?';

        return Results.Redirect(
            path + separator + "error=" + Uri.EscapeDataString(code.ToString()) + rest);
    }

    private static string Challenge([NeverLogged] string verifier) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    // REG-IDENT-008: the provider is asked for the person's identity and address and
    // nothing else, with the return the deployment registered and never one a request
    // names.
    private static string Authorization(
        SocialProvider declared,
        Uri authorize,
        ProviderMetadata configured,
        OpaqueToken state,
        OpaqueToken nonce,
        [NeverLogged] string? verifier)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("response_type", "code"),
            new("client_id", declared.ClientIds[0]),
            new("redirect_uri", declared.Return.AbsoluteUri),
            new("scope", "openid email"),
            new("state", state.Value),
            new("nonce", nonce.Value),
        };

        if (configured.FormPost)
        {
            parameters.Add(new("response_mode", "form_post"));
        }

        if (verifier is not null)
        {
            parameters.Add(new("code_challenge", Challenge(verifier)));
            parameters.Add(new("code_challenge_method", "S256"));
        }

        var address = new StringBuilder(authorize.AbsoluteUri);
        char separator = authorize.Query.Length > 0 ? '&' : '?';

        foreach ((string name, string value) in parameters)
        {
            _ = address
                .Append(separator)
                .Append(name)
                .Append('=')
                .Append(Uri.EscapeDataString(value));
            separator = '&';
        }

        return address.ToString();
    }

    // REG-IDENT-008: an address counts as verified by the sign-in only where the
    // provider says it verified it; the claim is a boolean at one provider and a
    // string at the other.
    private static bool Verified(JsonWebToken identity) =>
        identity.TryGetPayloadValue("email_verified", out object? verified)
        && verified switch
        {
            bool stated => stated,
            string stated => string.Equals(stated, "true", StringComparison.Ordinal),
            _ => false,
        };

    private static string? Claim(JsonWebToken identity, string name) =>
        identity.TryGetPayloadValue(name, out string? value) && value is { Length: > 0 }
            ? value
            : null;

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // BFF-CSRF-005a: a round trip to sign in or register is bound to the
    // pre-authentication session, and one to link to the session it links for, which
    // must already be allowed to link (IDN-LIFE-012, 10 section 5a).
    private async ValueTask<Result<ProviderBinding>> BindingAsync(
        HttpContext context,
        Factor provider,
        ProviderIntent intended,
        CancellationToken cancellationToken)
    {
        if (intended is ProviderIntent.Link)
        {
            if (browser.Context is not AccessContext holder || browser.Live is not Session live)
            {
                return Result.Failure<ProviderBinding>(Error.From(ErrorCodes.SessionExpired));
            }

            return (await credentials
                    .LinkableAsync(CredentialAuthority.Of(holder, live.Id), provider, cancellationToken)
                    .ConfigureAwait(false))
                .Match(
                    () => Result.Success(ProviderBinding.Within(live.Id)),
                    Result.Failure<ProviderBinding>);
        }

        if (browser.Live is not null)
        {
            return Result.Failure<ProviderBinding>(Error.From(ErrorCodes.RegistrationSignedIn));
        }

        if (browser.FirstContact is not PreAuthentication contact)
        {
            BrowserProfileLog.ProviderUnbound(log, context.TraceIdentifier);

            return Result.Failure<ProviderBinding>(Error.From(ErrorCodes.SessionCsrfInvalid));
        }

        return intended is ProviderIntent.Register && contact.Registration is null
            ? Result.Failure<ProviderBinding>(Error.From(ErrorCodes.SessionExpired))
            : Result.Success(ProviderBinding.Before(contact.Fingerprint));
    }

    // IDN-LIFE-012, REG-IDENT-008: the code is traded on this server's own connection,
    // and the identity token it is traded for is believed only once it verifies
    // against the provider's own keys and carries the nonce this browser was sent with.
    private async ValueTask<JsonWebToken?> IdentityAsync(
        Factor provider,
        ProviderAttempt attempt,
        [NeverLogged] string code,
        CancellationToken cancellationToken)
    {
        if (providers.Of(provider) is not SocialProvider declared
            || await providers.SignInAsync(provider, cancellationToken).ConfigureAwait(false)
                is not { Token: Uri exchange }
            || await ExchangedAsync(declared, exchange, attempt, code, cancellationToken)
                .ConfigureAwait(false) is not string token
            || await providers.IdentityAsync(provider, token, cancellationToken).ConfigureAwait(false)
                is not JsonWebToken identity
            || Claim(identity, "nonce") is not string nonce
            || !CryptographicOperations.FixedTimeEquals(
                OpaqueToken.Of(nonce).Fingerprint(),
                attempt.NonceFingerprint))
        {
            return null;
        }

        return identity;
    }

    private async Task<string?> ExchangedAsync(
        SocialProvider declared,
        Uri exchange,
        ProviderAttempt attempt,
        [NeverLogged] string code,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = declared.Return.AbsoluteUri,
            ["client_id"] = declared.ClientIds[0],
            ["client_secret"] = Encoding.UTF8.GetString(declared.Secret.Span),
        };

        if (attempt.Verifier is string verifier)
        {
            parameters["code_verifier"] = verifier;
        }

        using HttpClient requests = channel.CreateClient(ProviderKeys.Channel);
        using var form = new FormUrlEncodedContent(parameters);
        using HttpResponseMessage answered = await requests
            .PostAsync(exchange, form, cancellationToken)
            .ConfigureAwait(false);

        if (!answered.IsSuccessStatusCode)
        {
            return null;
        }

        using var body = JsonDocument.Parse(
            await answered.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

        return body.RootElement.ValueKind is JsonValueKind.Object
            && body.RootElement.TryGetProperty("id_token", out JsonElement token)
            && token.ValueKind is JsonValueKind.String
                ? token.GetString()
                : null;
    }

    // REG-IDENT-008: an identity already linked signs in rather than registering, and
    // the registration it interrupted ends; any other is handed to the registration.
    private async Task<IResult> RegisteredAsync(
        HttpContext context,
        Factor provider,
        [NeverLogged] string subject,
        JsonWebToken identity,
        string destination,
        CancellationToken cancellationToken)
    {
        if (browser.FirstContact?.Registration is not RegistrationSessionId session)
        {
            return Back(destination, ErrorCodes.SessionExpired);
        }

        SessionOrigin origin = RequestOrigin.Of(context.Request);
        Error? failure = null;

        ProvidedRegistration provided = (await registration
                .ProvidedAsync(
                    session,
                    provider,
                    subject,
                    ProvidedAddress.Of(provider, Claim(identity, "email"), Verified(identity), Claim(identity, "hd")),
                    CredentialLabel.Of(origin.Device),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<ProvidedRegistration>(error, ref failure));

        if (failure is not null)
        {
            return Back(destination, failure.Code);
        }

        if (!provided.Linked)
        {
            return Results.Redirect(destination);
        }

        if ((await registration.AbandonAsync(session, null, cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error) is Error unended)
        {
            return Back(destination, unended.Code);
        }

        return await SignedInAsync(
                context,
                await authentication
                    .DelegatedAsync(provider, subject, origin, cancellationToken)
                    .ConfigureAwait(false),
                destination,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IResult> LinkedAsync(
        HttpContext context,
        Factor provider,
        [NeverLogged] string subject,
        string destination,
        CancellationToken cancellationToken)
    {
        if (browser.Context is not AccessContext holder || browser.Live is not Session live)
        {
            return Back(destination, ErrorCodes.SessionExpired);
        }

        Result linked = await credentials
            .LinkAsync(
                CredentialAuthority.Of(holder, live.Id),
                provider,
                subject,
                CredentialLabel.Of(RequestOrigin.Of(context.Request).Device),
                RequestOrigin.Source(context.Request),
                cancellationToken)
            .ConfigureAwait(false);

        return linked.Match(
            () => Results.Redirect(destination),
            refused => Back(destination, refused.Code));
    }

    // BFF-CSRF-005a AC3, AUTH-SESS-006: the session pair is written and what the
    // browser carried before the session existed is ended rather than left beside it.
    private async Task<IResult> SignedInAsync(
        HttpContext context,
        Result<SignInOutcome> outcome,
        string destination,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        SignInOutcome reached = outcome
            .Match(value => value, error => Withheld<SignInOutcome>(error, ref failure));

        if (failure is not null)
        {
            return Back(destination, failure.Code);
        }

        if (reached.Session is IssuedSession issued)
        {
            cookies.Write(context.Response, issued);
            cookies.ClearFirstContact(context.Response);

            if (browser.FirstContactSecret is OpaqueToken carried)
            {
                await contacts.RotateAsync(carried, cancellationToken).ConfigureAwait(false);
            }
        }

        return Results.Redirect(destination);
    }
}
