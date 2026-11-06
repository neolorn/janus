using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The records of processing: generated on demand from the declaration, the
/// configuration and the grants, and the one place the three fields a person supplies
/// are kept.
/// </summary>
/// <remarks>
/// Implements PRIV-PRIN-002, PRIV-ROPA-001 and LIB-API-005. There is no stored
/// inventory of processing activities: adding a purpose to the declaration changes
/// the next generated register and nothing else is edited.
/// No host implements this interface: a host calls it, and a member the library adds to
/// it is an additive change (D-268).
/// </remarks>
public interface IProcessingRecords
{
    /// <summary>
    /// Generates the records of processing as they stand.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The register, or the refusal.</returns>
    ValueTask<Result<ProcessingRegister>> GenerateAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the three fields of the register a person supplies.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="record">What they supplied.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or the refusal: <c>api.request.malformed</c> naming <c>dataOwner</c> or
    /// <c>organizationalSecurityMeasures</c> where the statement is given blank or
    /// longer than 1024 characters after trimming. One omitted is cleared.
    /// </returns>
    ValueTask<Result> DeclareAsync(
        AccessContext context,
        ComplianceRecord record,
        CancellationToken cancellationToken);
}
