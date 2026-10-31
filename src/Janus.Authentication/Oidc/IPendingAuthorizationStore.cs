using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Oidc;

/// <summary>
/// Where the authorization a browser has waiting for its sign-in is kept, carried and
/// spent.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-012 and CONV-DESIGN-003 (D-224, D-226). A browser has one at
/// most, and so has a session record: keeping one replaces what the browser had, and
/// carrying one replaces what the record had. Every operation runs inside the unit of
/// work in progress.
/// </remarks>
internal interface IPendingAuthorizationStore
{
    /// <summary>
    /// Keeps one for the browser it names, replacing any the browser already had.
    /// </summary>
    /// <param name="pending">The pending authorization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of keeping it.</returns>
    ValueTask KeepAsync(PendingAuthorization pending, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the one a browser has over to the session record its sign-in
    /// established, replacing any the record already had.
    /// </summary>
    /// <param name="preAuthentication">What the browser's cookie fingerprints to.</param>
    /// <param name="record">The session record.</param>
    /// <param name="now">The instant its lifetime is judged at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether one within its lifetime was carried.</returns>
    ValueTask<bool> CarryAsync(
        byte[] preAuthentication,
        SessionId record,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether a session record has one waiting within its lifetime.
    /// </summary>
    /// <param name="record">The session record.</param>
    /// <param name="now">The instant its lifetime is judged at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it has.</returns>
    ValueTask<bool> WaitsAsync(SessionId record, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Spends the one a session record has, which a second take of finds nothing.
    /// </summary>
    /// <param name="record">The session record.</param>
    /// <param name="now">The instant its lifetime is judged at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Its parameters, or nothing where none within its lifetime was held.</returns>
    ValueTask<string?> TakeAsync(SessionId record, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Ends every one whose lifetime has run out.
    /// </summary>
    /// <param name="now">The instant the lifetimes are judged at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were ended.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
