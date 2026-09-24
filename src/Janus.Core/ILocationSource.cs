using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What supplies the local IP-to-city database a session's city is resolved from. The
/// library fetches no database itself: a deployment registers one of these, and until
/// it does no session carries a location.
/// </summary>
/// <remarks>
/// <para>
/// Implements LIB-EXT-001, INT-GEN-006 and AUTH-SESS-013. The library opens the file the
/// first time a process resolves an address and again every
/// <c>location.database.refresh</c>, reads it whole, and resolves every address against
/// the copy it holds, so no address a person signs in from leaves the process to be
/// resolved. Opening it reads a file the deployment holds and reaches no network;
/// keeping that file current is the deployment's.
/// </para>
/// <para>
/// The file is UTF-8 text. A line <c># YYYY-MM-DD</c> gives the date the data was
/// produced, which <c>location.database.maxage</c> is judged against. Every other line
/// that is not empty is one range of addresses, six fields separated by tabs: the first
/// address, the last address (inclusive, of the same family), the country as an
/// ISO 3166-1 alpha-2 code or nothing, the city or nothing, and the latitude and the
/// longitude of the city in decimal degrees, present exactly where the city is. Ranges
/// do not overlap. A file with no date, or with a line that cannot be read, is refused
/// whole and the copy held before it is kept.
/// </para>
/// </remarks>
public interface ILocationSource
{
    /// <summary>
    /// Opens the database file as it now stands.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The file, which the library reads to its end and disposes, or the failure where
    /// none could be had.
    /// </returns>
    ValueTask<Result<Stream>> OpenAsync(CancellationToken cancellationToken);
}
