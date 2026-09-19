using System;
using Janus.Core;

namespace Janus.Identity.Profiles;

/// <summary>
/// The image an account shows for itself, held apart from the rest of the profile.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-002, IDN-ATTR-003 and IDN-ATTR-004. The bytes are in the
/// database rather than on a filesystem, so they are in the backup regime, under the
/// one erasure and behind the one access gate. What reaches this type is already the
/// re-encoded image: the validation and the re-encoding are the upload path's.
/// </remarks>
internal sealed class ProfilePhoto
{
    private ProfilePhoto(SubjectId subject, ReadOnlyMemory<byte> image, DateTimeOffset updatedAt)
    {
        Subject = subject;
        Image = image;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Whose photo it is.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// The stored image, which is JPEG and carries no metadata segment.
    /// </summary>
    public ReadOnlyMemory<byte> Image { get; }

    /// <summary>
    /// When the image was last replaced.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; }

    /// <summary>
    /// Records an image the upload path has validated and re-encoded.
    /// </summary>
    /// <param name="subject">Whose photo it is.</param>
    /// <param name="image">The re-encoded image.</param>
    /// <param name="updatedAt">When it was uploaded.</param>
    /// <returns>The photo.</returns>
    /// <exception cref="ArgumentException">The image carries no bytes.</exception>
    public static ProfilePhoto Of(SubjectId subject, ReadOnlyMemory<byte> image, DateTimeOffset updatedAt)
    {
        if (image.IsEmpty)
        {
            throw new ArgumentException("A photo of no bytes is not a photo.", nameof(image));
        }

        return new ProfilePhoto(subject, image, updatedAt);
    }
}
