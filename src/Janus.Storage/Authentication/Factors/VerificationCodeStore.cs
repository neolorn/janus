using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// The codes outstanding, over the <c>verification_codes</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>Implements AUTH-FACT-004 AC2 and CONV-DESIGN-003.</remarks>
internal sealed class VerificationCodeStore(JanusDbContext context) : IVerificationCodeStore
{
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
