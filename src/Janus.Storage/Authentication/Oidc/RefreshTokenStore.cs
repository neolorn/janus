using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The refresh tokens, over the <c>oidc_refresh_tokens</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>Implements AUTH-OIDC-003, AUTH-KEY-003 and CONV-DESIGN-003.</remarks>
internal sealed class RefreshTokenStore(JanusDbContext context) : IRefreshTokenStore
{
    /// <inheritdoc/>
    public async ValueTask<RefreshToken?> FindAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        RefreshTokenRecord? record = await HeldAsync(fingerprint, cancellationToken).ConfigureAwait(false);

        return record is null ? null : Token(record);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RefreshToken>> OfAsync(
        RefreshFamilyId family,
        CancellationToken cancellationToken) =>
        await context.RefreshTokens
            .Where(token => token.Family == family)
            .OrderBy(token => token.IssuedAt)
            .Select(token => Token(token))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        await context.RefreshTokens
            .AddAsync(
                new RefreshTokenRecord
                {
                    Fingerprint = token.Fingerprint,
                    Family = token.Family,
                    ClientId = token.ClientId,
                    Subject = token.Subject,
                    Session = token.Session,
                    Scope = token.Scope,
                    IssuedAt = token.IssuedAt,
                    ExpiresAt = token.ExpiresAt,
                    ConsumedAt = token.ConsumedAt,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        RefreshTokenRecord record = await HeldAsync(token.Fingerprint, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The token has no row to carry the change.");

        record.ConsumedAt = token.ConsumedAt;
    }

    /// <inheritdoc/>
    public async ValueTask RemoveFamilyAsync(
        RefreshFamilyId family,
        CancellationToken cancellationToken)
    {
        await context.RefreshTokens
            .Where(token => token.Family == family)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.RefreshTokens
            .Where(token => token.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private static RefreshToken Token(RefreshTokenRecord record) =>
        RefreshToken.Existing(
            record.Fingerprint,
            record.Family,
            record.ClientId,
            record.Subject,
            record.Session,
            record.Scope,
            record.IssuedAt,
            record.ExpiresAt,
            record.ConsumedAt);

    private async ValueTask<RefreshTokenRecord?> HeldAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken) =>
        await context.RefreshTokens.FindAsync([fingerprint], cancellationToken).ConfigureAwait(false);
}
