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

    private readonly List<string> _asked = [];

    /// <summary>
    /// Every export recorded, in the order it was.
    /// </summary>
    public IReadOnlyList<ExportedAccess> Exports => _exports;

    /// <summary>
    /// What was asked of the refusals, in the order it was asked (held, recorded,
    /// counted), each with whether the unit of work a test watches was open then.
    /// </summary>
    public IReadOnlyList<string> Asked => _asked;

    /// <summary>
    /// Whether the unit of work a test watches is open, where a test names one.
    /// </summary>
    public Func<bool> Opened { get; set; } = () => false;

    /// <inheritdoc/>
    public ValueTask HoldAsync(SubjectId acting, string? principal, CancellationToken cancellationToken)
    {
        _asked.Add("held:" + Opened());

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(DeniedAccess denial, CancellationToken cancellationToken)
    {
        _asked.Add("recorded:" + Opened());
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
        SubjectId acting,
        string? principal,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken)
    {
        _asked.Add("counted:" + Opened());

        return ValueTask.FromResult(_denials.Count(denial =>
            (denial.Acting ?? default) == acting
            && denial.Principal == principal
            && denial.At >= from
            && denial.At < until));
    }
}
