using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Identity.Identifiers;

/// <summary>
/// Where an account's identifiers are read and written.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-004, REG-IDENT-002, PRIV-RIGHT-005a, PRIV-RIGHT-005c and
/// CONV-DESIGN-003. A value crosses this boundary in the two forms the account holds it
/// in; which of them is encrypted, which is fingerprinted and which is looked up is the
/// implementation's business.
/// </remarks>
internal interface IIdentifierStore
{
    /// <summary>
    /// Reads one account's identifiers and the backup settings it has changed.
    /// </summary>
    /// <param name="subject">Whose identifiers to read.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The set, empty where the account holds none.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The subject's key has been erased, so the identifiers it holds are unreadable.
    /// </exception>
    ValueTask<IdentifierSet> FindBySubjectAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the account an identifier belongs to. An identifier belongs to at most one
    /// account, and one whose fingerprint erasure neutralised belongs to none.
    /// </summary>
    /// <param name="kind">Which kind the value is.</param>
    /// <param name="canonical">The value in its canonical form.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The account holding it, or nothing where no account holds it.</returns>
    ValueTask<SubjectId?> FindOwnerAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken);

    /// <summary>
    /// Carries the set as it now stands onto the rows: identifiers the account has
    /// taken on, the verifications and primary roles it has moved, and the backup
    /// settings it has changed or returned to the default.
    /// </summary>
    /// <param name="set">The account's identifiers as they now stand.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording them.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The subject's key has been erased, so nothing more is written under it.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">The subject has no key.</exception>
    ValueTask RecordAsync(IdentifierSet set, CancellationToken cancellationToken);
}
