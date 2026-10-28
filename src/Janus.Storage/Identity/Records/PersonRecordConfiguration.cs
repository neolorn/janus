using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Records;

/// <summary>
/// How a person record is stored.
/// </summary>
/// <remarks>
/// Implements REC-REC-001, REC-REC-002, REC-AGE-001, REC-ATT-001, IDN-PRIN-003 and
/// CONV-DESIGN-003. The row is never removed: erasure destroys the key its two encrypted
/// columns are written under and leaves it <c>deleted</c>. Its subject's key row is its
/// parent, named by a constraint the migration writes, since the key's identifier is a
/// type of its own.
/// </remarks>
internal sealed class PersonRecordConfiguration : IEntityTypeConfiguration<PersonRecordRow>
{
    /// <summary>
    /// The table the two encrypted columns name as their location.
    /// </summary>
    public const string Table = "person_records";

    /// <summary>
    /// The column the name is written to.
    /// </summary>
    public const string NameColumn = "enc_name";

    /// <summary>
    /// The column the date of birth is written to.
    /// </summary>
    public const string DateOfBirthColumn = "enc_date_of_birth";

    // Chapter 10 section 5.12b: a takedown holds the deletion it found, which is never
    // its own.
    private static readonly string[] HeldDeletions =
    [
        VocabularyConverter<DeletionOrigin>.Write(DeletionOrigin.OutOfBandRequest),
        VocabularyConverter<DeletionOrigin>.Write(DeletionOrigin.Self),
    ];

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PersonRecordRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_person_records_state",
                Vocabulary.Admits<PersonRecordState>("state"));
            table.HasCheckConstraint(
                "ck_person_records_origin",
                Vocabulary.Admits<PersonRecordOrigin>("origin"));

            // REC-REC-002: a restriction is held only through a window it comes back from.
            table.HasCheckConstraint(
                "ck_person_records_restriction_held",
                "NOT restriction_held OR state = 'deleting'");
            table.HasCheckConstraint(
                "ck_person_records_deleting_by",
                "deleting_by IS NULL OR "
                    + Vocabulary.Admits<DeletionOrigin>("deleting_by"));
            table.HasCheckConstraint(
                "ck_person_records_deleting",
                "(deleting_by IS NULL) = (deleting_since IS NULL)");
            table.HasCheckConstraint(
                "ck_person_records_deletion_held",
                "deletion_held IS NULL OR "
                    + Vocabulary.Admits("deletion_held", HeldDeletions));
            table.HasCheckConstraint(
                "ck_person_records_deletion_held_state",
                "deletion_held IS NULL OR (state = 'deleting' AND deleting_by = 'takedown')");
            table.HasCheckConstraint(
                "ck_person_records_deletion_held_since",
                "(deletion_held IS NULL) = (deletion_held_since IS NULL)");

            // REC-ATT-001: an attachment is its account and its instant together.
            table.HasCheckConstraint(
                "ck_person_records_attached",
                "(attached_to IS NULL) = (attached_at IS NULL)");

            // REC-AGE-001: the answer is an affirmation or a band, never both, and its
            // instant comes with whichever it recorded; the date comes with an answer.
            table.HasCheckConstraint(
                "ck_person_records_age_group",
                "age_group IS NULL OR " + Vocabulary.Admits<AgeGroup>("age_group"));
            table.HasCheckConstraint(
                "ck_person_records_age_answer",
                "adult_affirmed IS NULL OR age_group IS NULL");
            table.HasCheckConstraint(
                "ck_person_records_answered_age_at",
                "(answered_age_at IS NULL) = (adult_affirmed IS NULL AND age_group IS NULL)");
            table.HasCheckConstraint(
                "ck_person_records_date_of_birth",
                "enc_date_of_birth IS NULL OR answered_age_at IS NOT NULL");

            table.HasCheckConstraint(
                "ck_person_records_subject_not_max_uuid",
                MaxUuid.Refused("subject"));
            table.HasCheckConstraint(
                "ck_person_records_attached_to_not_max_uuid",
                MaxUuid.Refused("attached_to"));
        });

        builder.HasKey(record => record.Subject).HasName("pk_person_records");

        builder.Property(record => record.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(record => record.Organization)
            .HasColumnName("organization")
            .HasConversion(id => id.Value, value => new OrganizationId(value));

        builder.Property(record => record.Origin)
            .HasColumnName("origin")
            .HasConversion(new VocabularyConverter<PersonRecordOrigin>());

        builder.Property(record => record.State)
            .HasColumnName("state")
            .HasConversion(new VocabularyConverter<PersonRecordState>());

        builder.Property(record => record.CreatedAt).HasColumnName("created_at");
        builder.Property(record => record.Language).HasColumnName("language");
        builder.Property(record => record.Name).HasColumnName(NameColumn);
        builder.Property(record => record.RestrictionHeld).HasColumnName("restriction_held");

        builder.Property(record => record.DeletingBy)
            .HasColumnName("deleting_by")
            .HasConversion(new VocabularyConverter<DeletionOrigin>());

        builder.Property(record => record.DeletingSince).HasColumnName("deleting_since");

        builder.Property(record => record.DeletionHeld)
            .HasColumnName("deletion_held")
            .HasConversion(new VocabularyConverter<DeletionOrigin>());

        builder.Property(record => record.DeletionHeldSince).HasColumnName("deletion_held_since");

        builder.Property(record => record.AttachedTo)
            .HasColumnName("attached_to")
            .HasConversion(subject => subject!.Value.Value, value => new SubjectId(value));

        builder.Property(record => record.AttachedAt).HasColumnName("attached_at");
        builder.Property(record => record.AdultAffirmed).HasColumnName("adult_affirmed");

        builder.Property(record => record.AgeGroup)
            .HasColumnName("age_group")
            .HasConversion(new VocabularyConverter<AgeGroup>());

        builder.Property(record => record.AnsweredAgeAt).HasColumnName("answered_age_at");
        builder.Property(record => record.DateOfBirth).HasColumnName(DateOfBirthColumn);

        // REC-REC-001: the organization is named at the record's writing and never
        // changed, and no organization row is ever removed from under it.
        builder.HasOne<OrganizationRecord>()
            .WithMany()
            .HasForeignKey(record => record.Organization)
            .HasConstraintName("fk_person_records_organization")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(record => record.AttachedTo)
            .HasConstraintName("fk_person_records_attached_to")
            .OnDelete(DeleteBehavior.Restrict);

        // REC-ADM-001: staff read an organization's records together.
        builder.HasIndex(record => record.Organization)
            .HasDatabaseName("ix_person_records_organization");

        // REC-ATT-002: an account's attached records are read together.
        builder.HasIndex(record => record.AttachedTo)
            .HasDatabaseName("ix_person_records_attached_to")
            .HasFilter("attached_to IS NOT NULL");

        // REC-REC-002: the sweep reads the records whose window is running.
        builder.HasIndex(record => record.DeletingSince)
            .HasDatabaseName("ix_person_records_deleting_since")
            .HasFilter("deleting_since IS NOT NULL");
    }
}
