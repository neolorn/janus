using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Privacy.Erasures;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Profiles;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Erasures;

/// <summary>
/// The erasure, over every table that holds a personal field of one subject.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements PRIV-RIGHT-005, PRIV-RIGHT-005a, PRIV-RIGHT-005c, IDN-LIFE-003b,
/// IDN-LIFE-014, IDN-ACCT-002 and IDN-PRIN-003. Every write here is made on one
/// context and committed by the caller's unit of work, so the four writes and the
/// erasures row reach the database together or not at all. No row is removed but the
/// photo, which PRIV-RIGHT-005 AC4 names, and which is a current likeness rather than
/// a record of something that happened.
/// </remarks>
internal sealed class SubjectEraser(JanusDbContext context) : ISubjectEraser
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

        await MarkErasedAsync(subject, cancellationToken).ConfigureAwait(false);
        await DestroyKeyAsync(subject, cancellationToken).ConfigureAwait(false);
        await NeutraliseFingerprintsAsync(subject, cancellationToken).ConfigureAwait(false);
        await RemovePhotoAsync(subject, cancellationToken).ConfigureAwait(false);

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
            record.DeletingSince);

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
    }

    private async ValueTask RemovePhotoAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        ProfilePhotoRecord? photo = await context.ProfilePhotos
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false);

        if (photo is not null)
        {
            context.ProfilePhotos.Remove(photo);
        }
    }
}
