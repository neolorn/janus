using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Authorization.Tests.Gate;

/// <summary>
/// The gate's refusal of what belongs to no organization, as a test deployment needs
/// it: every caller is refused, as the gate refuses a caller holding nothing.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that answers as the gate answers a caller with no grant, not a
/// recorder of calls.
/// </remarks>
internal sealed class UnscopedRefusalInMemory : IUnscopedRefusal
{
    /// <inheritdoc/>
    public ValueTask<Error> RefusedAsync(
        AccessContext context,
        Permission permission,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Error.From(ErrorCodes.Denied));
}
