using System;
using Janus.Core;

namespace Janus.Storage.Identity.Records;

/// <summary>
/// The <c>person_records</c> row.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-003. What EF Core maps is this row and never the person record
/// itself; the store translates between the two.
/// </remarks>
internal sealed class PersonRecordRow
{
    /// <summary>
    /// The subject column, which is this table's key and the column every personal
    /// field of the record names as its subject.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>organization</c> column.
    /// </summary>
    public OrganizationId Organization { get; set; }

    /// <summary>
    /// The <c>origin</c> column.
    /// </summary>
    public PersonRecordOrigin Origin { get; set; }

    /// <summary>
    /// The <c>state</c> column.
    /// </summary>
    public PersonRecordState State { get; set; }

    /// <summary>
    /// The <c>created_at</c> column.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The <c>language</c> column.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// The <c>enc_name</c> column, under the record's key.
    /// </summary>
    public byte[]? Name { get; set; }

    /// <summary>
    /// The <c>restriction_held</c> column.
    /// </summary>
    public bool RestrictionHeld { get; set; }

    /// <summary>
    /// The <c>deleting_by</c> column.
    /// </summary>
    public DeletionOrigin? DeletingBy { get; set; }

    /// <summary>
    /// The <c>deleting_since</c> column.
    /// </summary>
    public DateTimeOffset? DeletingSince { get; set; }

    /// <summary>
    /// The <c>deletion_held</c> column.
    /// </summary>
    public DeletionOrigin? DeletionHeld { get; set; }

    /// <summary>
    /// The <c>deletion_held_since</c> column.
    /// </summary>
    public DateTimeOffset? DeletionHeldSince { get; set; }

    /// <summary>
    /// The <c>attached_to</c> column.
    /// </summary>
    public SubjectId? AttachedTo { get; set; }

    /// <summary>
    /// The <c>attached_at</c> column.
    /// </summary>
    public DateTimeOffset? AttachedAt { get; set; }

    /// <summary>
    /// The <c>adult_affirmed</c> column.
    /// </summary>
    public bool? AdultAffirmed { get; set; }

    /// <summary>
    /// The <c>age_group</c> column.
    /// </summary>
    public AgeGroup? AgeGroup { get; set; }

    /// <summary>
    /// The <c>answered_age_at</c> column.
    /// </summary>
    public DateTimeOffset? AnsweredAgeAt { get; set; }

    /// <summary>
    /// The <c>enc_date_of_birth</c> column, under the record's key.
    /// </summary>
    public byte[]? DateOfBirth { get; set; }
}
