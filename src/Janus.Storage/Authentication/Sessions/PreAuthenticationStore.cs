using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Sessions;

/// <summary>
/// What a browser carries before it holds a session, over the
/// <c>preauthentication_sessions</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements BFF-CSRF-005a and CONV-DESIGN-003. Nothing here is a personal field, so
/// no key is unwrapped and none is needed: the row is two fingerprints, two instants
/// and what the browser has in flight.
/// </remarks>
internal sealed class PreAuthenticationStore(JanusDbContext context) : IPreAuthenticationStore
{
    /// <inheritdoc/>
    public async ValueTask<PreAuthentication?> FindAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        PreAuthenticationRecord? record = await context.PreAuthenticationSessions
            .FindAsync([fingerprint], cancellationToken)
            .ConfigureAwait(false);

        return record is null
            ? null
            : PreAuthentication.Existing(
                record.Fingerprint,
                record.CsrfFingerprint,
                record.CreatedAt,
                record.ExpiresAt,
                record.Registration,
                record.Enrolment);
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(
        PreAuthentication preAuthentication,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preAuthentication);

        await context.PreAuthenticationSessions
            .AddAsync(
                new PreAuthenticationRecord
                {
                    Fingerprint = preAuthentication.Fingerprint,
                    CsrfFingerprint = preAuthentication.CsrfFingerprint,
                    CreatedAt = preAuthentication.CreatedAt,
                    ExpiresAt = preAuthentication.ExpiresAt,
                    Registration = preAuthentication.Registration,
                    Enrolment = preAuthentication.Enrolment,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(
        PreAuthentication preAuthentication,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preAuthentication);

        PreAuthenticationRecord record = await context.PreAuthenticationSessions
            .FindAsync([preAuthentication.Fingerprint], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The first contact has no row to carry the change.");

        record.ExpiresAt = preAuthentication.ExpiresAt;
        record.Registration = preAuthentication.Registration;
        record.Enrolment = preAuthentication.Enrolment;
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(byte[] fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        PreAuthenticationRecord? record = await context.PreAuthenticationSessions
            .FindAsync([fingerprint], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.PreAuthenticationSessions.Remove(record);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.PreAuthenticationSessions
            .Where(contact => contact.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
