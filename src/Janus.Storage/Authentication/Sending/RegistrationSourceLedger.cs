using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// Where registration sessions are counted against the source that started them.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <param name="fingerprintKey">What the sources are hashed under.</param>
/// <remarks>Implements AUTH-ABUSE-008 and CONV-DESIGN-003.</remarks>
internal sealed class RegistrationSourceLedger(
    StoreContext context,
    ReadOnlyMemory<byte> fingerprintKey) : IRegistrationSources
{
    private static readonly TimeSpan Kept = TimeSpan.FromHours(1);

    /// <inheritdoc/>
    public async ValueTask RecordAsync(
        string source,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        DateTimeOffset oldest = at - Kept;

        await context.RegistrationSources
            .Where(started => started.At < oldest)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        context.RegistrationSources.Add(new RegistrationSourceRecord
        {
            Id = Guid.CreateVersion7(at),
            Source = Hashed(source),
            At = at,
        });
    }

    /// <inheritdoc/>
    public async ValueTask<int> SinceAsync(
        string source,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        byte[] hashed = Hashed(source);

        return await context.RegistrationSources
            .CountAsync(
                started => started.Source == hashed && started.At >= from,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private byte[] Hashed(string source) =>
        Fingerprint.Compute(Encoding.UTF8.GetBytes(source), fingerprintKey.Span);
}
