using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Identity.Audit;

/// <summary>
/// Where the audit trail's monthly partitions are created ahead of the rows that need
/// them and dropped once their category's retention has passed, each through the one
/// function the migration made for it.
/// </summary>
/// <remarks>
/// Implements PRIV-RET-002, OPS-MIG-003a and CONV-DESIGN-003. It is reached over the
/// maintenance credential only: the application's own holds no right to either
/// function, and nothing here deletes a row.
/// </remarks>
internal interface IAuditPartitions
{
    /// <summary>
    /// Whether the connection runs with the rights of the maintenance role and with no
    /// path to the application's.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>Whether it is the maintenance credential.</returns>
    ValueTask<bool> UnderMaintenanceCredentialAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates the current month's partition of each category and the two after it,
    /// where they do not yet exist.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many partitions were created.</returns>
    ValueTask<int> EnsureAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Drops every partition whose end is older than its category's retention.
    /// </summary>
    /// <param name="securityRetention">How long the security category is kept.</param>
    /// <param name="routineRetention">How long the routine category is kept.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many partitions were dropped.</returns>
    ValueTask<int> DropExpiredAsync(
        TimeSpan securityRetention,
        TimeSpan routineRetention,
        CancellationToken cancellationToken);
}
