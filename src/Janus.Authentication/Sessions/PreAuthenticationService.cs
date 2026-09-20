using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sessions;

/// <summary>
/// What a browser is given on first contact, and what that then answers to.
/// </summary>
/// <param name="store">Where a pre-authentication session is held.</param>
/// <param name="configuration">Where its lifetime is read.</param>
/// <param name="work">The transaction the whole of one operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">The randomness the two tokens are drawn from.</param>
/// <remarks>
/// Implements BFF-CSRF-005a and BFF-CSRF-005b. It carries no identity and grants no
/// access, so nothing here takes an access context: what it answers is whether this
/// browser is the one that started something, and never who the browser is.
/// </remarks>
internal sealed class PreAuthenticationService(
    IPreAuthenticationStore store,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    /// <summary>
    /// Issues one to a browser that carried nothing.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The two values the browser is to carry, or the refusal.</returns>
    public async ValueTask<Result<IssuedPreAuthentication>> IssueAsync(
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.RegistrationSessionLifetime, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedPreAuthentication>(failure);
        }

        var secret = OpaqueToken.Draw(randomness);
        var token = OpaqueToken.Draw(randomness);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        await store
            .AddAsync(
                PreAuthentication.Issue(secret, token, time.GetUtcNow(), lifetime),
                cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new IssuedPreAuthentication(secret, token));
    }

    /// <summary>
    /// The pre-authentication session a cookie answers to, where one does and it is
    /// still within its lifetime.
    /// </summary>
    /// <param name="secret">The token the cookie carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The session, or nothing.</returns>
    public async ValueTask<PreAuthentication?> FindAsync(
        OpaqueToken secret,
        CancellationToken cancellationToken)
    {
        PreAuthentication? held = await store
            .FindAsync(secret.Fingerprint(), cancellationToken)
            .ConfigureAwait(false);

        return held is null || held.HasExpired(time.GetUtcNow()) ? null : held;
    }

    /// <summary>
    /// Binds a registration to the browser that started it (BFF-CSRF-005b).
    /// </summary>
    /// <param name="preAuthentication">The browser's pre-authentication session.</param>
    /// <param name="registration">The registration it has in flight.</param>
    /// <param name="expiresAt">When both stop answering, which is one instant.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of binding them.</returns>
    /// <exception cref="ArgumentNullException">The session is absent.</exception>
    public async ValueTask CarryAsync(
        PreAuthentication preAuthentication,
        RegistrationSessionId registration,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preAuthentication);

        preAuthentication.Carry(registration, expiresAt);

        await store.RecordAsync(preAuthentication, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Forgets the registration a browser had in flight, which is what abandoning one
    /// leaves behind.
    /// </summary>
    /// <param name="preAuthentication">The browser's pre-authentication session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of forgetting it.</returns>
    /// <exception cref="ArgumentNullException">The session is absent.</exception>
    public async ValueTask ReleaseAsync(
        PreAuthentication preAuthentication,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preAuthentication);

        preAuthentication.Release();

        await store.RecordAsync(preAuthentication, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends one, which is what authentication does rather than leaving it beside the
    /// session it became (BFF-CSRF-005a AC3).
    /// </summary>
    /// <param name="secret">The token the cookie carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of ending it.</returns>
    public async ValueTask RotateAsync(OpaqueToken secret, CancellationToken cancellationToken) =>
        await store.RemoveAsync(secret.Fingerprint(), cancellationToken).ConfigureAwait(false);

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
