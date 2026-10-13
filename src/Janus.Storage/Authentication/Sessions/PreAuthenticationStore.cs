using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Sessions;

/// <summary>
/// What a browser carries before it holds a session, over the
/// <c>preauthentication_sessions</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="deployment">The deployment's data key, which the proof key is wrapped under.</param>
/// <remarks>
/// Implements BFF-CSRF-005a, BFF-SESS-006, CONV-DESIGN-003 and OPS-SEC-001. Nothing
/// here is a personal field: the row is two fingerprints, two instants and what the
/// browser has in flight. The one secret among them is the sign-on proof key, which
/// the server presents and the browser never sees, so it is wrapped on the way in and
/// unwrapped for the one caller that redeems a code with it.
/// </remarks>
internal sealed class PreAuthenticationStore(
    StoreContext context,
    DeploymentDataKeyStore deployment) : IPreAuthenticationStore
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
                record.Enrolment,
                await ReadAsync(record, cancellationToken).ConfigureAwait(false));
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

        await WriteAsync(record, preAuthentication.SignOn, cancellationToken).ConfigureAwait(false);
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

    // BFF-SESS-006: the three columns are written together, so a row either carries a
    // whole sign-on or carries none, and the proof key goes down wrapped.
    private async ValueTask WriteAsync(
        PreAuthenticationRecord record,
        SignOnAttempt? attempt,
        CancellationToken cancellationToken)
    {
        if (attempt is null)
        {
            record.SignOnState = null;
            record.SignOnVerifier = null;
            record.SignOnReturn = null;

            return;
        }

        byte[] verifier = Encoding.ASCII.GetBytes(attempt.Verifier);
        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            record.SignOnState = attempt.StateFingerprint;
            record.SignOnVerifier = PersonalFieldCipher.Wrap(verifier, deploymentKey);
            record.SignOnReturn = attempt.ReturnTo;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(verifier);
            CryptographicOperations.ZeroMemory(deploymentKey);
        }
    }

    private async ValueTask<SignOnAttempt?> ReadAsync(
        PreAuthenticationRecord record,
        CancellationToken cancellationToken)
    {
        if (record.SignOnState is not byte[] state
            || record.SignOnVerifier is not byte[] wrapped
            || record.SignOnReturn is not string returnTo)
        {
            return null;
        }

        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);
        byte[] verifier = [];

        try
        {
            verifier = PersonalFieldCipher.Unwrap(wrapped, deploymentKey);

            return new SignOnAttempt(state, Encoding.ASCII.GetString(verifier), returnTo);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(deploymentKey);
            CryptographicOperations.ZeroMemory(verifier);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.PreAuthenticationSessions
            .Where(contact => contact.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
