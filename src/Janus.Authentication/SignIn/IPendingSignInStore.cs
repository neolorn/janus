using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.SignIn;

/// <summary>
/// Where the sign-in links and codes that have gone out are held until they are used
/// or lapse.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-003 and AUTH-KEY-003. One outstanding link or code per
/// account per factor: asking again replaces what went before, so an older message is
/// never a second way in.
/// </remarks>
internal interface IPendingSignInStore
{
    /// <summary>
    /// The pending sign-in a token answers to.
    /// </summary>
    /// <param name="fingerprint">What the token hashes to.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The pending sign-in, or nothing.</returns>
    ValueTask<PendingSignIn?> FindAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// The account's outstanding sign-in of one kind.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="factor">Which kind.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The pending sign-in, or nothing.</returns>
    ValueTask<PendingSignIn?> FindAsync(
        SubjectId subject,
        Factor factor,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records one, replacing whatever the account had outstanding of that kind.
    /// </summary>
    /// <param name="pending">The pending sign-in.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    ValueTask ReplaceAsync(PendingSignIn pending, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change to one already held.
    /// </summary>
    /// <param name="pending">The pending sign-in.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    ValueTask RecordAsync(PendingSignIn pending, CancellationToken cancellationToken);

    /// <summary>
    /// Ends one, whether it was used or abandoned.
    /// </summary>
    /// <param name="fingerprint">What the token hashes to.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    ValueTask RemoveAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Clears what has lapsed.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were cleared.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
