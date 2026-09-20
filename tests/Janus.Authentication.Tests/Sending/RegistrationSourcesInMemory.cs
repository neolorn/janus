using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// Where registration sessions are counted per source.
/// </summary>
internal sealed class RegistrationSourcesInMemory : IRegistrationSources
{
    private readonly List<(string Source, DateTimeOffset At)> _started = [];

    /// <summary>
    /// Records sessions from one source without running a registration.
    /// </summary>
    /// <param name="source">The address.</param>
    /// <param name="at">When each started.</param>
    public void Given(string source, params DateTimeOffset[] at) =>
        _started.AddRange(at.Select(one => (source, one)));

    /// <inheritdoc/>
    public ValueTask RecordAsync(string source, DateTimeOffset at, CancellationToken cancellationToken)
    {
        _started.Add((source, at));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SinceAsync(
        string source,
        DateTimeOffset from,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_started.Count(started =>
            string.Equals(started.Source, source, StringComparison.Ordinal) && started.At >= from));
}
