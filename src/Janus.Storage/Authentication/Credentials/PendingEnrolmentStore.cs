using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Credentials;

/// <summary>
/// The enrolments a texted code has yet to confirm, over the
/// <c>pending_enrolments</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="ring">The key ring the keys are borrowed from at each use.</param>
/// <param name="randomness">The randomness each initialisation vector is drawn from.</param>
/// <remarks>
/// Implements AUTH-FACT-002, AUTH-FACT-004, PRIV-RIGHT-005a and CONV-DESIGN-003.
/// Staging again replaces what the account had pending of that entry, which is what
/// stops an older code confirming anything. The number and the code are held under the
/// account's own key, so an erasure leaves both unreadable.
/// </remarks>
internal sealed class PendingEnrolmentStore(
    StoreContext context,
    IKeyRing ring,
    RandomNumberGenerator randomness) : IPendingEnrolmentStore
{
    /// <inheritdoc/>
    public async ValueTask<PendingEnrolment?> FindAsync(
        SubjectId subject,
        Factor factor,
        CancellationToken cancellationToken) =>
        await ReadAsync(
                await context.PendingEnrolments
                    .FindAsync([subject, factor], cancellationToken)
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<PendingEnrolment?> FindForUpdateAsync(
        SubjectId subject,
        Factor factor,
        CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A pending enrolment is held only inside the operation's transaction.");
        }

        string entry = VocabularyConverter<Factor>.Write(factor);

        PendingEnrolmentRecord? tracked = context.PendingEnrolments.Local
            .FirstOrDefault(record => record.Subject == subject && record.Factor == factor);

        PendingEnrolmentRecord? held = (await context.PendingEnrolments
                .FromSql(
                    $"SELECT * FROM identity.pending_enrolments WHERE subject = {subject.Value} AND factor = {entry} FOR UPDATE")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();

        // A row the context already tracks was read before the lock, so it is read
        // again; one another transaction removed meanwhile leaves the context.
        if (tracked is not null && context.Entry(tracked).State is EntityState.Unchanged)
        {
            await context.Entry(tracked).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        return await ReadAsync(held, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ReplaceAsync(PendingEnrolment pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        PendingEnrolmentRecord? standing = await context.PendingEnrolments
            .FindAsync([pending.Subject, pending.Factor], cancellationToken)
            .ConfigureAwait(false);

        byte[] dataKey = await DataKeyAsync(pending.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            // The key of the row is the account and the entry, so what stands is
            // written over and nothing of the enrolment it held is left.
            PendingEnrolmentRecord record = standing ?? new PendingEnrolmentRecord
            {
                Subject = pending.Subject,
                Factor = pending.Factor,
            };

            record.Label = pending.Label.Value;
            record.Number = PersonalFieldCipher.Encrypt(
                dataKey,
                Located(pending.Subject, PendingEnrolmentConfiguration.NumberColumn),
                Encoding.UTF8.GetBytes(pending.Number),
                randomness);
            record.Send = pending.Send;
            record.Code = PersonalFieldCipher.Encrypt(
                dataKey,
                Located(pending.Subject, PendingEnrolmentConfiguration.CodeColumn),
                pending.Code,
                randomness);
            record.IssuedAt = pending.IssuedAt;
            record.ExpiresAt = pending.ExpiresAt;
            record.WrongAttempts = pending.WrongAttempts;

            if (standing is null)
            {
                context.PendingEnrolments.Add(record);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(PendingEnrolment pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        PendingEnrolmentRecord record = await context.PendingEnrolments
            .FindAsync([pending.Subject, pending.Factor], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The pending enrolment has no row to carry the change.");

        record.WrongAttempts = pending.WrongAttempts;
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(SubjectId subject, Factor factor, CancellationToken cancellationToken)
    {
        PendingEnrolmentRecord? record = await context.PendingEnrolments
            .FindAsync([subject, factor], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.PendingEnrolments.Remove(record);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.PendingEnrolments
            .Where(pending => pending.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private static PersonalFieldLocation Located(SubjectId subject, string column) =>
        new(subject, PendingEnrolmentConfiguration.Table, column);

    // A label this library wrote is a label this library accepts, so a stored value
    // that no longer parses is a corrupted row and not a label to drop quietly.
    private static CredentialLabel Label(string stored) =>
        CredentialLabel.TryParse(stored, out CredentialLabel label)
            ? label
            : throw new InvalidOperationException("The stored label is not a label.");

    private async ValueTask<PendingEnrolment?> ReadAsync(
        PendingEnrolmentRecord? record,
        CancellationToken cancellationToken)
    {
        if (record is null)
        {
            return null;
        }

        byte[] dataKey = await DataKeyAsync(record.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            return PendingEnrolment.Existing(
                record.Subject,
                record.Factor,
                Label(record.Label),
                Encoding.UTF8.GetString(PersonalFieldCipher.Decrypt(
                    dataKey,
                    Located(record.Subject, PendingEnrolmentConfiguration.NumberColumn),
                    record.Number)),
                record.Send,
                PersonalFieldCipher.Decrypt(
                    dataKey,
                    Located(record.Subject, PendingEnrolmentConfiguration.CodeColumn),
                    record.Code),
                record.IssuedAt,
                record.ExpiresAt,
                record.WrongAttempts);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private async ValueTask<byte[]> DataKeyAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        SubjectKeyRecord key = await context.SubjectKeys
            .FindAsync([SubjectKeyId.Of(subject)], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key to hold an enrolment under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, ring);
    }
}
