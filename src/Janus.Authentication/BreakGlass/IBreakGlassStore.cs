using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.BreakGlass;

/// <summary>
/// Where the issues of the break-glass credential and the attempts at it are kept.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-002 and OPS-BOOT-004. Every operation joins the transaction the
/// caller holds.
/// </remarks>
internal interface IBreakGlassStore
{
    /// <summary>
    /// Holds the credential against a concurrent use or generation until the
    /// transaction ends, so what stands is read by one operation at a time.
    /// </summary>
    /// <param name="cancellationToken">Abandons the wait.</param>
    /// <returns>The work of holding it.</returns>
    ValueTask HoldAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The issue that stands: neither used nor replaced.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The issue, or nothing where none stands.</returns>
    ValueTask<BreakGlassCredential?> StandingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The issue most recently spent.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The issue, or nothing where none has been.</returns>
    ValueTask<BreakGlassCredential?> LastConsumedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Keeps a newly generated issue.
    /// </summary>
    /// <param name="credential">The issue.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of keeping it.</returns>
    ValueTask AddAsync(BreakGlassCredential credential, CancellationToken cancellationToken);

    /// <summary>
    /// Records that an issue was spent or replaced, where it still stood when the
    /// write reached it.
    /// </summary>
    /// <param name="credential">The issue as it now stands.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>
    /// Whether it was recorded, and not where a concurrent use or replacement reached
    /// the issue first.
    /// </returns>
    ValueTask<bool> RecordAsync(BreakGlassCredential credential, CancellationToken cancellationToken);

    /// <summary>
    /// Counts one attempt at the credential, from any source, and answers how many
    /// were counted since an instant, this one included. Attempts are counted one at
    /// a time, so two arriving together are never both answered as the fifth.
    /// </summary>
    /// <param name="at">When the attempt arrived.</param>
    /// <param name="since">The start of the window counted.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>How many attempts the window holds.</returns>
    ValueTask<int> AttemptedAsync(DateTimeOffset at, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets the attempts older than an instant, which no window reaches any more.
    /// </summary>
    /// <param name="before">The instant.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>How many were forgotten.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset before, CancellationToken cancellationToken);
}
