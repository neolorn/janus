using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The licences and permits the system warns of, and the maintenance log, as the
/// management application reads and edits them.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, OPS-MAINT-001 and chapter 09 section 8a. Every operation
/// answers to <c>compliance:manage</c> in the administrative organization. The log is
/// appended to and read; nothing here removes or changes an entry. The assessment
/// records are declared through <see cref="IProcessingRecords.DeclareAsync"/>.
/// </remarks>
public interface IMaintenanceRecords
{
    /// <summary>
    /// The licences and permits, soonest to lapse first.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The licences and permits, or the refusal.</returns>
    ValueTask<Result<IReadOnlyList<Licence>>> LicencesAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the licences and permits with the list given.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="licences">What now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or the refusal: <c>api.request.malformed</c> naming <c>licences</c>
    /// where two entries share an identifier.
    /// </returns>
    ValueTask<Result> ReplaceLicencesAsync(
        AccessContext context,
        IReadOnlyList<Licence> licences,
        CancellationToken cancellationToken);

    /// <summary>
    /// The maintenance log, most recently performed first.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The entries, or the refusal.</returns>
    ValueTask<Result<IReadOnlyList<MaintenanceEntry>>> LogAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a task performed or a review made, under the person who asks.
    /// </summary>
    /// <param name="context">Who is asking, who is the one recorded as having performed it.</param>
    /// <param name="task">Which task or review.</param>
    /// <param name="performedAt">When it was performed.</param>
    /// <param name="note">What they noted, where they noted anything.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The entry, or the refusal: <c>api.request.malformed</c> naming
    /// <c>performedAt</c> where it lies after now.
    /// </returns>
    ValueTask<Result<MaintenanceEntry>> RecordAsync(
        AccessContext context,
        MaintenanceTask task,
        DateTimeOffset performedAt,
        string? note,
        CancellationToken cancellationToken);
}
