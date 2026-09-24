using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Identity.Audit;

namespace Janus.Storage.Identity.Audit;

/// <summary>
/// The audit trail's monthly partitions, through the two <c>SECURITY DEFINER</c>
/// functions the migration created for them.
/// </summary>
/// <param name="connections">Where each call takes its connection from.</param>
/// <remarks>
/// Implements PRIV-RET-002 and OPS-MIG-003a. The functions run with the rights of the
/// migration role that owns them, and hold each retention to its PRIV-RET-001 floor
/// whatever is passed in, so the caller can neither alter schema nor shorten retention.
/// </remarks>
internal sealed class AuditPartitions(DataConnections connections) : IAuditPartitions
{
    // OPS-MIG-003a: the rights of the maintenance role, and no path to the application's,
    // which a superuser holds as it holds every role's.
    private const string Credential =
        """
        SELECT pg_has_role(current_user, 'identity_maintenance', 'USAGE')
            AND NOT pg_has_role(current_user, 'identity_app', 'MEMBER');
        """;

    private const string Ensure = "SELECT identity.audit_ensure_partitions();";

    private const string Drop = "SELECT identity.audit_drop_expired_partitions(@security, @routine);";

    /// <inheritdoc/>
    public async ValueTask<bool> UnderMaintenanceCredentialAsync(CancellationToken cancellationToken) =>
        await ScalarAsync<bool>(Credential, parameters: null, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<int> EnsureAsync(CancellationToken cancellationToken) =>
        await ScalarAsync<int>(Ensure, parameters: null, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<int> DropExpiredAsync(
        TimeSpan securityRetention,
        TimeSpan routineRetention,
        CancellationToken cancellationToken) =>
        await ScalarAsync<int>(
                Drop,
                new { security = securityRetention, routine = routineRetention },
                cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask<TValue> ScalarAsync<TValue>(
        string sql,
        object? parameters,
        CancellationToken cancellationToken)
        where TValue : struct
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        return await ambient.Connection
            .ExecuteScalarAsync<TValue>(new CommandDefinition(
                sql,
                parameters,
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
