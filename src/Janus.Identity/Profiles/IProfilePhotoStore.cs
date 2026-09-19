using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Identity.Profiles;

/// <summary>
/// Where an account's photo is read and written.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-003, PRIV-RIGHT-005a and CONV-DESIGN-003. The photo is a port of
/// its own over a table of its own, so nothing that reads an account reads image bytes
/// by accident.
/// </remarks>
internal interface IProfilePhotoStore
{
    /// <summary>
    /// Reads one account's photo.
    /// </summary>
    /// <param name="subject">Whose photo to read.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The photo, or nothing where the account shows none.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The subject's key has been erased, so the image is unreadable.
    /// </exception>
    ValueTask<ProfilePhoto?> FindBySubjectAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Records an account's photo, replacing the one it showed.
    /// </summary>
    /// <param name="photo">The photo to record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The subject's key has been erased, so nothing more is written under it.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">The subject has no key.</exception>
    ValueTask RecordAsync(ProfilePhoto photo, CancellationToken cancellationToken);

    /// <summary>
    /// Gives up the photo an account shows. A photo is what the account looks like now
    /// and not a record of something that happened (IDN-PRIN-003), so the row goes with
    /// it.
    /// </summary>
    /// <param name="subject">Whose photo to give up.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of giving it up.</returns>
    ValueTask RemoveAsync(SubjectId subject, CancellationToken cancellationToken);
}
