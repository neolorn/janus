using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Identity.Profiles;

/// <summary>
/// Where an account's profile is read and written.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-007, REG-PROF-001, PRIV-RIGHT-005a and CONV-DESIGN-003. Every
/// field of the profile is held under the subject's own key, so a read of one is a read
/// of the key as well.
/// </remarks>
internal interface IProfileStore
{
    /// <summary>
    /// Reads one account's profile.
    /// </summary>
    /// <param name="subject">Whose profile to read.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The profile, empty where the account has filled nothing in.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The subject's key has been erased, so the profile it wrote is unreadable.
    /// </exception>
    ValueTask<Profile> FindBySubjectAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the profile as it now stands onto the row, writing one where the account
    /// had none and clearing a field the account gave up.
    /// </summary>
    /// <param name="profile">The profile as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The subject's key has been erased, so nothing more is written under it.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">The subject has no key.</exception>
    ValueTask RecordAsync(Profile profile, CancellationToken cancellationToken);
}
