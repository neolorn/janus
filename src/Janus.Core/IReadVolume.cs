using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// Where the records a person is shown are counted, so that one reading far beyond
/// their own pattern is noticed. A host that applies the gate's filter to a query of
/// its own, or exports records, says here how many records the person was given.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-005 and LIB-API-004. Counts are kept per acting person and per
/// calendar day in <c>privacy.calendar.timezone</c>, and compared with that person's own
/// daily mean over <c>exfiltration.readvolume.baselinewindow</c>, never with a fixed
/// number; work done by a system principal is nobody's reading and is not counted.
/// </remarks>
public interface IReadVolume
{
    /// <summary>
    /// Counts the records one gate-filtered query or one export returned.
    /// </summary>
    /// <param name="context">Who was given them.</param>
    /// <param name="records">How many records were returned.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or <c>api.request.malformed</c> naming <c>records</c> where the count is
    /// negative, or the refusal met reading the zone the day is counted in.
    /// </returns>
    ValueTask<Result> ReturnedAsync(AccessContext context, int records, CancellationToken cancellationToken);
}
