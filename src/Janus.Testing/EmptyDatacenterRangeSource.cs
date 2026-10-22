using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Testing;

/// <summary>
/// A datacenter range file holding only its first line, dated the day it is read, and no
/// range, so a development deployment raises no degradation for it and counts no source
/// as a datacenter.
/// </summary>
/// <param name="time">What gives the day the file is read.</param>
/// <remarks>Implements LIB-TEST-003 and AUTH-ABUSE-008.</remarks>
public sealed class EmptyDatacenterRangeSource(TimeProvider time) : IDatacenterRangeSource
{
    /// <inheritdoc/>
    public ValueTask<Result<Stream>> OpenAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(DatedFile.Of(time)));
}
