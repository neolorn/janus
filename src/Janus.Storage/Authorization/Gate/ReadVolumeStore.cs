using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// Where the records each person was given are counted by day and each person's daily
/// mean is kept.
/// </summary>
/// <param name="connections">The connection and transaction the operation holds.</param>
/// <remarks>
/// Implements OPS-ALERT-005 and CONV-DESIGN-003. A day's count is added to in one
/// statement whose sum the database keeps, so two queries reported together are both
/// counted and neither reads the other's count before writing.
/// </remarks>
internal sealed class ReadVolumeStore(DataConnections connections) : IReadVolumeStore
{
    private const string Add =
        """
        INSERT INTO identity.read_volume (actor, day, records)
        VALUES (@actor, @day, @records)
        ON CONFLICT (actor, day) DO UPDATE SET records = read_volume.records + excluded.records
        RETURNING records;
        """;

    private const string Baseline =
        """
        SELECT daily_mean FROM identity.read_baselines WHERE actor = @actor;
        """;

    private const string Rebaseline =
        """
        DELETE FROM identity.read_volume WHERE day < @oldest;
        DELETE FROM identity.read_baselines;
        INSERT INTO identity.read_baselines (actor, daily_mean)
        SELECT actor, SUM(records)::numeric / @days
        FROM identity.read_volume
        WHERE day < @today
        GROUP BY actor;
        """;

    private const string Baselined =
        """
        SELECT count(*)::int FROM identity.read_baselines;
        """;

    /// <inheritdoc/>
    public async ValueTask<long> AddAsync(
        SubjectId actor,
        DateOnly day,
        int records,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        return await ambient.Connection
            .ExecuteScalarAsync<long>(new CommandDefinition(
                Add,
                new { actor = actor.Value, day, records = (long)records },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<decimal> BaselineAsync(SubjectId actor, CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        return await ambient.Connection
            .ExecuteScalarAsync<decimal?>(new CommandDefinition(
                Baseline,
                new { actor = actor.Value },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false) ?? 0m;
    }

    /// <inheritdoc/>
    public async ValueTask<int> RebaselineAsync(DateOnly today, int days, CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Rebaseline,
                new { today, oldest = today.AddDays(-days), days = (decimal)days },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return await ambient.Connection
            .ExecuteScalarAsync<int>(new CommandDefinition(
                Baselined,
                transaction: ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
