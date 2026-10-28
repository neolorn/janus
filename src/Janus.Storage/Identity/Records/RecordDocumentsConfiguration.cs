using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Records;

/// <summary>
/// How what a collection presented to the person, and what they accepted, is stored.
/// </summary>
/// <remarks>
/// Implements REC-REC-001, REC-COL-002, PRIV-CONS-008a and CONV-DESIGN-003. Each
/// collection adds one row; none is changed or removed, so every presentation and
/// acceptance stays with its instant.
/// </remarks>
internal sealed class RecordDocumentsConfiguration : IEntityTypeConfiguration<RecordDocumentsRow>
{
    // The length of a legal document's version, as the accounts table holds it.
    private const int DocumentVersionLength = 64;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RecordDocumentsRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("person_record_documents", table => table.HasCheckConstraint(
            "ck_person_record_documents_subject_not_max_uuid",
            MaxUuid.Refused("subject")));

        builder.HasKey(documents => documents.Id).HasName("pk_person_record_documents");

        builder.Property(documents => documents.Id).HasColumnName("documents_id");

        builder.Property(documents => documents.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(documents => documents.NoticeVersion)
            .HasColumnName("notice_version")
            .HasMaxLength(DocumentVersionLength);

        builder.Property(documents => documents.TermsVersion)
            .HasColumnName("terms_version")
            .HasMaxLength(DocumentVersionLength);

        builder.Property(documents => documents.RecordedAt).HasColumnName("recorded_at");

        builder.HasOne<PersonRecordRow>()
            .WithMany()
            .HasForeignKey(documents => documents.Subject)
            .HasConstraintName("fk_person_record_documents_subject")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(documents => documents.Subject)
            .HasDatabaseName("ix_person_record_documents_subject");
    }
}
