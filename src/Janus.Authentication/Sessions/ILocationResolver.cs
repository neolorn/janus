using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// Where the city shown on a session comes from: the address the session was used
/// from, resolved in process against a local database.
/// </summary>
/// <remarks>
/// Implements INT-GEN-006 and AUTH-SESS-013. No caller supplies a place: the session
/// records the address it already has and the resolver says what it can about it, so
/// nothing outside the library can write a city into a session. A database that is
/// missing or stale answers no location and raises the degradation; it never reaches
/// out to a third party, which would hand every sign-in address to whoever runs the
/// lookup.
/// </remarks>
internal interface ILocationResolver
{
    /// <summary>
    /// Where an IP address is, no finer than a city.
    /// </summary>
    /// <param name="ipAddress">The IP address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The location and where its city lies, nothing where the database could not say,
    /// or the failure where the resolver could not report what it had to report.
    /// </returns>
    ValueTask<Result<ResolvedLocation?>> ResolveAsync(
        string ipAddress,
        CancellationToken cancellationToken);
}
