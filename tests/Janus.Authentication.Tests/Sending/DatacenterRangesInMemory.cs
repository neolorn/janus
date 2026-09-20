using System;
using System.Collections.Generic;
using Janus.Authentication.Sending;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The bundled datacenter ranges, answering for whatever addresses a test named.
/// </summary>
internal sealed class DatacenterRangesInMemory : IDatacenterRanges
{
    /// <summary>
    /// The addresses the file covers.
    /// </summary>
    public HashSet<string> Inside { get; } = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public bool Contains(string source) => Inside.Contains(source);
}
