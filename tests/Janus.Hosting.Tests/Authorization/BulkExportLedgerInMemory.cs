using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// When each actor's recent exports were admitted, held in memory.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class BulkExportLedgerInMemory : IBulkExportLedger
{
    private readonly List<(SubjectId? Actor, string? Principal, DateTimeOffset At)> _admitted = [];

    /// <summary>
    /// Every admission held, oldest first.
    /// </summary>
    public IReadOnlyList<(SubjectId? Actor, string? Principal, DateTimeOffset At)> Admitted => _admitted;

    /// <summary>
    /// Gets or sets what another transaction commits while this one waits for the
    /// actor's exports, so a test may admit one under a count about to be made.
    /// </summary>
    public Action? Holding { get; set; }

    /// <inheritdoc/>
    public ValueTask HoldAsync(SubjectId? actor, string? principal, CancellationToken cancellationToken)
    {
        Holding?.Invoke();

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<DateTimeOffset>> SinceAsync(
        SubjectId? actor,
        string? principal,
        DateTimeOffset since,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<DateTimeOffset>>(
        [
            .. Of(actor, principal).Where(at => at > since),
        ]);

    /// <inheritdoc/>
    public ValueTask RecordAsync(
        SubjectId? actor,
        string? principal,
        DateTimeOffset at,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        _ = _admitted.RemoveAll(one => one.Actor == actor
            && string.Equals(one.Principal, principal, StringComparison.Ordinal)
            && one.At <= since);

        _admitted.Add((actor, principal, at));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_admitted.RemoveAll(one => one.At <= since));

    private IEnumerable<DateTimeOffset> Of(SubjectId? actor, string? principal) =>
        _admitted
            .Where(one => one.Actor == actor && string.Equals(one.Principal, principal, StringComparison.Ordinal))
            .Select(one => one.At)
            .Order();
}
