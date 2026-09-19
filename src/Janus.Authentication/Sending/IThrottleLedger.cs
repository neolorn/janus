using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Sending;

/// <summary>
/// Where failed attempts are counted. Nothing here says whether an account exists:
/// an identifier no account holds is counted exactly as one an account holds
/// (AUTH-ABUSE-002).
/// </summary>
/// <remarks>Implements AUTH-ABUSE-001 and CONV-DESIGN-003.</remarks>
internal interface IThrottleLedger
{
    /// <summary>
    /// What one scope has accumulated.
    /// </summary>
    /// <param name="scope">Which scope.</param>
    /// <param name="key">The source, the account or the identifier.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The counter, or nothing where none stands.</returns>
    ValueTask<ThrottleCounter?> FindAsync(
        ThrottleScope scope,
        string key,
        CancellationToken cancellationToken);

    /// <summary>
    /// Counts one failure against a scope.
    /// </summary>
    /// <param name="scope">Which scope.</param>
    /// <param name="key">The source, the account or the identifier.</param>
    /// <param name="standing">What stood before it, after decay.</param>
    /// <param name="at">When it was.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of counting it.</returns>
    ValueTask FailedAsync(
        ThrottleScope scope,
        string key,
        int standing,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Forgets what one scope accumulated, which a successful sign-in does.
    /// </summary>
    /// <param name="scope">Which scope.</param>
    /// <param name="key">The source, the account or the identifier.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of forgetting it.</returns>
    ValueTask ClearAsync(ThrottleScope scope, string key, CancellationToken cancellationToken);
}
