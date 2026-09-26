using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// The refusal of what belongs to no organization, as the access gate answers it.
/// </summary>
/// <param name="refuse">
/// The gate's refusal, from the one place a refusal is recorded and counted.
/// </param>
/// <remarks>
/// Implements CONV-DESIGN-002 AC3 and AUTHZ-SCOPE-001: the operations on groups and
/// grants ask here, and the answer is the gate's, so the refusal is recorded and
/// counted as every other one is and in no second place. The gate is named where it is
/// registered and nowhere else (LIB-SEAM-001 AC1).
/// </remarks>
internal sealed class GatedUnscopedRefusal(
    Func<AccessContext, Permission, CancellationToken, ValueTask<Error>> refuse) : IUnscopedRefusal
{
    /// <inheritdoc/>
    public ValueTask<Error> RefusedAsync(
        AccessContext context,
        Permission permission,
        CancellationToken cancellationToken) =>
        refuse(context, permission, cancellationToken);
}
