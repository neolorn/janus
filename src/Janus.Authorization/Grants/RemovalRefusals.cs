using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authorization.Grants;

/// <summary>
/// The codes the host's check refused a removal of access with in the operation being
/// served, which the boundary answers 409 though the reference does not list them.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-005 (D-281) beside BFF-HOST-003: a code the reference does not
/// list is a fault of the code that answered it, save the host's own refusal of a removal,
/// which the library hands back as the host named it. Held for the scope of one request
/// or job run, so a code is known as the check's only where the check gave it.
/// </remarks>
internal sealed class RemovalRefusals
{
    private readonly HashSet<ErrorCode> _refused = [];

    /// <summary>
    /// Keeps a code the host's check refused with.
    /// </summary>
    /// <param name="code">The code.</param>
    public void Record(ErrorCode code) => _refused.Add(code);

    /// <summary>
    /// Whether the host's check refused with the code in this scope.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>Whether it did.</returns>
    public bool Holds(ErrorCode code) => _refused.Contains(code);
}
