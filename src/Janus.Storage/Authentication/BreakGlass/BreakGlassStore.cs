using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.BreakGlass;
using Janus.Authentication.Passwords;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.BreakGlass;

/// <summary>
/// The issues of the break-glass credential and the attempts at it, over the
/// <c>break_glass_credentials</c> and <c>break_glass_attempts</c> tables.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="connections">The connection and transaction the operation holds.</param>
/// <remarks>
/// Implements OPS-BOOT-002, OPS-BOOT-004 and CONV-DESIGN-003. Both holds are
/// transaction-scoped advisory locks, so a crashed operation releases them with its
/// transaction and nothing is left to clear.
/// </remarks>
internal sealed class BreakGlassStore(StoreContext context, DataConnections connections) : IBreakGlassStore
{
    private const string Hold =
        "SELECT pg_advisory_xact_lock(hashtext('identity.break_glass_credentials'));";

    private const string Serialize =
        "SELECT pg_advisory_xact_lock(hashtext('identity.break_glass_attempts'));";

    private const string Attempt =
        """
        INSERT INTO identity.break_glass_attempts (id, attempted_at)
        VALUES (@id, @at);
        """;

    private const string Counted =
        """
        SELECT count(*)
        FROM identity.break_glass_attempts
        WHERE attempted_at > @since;
        """;

    /// <inheritdoc/>
    public async ValueTask HoldAsync(CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(Hold, transaction: ambient.Transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<BreakGlassCredential?> StandingAsync(CancellationToken cancellationToken)
    {
        BreakGlassCredentialRecord? record = await context.BreakGlassCredentials
            .AsNoTracking()
            .Where(credential => credential.ConsumedAt == null && credential.ReplacedAt == null)
            .OrderByDescending(credential => credential.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Held(record);
    }

    /// <inheritdoc/>
    public async ValueTask<BreakGlassCredential?> LastConsumedAsync(CancellationToken cancellationToken)
    {
        BreakGlassCredentialRecord? record = await context.BreakGlassCredentials
            .AsNoTracking()
            .Where(credential => credential.ConsumedAt != null)
            .OrderByDescending(credential => credential.ConsumedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Held(record);
    }

    /// <inheritdoc/>
    public ValueTask AddAsync(BreakGlassCredential credential, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);

        context.BreakGlassCredentials.Add(new BreakGlassCredentialRecord
        {
            Id = credential.Id,
            Hash = credential.Hash.Encoded,
            IssuedBy = credential.IssuedBy,
            IssuedAt = credential.IssuedAt,
            ConsumedAt = credential.ConsumedAt,
            ReplacedAt = credential.ReplacedAt,
        });

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RecordAsync(BreakGlassCredential credential, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);

        int recorded = await context.BreakGlassCredentials
            .Where(held => held.Id == credential.Id && held.ConsumedAt == null && held.ReplacedAt == null)
            .ExecuteUpdateAsync(
                columns => columns
                    .SetProperty(held => held.ConsumedAt, credential.ConsumedAt)
                    .SetProperty(held => held.ReplacedAt, credential.ReplacedAt),
                cancellationToken)
            .ConfigureAwait(false);

        return recorded == 1;
    }

    /// <inheritdoc/>
    public async ValueTask<int> AttemptedAsync(
        DateTimeOffset at,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(Serialize, transaction: ambient.Transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Attempt,
                new { id = Guid.CreateVersion7(at), at },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        long counted = await ambient.Connection
            .ExecuteScalarAsync<long>(new CommandDefinition(
                Counted,
                new { since },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return (int)counted;
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset before, CancellationToken cancellationToken) =>
        await context.BreakGlassAttempts
            .Where(attempt => attempt.AttemptedAt < before)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private static BreakGlassCredential Held(BreakGlassCredentialRecord record) =>
        BreakGlassCredential.Held(
            record.Id,
            PasswordHash.Parse(record.Hash),
            record.IssuedBy,
            record.IssuedAt,
            record.ConsumedAt,
            record.ReplacedAt);
}
