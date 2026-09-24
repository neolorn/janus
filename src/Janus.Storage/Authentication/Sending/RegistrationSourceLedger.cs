using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// Where registration sessions are counted against the source that started them.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <param name="fingerprintKeys">The versions the sources are hashed under.</param>
/// <remarks>
/// Implements AUTH-ABUSE-008, OPS-SEC-003 and CONV-DESIGN-003. A start recorded under a
/// previous version of the fingerprint key still counts until the rotation retires it.
/// </remarks>
internal sealed class RegistrationSourceLedger(
    StoreContext context,
    FingerprintKeys fingerprintKeys) : IRegistrationSources
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
            Source = Fingerprint.Compute(Encoding.UTF8.GetBytes(source), fingerprintKeys),
            FingerprintVersion = fingerprintKeys.CurrentVersion,
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

        int counted = 0;

        foreach (byte[] hashed in Candidates(source))
        {
            counted += await context.RegistrationSources
                .CountAsync(
                    started => started.Source == hashed && started.At >= from,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return counted;
    }

    private IReadOnlyList<byte[]> Candidates(string source) =>
        Fingerprint.Candidates(Encoding.UTF8.GetBytes(source), fingerprintKeys);
}
