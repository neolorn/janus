using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Profiles;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Privacy.SubjectKeys;

namespace Janus.Storage.Identity.Profiles;

/// <summary>
/// An account's profile, over the <c>profiles</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions a subject key may be wrapped under.</param>
/// <param name="randomness">The randomness each initialisation vector is drawn from.</param>
/// <remarks>
/// Implements IDN-ATTR-007, REG-PROF-001, PRIV-RIGHT-005a and CONV-DESIGN-003. The
/// subject's data key is unwrapped once for an operation and cleared before it returns
/// (PRIV-RIGHT-005a AC12).
/// </remarks>
internal sealed class ProfileStore(
    StoreContext context,
    KeyEncryptionKeys keyEncryptionKeys,
    RandomNumberGenerator randomness) : IProfileStore
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <inheritdoc/>
    public async ValueTask<Profile> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ProfileRecord? record = await FindAsync(subject, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            return Profile.Empty(subject);
        }

        byte[] dataKey = await DataKeyAsync(subject, cancellationToken).ConfigureAwait(false);

        try
        {
            return Profile.Existing(
                subject,
                Name(dataKey, subject, ProfileConfiguration.DisplayNameColumn, record.DisplayName),
                Legal(dataKey, subject, record.LegalName),
                Born(dataKey, subject, record.DateOfBirth));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(Profile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        ProfileRecord? record = await FindAsync(profile.Subject, cancellationToken).ConfigureAwait(false);
        byte[] dataKey = await DataKeyAsync(profile.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            if (record is null)
            {
                record = new ProfileRecord { Subject = profile.Subject };
                context.Profiles.Add(record);
            }

            record.DisplayName = Carry(
                record.DisplayName,
                profile.DisplayName?.Value,
                profile.Subject,
                ProfileConfiguration.DisplayNameColumn,
                dataKey);

            record.LegalName = Carry(
                record.LegalName,
                profile.LegalName?.Value,
                profile.Subject,
                ProfileConfiguration.LegalNameColumn,
                dataKey);

            record.DateOfBirth = Carry(
                record.DateOfBirth,
                profile.DateOfBirth?.ToString(DateFormat, CultureInfo.InvariantCulture),
                profile.Subject,
                ProfileConfiguration.DateOfBirthColumn,
                dataKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private static string? Read(
        ReadOnlySpan<byte> dataKey,
        SubjectId subject,
        string column,
        byte[]? stored) =>
        stored is null ? null : Encoding.UTF8.GetString(PersonalFieldCipher.Decrypt(
            dataKey,
            new PersonalFieldLocation(subject, ProfileConfiguration.Table, column),
            stored));

    // A value this library wrote is a value this library accepts, so a stored form that
    // no longer parses is a corrupted row and not a field to drop quietly.
    private static DisplayName? Name(
        ReadOnlySpan<byte> dataKey,
        SubjectId subject,
        string column,
        byte[]? stored)
    {
        string? held = Read(dataKey, subject, column, stored);

        if (held is null)
        {
            return null;
        }

        return DisplayName.TryParse(held, out DisplayName name)
            ? name
            : throw new InvalidOperationException("The stored display name is not a display name.");
    }

    private static LegalName? Legal(ReadOnlySpan<byte> dataKey, SubjectId subject, byte[]? stored)
    {
        string? held = Read(dataKey, subject, ProfileConfiguration.LegalNameColumn, stored);

        if (held is null)
        {
            return null;
        }

        return LegalName.TryParse(held, out LegalName name)
            ? name
            : throw new InvalidOperationException("The stored legal name is not a legal name.");
    }

    private static DateOnly? Born(ReadOnlySpan<byte> dataKey, SubjectId subject, byte[]? stored)
    {
        string? held = Read(dataKey, subject, ProfileConfiguration.DateOfBirthColumn, stored);

        if (held is null)
        {
            return null;
        }

        return DateOnly.TryParseExact(
            held,
            DateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateOnly born)
            ? born
            : throw new InvalidOperationException("The stored date of birth is not a date.");
    }

    // Re-encrypting an unchanged value would draw a new initialisation vector and write
    // a column the account did not change, so the stored form is read back first and
    // only a value that differs is written again.
    private byte[]? Carry(
        byte[]? stored,
        string? value,
        SubjectId subject,
        string column,
        ReadOnlySpan<byte> dataKey)
    {
        if (value is null)
        {
            return null;
        }

        return string.Equals(Read(dataKey, subject, column, stored), value, StringComparison.Ordinal)
            ? stored
            : PersonalFieldCipher.Encrypt(
                dataKey,
                new PersonalFieldLocation(subject, ProfileConfiguration.Table, column),
                Encoding.UTF8.GetBytes(value),
                randomness);
    }

    private async ValueTask<ProfileRecord?> FindAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.Profiles.FindAsync([subject], cancellationToken).ConfigureAwait(false);

    private async ValueTask<byte[]> DataKeyAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        SubjectKeyRecord key = await context.SubjectKeys
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key to read its profile under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, keyEncryptionKeys);
    }
}
