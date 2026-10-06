using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Privacy.Consents;

/// <summary>
/// How a consent is stored.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-001, PRIV-CONS-002, PRIV-CONS-004 and CONV-ENUM-001. A row
/// names one purpose, which is what makes a bundled record unrepresentable, and a
/// subject holds one live row a purpose, which the index keeps whatever two grants at
/// once decide (CONV-DESIGN-003); the mechanism and the capture path are constrained
/// columns because the code branches on each value.
/// </remarks>
internal sealed class ConsentConfiguration : IEntityTypeConfiguration<ConsentRecordRow>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ConsentRecordRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("consents", table =>
        {
            table.HasCheckConstraint(
                "ck_consents_mechanism",
                Vocabulary.Admits<ConsentMechanism>("mechanism"));
            table.HasCheckConstraint("ck_consents_kind", Vocabulary.Admits<ConsentKind>("kind"));
            table.HasCheckConstraint("ck_consents_purpose", "length(trim(purpose)) > 0");

            table.HasCheckConstraint(
                "ck_consents_subject_not_max_uuid",
                MaxUuid.Refused("subject"));
        });

        builder.HasKey(consent => consent.Id).HasName("pk_consents");

        builder.Property(consent => consent.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(consent => consent.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(consent => consent.Purpose).HasColumnName("purpose");
        builder.Property(consent => consent.Document).HasColumnName("document");
        builder.Property(consent => consent.NoticeVersion).HasColumnName("notice_version");

        builder.Property(consent => consent.Mechanism)
            .HasColumnName("mechanism")
            .HasConversion(new VocabularyConverter<ConsentMechanism>());

        builder.Property(consent => consent.Kind)
            .HasColumnName("kind")
            .HasConversion(new VocabularyConverter<ConsentKind>());

        builder.Property(consent => consent.GrantedAt).HasColumnName("granted_at");
        builder.Property(consent => consent.WithdrawnAt).HasColumnName("withdrawn_at");
        builder.Property(consent => consent.SupersededAt).HasColumnName("superseded_at");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(consent => consent.Subject)
            .HasConstraintName("fk_consents_subject")
            .OnDelete(DeleteBehavior.Restrict);

        // PRIV-CONS-001 AC4: at most one live record a subject and purpose.
        builder.HasIndex(consent => new { consent.Subject, consent.Purpose })
            .HasDatabaseName("ux_consents_live")
            .IsUnique()
            .HasFilter("withdrawn_at IS NULL AND superseded_at IS NULL");

        // What one subject holds is read whole, oldest first.
        builder.HasIndex(consent => new { consent.Subject, consent.GrantedAt })
            .HasDatabaseName("ix_consents_subject");

        // PRIV-CONS-007: a material revision reads every live consent at once.
        builder.HasIndex(consent => consent.NoticeVersion)
            .HasDatabaseName("ix_consents_live")
            .HasFilter("withdrawn_at IS NULL AND superseded_at IS NULL");
    }
}
