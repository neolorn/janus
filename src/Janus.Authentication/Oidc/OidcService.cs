using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Oidc;

/// <summary>
/// What the library owns of the provider: the registry, the one-time codes a live
/// session is issued, the exchange those codes settle, the rotation of a refresh
/// token, the claims a token covers, and the keys it is validated against.
/// </summary>
/// <param name="clients">The registry of manually registered clients.</param>
/// <param name="codes">Where the one-time codes wait to be exchanged.</param>
/// <param name="tokens">Where the refresh tokens are held, by family.</param>
/// <param name="keys">What signs tokens and publishes the set they validate against.</param>
/// <param name="sessions">Where the session record a token stands on is read and ended.</param>
/// <param name="identifiers">Where the primary address and the language are read.</param>
/// <param name="accounts">Where the display name is read.</param>
/// <param name="audit">Where a revoked family is recorded.</param>
/// <param name="configuration">Where the lifetimes come from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a code and a token are drawn from.</param>
/// <remarks>
/// Implements LIB-API-005, AUTH-OIDC-001, AUTH-OIDC-002, AUTH-OIDC-003, AUTH-OIDC-004,
/// AUTH-SESS-012, AUTH-KEY-001 and API-REDIR-001. Every token stands on a session
/// record: the access token is minted from it, the refresh token is a handle on it and
/// never outlives it, and ending the record ends both.
/// </remarks>
internal sealed class OidcService(
    IOidcClientStore clients,
    IAuthorizationCodeStore codes,
    IRefreshTokenStore tokens,
    SigningKeys keys,
    ISessionStore sessions,
    IIdentifierDirectory identifiers,
    IAccountDirectory accounts,
    IOidcAudit audit,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness) : IOidc
{
    private const string Separator = " ";

    /// <inheritdoc/>
    public async ValueTask<Result<OidcClient>> FindClientAsync(
        string clientId,
        CancellationToken cancellationToken) =>
        await clients.FindAsync(clientId, cancellationToken).ConfigureAwait(false)
            is OidcClient registered
            ? Result.Success(registered)
            : Result.Failure<OidcClient>(Error.From(ErrorCodes.Denied));

    /// <inheritdoc/>
    public async ValueTask<Result<IssuedCode>> IssueCodeAsync(
        AuthorizationIntent intent,
        SessionId? session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);

        // AUTH-OIDC-001 AC2, API-REDIR-001: an unregistered client obtains nothing, and
        // the destination is the one the registry holds and never the one asked for.
        if (await clients.FindAsync(intent.ClientId, cancellationToken).ConfigureAwait(false)
            is not OidcClient client)
        {
            return Result.Failure<IssuedCode>(Error.From(ErrorCodes.Denied));
        }

        if (!string.Equals(client.Redirect, intent.Redirect, StringComparison.Ordinal))
        {
            return Result.Failure<IssuedCode>(Error.From(ErrorCodes.Denied));
        }

        // AUTH-SESS-012 AC4: the verifier is judged against the challenge, so a request
        // that carries none is refused rather than issued a code nothing protects.
        if (intent.CodeChallenge is not { Length: > 0 }
            || !string.Equals(intent.CodeChallengeMethod, "S256", StringComparison.Ordinal))
        {
            return Result.Failure<IssuedCode>(Error.From(ErrorCodes.Denied));
        }

        DateTimeOffset now = time.GetUtcNow();

        // AUTH-SESS-012 AC3: a silent request where no session answers is told so and
        // is never quietly issued one.
        if (session is not SessionId held
            || await sessions.FindAsync(held, cancellationToken).ConfigureAwait(false)
                is not Session live
            || live.EndedAt is not null
            || now >= live.AbsoluteExpiry)
        {
            return Result.Failure<IssuedCode>(Error.From(ErrorCodes.SessionExpired));
        }

        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.OidcCodeLifetime, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedCode>(failure);
        }

        var drawn = OpaqueToken.Draw(randomness);
        var code = AuthorizationCode.Issue(
            drawn.Fingerprint(),
            client,
            live.Subject,
            live.Spine,
            intent.Redirect,
            intent.CodeChallenge,
            intent.CodeChallengeMethod,
            intent.Scope,
            intent.Nonce,
            now,
            lifetime);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await codes.AddAsync(code, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new IssuedCode(drawn.Value, code.ExpiresAt));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IssuedToken>> RedeemCodeAsync(
        CodeRedemption redemption,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(redemption);

        if (await AuthenticatedAsync(redemption.ClientId, redemption.ClientSecret, cancellationToken)
                .ConfigureAwait(false)
            is not OidcClient client)
        {
            return Result.Failure<IssuedToken>(Error.From(ErrorCodes.Denied));
        }

        DateTimeOffset now = time.GetUtcNow();
        AuthorizationCode? code = await codes
            .FindAsync(OpaqueToken.Of(redemption.Code).Fingerprint(), cancellationToken)
            .ConfigureAwait(false);

        if (code is null || !code.Opens(client.ClientId, redemption.Redirect, now))
        {
            return Result.Failure<IssuedToken>(Error.From(ErrorCodes.CodeInvalid));
        }

        // AUTH-SESS-012 AC4: the code was issued against a challenge, and only the
        // holder of the verifier that produced it may exchange it.
        if (!Proves(redemption.CodeVerifier, code.Challenge))
        {
            return Result.Failure<IssuedToken>(Error.From(ErrorCodes.CodeInvalid));
        }

        // AUTH-OIDC-004 AC1: no token is minted from a record that has been revoked.
        if (await sessions.FindAsync(code.Session, cancellationToken).ConfigureAwait(false)
            is not Session live
            || live.EndedAt is not null
            || now >= live.AbsoluteExpiry)
        {
            return Result.Failure<IssuedToken>(Error.From(ErrorCodes.SessionExpired));
        }

        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.OidcAccessTokenLifetime, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedToken>(failure);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        code.Spend(now);

        await codes.RecordAsync(code, cancellationToken).ConfigureAwait(false);

        // AUTH-OIDC-002 AC2: a browser application's own layer holds no token after the
        // exchange, so it is issued nothing to hold.
        string? refresh = client.Kind is OidcClientKind.Protocol
            ? await IssuedRefreshAsync(
                    RefreshFamilyId.New(time),
                    client.ClientId,
                    live,
                    code.Scope,
                    now,
                    cancellationToken)
                .ConfigureAwait(false)
            : null;

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new IssuedToken(
            live.Subject,
            live.Spine,
            code.Scope,
            code.Nonce,
            lifetime,
            refresh));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IssuedToken>> RefreshAsync(
        RefreshRedemption redemption,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(redemption);

        if (await AuthenticatedAsync(redemption.ClientId, redemption.ClientSecret, cancellationToken)
                .ConfigureAwait(false)
            is not OidcClient client)
        {
            return Result.Failure<IssuedToken>(Error.From(ErrorCodes.Denied));
        }

        DateTimeOffset now = time.GetUtcNow();
        RefreshToken? held = await tokens
            .FindAsync(OpaqueToken.Of(redemption.RefreshToken).Fingerprint(), cancellationToken)
            .ConfigureAwait(false);

        if (held is null || !string.Equals(held.ClientId, client.ClientId, StringComparison.Ordinal))
        {
            return Result.Failure<IssuedToken>(Error.From(ErrorCodes.CodeInvalid));
        }

        // AUTH-OIDC-003 AC1: a token presented twice means a copy is in someone's hands
        // and there is no telling whose, so everything derived from the session goes.
        if (held.ConsumedAt is not null)
        {
            await RevokeAsync(held, now, cancellationToken).ConfigureAwait(false);

            return Result.Failure<IssuedToken>(Error.From(ErrorCodes.CodeReplayed));
        }

        // AUTH-OIDC-003 AC3: the token is a handle on the record, so it stops when the
        // record does and has no lifetime to outlive it with.
        if (now >= held.ExpiresAt
            || await sessions.FindAsync(held.Session, cancellationToken).ConfigureAwait(false)
                is not Session live
            || live.EndedAt is not null
            || now >= live.AbsoluteExpiry
            || now >= live.IdleExpiry)
        {
            return Result.Failure<IssuedToken>(Error.From(ErrorCodes.SessionExpired));
        }

        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.OidcAccessTokenLifetime, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedToken>(failure);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        held.Consume(now);

        await tokens.RecordAsync(held, cancellationToken).ConfigureAwait(false);

        string rotated = await IssuedRefreshAsync(
                held.Family,
                client.ClientId,
                live,
                held.Scope,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new IssuedToken(
            live.Subject,
            live.Spine,
            held.Scope,
            Nonce: null,
            lifetime,
            rotated));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<OidcClaims>> ClaimsAsync(
        SubjectId subject,
        string scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (await accounts.StateAsync(subject, cancellationToken).ConfigureAwait(false)
            is not AccountState.Active)
        {
            return Result.Failure<OidcClaims>(Error.From(ErrorCodes.Denied));
        }

        // Chapter 09 section 9 (D-153): each scope names what it gives, and nothing
        // outside those three lists leaves the library.
        bool email = Covers(scope, "email");
        bool profile = Covers(scope, "profile");

        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        HeldIdentifier? primary = null;
        HeldIdentifier? username = null;

        foreach (HeldIdentifier identifier in held.All)
        {
            if (identifier.Kind is IdentifierKind.Email && identifier.IsPrimary)
            {
                primary = identifier;
            }

            if (identifier.Kind is IdentifierKind.Username)
            {
                username = identifier;
            }
        }

        HeldProfile shown = profile
            ? await accounts.ProfileAsync(subject, cancellationToken).ConfigureAwait(false)
            : new HeldProfile(null, null, null, null);

        return Result.Success(new OidcClaims(
            subject,
            email ? primary?.Canonical : null,
            email && primary is not null ? primary.IsVerified : null,
            profile ? shown.DisplayName?.Value : null,
            profile ? username?.Canonical : null,
            profile
                ? await identifiers.LanguageAsync(subject, cancellationToken).ConfigureAwait(false)
                : null));
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<PublishedSigningKey>>> KeysAsync(
        CancellationToken cancellationToken) =>
        keys.PublishedAsync(cancellationToken);

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static bool Covers(string scope, string wanted)
    {
        foreach (string asked in scope.Split(Separator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(asked, wanted, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // The challenge is the verifier's hash, so the verifier proves it without the
    // challenge ever being reversible (AUTH-SESS-012).
    private static bool Proves(string verifier, string challenge)
    {
        if (verifier is not { Length: > 0 })
        {
            return false;
        }

        Span<byte> computed = stackalloc byte[32];
        _ = SHA256.HashData(Encoding.ASCII.GetBytes(verifier), computed);

        return Base64Url.IsValid(challenge)
            && CryptographicOperations.FixedTimeEquals(
                computed,
                Base64Url.DecodeFromChars(challenge));
    }

    // AUTH-OIDC-001 AC2: the client is what the registry holds and what the secret
    // authenticates, and neither alone is enough.
    private async ValueTask<OidcClient?> AuthenticatedAsync(
        string clientId,
        string secret,
        CancellationToken cancellationToken)
    {
        if (await clients.FindAsync(clientId, cancellationToken).ConfigureAwait(false)
            is not OidcClient client
            || secret is not { Length: > 0 })
        {
            return null;
        }

        return await clients
            .AuthenticatesAsync(client.ClientId, OpaqueToken.Of(secret).Fingerprint(), cancellationToken)
            .ConfigureAwait(false)
            ? client
            : null;
    }

    // AUTH-OIDC-003: the token stops no later than the record it is a handle on, which
    // is the earlier of the record's idle and absolute expiry.
    private async ValueTask<string> IssuedRefreshAsync(
        RefreshFamilyId family,
        string clientId,
        Session live,
        string scope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var drawn = OpaqueToken.Draw(randomness);

        await tokens
            .AddAsync(
                RefreshToken.Issue(
                    drawn.Fingerprint(),
                    family,
                    clientId,
                    live.Subject,
                    live.Spine,
                    scope,
                    now,
                    live.IdleExpiry < live.AbsoluteExpiry ? live.IdleExpiry : live.AbsoluteExpiry),
                cancellationToken)
            .ConfigureAwait(false);

        return drawn.Value;
    }

    // AUTH-OIDC-003 AC1 and AC2: the family goes, the sessions derived from the record
    // go with it, and the whole of it is recorded.
    private async ValueTask RevokeAsync(
        RefreshToken held,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await tokens.RemoveFamilyAsync(held.Family, cancellationToken).ConfigureAwait(false);
        await sessions.EndSpineAsync(held.Session, now, cancellationToken).ConfigureAwait(false);
        await audit
            .ReusedAsync(held.Subject, held.ClientId, held.Session, now, cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
