using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// Where sessions are read and written. The cookie's value is never stored: the row
/// holds its fingerprint, so a dump of the table yields no usable session.
/// </summary>
/// <remarks>Implements AUTH-SESS-001, AUTH-SESS-003 and CONV-DESIGN-003.</remarks>
internal interface ISessionStore
{
    /// <summary>
    /// Reads one session.
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The session, or nothing where there is none.</returns>
    ValueTask<Session?> FindAsync(SessionId id, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the session a presented secret belongs to.
    /// </summary>
    /// <param name="fingerprint">The fingerprint of the presented secret.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The session, or nothing where no live secret matches.</returns>
    ValueTask<Session?> FindByFingerprintAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Records a session and the fingerprint of the secret issued with it.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="fingerprint">The fingerprint of its secret.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddAsync(Session session, byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change the session made onto its row.
    /// </summary>
    /// <param name="session">The session as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordAsync(Session session, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the secret a session answers to, invalidating the one before it
    /// rather than leaving it orphaned.
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="fingerprint">The fingerprint of the new secret.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of replacing it.</returns>
    ValueTask ReplaceSecretAsync(SessionId id, byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Every session of an account that has not ended.
    /// </summary>
    /// <param name="subject">Whose sessions.</param>
    /// <param name="at">The instant expiry is judged at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The sessions.</returns>
    ValueTask<IReadOnlyList<Session>> LiveOfAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ends every session standing on one record.
    /// </summary>
    /// <param name="spine">The record.</param>
    /// <param name="at">When they ended.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of ending them.</returns>
    ValueTask EndSpineAsync(SessionId spine, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>
    /// Ends every session of an account, which is the operation an account's
    /// suspension and an offboarding use.
    /// </summary>
    /// <param name="subject">Whose sessions.</param>
    /// <param name="at">When they ended.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of ending them.</returns>
    ValueTask EndAccountAsync(SubjectId subject, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>
    /// Ends every session in the deployment, which is the emergency operation and
    /// nothing an ordinary path reaches.
    /// </summary>
    /// <param name="at">When they ended.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of ending them.</returns>
    ValueTask EndEveryAsync(DateTimeOffset at, CancellationToken cancellationToken);
}
