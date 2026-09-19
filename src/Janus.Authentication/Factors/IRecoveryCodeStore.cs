using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// Where an account's recovery codes are read and written. An account holds one set;
/// generating another replaces it whole.
/// </summary>
/// <remarks>Implements AUTH-FACT-008, AUTH-FACT-009 and CONV-DESIGN-003.</remarks>
internal interface IRecoveryCodeStore
{
    /// <summary>
    /// The account's set.
    /// </summary>
    /// <param name="subject">Whose set.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The set, or nothing where the account holds none.</returns>
    ValueTask<RecoveryCodeSet?> FindAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the account's set, replacing whatever it held.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask ReplaceAsync(RecoveryCodeSet set, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change a set made onto its rows.
    /// </summary>
    /// <param name="set">The set as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordAsync(RecoveryCodeSet set, CancellationToken cancellationToken);
}
