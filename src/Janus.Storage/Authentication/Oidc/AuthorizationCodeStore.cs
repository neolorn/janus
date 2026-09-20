using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The authorization codes waiting to be exchanged, over the <c>oidc_codes</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>Implements AUTH-SESS-012, AUTH-KEY-003 and CONV-DESIGN-003.</remarks>
internal sealed class AuthorizationCodeStore(JanusDbContext context) : IAuthorizationCodeStore
{
    /// <inheritdoc/>
    public async ValueTask<AuthorizationCode?> FindAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        AuthorizationCodeRecord? record = await HeldAsync(fingerprint, cancellationToken)
            .ConfigureAwait(false);

        return record is null
            ? null
            : AuthorizationCode.Existing(
                record.Fingerprint,
                record.ClientId,
                record.Subject,
                record.Session,
                record.Redirect,
                record.Challenge,
                record.ChallengeMethod,
                record.Scope,
                record.Nonce,
                record.IssuedAt,
                record.ExpiresAt,
                record.SpentAt);
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(AuthorizationCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        await context.AuthorizationCodes
            .AddAsync(
                new AuthorizationCodeRecord
                {
                    Fingerprint = code.Fingerprint,
                    ClientId = code.ClientId,
                    Subject = code.Subject,
                    Session = code.Session,
                    Redirect = code.Redirect,
                    Challenge = code.Challenge,
                    ChallengeMethod = code.ChallengeMethod,
                    Scope = code.Scope,
                    Nonce = code.Nonce,
                    IssuedAt = code.IssuedAt,
                    ExpiresAt = code.ExpiresAt,
                    SpentAt = code.SpentAt,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(AuthorizationCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        AuthorizationCodeRecord record = await HeldAsync(code.Fingerprint, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The code has no row to carry the change.");

        record.SpentAt = code.SpentAt;
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.AuthorizationCodes
            .Where(code => code.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask<AuthorizationCodeRecord?> HeldAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken) =>
        await context.AuthorizationCodes.FindAsync([fingerprint], cancellationToken)
            .ConfigureAwait(false);
}
