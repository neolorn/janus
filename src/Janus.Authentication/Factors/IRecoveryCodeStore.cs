using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// Where an account's recovery codes are read and written. An account holds one set;
/// generating another replaces it whole.
/// </summary>
/// <remarks>Implements AUTH-FACT-008, AUTH-FACT-009 and CONV-DESIGN-003.</remarks>
internal interface IRecoveryCodeStore
{
    /// <summary>
    /// The account's set.
    /// </summary>
    /// <param name="subject">Whose set.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The set, or nothing where the account holds none.</returns>
    ValueTask<RecoveryCodeSet?> FindAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the account's set, replacing whatever it held.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask ReplaceAsync(RecoveryCodeSet set, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change a set made onto its rows.
    /// </summary>
    /// <param name="set">The set as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordAsync(RecoveryCodeSet set, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the account's set, which invalidating the last second factor does: the
    /// codes have nothing left to stand in for (AUTH-RECOV-007).
    /// </summary>
    /// <param name="subject">Whose set.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing it.</returns>
    ValueTask RemoveAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// The accounts whose set is owed its one reminder: generated at or before an
    /// instant, never reminded, and held by an account that is active. Oldest set first.
    /// </summary>
    /// <param name="generatedBy">The latest generation old enough to be reminded of.</param>
    /// <param name="count">How many at most.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The accounts.</returns>
    ValueTask<IReadOnlyList<SubjectId>> DueReminderAsync(
        DateTimeOffset generatedBy,
        int count,
        CancellationToken cancellationToken);
}
