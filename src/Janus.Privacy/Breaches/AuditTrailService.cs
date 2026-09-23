using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Policies;

namespace Janus.Privacy.Breaches;

/// <summary>
/// The audit trail read by data subject, for a caller holding <c>audit:read</c>.
/// </summary>
/// <param name="scope">Whether the caller may ask.</param>
/// <param name="trail">Where the trail is read.</param>
/// <remarks>
/// Implements PRIV-BREACH-002, LIB-API-005 and AUTHZ-CONCEAL-005.
/// </remarks>
internal sealed class AuditTrailService(AdministrativeScope scope, IAuditTrailStore trail) : IAuditTrail
{
    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<AuditEntry>>> OfSubjectAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope.RefusedAsync(context, Permissions.AuditRead, cancellationToken).ConfigureAwait(false)
            is Error denied)
        {
            return Result.Failure<IReadOnlyList<AuditEntry>>(denied);
        }

        return Result.Success(await trail.OfSubjectAsync(subject, cancellationToken).ConfigureAwait(false));
    }
}
