using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.SignIn;
using Janus.Core;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.SignIn;

/// <summary>
/// The sign-in links and codes that have gone out, over the <c>signin_links</c>
/// table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions a subject key may be wrapped under.</param>
/// <param name="randomness">The randomness each initialisation vector is drawn from.</param>
/// <remarks>
/// Implements AUTH-FACT-003, PRIV-RIGHT-005a and CONV-DESIGN-003. Asking again
/// replaces what the account had outstanding of that kind, which is what stops an
/// older message being a second way in. The code is held rather than fingerprinted,
/// because a link opened away from the asking browser has to show it (REG-SESS-003),
/// so it is held under the account's own key and an erasure leaves it unreadable.
/// </remarks>
internal sealed class PendingSignInStore(
    StoreContext context,
    KeyEncryptionKeys keyEncryptionKeys,
    RandomNumberGenerator randomness) : IPendingSignInStore
{
    /// <inheritdoc/>
    public async ValueTask<PendingSignIn?> FindAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        return await ReadAsync(
                await context.SignInLinks
                    .FindAsync([fingerprint], cancellationToken)
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<PendingSignIn?> FindAsync(
        SubjectId subject,
        Factor factor,
        CancellationToken cancellationToken) =>
        await ReadAsync(
                await context.SignInLinks
                    .SingleOrDefaultAsync(
                        pending => pending.Subject == subject && pending.Factor == factor,
                        cancellationToken)
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask ReplaceAsync(PendingSignIn pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        PendingSignInRecord? standing = await context.SignInLinks
            .SingleOrDefaultAsync(
                held => held.Subject == pending.Subject && held.Factor == pending.Factor,
                cancellationToken)
            .ConfigureAwait(false);

        if (standing is not null)
        {
            context.SignInLinks.Remove(standing);
        }

        await context.SignInLinks
            .AddAsync(
                new PendingSignInRecord
                {
                    Token = pending.Fingerprint,
                    Subject = pending.Subject,
                    Factor = pending.Factor,
                    Email = pending.Email,
                    Code = await HeldAsync(pending, cancellationToken).ConfigureAwait(false),
                    Browser = pending.Browser,
                    IssuedAt = pending.IssuedAt,
                    ExpiresAt = pending.ExpiresAt,
                    WrongAttempts = pending.WrongAttempts,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(PendingSignIn pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        PendingSignInRecord record = await context.SignInLinks
            .FindAsync([pending.Fingerprint], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The pending sign-in has no row to carry the change.");

        record.WrongAttempts = pending.WrongAttempts;
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(byte[] fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        PendingSignInRecord? record = await context.SignInLinks
            .FindAsync([fingerprint], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.SignInLinks.Remove(record);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.SignInLinks
            .Where(pending => pending.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private static PersonalFieldLocation Located(SubjectId subject) =>
        new(subject, PendingSignInConfiguration.Table, PendingSignInConfiguration.CodeColumn);

    private async ValueTask<byte[]> HeldAsync(
        PendingSignIn pending,
        CancellationToken cancellationToken)
    {
        byte[] dataKey = await DataKeyAsync(pending.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            return PersonalFieldCipher.Encrypt(
                dataKey,
                Located(pending.Subject),
                pending.Code,
                randomness);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private async ValueTask<PendingSignIn?> ReadAsync(
        PendingSignInRecord? record,
        CancellationToken cancellationToken)
    {
        if (record is null)
        {
            return null;
        }

        byte[] dataKey = await DataKeyAsync(record.Subject, cancellationToken).ConfigureAwait(false);
        byte[] code;

        try
        {
            code = PersonalFieldCipher.Decrypt(dataKey, Located(record.Subject), record.Code);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        return PendingSignIn.Existing(
            record.Token,
            record.Subject,
            record.Factor,
            record.Email,
            code,
            record.Browser,
            record.IssuedAt,
            record.ExpiresAt,
            record.WrongAttempts);
    }

    private async ValueTask<byte[]> DataKeyAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        SubjectKeyRecord key = await context.SubjectKeys
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key to hold a code under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, keyEncryptionKeys);
    }
}
