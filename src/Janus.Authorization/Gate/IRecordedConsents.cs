using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where the gate reads what a subject has consented to.
/// </summary>
/// <remarks>
/// Implements PRIV-SENS-002 and PRIV-SENS-002a. The record belongs to the privacy
/// area, which is out of this one's reach (CONV-LAYOUT-001), so the gate reads the
/// one record it evaluates through a port of its own (CONV-DESIGN-003).
/// </remarks>
internal interface IRecordedConsents
{
    /// <summary>
    /// What the subject decided about one purpose.
    /// </summary>
    /// <param name="subject">The account.</param>
    /// <param name="purpose">The purpose the action is done for.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The record, or nothing where the subject has never decided about the purpose,
    /// which is a consent that was not given and never a consent assumed.
    /// </returns>
    ValueTask<ConsentRecord?> OfAsync(
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken);
}
