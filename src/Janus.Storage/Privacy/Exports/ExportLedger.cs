using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Exports;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Exports;

/// <summary>
/// The exports an account has taken, over the <c>privacy_exports</c> table.
/// </summary>
/// <param name="context">The context the operation's reads and writes run on.</param>
/// <remarks>
/// Implements PRIV-RIGHT-003, D-086 and CONV-DESIGN-003. The row is added onto the
/// transaction in progress, so an export that is counted is an export the caller
/// committed.
/// </remarks>
internal sealed class ExportLedger(JanusDbContext context) : IExportLedger
{
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
