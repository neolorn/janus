using System;
using System.Collections.Generic;
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
/// <param name="keys">What signs tokens and publishes the set they validate against.</param>
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
    SigningKeys keys,
    ISessionStore sessions,
    IIdentifierDirectory identifiers,
    IAccountDirectory accounts,
    IOidcAudit audit,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time) : IOidc
{
    private static readonly char[] Separator = [' '];

    /// <summary>
    /// What a token minted from a session record may carry and how long it may last.
    /// </summary>
    /// <param name="session">The record the token stands on.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// What to mint from, or <c>auth.session.expired</c> where the record has been
    /// revoked or has reached either of its expiries: no token is minted from a record
    /// that no longer answers.
    /// </returns>
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
        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await sessions.EndSpineAsync(session, now, cancellationToken).ConfigureAwait(false);
        await audit.ReusedAsync(subject, clientId, session, now, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
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
}
