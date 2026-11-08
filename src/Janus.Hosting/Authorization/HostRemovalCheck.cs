using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// The host's check of a removal of access, where the host registered one, asked so that
/// nothing it throws lets a removal through.
/// </summary>
/// <param name="checks">The check the host registered, at most one (LIB-HOST-001).</param>
/// <remarks>
/// Implements AUTHZ-GRANT-005 (D-281, Q4). A check that throws, or that does not answer
/// before the request or the job's pass is abandoned for another reason, refuses with
/// <c>authz.removal.uncheckable</c>; the change's own cancellation abandons it. No time
/// limit is set: the request's own and the job's bound the wait.
/// </remarks>
internal sealed class HostRemovalCheck(IEnumerable<IAccessRemovalCheck> checks) : IRemovalCheck
{
    private readonly IAccessRemovalCheck? _check = checks.FirstOrDefault();

    /// <inheritdoc/>
    public bool Registered => _check is not null;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The removal is absent.</exception>
    public async ValueTask<Result> AskAsync(AccessRemoval removal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(removal);

        if (_check is null)
        {
            return Uncheckable();
        }

        try
        {
            return await _check.CheckAsync(removal, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception fault) when (fault is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(Error.From(ErrorCodes.RemovalUncheckable));
        }
    }

    private static Result Uncheckable() => Result.Failure(Error.From(ErrorCodes.RemovalUncheckable));
}
