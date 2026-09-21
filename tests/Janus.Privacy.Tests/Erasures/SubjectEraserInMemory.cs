using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Erasures;

namespace Janus.Privacy.Tests.Erasures;

/// <summary>
/// The erasure itself, recorded rather than carried: the rows a real one would leave
/// belong to the storage area, so what a test of the sweep can read back is that it
/// was asked, for whom, and why.
/// </summary>
internal sealed class SubjectEraserInMemory : ISubjectEraser
{
    private readonly List<Erasure> _erased = [];

    /// <summary>
    /// Every subject erased, in order.
    /// </summary>
    public IReadOnlyList<Erasure> Erased => _erased;

    /// <inheritdoc/>
    public ValueTask<Erasure> EraseAsync(
        SubjectId subject,
        ErasureReason reason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (_erased.Exists(erasure => erasure.Subject == subject))
        {
            throw new InvalidOperationException("The subject has already been erased.");
        }

        var erasure = Erasure.Begun(subject, at, reason);

        _erased.Add(erasure);

        return ValueTask.FromResult(erasure);
    }
}
