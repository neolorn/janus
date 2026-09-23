using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Accounts;

/// <summary>
/// The links the lifecycle notices carried, over the <c>lifecycle_links</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements IDN-LIFE-013, IDN-LIFE-014 and CONV-DESIGN-003. Issuing one replaces
/// whatever the account had outstanding, so an older notice is never a second way
/// back.
/// </remarks>
internal sealed class LifecycleLinkStore(StoreContext context) : ILifecycleLinkStore
{
    /// <inheritdoc/>
    public async ValueTask<LifecycleLink?> FindAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        return await context.LifecycleLinks
                .FindAsync([fingerprint], cancellationToken)
                .ConfigureAwait(false)
            is LifecycleLinkRecord record
            ? LifecycleLink.Existing(record.Subject, record.Kind, record.Token, record.IssuedAt)
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask ReplaceAsync(LifecycleLink link, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);

        await RemoveAsync(link.Subject, cancellationToken).ConfigureAwait(false);

        await context.LifecycleLinks
            .AddAsync(
                new LifecycleLinkRecord
                {
                    Token = link.Token,
                    Subject = link.Subject,
                    Kind = link.Kind,
                    IssuedAt = link.IssuedAt,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        LifecycleLinkRecord? standing = await context.LifecycleLinks
            .SingleOrDefaultAsync(link => link.Subject == subject, cancellationToken)
            .ConfigureAwait(false);

        if (standing is not null)
        {
            context.LifecycleLinks.Remove(standing);
        }
    }
}
