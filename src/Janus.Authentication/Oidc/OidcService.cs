using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Oidc;

/// <summary>
/// What the library owns of the provider: the session record every token is minted
/// from, the end of everything derived from a record whose token came back twice, the
/// claims a token covers, and the keys it is validated against.
/// </summary>
/// <param name="source">The signing keys the set a token validates against is read from.</param>
/// <param name="sessions">Where the session record a token stands on is read and ended.</param>
/// <param name="identifiers">Where the primary address and the language are read.</param>
/// <param name="accounts">Where the display name is read.</param>
/// <param name="audit">Where a revoked family is recorded.</param>
/// <param name="configuration">Where the lifetime comes from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, AUTH-OIDC-001, AUTH-OIDC-002, AUTH-OIDC-003, AUTH-OIDC-004,
/// AUTH-SESS-012, AUTH-KEY-001 and API-REDIR-001. Every token stands on a session
/// record: the access token is minted from it, the refresh token is a handle on it and
/// never outlives it, and ending the record ends both.
/// </remarks>
internal sealed class OidcService(
    SigningCredentialSource source,
    ISessionStore sessions,
    IIdentifierDirectory identifiers,
    IAccountDirectory accounts,
    IOidcAudit audit,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time) : IOidc, ITokenMinting
{
    private static readonly char[] Separator = [' '];

    /// <inheritdoc/>
    public async ValueTask<Result<MintedSession>> MintAsync(
        SessionId session,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();

        // AUTH-OIDC-004 AC1, AUTH-OIDC-003 AC3: nothing is minted from a record that
        // has been revoked or has reached either of its expiries.
        if (await sessions.FindAsync(session, cancellationToken).ConfigureAwait(false)
            is not Session live
            || live.EndedAt is not null
            || now >= live.AbsoluteExpiry
            || now >= live.IdleExpiry)
        {
            return Result.Failure<MintedSession>(Error.From(ErrorCodes.SessionExpired));
        }

        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.OidcAccessTokenLifetime, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        return failure is Error refusal
            ? Result.Failure<MintedSession>(refusal)
            : Result.Success(new MintedSession(
                live.Subject,
                lifetime,
                (live.IdleExpiry < live.AbsoluteExpiry ? live.IdleExpiry : live.AbsoluteExpiry) - now));
    }

    /// <summary>
    /// Ends everything derived from a session record because a token standing on it
    /// was presented a second time, and records that it happened.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="session">The record everything derived from.</param>
    /// <param name="clientId">Which client presented it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of ending them.</returns>
    public async ValueTask ReuseAsync(
        SubjectId subject,
        SessionId session,
        string clientId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();

        // AUTH-OIDC-003 AC1 and AC2: a token presented twice means a copy is in
        // someone's hands and there is no telling whose, so everything derived from the
        // record goes and the whole of it is recorded.
        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(_ => { }, error => throw new InvalidOperationException(error.Code.ToString()));
        await sessions.EndSpineAsync(session, now, cancellationToken).ConfigureAwait(false);
        await audit.ReusedAsync(subject, clientId, session, now, cancellationToken).ConfigureAwait(false);
        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<OidcClaims>> ClaimsAsync(
        AccessContext context,
        string scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scope);

        // LIB-API-005 (D-166, 160): a caller in process reads the claims of the identity
        // it carries and of no other account.
        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure<OidcClaims>(Error.From(ErrorCodes.Denied));
        }

        // IDN-ACCT-007: a restricted account signs on and reads its mail, as an active
        // one does.
        if (await accounts.StateAsync(subject, cancellationToken).ConfigureAwait(false)
            is not (AccountState.Active or AccountState.Restricted))
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
    public async ValueTask<Result<IReadOnlyList<PublishedSigningKey>>> KeysAsync(
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        // AUTH-KEY-001: a request for the key set is a read of the signing keys, so a
        // change its times make due is made before the set is answered.
        SigningKeySet set = (await source.ReadAsync(configuration, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<SigningKeySet>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IReadOnlyList<PublishedSigningKey>>(failure);
        }

        return Result.Success<IReadOnlyList<PublishedSigningKey>>(
            [.. set.Published(time.GetUtcNow()).Select(held => new PublishedSigningKey(
                held.Key.KeyId,
                held.Key.Algorithm,
                held.Key.PublicKey,
                held.Key.OverlapEndsAt))]);
    }

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
}
