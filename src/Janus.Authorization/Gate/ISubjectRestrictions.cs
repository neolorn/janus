using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where the gate reads whether a subject's processing is restricted.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GATE-006. The state belongs to the account, which is another
/// area's aggregate and therefore out of this one's reach (LIB-PKG-001), so the gate
/// reads the one fact it evaluates through a port of its own (CONV-DESIGN-003).
/// </remarks>
internal interface ISubjectRestrictions
{
    /// <summary>
    /// Whether the subject's processing is restricted.
    /// </summary>
    /// <param name="subject">The account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether it is restricted, which a subject the library holds no account for is
    /// not: it holds no grant either, so nothing is conferred to restrict.
    /// </returns>
    ValueTask<bool> IsRestrictedAsync(SubjectId subject, CancellationToken cancellationToken);
}
