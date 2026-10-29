using System;
using System.Globalization;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Records;

/// <summary>
/// How a reference a collection gave a person record is stored.
/// </summary>
/// <remarks>
/// Implements REC-REC-001, REC-COL-002, PRIV-RIGHT-005c and CONV-DESIGN-003. A reference
/// is held as a keyed fingerprint and in no other form, so no column here can be read
/// back into it.
/// </remarks>
internal sealed class RecordReferenceConfiguration : IEntityTypeConfiguration<RecordReferenceRow>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RecordReferenceRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("person_record_references", table =>
        {
            table.HasCheckConstraint(
                "ck_person_record_references_fingerprint",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"octet_length(fingerprint) = {Fingerprint.Length}"));

            table.HasCheckConstraint(
                "ck_person_record_references_subject_not_max_uuid",
                MaxUuid.Refused("subject"));
        });

        builder.HasKey(reference => reference.Id).HasName("pk_person_record_references");

        builder.Property(reference => reference.Id).HasColumnName("reference_id");

        builder.Property(reference => reference.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(reference => reference.Fingerprint).HasColumnName("fingerprint");
        builder.Property(reference => reference.FingerprintVersion).HasColumnName("fingerprint_version");
        builder.Property(reference => reference.AddedAt).HasColumnName("added_at");

        builder.HasOne<PersonRecordRow>()
            .WithMany()
            .HasForeignKey(reference => reference.Subject)
            .HasConstraintName("fk_person_record_references_subject")
            .OnDelete(DeleteBehavior.Restrict);

        // REC-ACC-001: the records holding a reference are found by its fingerprint; a
        // neutralised one is outside the index.
        builder.HasIndex(reference => reference.Fingerprint)
            .HasDatabaseName("ix_person_record_references_fingerprint")
            .HasFilter("fingerprint <> decode(repeat('00', " + Fingerprint.Length + "), 'hex')");

        builder.HasIndex(reference => reference.Subject)
            .HasDatabaseName("ix_person_record_references_subject");
    }
}
