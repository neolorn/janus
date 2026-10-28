using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Grants;

/// <summary>
/// Where a grant or a group's member is read to name a subject an account bears.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-007, REC-REC-001 and CONV-DESIGN-003. The account belongs to
/// another area's aggregate and is out of this one's reach (LIB-PKG-001), so the one
/// fact a grant and a member are judged by is read through a port of its own. A person
/// record's subject is drawn from the space accounts' are and is borne by no account, so
/// it is answered as any subject no account bears is.
/// </remarks>
internal interface IAccountSubjects
{
    /// <summary>
    /// Whether an account bears the subject, in whatever state it is.
    /// </summary>
    /// <param name="subject">The subject named.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it does.</returns>
    ValueTask<bool> BearsAsync(SubjectId subject, CancellationToken cancellationToken);
}
