using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What supplies the file of datacenter ranges the bot defence matches a registration's
/// address against. The library ships no ranges and fetches none: a deployment registers
/// one of these, and until it does the <c>datacenterRange</c> signal does not fire.
/// </summary>
/// <remarks>
/// <para>
/// Implements LIB-HOST-001, LIB-EXT-001 and AUTH-ABUSE-008. The library opens the file
/// the first time a process judges an address and again every
/// <c>abuse.botdefence.ranges.refresh</c>, reads it whole, and matches every address
/// against the copy it holds, so no address a person registers from leaves the process
/// to be judged. Opening it reads a file the deployment holds and reaches no network;
/// keeping that file current is the deployment's. While <c>datacenterRange</c> is among
/// <c>abuse.botdefence.signals</c> and no file is held, or the file held is older than
/// <c>abuse.botdefence.ranges.maxage</c>, the signal does not fire and
/// <c>degradation</c> is raised, until the deployment supplies a file or takes the
/// signal out of the set.
/// </para>
/// <para>
/// The file is UTF-8 text. A line <c># YYYY-MM-DD</c> gives the date the data was
/// produced, which <c>abuse.botdefence.ranges.maxage</c> is judged against. Every other
/// line that is not empty is one range of addresses, two fields separated by a tab: the
/// first address and the last address (inclusive, of the same family). Ranges do not
/// overlap. A file with no date, with a line that cannot be read, or with a range that
/// is of mixed family, reversed or overlapping is refused whole and the copy held before
/// it is kept.
/// </para>
/// </remarks>
public interface IDatacenterRangeSource
{
    /// <summary>
    /// Opens the range file as it now stands.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The file, which the library reads to its end and disposes, or the failure where
    /// none could be had.
    /// </returns>
    ValueTask<Result<Stream>> OpenAsync(CancellationToken cancellationToken);
}
