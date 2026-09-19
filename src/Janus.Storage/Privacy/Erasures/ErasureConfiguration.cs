using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Privacy.Erasures;

/// <summary>
/// How an erasure is stored.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003b, CONV-ENUM-001 and CONV-DESIGN-003. The status is a
/// constrained column because the code branches on each value, and a status the code
/// does not branch on is refused by the database.
/// </remarks>
internal sealed class ErasureConfiguration : IEntityTypeConfiguration<ErasureRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ErasureRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("erasures", table =>
        {
            table.HasCheckConstraint(
                "ck_erasures_status",
                Vocabulary.Admits<ErasureStatus>("status"));
            table.HasCheckConstraint(
                "ck_erasures_reason",
                Vocabulary.Admits<ErasureReason>("reason"));
            table.HasCheckConstraint("ck_erasures_attempts", "attempts >= 0");
        });

        builder.HasKey(erasure => erasure.Subject).HasName("pk_erasures");

        builder.Property(erasure => erasure.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(erasure => erasure.RequestedAt).HasColumnName("requested_at");

        builder.Property(erasure => erasure.Reason)
            .HasColumnName("reason")
            .HasConversion(new VocabularyConverter<ErasureReason>());

        builder.Property(erasure => erasure.Status)
            .HasColumnName("status")
            .HasConversion(new VocabularyConverter<ErasureStatus>());

        builder.Property(erasure => erasure.Attempts).HasColumnName("attempts");

        builder.HasOne<AccountRecord>()
            .WithOne()
            .HasForeignKey<ErasureRecord>(erasure => erasure.Subject)
            .HasConstraintName("fk_erasures_subject")
            .OnDelete(DeleteBehavior.Restrict);

        // IDN-LIFE-003b AC2: every outstanding erasure in one query.
        builder.HasIndex(erasure => erasure.RequestedAt)
            .HasDatabaseName("ix_erasures_outstanding")
            .HasFilter("status <> 'complete'");
    }
}
