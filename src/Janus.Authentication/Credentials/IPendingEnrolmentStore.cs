using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Credentials;

/// <summary>
/// Where the enrolments a texted code has yet to confirm are held until they are
/// confirmed, replaced, ended with their number or lapse.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-002 and CONV-DESIGN-003. One pending enrolment per account per
/// entry: staging again replaces what went before, so an older code confirms nothing.
/// </remarks>
internal interface IPendingEnrolmentStore
{
    /// <summary>
    /// The pending enrolment of one entry on an account.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="factor">Which entry.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The pending enrolment, or nothing.</returns>
    ValueTask<PendingEnrolment?> FindAsync(SubjectId subject, Factor factor, CancellationToken cancellationToken);

    /// <summary>
    /// The pending enrolment of one entry on an account, its row held until the
    /// transaction of the operation ends, so a code presented against it, and the end
    /// of the number it is bound to, are each decided alone.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="factor">Which entry.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The pending enrolment as it stands once the lock is taken, or nothing.</returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    ValueTask<PendingEnrolment?> FindForUpdateAsync(
        SubjectId subject,
        Factor factor,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records one, replacing whatever the account had pending of that entry.
    /// </summary>
    /// <param name="pending">The pending enrolment.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    ValueTask ReplaceAsync(PendingEnrolment pending, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change to one already held.
    /// </summary>
    /// <param name="pending">The pending enrolment.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    ValueTask RecordAsync(PendingEnrolment pending, CancellationToken cancellationToken);

    /// <summary>
    /// Ends one, whether it was confirmed or abandoned.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="factor">Which entry.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    ValueTask RemoveAsync(SubjectId subject, Factor factor, CancellationToken cancellationToken);

    /// <summary>
    /// Clears what has lapsed.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were cleared.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
