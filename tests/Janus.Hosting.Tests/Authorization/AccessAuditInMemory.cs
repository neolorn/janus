using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The refusals and exports the gate recorded, held in memory.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class AccessAuditInMemory : IAccessAudit
{
    private readonly List<DeniedAccess> _denials = [];

    private readonly List<ExportedAccess> _exports = [];

    /// <summary>
    /// Every export recorded, in the order it was.
    /// </summary>
    public IReadOnlyList<ExportedAccess> Exports => _exports;

    /// <inheritdoc/>
    public ValueTask RecordAsync(DeniedAccess denial, CancellationToken cancellationToken)
    {
        _denials.Add(denial);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(ExportedAccess export, CancellationToken cancellationToken)
    {
        _exports.Add(export);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<DeniedAccess?> FindAsync(
        AuditRecordId correlation,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_denials.FirstOrDefault(denial => denial.Correlation == correlation));

    /// <inheritdoc/>
    public ValueTask<int> CountAsync(
        SubjectId? acting,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_denials.Count(denial =>
            denial.Acting == acting && denial.At >= from && denial.At < until));
}
