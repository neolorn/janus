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

    /// <summary>
    /// Whether the subject's processing is restricted, read with the account's row held
    /// until the open transaction ends, so that a restriction commits before this read
    /// or after that transaction.
    /// </summary>
    /// <param name="subject">The account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether it is restricted as committed when the hold was taken, or nothing where
    /// no transaction is open, in which nothing can be held.
    /// </returns>
    /// <remarks>
    /// Implements AUTHZ-GATE-006 (D-183) and CONV-DESIGN-003. The hold is shared, so
    /// actions of one account do not wait for each other, and a transaction that already
    /// holds the row for a change of its own takes it again without waiting.
    /// </remarks>
    ValueTask<bool?> HoldAsync(SubjectId subject, CancellationToken cancellationToken);
}
