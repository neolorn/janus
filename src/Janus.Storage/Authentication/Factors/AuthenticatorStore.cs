using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// Enrolled credentials, over the <c>authenticators</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions a subject key may be wrapped under.</param>
/// <param name="randomness">The randomness the initialisation vector is drawn from.</param>
/// <param name="fingerprintKeys">The versions a provider's subject is fingerprinted under.</param>
/// <remarks>
/// Implements AUTH-FACT-001, AUTH-FACT-006, IDN-LIFE-012a, PRIV-RIGHT-005c, OPS-SEC-003
/// and CONV-DESIGN-003. The shared secret of a code generator is written under the
/// person's key, so a dump of the table yields no usable secret; the subject a social
/// provider knows a linked identity by is found by its keyed fingerprint and held under
/// the person's key, so a dump does not say who the person is at the provider either,
/// and a rotation of the fingerprint key can compute the fingerprint again.
/// </remarks>
internal sealed class AuthenticatorStore(
    StoreContext context,
    KeyEncryptionKeys keyEncryptionKeys,
    RandomNumberGenerator randomness,
    FingerprintKeys fingerprintKeys) : IAuthenticatorStore
{
    /// <inheritdoc/>
    public async ValueTask<Authenticator?> FindAsync(
        AuthenticatorId id,
        CancellationToken cancellationToken)
    {
        AuthenticatorRecord? record = await context.Authenticators
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ReadAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Authenticator?> ByCredentialAsync(
        ReadOnlyMemory<byte> credentialId,
        CancellationToken cancellationToken)
    {
        byte[] named = credentialId.ToArray();

        AuthenticatorRecord? record = await context.Authenticators
            .FirstOrDefaultAsync(credential => credential.CredentialId == named, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ReadAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Authenticator?> ByProviderAsync(
        Factor provider,
        string providerSubject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerSubject);

        foreach (byte[] fingerprint in Candidates(providerSubject))
        {
            AuthenticatorRecord? record = await context.Authenticators
                .FirstOrDefaultAsync(
                    credential => credential.Factor == provider && credential.ProviderSubject == fingerprint,
                    cancellationToken)
                .ConfigureAwait(false);

            // A fingerprint erasure neutralised is nobody's (PRIV-RIGHT-005c).
            if (record is not null && Fingerprint.Matches(record.ProviderSubject, fingerprint))
            {
                return await ReadAsync(record, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Authenticator>> OfAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        List<AuthenticatorRecord> records = await context.Authenticators
            .Where(credential => credential.Subject == subject)
            .OrderBy(credential => credential.AddedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (records.Count is 0)
        {
            return [];
        }

        byte[] dataKey = await DataKeyAsync(subject, cancellationToken).ConfigureAwait(false);

        try
        {
            return [.. records.Select(record => Read(dataKey, record))];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public ValueTask AddAsync(Authenticator authenticator, CancellationToken cancellationToken) =>
        AddedAsync(authenticator, providerSubject: null, cancellationToken);

    /// <inheritdoc/>
    public ValueTask LinkAsync(
        Authenticator authenticator,
        string providerSubject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerSubject);

        return AddedAsync(authenticator, providerSubject, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(Authenticator authenticator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authenticator);

        AuthenticatorRecord record = await context.Authenticators
            .FindAsync([authenticator.Id], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The credential has no row to carry the change.");

        record.Label = authenticator.Label.Value;
        record.State = authenticator.State;
        record.LastUsedAt = authenticator.LastUsedAt;
        record.InvalidatesAt = authenticator.InvalidatesAt;
        record.Confirmed = authenticator.Confirmed;
        record.TotpConsumedStep = authenticator.Totp?.ConsumedStep;
        record.Counter = authenticator.WebAuthn?.Counter;
        record.BackupState = authenticator.WebAuthn?.BackupState;
        record.IsPreferred = authenticator.IsPreferred;
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(AuthenticatorId id, CancellationToken cancellationToken)
    {
        AuthenticatorRecord? record = await context.Authenticators
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.Authenticators.Remove(record);
        }
    }

    private static PersonalFieldLocation Located(SubjectId subject) =>
        new(subject, AuthenticatorConfiguration.Table, AuthenticatorConfiguration.TotpSecretColumn);

    private static PersonalFieldLocation Linked(SubjectId subject) =>
        new(subject, AuthenticatorConfiguration.Table, AuthenticatorConfiguration.ProviderSubjectColumn);

    private static Authenticator Read(ReadOnlySpan<byte> dataKey, AuthenticatorRecord record) =>
        Authenticator.Existing(
            record.Id,
            record.Subject,
            record.Factor,
            Label(record.Label),
            record.State,
            record.AddedAt,
            record.LastUsedAt,
            record.InvalidatesAt,
            record.Confirmed,
            record.TotpSecret is null
                ? null
                : new TotpMaterial(
                    PersonalFieldCipher.Decrypt(dataKey, Located(record.Subject), record.TotpSecret),
                    record.TotpConsumedStep),
            record.CredentialId is null
                ? null
                : new WebAuthnMaterial(
                    record.CredentialId,
                    record.PublicKey!,
                    record.Algorithm!.Value,
                    record.RelyingParty!,
                    record.Counter is long counter ? (uint)counter : null,
                    record.BackupEligible!.Value,
                    record.BackupState!.Value),
            record.IsPreferred);

    // A label this library wrote is a label this library accepts, so a stored value
    // that no longer parses is a corrupted row and not a label to drop quietly.
    private static CredentialLabel Label(string stored) =>
        CredentialLabel.TryParse(stored, out CredentialLabel label)
            ? label
            : throw new InvalidOperationException("The stored label is not a label.");

    private async ValueTask AddedAsync(
        Authenticator authenticator,
        string? providerSubject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authenticator);

        var record = new AuthenticatorRecord
        {
            Id = authenticator.Id,
            Subject = authenticator.Subject,
            Factor = authenticator.Factor,
            Label = authenticator.Label.Value,
            State = authenticator.State,
            AddedAt = authenticator.AddedAt,
            LastUsedAt = authenticator.LastUsedAt,
            InvalidatesAt = authenticator.InvalidatesAt,
            Confirmed = authenticator.Confirmed,
            CredentialId = authenticator.WebAuthn?.CredentialId.ToArray(),
            PublicKey = authenticator.WebAuthn?.PublicKey.ToArray(),
            Algorithm = authenticator.WebAuthn?.Algorithm,
            RelyingParty = authenticator.WebAuthn?.RelyingPartyId,
            Counter = authenticator.WebAuthn?.Counter,
            BackupEligible = authenticator.WebAuthn?.BackupEligible,
            BackupState = authenticator.WebAuthn?.BackupState,
            TotpConsumedStep = authenticator.Totp?.ConsumedStep,
            IsPreferred = authenticator.IsPreferred,
        };

        if (authenticator.Totp is not null || providerSubject is not null)
        {
            byte[] dataKey = await DataKeyAsync(authenticator.Subject, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                if (authenticator.Totp is not null)
                {
                    record.TotpSecret = PersonalFieldCipher.Encrypt(
                        dataKey,
                        Located(authenticator.Subject),
                        authenticator.Totp.Secret.Span,
                        randomness);
                }

                if (providerSubject is not null)
                {
                    byte[] linked = Encoding.UTF8.GetBytes(providerSubject);

                    record.ProviderSubject = Fingerprint.Compute(linked, fingerprintKeys);
                    record.FingerprintVersion = fingerprintKeys.CurrentVersion;
                    record.EncryptedProviderSubject = PersonalFieldCipher.Encrypt(
                        dataKey,
                        Linked(authenticator.Subject),
                        linked,
                        randomness);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dataKey);
            }
        }

        context.Authenticators.Add(record);
    }

    private IReadOnlyList<byte[]> Candidates(string providerSubject) =>
        Fingerprint.Candidates(Encoding.UTF8.GetBytes(providerSubject), fingerprintKeys);

    private async ValueTask<Authenticator> ReadAsync(
        AuthenticatorRecord record,
        CancellationToken cancellationToken)
    {
        if (record.TotpSecret is null)
        {
            return Read([], record);
        }

        byte[] dataKey = await DataKeyAsync(record.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            return Read(dataKey, record);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private async ValueTask<byte[]> DataKeyAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        SubjectKeyRecord key = await context.SubjectKeys
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key to read its credentials under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, keyEncryptionKeys);
    }
}
