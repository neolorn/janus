using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Oidc;

/// <summary>
/// The authorization a browser has waiting for its sign-in: kept where a request found
/// no live session, read where a sign-in completes, and spent by the continuation.
/// </summary>
/// <param name="store">Where it is held.</param>
/// <param name="configuration">Where its lifetime is read.</param>
/// <param name="work">The transaction each operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-SESS-012 AC10 (D-224, D-226). The rotation that ends the
/// pre-authentication session carries it to the session record, in the
/// pre-authentication session's own transaction (<see cref="PreAuthenticationService"/>).
/// </remarks>
internal sealed class PendingAuthorizations(
    IPendingAuthorizationStore store,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// Keeps a request for the browser whose pre-authentication session it arrived on,
    /// replacing any it had.
    /// </summary>
    /// <param name="browser">The browser's pre-authentication session.</param>
    /// <param name="parameters">The request's validated parameters.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the failure to keep it.</returns>
    /// <exception cref="ArgumentNullException">The session is absent.</exception>
    public async ValueTask<Result> KeepAsync(
        PreAuthentication browser,
        string parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(browser);

        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.OidcContinuationLifetime, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, error => Withheld(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        await store
            .KeepAsync(
                PendingAuthorization.Keep(browser.Fingerprint, parameters, time.GetUtcNow(), lifetime),
                cancellationToken)
            .ConfigureAwait(false);

        return await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Spends the one a session record has.
    /// </summary>
    /// <param name="record">The session record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Its parameters, or nothing where none was waiting.</returns>
    public async ValueTask<string?> TakeAsync(SessionId record, CancellationToken cancellationToken)
    {
        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(_ => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        string? taken = await store.TakeAsync(record, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        return taken;
    }

    private static TimeSpan Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default;
    }
}
