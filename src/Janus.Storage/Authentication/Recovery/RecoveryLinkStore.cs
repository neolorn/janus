using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Recovery;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Recovery;

/// <summary>
/// The links recovery has sent, over the <c>recovery_links</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements AUTH-RECOV-002, AUTH-RECOV-005 and CONV-DESIGN-003. Asking again
/// replaces what the account had outstanding of that purpose, which is what stops an
/// older message being a second way in. Nothing here holds a personal attribute: the
/// row is a fingerprint, two identifiers and a pair of instants.
/// </remarks>
internal sealed class RecoveryLinkStore(StoreContext context) : IRecoveryLinkStore
{
    /// <inheritdoc/>
    public async ValueTask<RecoveryLink?> FindAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        return Read(
            await context.RecoveryLinks
                .FindAsync([fingerprint], cancellationToken)
                .ConfigureAwait(false));
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<RecoveryLink?> FindForUpdateAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A recovery link's row is held only inside the operation's transaction.");
        }

        bool tracked = context.RecoveryLinks.Local.Any(record => CryptographicOperations.FixedTimeEquals(record.Token, fingerprint));

        RecoveryLinkRecord? held = (await context.RecoveryLinks
                .FromSql($"SELECT * FROM identity.recovery_links WHERE token = {fingerprint} FOR UPDATE")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();

        // A row the context already tracks was read before the lock, so it is read again:
        // what the decision is made on is the row as it stood when the lock was taken.
        if (held is not null && tracked)
        {
            await context.Entry(held).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        return Read(held);
    }

    /// <inheritdoc/>
    public async ValueTask<RecoveryLink?> FindAsync(
        EnrolmentSessionId session,
        CancellationToken cancellationToken) =>
        Read(
            await context.RecoveryLinks
                .SingleOrDefaultAsync(link => link.Session == session, cancellationToken)
                .ConfigureAwait(false));

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<RecoveryLink?> FindForUpdateAsync(
        EnrolmentSessionId session,
        CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A recovery link's row is held only inside the operation's transaction.");
        }

        bool tracked = context.RecoveryLinks.Local.Any(record => record.Session == session);

        RecoveryLinkRecord? held = (await context.RecoveryLinks
                .FromSql($"SELECT * FROM identity.recovery_links WHERE session = {session.Value} FOR UPDATE")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();

        // As for a token: a tracked row was read before the lock, so it is read again.
        if (held is not null && tracked)
        {
            await context.Entry(held).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        return Read(held);
    }

    /// <inheritdoc/>
    public async ValueTask ReplaceAsync(RecoveryLink link, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);

        RecoveryLinkRecord? standing = await context.RecoveryLinks
            .SingleOrDefaultAsync(
                held => held.Subject == link.Subject
                    && held.Purpose == link.Purpose
                    && held.SpentAt == null,
                cancellationToken)
            .ConfigureAwait(false);

        if (standing is not null)
        {
            context.RecoveryLinks.Remove(standing);
        }

        await context.RecoveryLinks
            .AddAsync(
                new RecoveryLinkRecord
                {
                    Token = link.Fingerprint,
                    Subject = link.Subject,
                    Purpose = link.Purpose,
                    IssuedAt = link.IssuedAt,
                    ExpiresAt = link.ExpiresAt,
                    Approver = link.Approver,
                    MailboxLost = link.MailboxLost,
                    Session = link.Session,
                    SpentAt = link.SpentAt,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(RecoveryLink link, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);

        RecoveryLinkRecord record = await context.RecoveryLinks
            .FindAsync([link.Fingerprint], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The recovery link has no row to carry the change.");

        record.Session = link.Session;
        record.SpentAt = link.SpentAt;
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(byte[] fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        RecoveryLinkRecord? record = await context.RecoveryLinks
            .FindAsync([fingerprint], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.RecoveryLinks.Remove(record);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.RecoveryLinks
            .Where(link => link.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private static RecoveryLink? Read(RecoveryLinkRecord? record) =>
        record is null
            ? null
            : RecoveryLink.Existing(
                record.Token,
                record.Subject,
                record.Purpose,
                record.IssuedAt,
                record.ExpiresAt,
                record.Approver,
                record.MailboxLost,
                record.Session,
                record.SpentAt);
}
