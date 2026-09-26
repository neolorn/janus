using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests.Alerting;

/// <summary>
/// What the environment measured of this host's clock and how its last certificate
/// renewal went, as a test sets them.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class EnvironmentInMemory : IClockReference, ICertificateRenewal
{
    private Result<TimeSpan> _offset = Result.Success(TimeSpan.Zero);

    private Result<DateTimeOffset?> _lastFailure = Result.Success<DateTimeOffset?>(null);

    /// <summary>
    /// Sets the offset the environment last measured.
    /// </summary>
    /// <param name="offset">The offset, positive where the host is ahead.</param>
    public void Measured(TimeSpan offset) => _offset = Result.Success(offset);

    /// <summary>
    /// Leaves the environment holding no measurement of the clock.
    /// </summary>
    public void Unmeasured() =>
        _offset = Result.Failure<TimeSpan>(Error.From(ErrorCodes.RequestMalformed));

    /// <summary>
    /// Sets how the most recent renewal went.
    /// </summary>
    /// <param name="failedAt">When it failed, or nothing where it succeeded.</param>
    public void Renewed(DateTimeOffset? failedAt) => _lastFailure = Result.Success(failedAt);

    /// <summary>
    /// Leaves how the most recent renewal went unreadable.
    /// </summary>
    public void RenewalUnread() =>
        _lastFailure = Result.Failure<DateTimeOffset?>(Error.From(ErrorCodes.RequestMalformed));

    /// <inheritdoc/>
    public ValueTask<Result<TimeSpan>> OffsetAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(_offset);

    /// <inheritdoc/>
    public ValueTask<Result<DateTimeOffset?>> LastFailureAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(_lastFailure);
}
