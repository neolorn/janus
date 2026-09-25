using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What the records of processing found missing. The register is generated and never
/// maintained, so what a human still has to supply is reported on it rather than
/// silently left blank.
/// </summary>
/// <remarks>
/// Implements PRIV-ROPA-001, PRIV-ROPA-002, PRIV-PRIN-002 and CONV-ENUM-001. A
/// finding is a code and what it is about; the sentence a reader sees is the
/// frontend's (CONV-CONTENT-001).
/// </remarks>
public enum RegisterFinding
{
    /// <summary>
    /// The deployment has named no data owner.
    /// </summary>
    [JsonStringEnumMemberName("data-owner-missing")]
    DataOwnerMissing = 0,

    /// <summary>
    /// The deployment has stated no organisational security measures.
    /// </summary>
    [JsonStringEnumMemberName("organisational-measures-missing")]
    OrganisationalMeasuresMissing = 1,

    /// <summary>
    /// The deployment has named no assessment links.
    /// </summary>
    [JsonStringEnumMemberName("assessment-links-missing")]
    AssessmentLinksMissing = 2,

    /// <summary>
    /// A purpose rests on a basis that requires an assessment and names none.
    /// </summary>
    [JsonStringEnumMemberName("assessment-missing")]
    AssessmentMissing = 3,

    /// <summary>
    /// A processor is declared without a data protection agreement.
    /// </summary>
    [JsonStringEnumMemberName("agreement-missing")]
    AgreementMissing = 4,

    /// <summary>
    /// A purpose is over a data category whose period cannot be read: none is
    /// declared, or the one the deployment stated is refused.
    /// </summary>
    [JsonStringEnumMemberName("retention-missing")]
    RetentionMissing = 5,

    /// <summary>
    /// The deployment admits minors and no resource type declares the children's
    /// sensitivity category, so no row is in the children's column and the register
    /// says so rather than reporting no children's processing.
    /// </summary>
    [JsonStringEnumMemberName("children-undeclared")]
    ChildrenUndeclared = 6,
}
