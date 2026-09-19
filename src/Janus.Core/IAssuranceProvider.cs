using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// Where the gate reads how far a caller's session has authenticated, when
/// authorization is consumed without this library's authentication.
/// </summary>
/// <remarks>
/// Implements LIB-HOST-004 and AUTH-STEP-003. A deployment that supplies none is a
/// deployment where every step-up gate is unmet: the gate fails closed rather than
/// assuming the session reached a level nothing reported.
/// </remarks>
public interface IAssuranceProvider
{
    /// <summary>
    /// What the caller's session has reached.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The level, or a failure where the session cannot be read, which leaves every
    /// step-up gate unmet.
    /// </returns>
    ValueTask<Result<AssuranceLevel>> LevelAsync(
        AccessContext context,
        CancellationToken cancellationToken);
}
