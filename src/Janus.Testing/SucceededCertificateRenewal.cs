using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Testing;

/// <summary>
/// A certificate renewal whose most recent attempt succeeded, so a development
/// deployment raises no degradation for its certificate.
/// </summary>
/// <remarks>Implements LIB-TEST-003 and INF-TLS-003.</remarks>
public sealed class SucceededCertificateRenewal : ICertificateRenewal
{
    /// <inheritdoc/>
    public ValueTask<Result<DateTimeOffset?>> LastFailureAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success<DateTimeOffset?>(null));
}
