using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Privacy.Consents;

/// <summary>
/// How an objection is stored.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-001a and CONV-ENUM-001. Keyed as a consent is, for the same
/// reason: a row an objection, and one standing row a subject and purpose.
/// </remarks>
internal sealed class ObjectionConfiguration : IEntityTypeConfiguration<ObjectionRecordRow>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ObjectionRecordRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("objections", table =>
        {
            table.HasCheckConstraint(
                "ck_objections_mechanism",
                Vocabulary.Admits<ConsentMechanism>("mechanism"));
            table.HasCheckConstraint("ck_objections_purpose", "length(trim(purpose)) > 0");

            table.HasCheckConstraint(
                "ck_objections_subject_not_max_uuid",
                MaxUuid.Refused("subject"));
        });

        builder.HasKey(objection => objection.Id).HasName("pk_objections");

        builder.Property(objection => objection.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(objection => objection.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(objection => objection.Purpose).HasColumnName("purpose");
        builder.Property(objection => objection.Document).HasColumnName("document");
        builder.Property(objection => objection.NoticeVersion).HasColumnName("notice_version");

        builder.Property(objection => objection.Mechanism)
            .HasColumnName("mechanism")
            .HasConversion(new VocabularyConverter<ConsentMechanism>());

        builder.Property(objection => objection.RecordedAt).HasColumnName("recorded_at");
        builder.Property(objection => objection.WithdrawnAt).HasColumnName("withdrawn_at");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(objection => objection.Subject)
            .HasConstraintName("fk_objections_subject")
            .OnDelete(DeleteBehavior.Restrict);

        // PRIV-RIGHT-001a AC6: at most one standing objection a subject and purpose.
        builder.HasIndex(objection => new { objection.Subject, objection.Purpose })
            .HasDatabaseName("ux_objections_standing")
            .IsUnique()
            .HasFilter("withdrawn_at IS NULL");

        // What one subject holds is read whole, oldest first.
        builder.HasIndex(objection => new { objection.Subject, objection.RecordedAt })
            .HasDatabaseName("ix_objections_subject");
    }
}
