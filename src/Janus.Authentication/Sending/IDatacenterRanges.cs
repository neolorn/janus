using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// What answers whether an address falls in a datacenter range: the file the
/// deployment supplies, read in process and matched against in memory.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-008. The bot defence asks only while <c>datacenterRange</c> is
/// among <c>abuse.botdefence.signals</c>, so an ask that finds no file held, or a file
/// older than <c>abuse.botdefence.ranges.maxage</c>, answers that the signal does not
/// fire and raises the degradation; it never reaches out to a third party, which would
/// hand every registering address to whoever runs the lookup.
/// </remarks>
internal interface IDatacenterRanges
{
    /// <summary>
    /// Whether an address falls in a range the file lists.
    /// </summary>
    /// <param name="ipAddress">
    /// The whole address the request arrived on, never the source its counts are kept
    /// under (AUTH-ABUSE-001).
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether it does; that it does not where no fresh file is held; or the failure
    /// where the degradation that says so could not be raised.
    /// </returns>
    ValueTask<Result<bool>> ContainsAsync(string ipAddress, CancellationToken cancellationToken);
}
