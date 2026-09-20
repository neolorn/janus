using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// Where registration sessions are held. Nothing else in the deployment knows one
/// exists: it reserves no identifier and writes into no other table.
/// </summary>
/// <remarks>
/// Implements REG-SESS-001 and CONV-DESIGN-003. A session that has lapsed is swept
/// and leaves nothing behind, which is why removal is a deletion and not a state.
/// </remarks>
internal interface IRegistrationSessionStore
{
    /// <summary>
    /// The session an identifier names.
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The session, or nothing where none answers to it.</returns>
    ValueTask<RegistrationSession?> FindAsync(
        RegistrationSessionId id,
        CancellationToken cancellationToken);

    /// <summary>
    /// The session that sent a link, found by what the token fingerprints to.
    /// </summary>
    /// <param name="fingerprint">The fingerprint of the token the message carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The session, or nothing where no live session sent it.</returns>
    ValueTask<RegistrationSession?> FindByLinkAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a newly created session.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddAsync(RegistrationSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the session as it now stands onto its row.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordAsync(RegistrationSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a session and everything it staged.
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing it.</returns>
    ValueTask RemoveAsync(RegistrationSessionId id, CancellationToken cancellationToken);

    /// <summary>
    /// Removes every session that has lapsed.
    /// </summary>
    /// <param name="now">The instant to judge them at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were swept.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
