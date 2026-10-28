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
    private readonly List<(SubjectId Record, OrganizationId Organization, RecordAccessSessionId Session, bool ByReference, DateTimeOffset At)> _accessed = [];
    private readonly List<(SubjectId Acting, SubjectId Record, string Field, string? Reason, DateTimeOffset At)> _changed = [];
    private readonly List<(SubjectId Account, SubjectId Record, OrganizationId Organization, DateTimeOffset At)> _attached = [];

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

    /// <summary>
    /// Every record a record access session reached, in order.
    /// </summary>
    public IReadOnlyList<(SubjectId Record, OrganizationId Organization, RecordAccessSessionId Session, bool ByReference, DateTimeOffset At)> Accessed =>
        _accessed;

    /// <summary>
    /// Every record attached to an account, in order.
    /// </summary>
    public IReadOnlyList<(SubjectId Account, SubjectId Record, OrganizationId Organization, DateTimeOffset At)> Attached =>
        _attached;

    /// <summary>
    /// Each field of a record changed, as recorded.
    /// </summary>
    public IReadOnlyList<(SubjectId Acting, SubjectId Record, string Field, string? Reason, DateTimeOffset At)> Changed =>
        _changed;

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

    /// <inheritdoc/>
    public ValueTask AccessedAsync(
        SubjectId record,
        OrganizationId organization,
        RecordAccessSessionId session,
        bool byReference,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _accessed.Add((record, organization, session, byReference, at));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask AttachedAsync(
        SubjectId account,
        string? breakGlassReason,
        SubjectId record,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _attached.Add((account, record, organization, at));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ChangedAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        string field,
        string? reason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _changed.Add((acting, record, field, reason, at));

        return ValueTask.CompletedTask;
    }
}
