using System.Collections.Generic;

namespace Janus.Hosting.Privacy;

/// <summary>
/// The body of <c>PUT /admin/compliance/assessments</c>: the three fields of the
/// records of processing a person supplies.
/// </summary>
/// <param name="DataOwner">Who owns the data inside the organisation.</param>
/// <param name="OrganizationalSecurityMeasures">The measures that are not the software's.</param>
/// <param name="AssessmentLinks">The LIA, DPIA and TIA references.</param>
/// <remarks>Implements PRIV-ROPA-001 and chapter 09 section 8a.</remarks>
internal sealed record AssessmentsRequest(
    string? DataOwner,
    string? OrganizationalSecurityMeasures,
    IReadOnlyList<string>? AssessmentLinks);
