using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Exports;

namespace Janus.Privacy.Tests.Exports;

/// <summary>
/// The exports an account has taken, in the order they were taken.
/// </summary>
internal sealed class ExportLedgerInMemory : IExportLedger
{
    private readonly List<(SubjectId Subject, DateTimeOffset At)> _taken = [];

    /// <summary>
    /// Every export counted, in the order it was counted.
    /// </summary>
    public IReadOnlyList<(SubjectId Subject, DateTimeOffset At)> Taken => _taken;

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<DateTimeOffset>> SinceAsync(
        SubjectId subject,
        DateTimeOffset since,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<DateTimeOffset>>(
        [
            .. _taken
                .Where(export => export.Subject == subject && export.At > since)
                .Select(export => export.At)
                .OrderBy(at => at),
        ]);

    /// <inheritdoc/>
    public ValueTask RecordAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _taken.Add((subject, at));

        return ValueTask.CompletedTask;
    }
}
