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
/// Implements PRIV-RIGHT-001a and CONV-ENUM-001. The same key as a consent, for the
/// same reason: one decision a purpose, and never a second opinion about the same
/// one.
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
        });

        builder.HasKey(objection => new { objection.Subject, objection.Purpose })
            .HasName("pk_objections");

        builder.Property(objection => objection.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(objection => objection.Purpose).HasColumnName("purpose");
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
    }
}
