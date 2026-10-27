using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Testing;

/// <summary>
/// A location file holding only its first line, dated the day it is read, and no range,
/// so a development deployment raises no degradation for its location database and
/// shows no location.
/// </summary>
/// <param name="time">What gives the day the file is read.</param>
/// <remarks>Implements LIB-TEST-003 and INT-GEN-006.</remarks>
public sealed class EmptyLocationSource(TimeProvider time) : ILocationSource
{
    /// <inheritdoc/>
    public ValueTask<Result<Stream>> OpenAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(DatedFile.Of(time)));
}
