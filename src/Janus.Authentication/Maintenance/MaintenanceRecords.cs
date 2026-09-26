using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Policies;
using Janus.Core;

namespace Janus.Authentication.Maintenance;

/// <summary>
/// The licences and permits the system warns of, and the maintenance log, as the
/// management application reads and edits them.
/// </summary>
/// <param name="scope">Whether the caller may administer the deployment's compliance records.</param>
/// <param name="store">Where they are kept.</param>
/// <param name="work">The one transaction a change is written in.</param>
/// <param name="time">The clock a performed task is judged against.</param>
/// <remarks>
/// Implements OPS-MAINT-001 and chapter 09 section 8a. Every operation answers to
/// <c>compliance:manage</c> in the administrative organization. The log is appended to
/// and read; nothing here removes or changes an entry.
/// </remarks>
internal sealed class MaintenanceRecords(
    AdministrativeScope scope,
    IMaintenanceStore store,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// The licences and permits, soonest to lapse first.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The licences and permits, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<IReadOnlyList<Licence>>> LicencesAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope.RefusedAsync(context, Permissions.ComplianceManage, cancellationToken)
                .ConfigureAwait(false)
            is Error denied)
        {
            return Result.Failure<IReadOnlyList<Licence>>(denied);
        }

        return Result.Success(await store.LicencesAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Replaces the licences and permits with the list given.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="licences">What now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The context or the list is absent.</exception>
    public async ValueTask<Result> ReplaceLicencesAsync(
        AccessContext context,
        IReadOnlyList<Licence> licences,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(licences);

        if (await scope.RefusedAsync(context, Permissions.ComplianceManage, cancellationToken)
                .ConfigureAwait(false)
            is Error denied)
        {
            return Result.Failure(denied);
        }

        // Two entries under one identifier are one record stated twice, and which of
        // them stands is not the library's to choose.
        if (licences.DistinctBy(licence => licence.Id).Count() != licences.Count)
        {
            return Result.Failure(Malformed("licences"));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await store.ReplaceLicencesAsync(licences, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// The maintenance log, most recently performed first.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The entries, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<IReadOnlyList<MaintenanceEntry>>> LogAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope.RefusedAsync(context, Permissions.ComplianceManage, cancellationToken)
                .ConfigureAwait(false)
            is Error denied)
        {
            return Result.Failure<IReadOnlyList<MaintenanceEntry>>(denied);
        }

        return Result.Success(await store.LogAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Records a task performed or a review made, under the person who asks.
    /// </summary>
    /// <param name="context">Who is asking, who is the one recorded as having performed it.</param>
    /// <param name="task">Which task or review.</param>
    /// <param name="performedAt">When it was performed.</param>
    /// <param name="note">What they noted, where they noted anything.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The entry, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<MaintenanceEntry>> RecordAsync(
        AccessContext context,
        MaintenanceTask task,
        DateTimeOffset performedAt,
        string? note,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope.RefusedAsync(context, Permissions.ComplianceManage, cancellationToken)
                .ConfigureAwait(false)
            is Error denied)
        {
            return Result.Failure<MaintenanceEntry>(denied);
        }

        // OPS-MAINT-001 AC3: the entry carries the person who performed the task, so a
        // caller that is no person records nothing.
        if (context.Acting is not SubjectId actor)
        {
            return Result.Failure<MaintenanceEntry>(Error.From(ErrorCodes.Denied));
        }

        DateTimeOffset now = time.GetUtcNow();

        // A task is recorded once it has been performed; a date ahead of the clock
        // would show a review as made that has not been.
        if (performedAt > now)
        {
            return Result.Failure<MaintenanceEntry>(Malformed("performedAt"));
        }

        var entry = new MaintenanceEntry(MaintenanceEntryId.Of(now), task, performedAt, actor, note);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await store.RecordAsync(entry, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(entry);
    }

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
