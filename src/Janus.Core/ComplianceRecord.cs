using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// The three fields of the records of processing that are declared by a person rather
/// than derived from the deployment, as the deployment last stated them.
/// </summary>
/// <param name="DataOwner">Who owns the data inside the organisation.</param>
/// <param name="OrganisationalSecurityMeasures">
/// The measures that are not the software's: training, access review, clear desk.
/// </param>
/// <param name="AssessmentLinks">
/// The legitimate interest, data protection impact and transfer impact assessments.
/// </param>
/// <remarks>
/// Implements PRIV-ROPA-001 and chapter 09 section 8a. Everything else on the
/// register is derived, so these three are the whole of what a person maintains.
/// </remarks>
public sealed record ComplianceRecord(
    string? DataOwner,
    string? OrganisationalSecurityMeasures,
    IReadOnlyList<string> AssessmentLinks);
