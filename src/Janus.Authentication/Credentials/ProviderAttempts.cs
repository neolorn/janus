using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Credentials;

/// <summary>
/// Starts and ends the round trips to social providers, each in its own unit of work.
/// </summary>
/// <param name="store">Where the attempts are held.</param>
/// <param name="work">The unit of work each change runs in.</param>
/// <param name="time">The clock an attempt is stamped by.</param>
/// <remarks>
/// Implements IDN-LIFE-012 and REG-IDENT-008. Every return takes the attempt it answers,
/// whether it succeeds or not, so a code is judged once.
/// </remarks>
internal sealed class ProviderAttempts(IProviderAttemptStore store, IUnitOfWork work, TimeProvider time)
{
    /// <summary>
    /// Binds a round trip to the browser that started it.
    /// </summary>
    /// <param name="binding">The browser.</param>
    /// <param name="attempt">What the return is judged against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of binding it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask BindAsync(
        ProviderBinding binding,
        ProviderAttempt attempt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(attempt);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await store.BindAsync(binding, attempt, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends the round trip a browser had in flight and returns it.
    /// </summary>
    /// <param name="binding">The browser.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The attempt, or nothing where the browser had none.</returns>
    /// <exception cref="ArgumentNullException">The binding is absent.</exception>
    public async ValueTask<ProviderAttempt?> TakeAsync(
        ProviderBinding binding,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binding);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        ProviderAttempt? taken = await store.TakeAsync(binding, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return taken;
    }
}
