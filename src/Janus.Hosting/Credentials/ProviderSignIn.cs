using System;
using System.Buffers.Text;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Alerting;
using Janus.Authentication.Credentials;
using Janus.Authentication.Registration;
using Janus.Authentication.Sessions;
using Janus.Authentication.SignIn;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

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
/// <param name="ring">Where each provider's credential is borrowed from at the exchange.</param>
/// <param name="alerts">Where a provider that could not be reached or read is raised.</param>
/// <param name="time">The clock a minted client secret and a raised degradation are dated by.</param>
/// <param name="randomness">What the state, the nonce and the proof key are drawn from.</param>
/// <param name="log">Where a refused round trip is recorded.</param>
/// <remarks>
/// Implements IDN-ACCT-001, IDN-LIFE-012, REG-IDENT-008, BFF-CSRF-005a and
/// BFF-OWN-001. The round trip is bound to what the browser already carries, its
/// pre-authentication session or its session, and what it was sent out with is held
/// beside that as fingerprints; the code is traded here and never in the browser; the
/// identity is the provider's <c>sub</c>, and the address the token names is never a
/// key. A browser that left for the provider comes back to the path it started from,
/// carrying the code of any refusal and never its words (CONV-CONTENT-001). A signing
/// credential has the client secret minted at each exchange, so none is stored and
/// none lapses (OPS-SEC-002). A provider whose discovery document, published keys or
/// token endpoint cannot be reached or read, every answer of the token endpoint but an
/// identity token and its refusal of the code included, is no refusal of what the person
/// presented: the browser comes back with the code of that, nothing is counted or
/// recorded as a failed authentication, and the degradation is raised under the
/// provider's scope (IDN-LIFE-012 AC6, CONV-LOG-005, OPS-OBS-002).
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
    IKeyRing ring,
    IAlertChannels alerts,
    TimeProvider time,
    RandomNumberGenerator randomness,
    ILogger<ProviderSignIn> log)
{
    // IDN-LIFE-012: how long a client secret minted for one exchange is good for.
    private static readonly TimeSpan MintedLifetime = TimeSpan.FromMinutes(5);

    // Chapter 10 section 5.23: the scope a provider that could not be reached or read is
    // raised under, before the provider's name.
    private const string UnavailableScope = "provider.unavailable:";

    // RFC 6749 section 5.2: the error a token endpoint refuses the grant itself with,
    // the code invalid, expired, revoked or issued to another client.
    private const string CodeRefused = "invalid_grant";

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
            return Back(context, destination, Error.From(ErrorCodes.RequestMalformed));
        }

        // A browser already signed in has nothing to sign in to, and goes where it was
        // going, as it does from the sign-on (BFF-SESS-006 AC1).
        if (intended is ProviderIntent.SignIn && browser.Live is not null)
        {
            return SeeOther(context, destination);
        }

        Error? failure = null;

        ProviderBinding binding = (await BindingAsync(context, provider, intended, cancellationToken)
                .ConfigureAwait(false))
            .Match(bound => bound, error => Withheld<ProviderBinding>(error, ref failure));

        if (failure is not null)
        {
            return Back(context, destination, failure);
        }

        // API-CONV-003: a provider the deployment does not declare is a factor that is
        // not permitted; nothing is presented, so nothing is counted.
        if (providers.Of(provider) is not SocialProvider declared)
        {
            BrowserProfileLog.ProviderUndeclared(log, context.TraceIdentifier, provider);

            return Back(context, destination, Error.From(ErrorCodes.FactorNotPermitted));
        }

        ProviderMetadata configured = (await providers.SignInAsync(provider, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<ProviderMetadata>(error, ref failure));

        if (failure is not null)
        {
            return await UnavailableAsync(context, provider, destination, failure, cancellationToken)
                .ConfigureAwait(false);
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

        return SeeOther(context, Authorization(declared, configured.Authorization!, configured, state, nonce, verifier));
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

        // AUTH-ABUSE-001 AC12: an address that has earned a delay is sent back first,
        // whatever its return carries and before any code is traded, so this server
        // makes no call to the provider on its behalf.
        if (await authentication
                .ExchangeDelayedAsync(RequestOrigin.Source(context.Request), cancellationToken)
                .ConfigureAwait(false)
            is Error delayed)
        {
            return Back(context, attempt.ReturnTo, delayed);
        }

        // AUTH-ABUSE-001 AC14, CONV-LOG-005: the provider's own error, a cancel
        // included, and a return with no code present nothing, so the browser goes back
        // with the code of a refused factor and nothing is counted or recorded.
        if (error is { Length: > 0 } || code is not { Length: > 0 } issued)
        {
            BrowserProfileLog.ProviderRefused(log, context.TraceIdentifier, provider);

            return Back(context, attempt.ReturnTo, Error.From(ErrorCodes.FactorRejected));
        }

        Error? unavailable = null;

        JsonWebToken? identity = (await IdentityAsync(provider, attempt, issued, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<JsonWebToken?>(error, ref unavailable));

        if (unavailable is not null)
        {
            return await UnavailableAsync(context, provider, attempt.ReturnTo, unavailable, cancellationToken)
                .ConfigureAwait(false);
        }

        if (identity?.Subject is not { Length: > 0 } subject)
        {
            BrowserProfileLog.ProviderExchangeRejected(log, context.TraceIdentifier, provider);

            // CONV-LOG-005, AUTH-ABUSE-001: an identity that did not hold up is a
            // refused factor, recorded and counted against the source behind its delay.
            Error refused = await authentication
                .ProviderRefusedAsync(provider, RequestOrigin.Source(context.Request), cancellationToken)
                .ConfigureAwait(false);

            return Back(context, attempt.ReturnTo, refused);
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

    // Chapter 09: the round trip answers 303 wherever it sends the browser, to the
    // provider, on to where it was going or back to where it started, so what follows
    // is a read whatever the request was.
    private static IResult SeeOther(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;

        return Results.Empty;
    }

    // BFF-ERR-001: the browser comes back to where it started with the code of what
    // refused it.
    private static IResult Back(HttpContext context, string destination, Error refusal) =>
        SeeOther(context, NavigationReturn.Refused(destination, refusal));

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

    // IDN-LIFE-012 AC6, OPS-OBS-002: a provider that could not be reached or read is
    // recorded, raised as the degradation it is under the provider's scope, naming the
    // provider and the part, and answered with its code. Nothing the person presented
    // failed, so nothing is counted and no failed authentication is recorded
    // (CONV-LOG-005). Any other refusal goes back as it is.
    private async Task<IResult> UnavailableAsync(
        HttpContext context,
        Factor provider,
        string destination,
        Error refusal,
        CancellationToken cancellationToken)
    {
        if (refusal.Code != ErrorCodes.ProviderUnavailable
            || !refusal.Details.TryGetValue(ProviderKeys.Part, out JsonElement part))
        {
            return Back(context, destination, refusal);
        }

        string named = ProviderRoutes.NameOf(provider);

        BrowserProfileLog.ProviderUnavailable(log, context.TraceIdentifier, provider, part.GetString()!);

        Result raised = await alerts
            .RaiseAsync(
                Alerts.Scoped(
                    AlertCondition.Degradation,
                    UnavailableScope + named,
                    time.GetUtcNow(),
                    new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                    {
                        ["provider"] = JsonSerializer.SerializeToElement(named),
                        [ProviderKeys.Part] = part,
                    }),
                cancellationToken)
            .ConfigureAwait(false);

        return Back(
            context,
            destination,
            raised.Match(() => Error.From(ErrorCodes.ProviderUnavailable), unraised => unraised));
    }

    // IDN-LIFE-012, REG-IDENT-008: the code is traded on this server's own connection,
    // and the identity token it is traded for is believed only once it verifies
    // against the provider's own keys and carries the nonce this browser was sent with.
    // An identity that does not hold up is nothing; a provider that could not be
    // reached or read is the failure it is.
    private async ValueTask<Result<JsonWebToken?>> IdentityAsync(
        Factor provider,
        ProviderAttempt attempt,
        [NeverLogged] string code,
        CancellationToken cancellationToken)
    {
        if (providers.Of(provider) is not SocialProvider declared)
        {
            return Result.Success<JsonWebToken?>(null);
        }

        Error? unavailable = null;

        ProviderMetadata configured = (await providers.SignInAsync(provider, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<ProviderMetadata>(error, ref unavailable));

        if (unavailable is not null)
        {
            return Result.Failure<JsonWebToken?>(unavailable);
        }

        string? token = (await ExchangedAsync(declared, configured, attempt, code, cancellationToken)
                .ConfigureAwait(false))
            .Match(traded => traded, error => Withheld<string?>(error, ref unavailable));

        if (unavailable is not null)
        {
            return Result.Failure<JsonWebToken?>(unavailable);
        }

        return Result.Success(
            token is not null
            && await providers.IdentityAsync(provider, configured, token, cancellationToken).ConfigureAwait(false)
                is JsonWebToken identity
            && Claim(identity, "nonce") is string nonce
            && CryptographicOperations.FixedTimeEquals(
                OpaqueToken.Of(nonce).Fingerprint(),
                attempt.NonceFingerprint)
                ? identity
                : null);
    }

    // IDN-LIFE-012 AC6: the code traded for an identity token: the token; nothing where
    // the provider refused the code itself, which is a 400 whose error is
    // invalid_grant and no other answer (RFC 6749 section 5.2); or that the token
    // endpoint could not be reached or read, which is every other answer, a refusal of
    // the deployment's own client and a success holding no identity token among them.
    private async Task<Result<string?>> ExchangedAsync(
        SocialProvider declared,
        ProviderMetadata configured,
        ProviderAttempt attempt,
        [NeverLogged] string code,
        CancellationToken cancellationToken)
    {
        // CONV-CODE-007: the credential is borrowed for the making of the form value and
        // no longer; a provider the deployment declared had it read at startup.
        string secret = ring
            .BorrowProviderCredential(
                ProviderRoutes.NameOf(declared.Provider),
                credential => Presented(credential, declared.ClientIds[0], configured.Issuer))
            .Match(presented => presented, error => throw new InvalidOperationException(error.Code.ToString()));

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = declared.Return.AbsoluteUri,
            ["client_id"] = declared.ClientIds[0],
            ["client_secret"] = secret,
        };

        if (attempt.Verifier is string verifier)
        {
            parameters["code_verifier"] = verifier;
        }

        try
        {
            using HttpClient requests = channel.CreateClient(ProviderKeys.Channel);
            using var form = new FormUrlEncodedContent(parameters);
            using HttpResponseMessage answered = await requests
                .PostAsync(configured.Token!, form, cancellationToken)
                .ConfigureAwait(false);

            using var body = JsonDocument.Parse(
                await answered.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

            if (answered.StatusCode is HttpStatusCode.BadRequest && Member(body, "error") is CodeRefused)
            {
                return Result.Success<string?>(null);
            }

            return answered.IsSuccessStatusCode && Member(body, "id_token") is { Length: > 0 } token
                ? Result.Success<string?>(token)
                : Result.Failure<string?>(ProviderKeys.Unavailable(ProviderPart.Token));
        }
        catch (Exception unanswered) when (Unreached(unanswered, cancellationToken))
        {
            return Result.Failure<string?>(ProviderKeys.Unavailable(ProviderPart.Token));
        }
    }

    // A member of the object a token endpoint answered with, where it holds it as text.
    private static string? Member(JsonDocument answered, string name) =>
        answered.RootElement.ValueKind is JsonValueKind.Object
        && answered.RootElement.TryGetProperty(name, out JsonElement member)
        && member.ValueKind is JsonValueKind.String
            ? member.GetString()
            : null;

    // IDN-LIFE-012 AC6: the token endpoint was not reached where no response came
    // back, the connection failing or the wait for it running out with the caller
    // still there, and was not read where what it answered a trade with is no JSON,
    // whatever its status.
    private static bool Unreached(Exception failure, CancellationToken cancellationToken) =>
        failure is HttpRequestException or JsonException
        || (failure is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    // IDN-LIFE-012, OPS-SEC-002: a static secret is presented as the provider issued
    // it; from a signing credential a client secret is minted for this exchange alone,
    // signed with ES256 under the key the provider issued, naming the client as its
    // subject and the provider's issuer as its audience, and good for five minutes.
    private string Presented(ProviderCredential credential, string client, string issuer)
    {
        if (!credential.IsSigned)
        {
            return Encoding.UTF8.GetString(credential.Material.Span);
        }

        using var key = ECDsa.Create();

        key.ImportPkcs8PrivateKey(credential.Material.Span, out _);

        DateTime now = time.GetUtcNow().UtcDateTime;

        // The key is made for this exchange and disposed after it, so no signature
        // provider made over it is kept for the next.
        var signing = new ECDsaSecurityKey(key)
        {
            KeyId = credential.KeyId,
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
        };

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = credential.Issuer,
                Audience = issuer,
                IssuedAt = now,
                Expires = now + MintedLifetime,
                Claims = new Dictionary<string, object>(StringComparer.Ordinal) { ["sub"] = client },
                SigningCredentials = new SigningCredentials(signing, SecurityAlgorithms.EcdsaSha256),
            });
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
            return Back(context, destination, Error.From(ErrorCodes.SessionExpired));
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
                    origin.Source,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<ProvidedRegistration>(error, ref failure));

        if (failure is not null)
        {
            return Back(context, destination, failure);
        }

        if (!provided.Linked)
        {
            return SeeOther(context, destination);
        }

        if ((await registration.AbandonAsync(session, null, cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error) is Error unended)
        {
            return Back(context, destination, unended);
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
            return Back(context, destination, Error.From(ErrorCodes.SessionExpired));
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
            () => SeeOther(context, destination),
            refused => Back(context, destination, refused));
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
            return Back(context, destination, failure);
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

        return SeeOther(context, destination);
    }
}
