using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Factors;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// The codes outstanding, over the <c>verification_codes</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="connections">The connection and transaction the operation holds.</param>
/// <remarks>Implements AUTH-FACT-004 AC2, AUTH-FACT-004 AC4 and CONV-DESIGN-003.</remarks>
internal sealed class VerificationCodeStore(StoreContext context, DataConnections connections)
    : IVerificationCodeStore
{
    private const string Hold =
        """
        SELECT 1 FROM identity.verification_codes WHERE holder = @holder FOR UPDATE;
        """;

    /// <inheritdoc/>
    public async ValueTask<VerificationCode?> FindAsync(
        byte[] holder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(holder);

        VerificationCodeRecord? record = await context.VerificationCodes
            .FindAsync([holder], cancellationToken)
            .ConfigureAwait(false);

        return record is null
            ? null
            : VerificationCode.Stored(
                record.Holder,
                record.Code,
                record.IssuedAt,
                record.ExpiresAt,
                record.Attempts);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is running.</exception>
    public async ValueTask<VerificationCode?> FindForUpdateAsync(
        byte[] holder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(holder);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        if (ambient.Transaction is null)
        {
            throw new InvalidOperationException("A code is held only inside the operation's transaction.");
        }

        _ = await ambient.Connection
            .ExecuteScalarAsync<int?>(new CommandDefinition(
                Hold,
                new { holder },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        // A row the context already tracks was read before the lock, so it is read again:
        // what the try decides on is the row as it stood when the lock was taken.
        if (context.VerificationCodes.Local
                .FirstOrDefault(record => CryptographicOperations.FixedTimeEquals(record.Holder, holder))
            is VerificationCodeRecord tracked)
        {
            await context.Entry(tracked).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        return await FindAsync(holder, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(VerificationCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        await context.VerificationCodes
            .AddAsync(
                new VerificationCodeRecord
                {
                    Holder = code.Holder,
                    Code = code.Code,
                    IssuedAt = code.IssuedAt,
                    ExpiresAt = code.ExpiresAt,
                    Attempts = code.Attempts,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(VerificationCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        VerificationCodeRecord record = await context.VerificationCodes
            .FindAsync([code.Holder], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The code has no row to carry the change.");

        record.Attempts = code.Attempts;
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(byte[] holder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(holder);

        VerificationCodeRecord? record = await context.VerificationCodes
            .FindAsync([holder], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.VerificationCodes.Remove(record);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.VerificationCodes
            .Where(code => code.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
