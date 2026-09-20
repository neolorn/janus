using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Recovery;

/// <summary>
/// Where the links recovery sends are held. The plain token never reaches a record.
/// </summary>
/// <remarks>Implements AUTH-RECOV-002, AUTH-RECOV-005 and CONV-DESIGN-003.</remarks>
internal interface IRecoveryLinkStore
{
    /// <summary>
    /// The link a token answers to.
    /// </summary>
    /// <param name="fingerprint">What the token hashes to.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The link, or nothing where there is none.</returns>
    ValueTask<RecoveryLink?> FindAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// The link that opened one enrolment session, which is that session.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The link, or nothing where there is none.</returns>
    ValueTask<RecoveryLink?> FindAsync(
        EnrolmentSessionId session,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a newly issued link, replacing whatever of that purpose the account had
    /// outstanding: asking again is what ends an older message.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask ReplaceAsync(RecoveryLink link, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change a link made onto its row.
    /// </summary>
    /// <param name="link">The link as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordAsync(RecoveryLink link, CancellationToken cancellationToken);

    /// <summary>
    /// Removes one link, which completing the enrolment it opened does.
    /// </summary>
    /// <param name="fingerprint">What the token hashes to.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing it.</returns>
    ValueTask RemoveAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Removes every link that has stopped working.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were removed.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
