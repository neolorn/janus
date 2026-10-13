using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Privacy.Exports;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Exports;

/// <summary>
/// The exports an account has taken, over the <c>privacy_exports</c> table.
/// </summary>
/// <param name="context">The context the operation's reads and writes run on.</param>
/// <param name="connections">Where the lock statement takes its connection from.</param>
/// <remarks>
/// Implements PRIV-RIGHT-003, D-086 and CONV-DESIGN-003. The row is added onto the
/// transaction in progress, so an export that is counted is an export the caller
/// committed.
/// </remarks>
internal sealed class ExportLedger(StoreContext context, DataConnections connections) : IExportLedger
{
    // D-166 X3: an export counts the subject's window and then records itself, so two
    // at once would each count the window without the other. The subject's exports are
    // held for the rest of the transaction; no read takes this lock.
    private const string Hold =
        "SELECT pg_advisory_xact_lock(hashtextextended('identity.privacy_exports/' || CAST(@subject AS text), 0));";

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask HoldAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A subject's exports are held only inside the operation's transaction.");
        }

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Hold,
                new { subject = subject.Value },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<DateTimeOffset>> SinceAsync(
        SubjectId subject,
        DateTimeOffset since,
        CancellationToken cancellationToken) =>
        await context.PrivacyExports
            .Where(export => export.Subject == subject && export.AssembledAt > since)
            .OrderBy(export => export.AssembledAt)
            .Select(export => export.AssembledAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask RecordAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await context.PrivacyExports
            .AddAsync(
                new ExportRecordRow { Id = Guid.CreateVersion7(at), Subject = subject, AssembledAt = at },
                cancellationToken)
            .ConfigureAwait(false);
}
