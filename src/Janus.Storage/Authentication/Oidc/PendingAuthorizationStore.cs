using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The authorizations browsers have waiting for their sign-in, over the
/// <c>pending_authorizations</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="deployment">The deployment's data key, which the parameters are wrapped under.</param>
/// <remarks>
/// Implements AUTH-SESS-012, CONV-DESIGN-003 and OPS-SEC-001 (D-224, D-226). The
/// parameters a client pushed may name the person it expects, so they go down wrapped
/// and come back only through the continuation that spends them. Each statement runs
/// at once, inside the unit of work in progress, so a carry lands before the
/// pre-authentication session it leaves is removed, and of two takes one spends it.
/// </remarks>
internal sealed class PendingAuthorizationStore(
    StoreContext context,
    DeploymentDataKeyStore deployment) : IPendingAuthorizationStore
{
    /// <inheritdoc/>
    public async ValueTask KeepAsync(PendingAuthorization pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        byte[] fingerprint = pending.PreAuthentication;

        _ = await context.PendingAuthorizations
            .Where(held => held.PreAuthentication == fingerprint)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        byte[] parameters = Encoding.UTF8.GetBytes(pending.Parameters);
        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await context.PendingAuthorizations
                .AddAsync(
                    new PendingAuthorizationRecord
                    {
                        Id = Guid.CreateVersion7(pending.CreatedAt),
                        PreAuthentication = fingerprint,
                        Parameters = PersonalFieldCipher.Wrap(parameters, deploymentKey),
                        CreatedAt = pending.CreatedAt,
                        ExpiresAt = pending.ExpiresAt,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(parameters);
            CryptographicOperations.ZeroMemory(deploymentKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> CarryAsync(
        byte[] preAuthentication,
        SessionId record,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preAuthentication);

        SessionId? bound = record;

        _ = await context.PendingAuthorizations
            .Where(held => held.Session == bound)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        int carried = await context.PendingAuthorizations
            .Where(held => held.PreAuthentication == preAuthentication && held.ExpiresAt > now)
            .ExecuteUpdateAsync(
                row => row
                    .SetProperty(held => held.Session, bound)
                    .SetProperty(held => held.PreAuthentication, (byte[]?)null),
                cancellationToken)
            .ConfigureAwait(false);

        return carried > 0;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> WaitsAsync(
        SessionId record,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        SessionId? bound = record;

        return await context.PendingAuthorizations
            .AnyAsync(held => held.Session == bound && held.ExpiresAt > now, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<string?> TakeAsync(
        SessionId record,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        SessionId? bound = record;

        PendingAuthorizationRecord? held = await context.PendingAuthorizations
            .AsNoTracking()
            .SingleOrDefaultAsync(pending => pending.Session == bound, cancellationToken)
            .ConfigureAwait(false);

        if (held is null)
        {
            return null;
        }

        // AUTH-SESS-012 AC10: the row goes whatever it held, and of two takes only the
        // one that removed it is answered with it.
        int spent = await context.PendingAuthorizations
            .Where(pending => pending.Id == held.Id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (spent == 0 || held.ExpiresAt <= now)
        {
            return null;
        }

        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);
        byte[] parameters = [];

        try
        {
            parameters = PersonalFieldCipher.Unwrap(held.Parameters, deploymentKey);

            return Encoding.UTF8.GetString(parameters);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(deploymentKey);
            CryptographicOperations.ZeroMemory(parameters);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.PendingAuthorizations
            .Where(pending => pending.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
