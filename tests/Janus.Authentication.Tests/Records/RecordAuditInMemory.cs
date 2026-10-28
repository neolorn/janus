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
    private readonly List<(SubjectId Acting, SubjectId Record, OrganizationId Organization, bool AgeAnswered, DateTimeOffset At)> _entered = [];

    /// <summary>
    /// Every collection recorded, in order.
    /// </summary>
    public IReadOnlyList<(SubjectId Record, OrganizationId Organization, bool Created, DateTimeOffset At)> Collected =>
        _collected;

    /// <summary>
    /// Every entry recorded, in order.
    /// </summary>
    public IReadOnlyList<(SubjectId Acting, SubjectId Record, OrganizationId Organization, bool AgeAnswered, DateTimeOffset At)> Entered =>
        _entered;

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

    /// <inheritdoc/>
    public ValueTask EnteredAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        OrganizationId organization,
        bool ageAnswered,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _entered.Add((acting, record, organization, ageAnswered, at));

        return ValueTask.CompletedTask;
    }
}
