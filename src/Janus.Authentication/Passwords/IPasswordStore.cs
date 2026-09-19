using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Passwords;

/// <summary>
/// Where an account's password is read and written.
/// </summary>
/// <remarks>Implements AUTH-PASS-007 and CONV-DESIGN-003.</remarks>
internal interface IPasswordStore
{
    /// <summary>
    /// Reads the password an account holds.
    /// </summary>
    /// <param name="subject">Whose password to read.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The password, or nothing where the account holds none.</returns>
    ValueTask<Password?> FindAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Records a password just set, replacing any the account held.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask SetAsync(Password password, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a rehash at raised parameters onto the row.
    /// </summary>
    /// <param name="password">The password as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RehashAsync(Password password, CancellationToken cancellationToken);
}
