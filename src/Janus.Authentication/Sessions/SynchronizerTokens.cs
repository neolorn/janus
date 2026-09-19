using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Sessions;

/// <summary>
/// Whether the synchronizer token a request presented is the one bound to the session
/// the request arrived on.
/// </summary>
/// <param name="sessions">Where sessions are held.</param>
/// <remarks>
/// Implements BFF-CSRF-001 and BFF-CSRF-006. The token is validated against the
/// record, not against a second copy of itself, because this is a stateful backend
/// and the session is what it binds to. Neither value is held: the row carries what
/// each fingerprints to, and the comparison is constant time, so neither how much of
/// a token matched nor whether a session exists can be read from how long it took.
/// </remarks>
internal sealed class SynchronizerTokens(ISessionStore sessions)
{
    /// <summary>
    /// Whether the pair a request carried belong together.
    /// </summary>
    /// <param name="secret">What the session cookie carried.</param>
    /// <param name="presented">What the request presented as the token.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether they do. A secret that answers to no session, a session with no token
    /// bound, and a token belonging to another session all answer no.
    /// </returns>
    public async ValueTask<bool> MatchesAsync(
        OpaqueToken secret,
        OpaqueToken presented,
        CancellationToken cancellationToken)
    {
        Session? session = await sessions
            .FindByFingerprintAsync(secret.Fingerprint(), cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return false;
        }

        byte[]? bound = await sessions
            .CsrfFingerprintAsync(session.Id, cancellationToken)
            .ConfigureAwait(false);

        return bound is not null
            && CryptographicOperations.FixedTimeEquals(bound, presented.Fingerprint());
    }
}
