using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Outbox;

namespace Janus.Privacy.Requests;

/// <summary>
/// Putting one account into the restricted state and telling the subscribers, which
/// is one act however the restriction was decided.
/// </summary>
/// <param name="accounts">Where the state changes.</param>
/// <param name="outbox">Where the change is announced.</param>
/// <remarks>
/// Implements PRIV-RIGHT-004 and IDN-LIFE-003a. The event goes on the outbox inside
/// the caller's transaction, so a restricted account and the word to the subscribers
/// commit together or not at all.
/// </remarks>
internal sealed class RestrictionGrant(IAccountStates accounts, IOutboxStore outbox)
{
    /// <summary>
    /// Restricts the account, where it is not restricted already.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the state changed.</returns>
    public async ValueTask<bool> ApplyAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (!await accounts.RestrictAsync(subject, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await outbox
            .AddAsync(
                Delivery.Of(subject, SubjectEventKind.RestrictionChanged, at, restricted: true),
                cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
