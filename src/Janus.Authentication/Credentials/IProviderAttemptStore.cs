using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Credentials;

/// <summary>
/// Where the round trips to social providers are held while the browser is away.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-012 and REG-IDENT-008. An attempt lives no longer than the
/// pre-authentication session or the session it is bound to.
/// </remarks>
internal interface IProviderAttemptStore
{
    /// <summary>
    /// Binds an attempt to a browser in place of any it had in flight.
    /// </summary>
    /// <param name="binding">The browser.</param>
    /// <param name="attempt">The attempt.</param>
    /// <param name="at">When it was started.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of binding it.</returns>
    ValueTask BindAsync(
        ProviderBinding binding,
        ProviderAttempt attempt,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes the attempt a browser had in flight and returns it, so that one return is
    /// judged once.
    /// </summary>
    /// <param name="binding">The browser.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The attempt, or nothing where the browser had none.</returns>
    ValueTask<ProviderAttempt?> TakeAsync(ProviderBinding binding, CancellationToken cancellationToken);
}
