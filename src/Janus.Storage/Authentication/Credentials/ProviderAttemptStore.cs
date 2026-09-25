using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Credentials;

/// <summary>
/// The round trips to social providers browsers have in flight, over the
/// <c>provider_attempts</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions the proof key is wrapped under.</param>
/// <param name="time">The clock an attempt's identifier is drawn by.</param>
/// <remarks>
/// Implements IDN-LIFE-012, REG-IDENT-008, CONV-DESIGN-003 and OPS-SEC-001. The one
/// secret on the row is the proof key, which the server presents and the browser never
/// sees, so it is wrapped on the way in and unwrapped for the one caller that redeems a
/// code with it.
/// </remarks>
internal sealed class ProviderAttemptStore(
    StoreContext context,
    KeyEncryptionKeys keyEncryptionKeys,
    TimeProvider time) : IProviderAttemptStore
{
    /// <inheritdoc/>
    public async ValueTask BindAsync(
        ProviderBinding binding,
        ProviderAttempt attempt,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(attempt);

        // One in flight per browser: a new start takes the place of the one before.
        ProviderAttemptRecord record = await BoundAsync(binding, cancellationToken).ConfigureAwait(false)
            ?? context.ProviderAttempts.Add(new ProviderAttemptRecord
            {
                Id = Guid.CreateVersion7(time.GetUtcNow()),
                PreAuthentication = binding.PreAuthentication,
                Session = binding.Session,
            }).Entity;

        record.Provider = attempt.Provider;
        record.Intent = attempt.Intent;
        record.State = attempt.StateFingerprint;
        record.Nonce = attempt.NonceFingerprint;
        record.Verifier = null;
        record.KeyVersion = null;
        record.ReturnTo = attempt.ReturnTo;
        record.CreatedAt = at;

        if (attempt.Verifier is string verifier)
        {
            byte[] plain = Encoding.ASCII.GetBytes(verifier);

            try
            {
                record.Verifier = PersonalFieldCipher.Wrap(plain, keyEncryptionKeys.Current.Span);
                record.KeyVersion = keyEncryptionKeys.CurrentVersion;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plain);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ProviderAttempt?> TakeAsync(
        ProviderBinding binding,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binding);

        if (await BoundAsync(binding, cancellationToken).ConfigureAwait(false)
            is not ProviderAttemptRecord record)
        {
            return null;
        }

        context.ProviderAttempts.Remove(record);

        return new ProviderAttempt(
            record.Provider,
            record.Intent,
            record.State,
            record.Nonce,
            Verifier(record),
            record.ReturnTo);
    }

    private async ValueTask<ProviderAttemptRecord?> BoundAsync(
        ProviderBinding binding,
        CancellationToken cancellationToken) =>
        binding.PreAuthentication is byte[] preAuthentication
            ? await context.ProviderAttempts
                .SingleOrDefaultAsync(
                    attempt => attempt.PreAuthentication == preAuthentication,
                    cancellationToken)
                .ConfigureAwait(false)
            : await context.ProviderAttempts
                .SingleOrDefaultAsync(attempt => attempt.Session == binding.Session, cancellationToken)
                .ConfigureAwait(false);

    private string? Verifier(ProviderAttemptRecord record)
    {
        if (record.Verifier is not byte[] wrapped || record.KeyVersion is not int version)
        {
            return null;
        }

        byte[] verifier = PersonalFieldCipher.Unwrap(
            PersonalDataFormat.Marker,
            version,
            wrapped,
            keyEncryptionKeys);

        try
        {
            return Encoding.ASCII.GetString(verifier);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(verifier);
        }
    }
}
