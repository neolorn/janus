using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Records;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Records;

/// <summary>
/// The three supplied fields of the records of processing, over the
/// <c>compliance_records</c> table.
/// </summary>
/// <param name="context">The context the operation's reads and writes run on.</param>
/// <param name="time">The clock the row is stamped with.</param>
/// <remarks>Implements PRIV-ROPA-001 and CONV-DESIGN-003.</remarks>
internal sealed class ComplianceStore(StoreContext context, TimeProvider time) : IComplianceStore
{
    private static readonly ComplianceRecord Nothing = new(null, null, []);

    /// <inheritdoc/>
    public async ValueTask<ComplianceRecord> ReadAsync(CancellationToken cancellationToken)
    {
        ComplianceRow? row = await context.ComplianceRecords
            .FirstOrDefaultAsync(record => record.Id == ComplianceRow.Only, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Nothing
            : new ComplianceRecord(row.DataOwner, row.OrganisationalMeasures, Links(row));
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(ComplianceRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        ComplianceRow? row = await context.ComplianceRecords
            .FirstOrDefaultAsync(held => held.Id == ComplianceRow.Only, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new ComplianceRow();
            _ = await context.ComplianceRecords.AddAsync(row, cancellationToken).ConfigureAwait(false);
        }

        row.DataOwner = record.DataOwner;
        row.OrganisationalMeasures = record.OrganisationalSecurityMeasures;
        row.AssessmentLinks = JsonSerializer.Serialize(
            record.AssessmentLinks,
            ComplianceDocument.Default.IReadOnlyListString);
        row.UpdatedAt = time.GetUtcNow();
    }

    private static IReadOnlyList<string> Links(ComplianceRow row) =>
        JsonSerializer.Deserialize(row.AssessmentLinks, ComplianceDocument.Default.IReadOnlyListString)
            ?? [];
}
