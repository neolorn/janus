using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Identity.Accounts;
using Janus.Privacy.Erasures;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Mailboxes;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Erasures;

/// <summary>
/// The erasure, over every table that holds a personal field of one subject.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="sessions">Where the subject's sessions are held.</param>
/// <param name="configuration">Where the username hold's length is read.</param>
/// <remarks>
/// Implements PRIV-RIGHT-005, PRIV-RIGHT-005a, PRIV-RIGHT-005c, IDN-LIFE-003b,
/// IDN-LIFE-014, IDN-ACCT-002 and IDN-PRIN-003. Every write here is made on one
/// context and committed by the caller's unit of work, so the three writes and the
/// erasures row reach the database together or not at all. No row is removed: the
/// photo's bytes are held under the same key as every other personal field, so
/// destroying it leaves them unreadable where they are (IDN-ATTR-003, IDN-PRIN-003).
/// The sessions go first (AUTH-SESS-010): a request arriving on one of them after the
/// key is gone would read fields it can no longer decrypt.
/// </remarks>
internal sealed class SubjectEraser(
    StoreContext context,
    ISessionStore sessions,
    IConfigurationStore configuration) : ISubjectEraser
{
    /// <inheritdoc/>
    public async ValueTask<Erasure> EraseAsync(
        SubjectId subject,
        ErasureReason reason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (await context.Erasures.FindAsync([subject], cancellationToken).ConfigureAwait(false)
            is not null)
        {
            throw new InvalidOperationException("The subject has already been erased.");
        }

        await sessions.EndAccountAsync(subject, at, cancellationToken).ConfigureAwait(false);
        await MarkErasedAsync(subject, cancellationToken).ConfigureAwait(false);
        await DestroyKeyAsync(subject, cancellationToken).ConfigureAwait(false);
        await HoldUsernameAsync(subject, at, cancellationToken).ConfigureAwait(false);
        await NeutraliseFingerprintsAsync(subject, cancellationToken).ConfigureAwait(false);

        var erasure = Erasure.Begun(subject, at, reason);

        await context.Erasures
            .AddAsync(
                new ErasureRecord
                {
                    Subject = erasure.Subject,
                    RequestedAt = erasure.RequestedAt,
                    Reason = erasure.Reason,
                    Status = erasure.Status,
                    Attempts = erasure.Attempts,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return erasure;
    }

    private async ValueTask MarkErasedAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        AccountRecord record = await context.Accounts
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no account to erase.");

        var account = Account.Existing(
            record.Subject,
            record.CreatedAt,
            record.State,
            record.SuspendedBy,
            record.DeletingBy,
            record.DeletingSince,
            registration: null);

        account.MarkErased();

        record.State = account.State;
    }

    private async ValueTask DestroyKeyAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        SubjectKeyRecord record = await context.SubjectKeys
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key to destroy.");

        var key = SubjectKey.Existing(
            record.Subject,
            record.FormatMarker,
            record.KeyVersion,
            record.WrappedKey);

        key.Erase();

        record.FormatMarker = key.FormatMarker;
        record.KeyVersion = key.KeyVersion;
        record.WrappedKey = key.WrappedKey.ToArray();
    }

    // REG-IDENT-009: the name stays out of reach for the evidential period, and the
    // fingerprint is the only thing left that knows it, so the hold is taken before
    // the fingerprints are neutralised.
    private async ValueTask HoldUsernameAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        IdentifierRecord? username = await context.Identifiers
            .Where(identifier =>
                identifier.Subject == subject && identifier.Kind == IdentifierKind.Username)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (username is null)
        {
            return;
        }

        TimeSpan held = (await configuration
                .ReadAsync(Janus.Core.Configuration.Settings.RetentionConsent, cancellationToken)
                .ConfigureAwait(false))
            .Match(
                read => read,
                _ => throw new InvalidOperationException("The username hold has no length to run for."));

        UsernameHoldRecord? standing = await context.UsernameHolds
            .FindAsync([username.Fingerprint], cancellationToken)
            .ConfigureAwait(false);

        // A name an earlier account gave up and this one took is held once, for
        // whichever period ends later.
        if (standing is not null)
        {
            standing.HeldFrom = at;
            standing.ReleasesAt = standing.ReleasesAt > at + held ? standing.ReleasesAt : at + held;

            return;
        }

        await context.UsernameHolds
            .AddAsync(
                new UsernameHoldRecord
                {
                    Fingerprint = username.Fingerprint,
                    HeldFrom = at,
                    ReleasesAt = at + held,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask NeutraliseFingerprintsAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        List<IdentifierRecord> identifiers = await context.Identifiers
            .Where(identifier => identifier.Subject == subject)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (IdentifierRecord identifier in identifiers)
        {
            identifier.Fingerprint = Fingerprint.Neutralised();
        }

        // PRIV-RIGHT-005c: the address of a mailbox the subject holds or last held is
        // theirs as well, and its fingerprint goes with the rest.
        List<MailboxRecord> mailboxes = await context.Mailboxes
            .Where(mailbox => mailbox.Holder == subject)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (MailboxRecord mailbox in mailboxes)
        {
            mailbox.Fingerprint = Fingerprint.Neutralised();
        }
    }
}
