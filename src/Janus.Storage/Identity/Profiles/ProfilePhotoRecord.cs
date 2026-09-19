using System;
using Janus.Core;

namespace Janus.Storage.Identity.Profiles;

/// <summary>
/// The <c>profile_photos</c> row.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-003, PRIV-RIGHT-005a and CONV-DESIGN-003. The image is
/// ciphertext under the subject's own key, so a dump yields no photograph and erasure
/// of the key is what makes it unrecoverable.
/// </remarks>
internal sealed class ProfilePhotoRecord
{
    /// <summary>
    /// The subject column, which is this table's key and the column the encrypted
    /// column names as its subject.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>enc_image</c> column.
    /// </summary>
    public byte[] Image { get; set; } = [];

    /// <summary>
    /// The <c>updated_at</c> column.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
