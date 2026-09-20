using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Identifiers;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Identity.Identifiers;

/// <summary>
/// An account's identifiers, over the <c>identifiers</c> and
/// <c>identifier_backup_settings</c> tables.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions a subject key may be wrapped under.</param>
/// <param name="fingerprintKey">The key the searchable fingerprints are computed under.</param>
/// <param name="randomness">The randomness each initialisation vector is drawn from.</param>
/// <remarks>
/// Implements IDN-ACCT-004, REG-IDENT-002, PRIV-RIGHT-005a, PRIV-RIGHT-005c and
/// CONV-DESIGN-003. The subject's data key is unwrapped once for an operation, however
/// many fields it touches, and cleared before the operation returns (PRIV-RIGHT-005a
/// AC12).
/// </remarks>
internal sealed class IdentifierStore(
    JanusDbContext context,
    KeyEncryptionKeys keyEncryptionKeys,
    ReadOnlyMemory<byte> fingerprintKey,
    RandomNumberGenerator randomness) : IIdentifierStore
{
    /// <inheritdoc/>
    public async ValueTask<IdentifierSet> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        List<IdentifierRecord> rows = await HeldAsync(subject, cancellationToken).ConfigureAwait(false);
        List<BackupSettingRecord> settings = await SettledAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        var identifiers = new List<Identifier>(rows.Count);

        if (rows.Count > 0)
        {
            byte[] dataKey = await DataKeyAsync(subject, cancellationToken).ConfigureAwait(false);

            try
            {
                foreach (IdentifierRecord row in rows)
                {
                    identifiers.Add(Identifier.Existing(
                        row.Id,
                        row.Subject,
                        row.Kind,
                        Read(dataKey, row, IdentifierConfiguration.EnteredColumn, row.Entered),
                        Read(dataKey, row, IdentifierConfiguration.CanonicalColumn, row.Canonical),
                        row.AddedAt,
                        row.VerifiedAt,
                        row.IsPrimary,
                        row.IsLocked));
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dataKey);
            }
        }

        return IdentifierSet.Of(subject, identifiers, settings.Select(Settled));
    }

    /// <inheritdoc/>
    public async ValueTask<SubjectId?> FindOwnerAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(canonical);

        byte[] fingerprint = Fingerprinted(canonical);

        IdentifierRecord? row = await context.Identifiers
            .Where(held => held.Kind == kind && held.Fingerprint == fingerprint)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // PRIV-RIGHT-005c: a neutralised fingerprint is nobody's, and no fingerprint
        // this function computes is the neutralised value, so the guard is the floor
        // rather than the lookup.
        return row is null || Fingerprint.IsNeutralised(row.Fingerprint) ? null : row.Subject;
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(IdentifierSet set, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);

        List<IdentifierRecord> rows = await HeldAsync(set.Subject, cancellationToken).ConfigureAwait(false);
        byte[] dataKey = await DataKeyAsync(set.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (Identifier identifier in set.All)
            {
                IdentifierRecord? row = rows.Find(held => held.Id == identifier.Id);

                if (row is null)
                {
                    context.Identifiers.Add(Taken(identifier, dataKey));
                }
                else
                {
                    Carry(identifier, row, dataKey);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        await RecordBackupsAsync(set, cancellationToken).ConfigureAwait(false);
    }

    private static BackupSetting Settled(BackupSettingRecord row) =>
        BackupSetting.Existing(row.Subject, row.Kind, Ruled(row), row.Named);

    private static BackupChoice Ruled(BackupSettingRecord row) => row.Rule switch
    {
        BackupSettingRecord.AllVerified => BackupChoice.AllVerified,
        BackupSettingRecord.PrimaryOnly => BackupChoice.PrimaryOnly,
        _ => BackupChoice.Named,
    };

    private static void Settle(BackupSetting setting, BackupSettingRecord row)
    {
        row.Rule = setting.Rule switch
        {
            BackupChoice.PrimaryOnly => BackupSettingRecord.PrimaryOnly,
            BackupChoice.Named => null,
            _ => BackupSettingRecord.AllVerified,
        };

        row.Named = setting.Rule is BackupChoice.Named ? setting.Named : null;
    }

    private static string Read(
        ReadOnlySpan<byte> dataKey,
        IdentifierRecord row,
        string column,
        ReadOnlySpan<byte> stored) =>
        Encoding.UTF8.GetString(PersonalFieldCipher.Decrypt(
            dataKey,
            new PersonalFieldLocation(row.Subject, IdentifierConfiguration.Table, column),
            stored));

    private byte[] Fingerprinted(string canonical) =>
        Fingerprint.Compute(Encoding.UTF8.GetBytes(canonical), fingerprintKey.Span);

    private byte[] Written(SubjectId subject, string column, string value, ReadOnlySpan<byte> dataKey) =>
        PersonalFieldCipher.Encrypt(
            dataKey,
            new PersonalFieldLocation(subject, IdentifierConfiguration.Table, column),
            Encoding.UTF8.GetBytes(value),
            randomness);

    private IdentifierRecord Taken(Identifier identifier, ReadOnlySpan<byte> dataKey) =>
        new()
        {
            Id = identifier.Id,
            Subject = identifier.Subject,
            Kind = identifier.Kind,
            Fingerprint = Fingerprinted(identifier.Canonical),
            CanonicalisationVersion = CanonicalForm.UnicodeVersion,
            Entered = Written(
                identifier.Subject,
                IdentifierConfiguration.EnteredColumn,
                identifier.Entered,
                dataKey),
            Canonical = Written(
                identifier.Subject,
                IdentifierConfiguration.CanonicalColumn,
                identifier.Canonical,
                dataKey),
            AddedAt = identifier.AddedAt,
            VerifiedAt = identifier.VerifiedAt,
            IsPrimary = identifier.IsPrimary,
            IsLocked = identifier.IsLocked,
        };

    private void Carry(Identifier identifier, IdentifierRecord row, ReadOnlySpan<byte> dataKey)
    {
        row.VerifiedAt = identifier.VerifiedAt;
        row.IsPrimary = identifier.IsPrimary;

        // Re-encrypting an unchanged value would draw a new initialisation vector and
        // write a column the account did not change, so the stored forms are read back
        // and only a value that differs is written again.
        bool entered = !string.Equals(
            Read(dataKey, row, IdentifierConfiguration.EnteredColumn, row.Entered),
            identifier.Entered,
            StringComparison.Ordinal);

        bool canonical = !string.Equals(
            Read(dataKey, row, IdentifierConfiguration.CanonicalColumn, row.Canonical),
            identifier.Canonical,
            StringComparison.Ordinal);

        if (entered)
        {
            row.Entered = Written(
                identifier.Subject,
                IdentifierConfiguration.EnteredColumn,
                identifier.Entered,
                dataKey);
        }

        if (canonical)
        {
            row.Canonical = Written(
                identifier.Subject,
                IdentifierConfiguration.CanonicalColumn,
                identifier.Canonical,
                dataKey);
            row.Fingerprint = Fingerprinted(identifier.Canonical);
            row.CanonicalisationVersion = CanonicalForm.UnicodeVersion;
        }
    }

    private async Task<List<IdentifierRecord>> HeldAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.Identifiers
            .Where(held => held.Subject == subject)
            .OrderBy(held => held.AddedAt)
            .ThenBy(held => held.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<List<BackupSettingRecord>> SettledAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.BackupSettings
            .Where(settled => settled.Subject == subject)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask<byte[]> DataKeyAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        SubjectKeyRecord key = await context.SubjectKeys
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key to read its identifiers under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, keyEncryptionKeys);
    }

    private async Task RecordBackupsAsync(IdentifierSet set, CancellationToken cancellationToken)
    {
        List<BackupSettingRecord> rows = await SettledAsync(set.Subject, cancellationToken)
            .ConfigureAwait(false);

        foreach (BackupSetting setting in set.Backups)
        {
            BackupSettingRecord? row = rows.Find(settled => settled.Kind == setting.Kind);

            // REG-IDENT-002: the default needs no row, so a kind returned to it gives
            // the row up rather than recording the default twice.
            if (setting.Rule is BackupChoice.AllVerified)
            {
                if (row is not null)
                {
                    context.BackupSettings.Remove(row);
                }

                continue;
            }

            if (row is null)
            {
                row = new BackupSettingRecord { Subject = set.Subject, Kind = setting.Kind };
                context.BackupSettings.Add(row);
            }

            Settle(setting, row);
        }
    }
}
