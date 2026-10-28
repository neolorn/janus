using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;

namespace Janus.Authentication.Tests.Records;

/// <summary>
/// What was recorded of what happened to person records, held in memory.
/// </summary>
internal sealed class RecordAuditInMemory : IRecordAudit
{
    private readonly List<(SubjectId Record, OrganizationId Organization, bool Created, DateTimeOffset At)> _collected = [];

    /// <summary>
    /// Every collection recorded, in order.
    /// </summary>
    public IReadOnlyList<(SubjectId Record, OrganizationId Organization, bool Created, DateTimeOffset At)> Collected =>
        _collected;

    /// <inheritdoc/>
    public ValueTask CollectedAsync(
        SubjectId record,
        OrganizationId organization,
        bool created,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _collected.Add((record, organization, created, at));

        return ValueTask.CompletedTask;
    }
}
