using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Preferences;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Privacy.SubjectKeys;

namespace Janus.Storage.Identity.Preferences;

/// <summary>
/// An account's preferences, over the <c>account_preferences</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions a subject key may be wrapped under.</param>
/// <param name="randomness">The randomness the initialisation vector is drawn from.</param>
/// <remarks>
/// Implements IDN-ATTR-001, REG-PREF-001, PRIV-RIGHT-005a and CONV-DESIGN-003. The
/// declared values cross the boundary as one document because the library never reads
/// one of them on its own and the cap of <c>preferences.maxsize</c> is over the set.
/// </remarks>
internal sealed class PreferenceStore(
    StoreContext context,
    KeyEncryptionKeys keyEncryptionKeys,
    RandomNumberGenerator randomness) : IPreferenceStore
{
    /// <inheritdoc/>
    public async ValueTask<PreferenceSet> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        PreferenceRecord? record = await FindAsync(subject, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            return PreferenceSet.Empty(subject);
        }

        if (record.Values is null)
        {
            return PreferenceSet.Existing(subject, record.Language, record.TimeZone, Nothing);
        }

        byte[] dataKey = await DataKeyAsync(subject, cancellationToken).ConfigureAwait(false);

        try
        {
            return PreferenceSet.Existing(
                subject,
                record.Language,
                record.TimeZone,
                Read(dataKey, subject, record.Values));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(PreferenceSet preferences, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        PreferenceRecord? record = await FindAsync(preferences.Subject, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            record = new PreferenceRecord { Subject = preferences.Subject };
            context.AccountPreferences.Add(record);
        }

        record.Language = preferences.Language;
        record.TimeZone = preferences.TimeZone;
        record.Values = await WrittenAsync(preferences, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, string> Nothing { get; } =
        new Dictionary<string, string>(capacity: 0);

    private static PersonalFieldLocation Located(SubjectId subject) =>
        new(subject, PreferenceConfiguration.Table, PreferenceConfiguration.ValuesColumn);

    private static Dictionary<string, string> Read(
        ReadOnlySpan<byte> dataKey,
        SubjectId subject,
        ReadOnlySpan<byte> stored) =>
        JsonSerializer.Deserialize(
            PersonalFieldCipher.Decrypt(dataKey, Located(subject), stored),
            PreferenceDocument.Default.DictionaryStringString)
            ?? throw new InvalidOperationException("The stored preferences are not a document.");

    private async ValueTask<PreferenceRecord?> FindAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.AccountPreferences.FindAsync([subject], cancellationToken).ConfigureAwait(false);

    private async ValueTask<byte[]?> WrittenAsync(
        PreferenceSet preferences,
        CancellationToken cancellationToken)
    {
        if (preferences.Values.Count == 0)
        {
            return null;
        }

        byte[] dataKey = await DataKeyAsync(preferences.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            return PersonalFieldCipher.Encrypt(
                dataKey,
                Located(preferences.Subject),
                JsonSerializer.SerializeToUtf8Bytes(
                    new Dictionary<string, string>(preferences.Values, StringComparer.Ordinal),
                    PreferenceDocument.Default.DictionaryStringString),
                randomness);
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
            ?? throw new InvalidOperationException("The subject has no key to read its preferences under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, keyEncryptionKeys);
    }
}
