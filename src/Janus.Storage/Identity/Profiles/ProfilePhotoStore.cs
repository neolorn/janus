using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Profiles;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Privacy.SubjectKeys;

namespace Janus.Storage.Identity.Profiles;

/// <summary>
/// An account's photo, over the <c>profile_photos</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions a subject key may be wrapped under.</param>
/// <param name="randomness">The randomness the initialisation vector is drawn from.</param>
/// <remarks>Implements IDN-ATTR-003, PRIV-RIGHT-005a and CONV-DESIGN-003.</remarks>
internal sealed class ProfilePhotoStore(
    StoreContext context,
    KeyEncryptionKeys keyEncryptionKeys,
    RandomNumberGenerator randomness) : IProfilePhotoStore
{
    /// <inheritdoc/>
    public async ValueTask<ProfilePhoto?> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ProfilePhotoRecord? record = await FindAsync(subject, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        byte[] dataKey = await DataKeyAsync(subject, cancellationToken).ConfigureAwait(false);

        try
        {
            return ProfilePhoto.Of(
                subject,
                PersonalFieldCipher.Decrypt(dataKey, Located(subject), record.Image),
                record.UpdatedAt);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(ProfilePhoto photo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(photo);

        ProfilePhotoRecord? record = await FindAsync(photo.Subject, cancellationToken).ConfigureAwait(false);
        byte[] dataKey = await DataKeyAsync(photo.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            if (record is null)
            {
                record = new ProfilePhotoRecord { Subject = photo.Subject };
                context.ProfilePhotos.Add(record);
            }

            record.Image = PersonalFieldCipher.Encrypt(
                dataKey,
                Located(photo.Subject),
                photo.Image.Span,
                randomness);

            record.UpdatedAt = photo.UpdatedAt;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        ProfilePhotoRecord? record = await FindAsync(subject, cancellationToken).ConfigureAwait(false);

        if (record is not null)
        {
            context.ProfilePhotos.Remove(record);
        }
    }

    private static PersonalFieldLocation Located(SubjectId subject) =>
        new(subject, ProfilePhotoConfiguration.Table, ProfilePhotoConfiguration.ImageColumn);

    private async ValueTask<ProfilePhotoRecord?> FindAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.ProfilePhotos.FindAsync([subject], cancellationToken).ConfigureAwait(false);

    private async ValueTask<byte[]> DataKeyAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        SubjectKeyRecord key = await context.SubjectKeys
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key to read its photo under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, keyEncryptionKeys);
    }
}
