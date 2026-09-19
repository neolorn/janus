using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.SubjectKeys;

namespace Janus.Storage.Privacy.SubjectKeys;

/// <summary>
/// Subject keys, over the <c>subject_keys</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>Implements PRIV-RIGHT-005a, OPS-SEC-003 and CONV-DESIGN-003.</remarks>
internal sealed class SubjectKeyStore(JanusDbContext context) : ISubjectKeyStore
{
    /// <inheritdoc/>
    public async ValueTask<SubjectKey?> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        SubjectKeyRecord? record = await context.SubjectKeys
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : SubjectKey.Existing(
            record.Subject,
            record.FormatMarker,
            record.KeyVersion,
            record.WrappedKey);
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(SubjectKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        await context.SubjectKeys
            .AddAsync(
                new SubjectKeyRecord
                {
                    Subject = key.Subject,
                    FormatMarker = key.FormatMarker,
                    KeyVersion = key.KeyVersion,
                    WrappedKey = key.WrappedKey.ToArray(),
                },
                cancellationToken)
            .ConfigureAwait(false);
    }
}
