using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Sessions;

/// <summary>
/// Where what a browser carries before it holds a session is read and written.
/// </summary>
/// <remarks>
/// Implements BFF-CSRF-005a and CONV-DESIGN-003. A row is found by what the cookie's
/// token fingerprints to and by nothing else, so the browser's value is never stored.
/// </remarks>
internal interface IPreAuthenticationStore
{
    /// <summary>
    /// The pre-authentication session a cookie answers to.
    /// </summary>
    /// <param name="fingerprint">What the token the cookie carried fingerprints to.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The session, or nothing where none answers to it.</returns>
    ValueTask<PreAuthentication?> FindAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Issues one.
    /// </summary>
    /// <param name="preAuthentication">The session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of issuing it.</returns>
    ValueTask AddAsync(PreAuthentication preAuthentication, CancellationToken cancellationToken);

    /// <summary>
    /// Carries one as it now stands onto its row.
    /// </summary>
    /// <param name="preAuthentication">The session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordAsync(PreAuthentication preAuthentication, CancellationToken cancellationToken);

    /// <summary>
    /// Ends one, which is what authentication does rather than leaving a second
    /// session beside the real one (BFF-CSRF-005a AC3).
    /// </summary>
    /// <param name="fingerprint">What the token the cookie carried fingerprints to.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of ending it.</returns>
    ValueTask RemoveAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Ends every one whose lifetime has run out.
    /// </summary>
    /// <param name="now">The instant the lifetimes are judged at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were ended.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
