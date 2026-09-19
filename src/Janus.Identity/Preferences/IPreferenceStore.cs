using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Identity.Preferences;

/// <summary>
/// Where an account's preferences are read and written.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-001, REG-PREF-001, PRIV-RIGHT-005a and CONV-DESIGN-003. The
/// declared values are held under the subject's key and the language and time zone are
/// not: a message still has to reach a person whose fields have been erased.
/// </remarks>
internal interface IPreferenceStore
{
    /// <summary>
    /// Reads one account's preferences.
    /// </summary>
    /// <param name="subject">Whose preferences to read.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The set, empty where the account has settled nothing.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The subject's key has been erased, so the declared values are unreadable.
    /// </exception>
    ValueTask<PreferenceSet> FindBySubjectAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the set as it now stands onto the row.
    /// </summary>
    /// <param name="preferences">The preferences as they now stand.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording them.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The subject's key has been erased, so nothing more is written under it.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">The subject has no key.</exception>
    ValueTask RecordAsync(PreferenceSet preferences, CancellationToken cancellationToken);
}
