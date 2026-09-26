using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.BreakGlass;

namespace Janus.Authentication.Tests.BreakGlass;

/// <summary>
/// The issues of the break-glass credential and the attempts at it, held as rows are:
/// what a read answers is a copy, so a change reaches the store only when it is
/// recorded.
/// </summary>
internal sealed class BreakGlassStoreInMemory : IBreakGlassStore
{
    private readonly List<BreakGlassCredential> _issues = [];

    /// <summary>
    /// When each attempt arrived, in order.
    /// </summary>
    public List<DateTimeOffset> Attempts { get; } = [];

    /// <summary>
    /// Every issue as it now stands, in the order generated.
    /// </summary>
    public IReadOnlyList<BreakGlassCredential> Issues => [.. _issues.Select(Copy)];

    /// <summary>
    /// How many times an issue was read, which is what any comparison with a hash
    /// begins with.
    /// </summary>
    public int Reads { get; private set; }

    /// <inheritdoc/>
    public ValueTask HoldAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask<BreakGlassCredential?> StandingAsync(CancellationToken cancellationToken)
    {
        Reads++;

        return ValueTask.FromResult(_issues.LastOrDefault(issue => issue.Stands) is { } standing ? Copy(standing) : null);
    }

    /// <inheritdoc/>
    public ValueTask<BreakGlassCredential?> LastConsumedAsync(CancellationToken cancellationToken)
    {
        Reads++;

        return ValueTask.FromResult(
            _issues.Where(issue => issue.ConsumedAt is not null).MaxBy(issue => issue.ConsumedAt) is { } spent
                ? Copy(spent)
                : null);
    }

    /// <inheritdoc/>
    public ValueTask AddAsync(BreakGlassCredential credential, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);

        _issues.Add(Copy(credential));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<bool> RecordAsync(BreakGlassCredential credential, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);

        int held = _issues.FindIndex(issue => issue.Id == credential.Id && issue.Stands);

        if (held < 0)
        {
            return ValueTask.FromResult(false);
        }

        _issues[held] = Copy(credential);

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<int> AttemptedAsync(DateTimeOffset at, DateTimeOffset since, CancellationToken cancellationToken)
    {
        Attempts.Add(at);

        return ValueTask.FromResult(Attempts.Count(attempted => attempted > since));
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset before, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Attempts.RemoveAll(attempted => attempted < before));

    private static BreakGlassCredential Copy(BreakGlassCredential issue) =>
        BreakGlassCredential.Held(
            issue.Id,
            issue.Hash,
            issue.IssuedBy,
            issue.IssuedAt,
            issue.ConsumedAt,
            issue.ReplacedAt);
}
