using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The datacenter ranges of a deployment, answering for whatever addresses a test
/// named, or refusing where a test stands in for a degradation that could not be
/// raised.
/// </summary>
internal sealed class DatacenterRangesInMemory : IDatacenterRanges
{
    /// <summary>
    /// The addresses the file covers.
    /// </summary>
    public HashSet<string> Inside { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Every address the ranges were asked about, in order.
    /// </summary>
    public List<string> Asked { get; } = [];

    /// <summary>
    /// What an ask answers with instead of whether the address is covered.
    /// </summary>
    public Error? Refusal { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<bool>> ContainsAsync(string ipAddress, CancellationToken cancellationToken)
    {
        Asked.Add(ipAddress);

        return ValueTask.FromResult(Refusal is Error refused
            ? Result.Failure<bool>(refused)
            : Result.Success(Inside.Contains(ipAddress)));
    }
}
