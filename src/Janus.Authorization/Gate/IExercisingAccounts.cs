using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where the gate reads which accounts are in a state to exercise a permission.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GATE-007 (D-218). The state belongs to the account, which is another
/// area's aggregate and therefore out of this one's reach (LIB-PKG-001), so the gate
/// reads the one fact it evaluates through a port of its own (CONV-DESIGN-003).
/// </remarks>
internal interface IExercisingAccounts
{
    /// <summary>
    /// Which of the subjects hold an account that is active, or, for a reading
    /// permission, restricted (AUTHZ-GATE-006).
    /// </summary>
    /// <param name="subjects">The subjects asked about.</param>
    /// <param name="reading">Whether the permission is reading.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Those of them that do, read in one statement.</returns>
    ValueTask<IReadOnlySet<SubjectId>> ExercisingAsync(
        IReadOnlyCollection<SubjectId> subjects,
        bool reading,
        CancellationToken cancellationToken);
}
