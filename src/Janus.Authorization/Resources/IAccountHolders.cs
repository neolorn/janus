using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Resources;

/// <summary>
/// Where the registration of a record reads which subjects hold an account.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-002a AC1. The account is another area's aggregate and therefore
/// out of this one's reach (LIB-PKG-001), so the one fact the registration evaluates is
/// read through a port of its own (CONV-DESIGN-003).
/// </remarks>
internal interface IAccountHolders
{
    /// <summary>
    /// Which of the subjects hold an account whose rights can still be exercised: one
    /// that is neither being deleted nor deleted.
    /// </summary>
    /// <param name="subjects">The subjects asked about.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Those of them that hold one.</returns>
    ValueTask<IReadOnlySet<SubjectId>> HoldingAsync(
        IReadOnlyCollection<SubjectId> subjects,
        CancellationToken cancellationToken);
}
