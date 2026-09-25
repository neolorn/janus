using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// What registration asks of the account directory: whether a value is already
/// somebody's, the language its holder reads, and the one write that turns a finished
/// registration into an account.
/// </summary>
/// <remarks>
/// Implements REG-SESS-001, REG-SESS-005, REG-SESS-007 and CONV-DESIGN-003. The
/// answer to the first never reaches the person registering: it decides only whether
/// a code is sent and whether the owner is told.
/// </remarks>
internal interface IRegistrationDirectory
{
    /// <summary>
    /// The account a value already belongs to, where one does.
    /// </summary>
    /// <param name="kind">Which kind the value is.</param>
    /// <param name="canonical">The value in its canonical form.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The account holding it, or nothing.</returns>
    ValueTask<SubjectId?> OwnerAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken);

    /// <summary>
    /// The language an account settled on, which is what its holder is told in.
    /// </summary>
    /// <param name="subject">The account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The language, or nothing where it settled none.</returns>
    ValueTask<string?> LanguageAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the account, its identifiers and what the person answered on the way
    /// in. The caller opens and commits the transaction it runs in.
    /// </summary>
    /// <param name="account">What the finished registration holds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of creating it.</returns>
    ValueTask CreateAsync(NewAccount account, CancellationToken cancellationToken);
}
