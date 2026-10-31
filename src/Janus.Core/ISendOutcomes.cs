using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// Where the host reports what became of a message the library handed it.
/// </summary>
/// <remarks>
/// Implements INT-SMS-005 (D-225). A report takes no access context and meets no gate: it
/// can only make a person less restricted, and marks nothing verified.
/// </remarks>
public interface ISendOutcomes
{
    /// <summary>
    /// Reports a message's final outcome. A message that failed releases what it counted
    /// against the restrictions; a delivered <c>secondfactor</c> SMS releases its count
    /// from each restriction marked <c>resend</c>.
    /// </summary>
    /// <param name="reference">The reference the message's <see cref="SendRequest"/> carried.</param>
    /// <param name="delivered">Whether the message was delivered, or failed for good.</param>
    /// <param name="cancellationToken">Abandons the report, which then changes nothing.</param>
    /// <returns>
    /// Whether the reference named a message the library holds, the report then applied
    /// once; false, with nothing changed, for any other reference; or the failure that
    /// stopped the report.
    /// </returns>
    ValueTask<Result<bool>> ReportAsync(
        SendReference reference,
        bool delivered,
        CancellationToken cancellationToken);
}
