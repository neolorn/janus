using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.BreakGlass;

/// <summary>
/// Which account the break-glass session belongs to.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-002 and CONV-DESIGN-003. The account is the one bootstrap marks
/// as the reserved <c>emergency</c> account, and nothing in the application marks
/// another.
/// </remarks>
internal interface IEmergencyAccount
{
    /// <summary>
    /// The reserved emergency account.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The account, or nothing before bootstrap has created it.</returns>
    ValueTask<SubjectId?> FindAsync(CancellationToken cancellationToken);
}
