using System;

namespace Janus.Storage.Privacy.Records;

/// <summary>
/// The <c>compliance_records</c> row: the three fields of the records of processing a
/// person supplies, for the one deployment.
/// </summary>
/// <remarks>
/// Implements PRIV-ROPA-001. One row, held at a fixed identifier, because a
/// deployment has one register and a second row would be a second answer to what the
/// regulator asked once.
/// </remarks>
internal sealed class ComplianceRow
{
    /// <summary>
    /// What the one row is held at.
    /// </summary>
    public const int Only = 1;

    /// <summary>The <c>id</c> column, which is always <see cref="Only"/>.</summary>
    public int Id { get; set; } = Only;

    /// <summary>The <c>data_owner</c> column.</summary>
    public string? DataOwner { get; set; }

    /// <summary>The <c>organisational_measures</c> column.</summary>
    public string? OrganisationalMeasures { get; set; }

    /// <summary>The <c>assessment_links</c> column, the references as a JSON array.</summary>
    public string AssessmentLinks { get; set; } = "[]";

    /// <summary>The <c>updated_at</c> column.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
