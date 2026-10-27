using System;
using Janus.Core;
using Janus.Privacy.Outbox;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Privacy.Outbox;

/// <summary>
/// How a delivery is stored.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003a, PRIV-RIGHT-001a, CONV-ENUM-001 and CONV-DESIGN-003. The
/// kind, the status and the change are constrained columns because the code branches on
/// each value, and one the code does not branch on is refused by the database.
/// </remarks>
internal sealed class DeliveryConfiguration : IEntityTypeConfiguration<DeliveryRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DeliveryRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox", table =>
        {
            table.HasCheckConstraint(
                "ck_outbox_kind",
                Vocabulary.Admits<SubjectEventKind>("kind"));
            table.HasCheckConstraint(
                "ck_outbox_status",
                Vocabulary.Admits<ErasureStatus>("status"));
            table.HasCheckConstraint(
                "ck_outbox_reason",
                Vocabulary.Admits<ErasureReason>("reason"));
            table.HasCheckConstraint(
                "ck_outbox_change",
                Vocabulary.Admits<DeliveryChange>("change"));

            // PRIV-RIGHT-001a, D-203: a consent or an objection change names its purpose and
            // its change, and no other fact names either.
            table.HasCheckConstraint(
                "ck_outbox_purpose",
                "(kind IN ('consent-changed', 'objection-changed')"
                    + " AND purpose IS NOT NULL AND length(trim(purpose)) > 0 AND change IS NOT NULL)"
                    + " OR (kind NOT IN ('consent-changed', 'objection-changed')"
                    + " AND purpose IS NULL AND change IS NULL)");

            table.HasCheckConstraint("ck_outbox_attempts", "attempts >= 0");

            table.HasCheckConstraint(
                "ck_outbox_subject_not_max_uuid",
                MaxUuid.Refused("subject"));
        });

        builder.HasKey(delivery => delivery.Id).HasName("pk_outbox");

        builder.Property(delivery => delivery.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new DeliveryId(value));

        builder.Property(delivery => delivery.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(delivery => delivery.Kind)
            .HasColumnName("kind")
            .HasConversion(new VocabularyConverter<SubjectEventKind>());

        builder.Property(delivery => delivery.RaisedAt).HasColumnName("raised_at");

        builder.Property(delivery => delivery.Restricted).HasColumnName("restricted");

        builder.Property(delivery => delivery.Reason)
            .HasColumnName("reason")
            .HasConversion(new VocabularyConverter<ErasureReason>());

        builder.Property(delivery => delivery.Purpose).HasColumnName("purpose");

        builder.Property(delivery => delivery.Change)
            .HasColumnName("change")
            .HasConversion(new VocabularyConverter<DeliveryChange>());

        builder.Property(delivery => delivery.Status)
            .HasColumnName("status")
            .HasConversion(new VocabularyConverter<ErasureStatus>());

        builder.Property(delivery => delivery.Attempts).HasColumnName("attempts");

        builder.Property(delivery => delivery.NextAttemptAt).HasColumnName("next_attempt_at");

        builder.Property(delivery => delivery.ClaimedUntil).HasColumnName("claimed_until");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(delivery => delivery.Subject)
            .HasConstraintName("fk_outbox_subject")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(delivery => delivery.Subject).HasDatabaseName("ix_outbox_subject");

        builder.HasMany(delivery => delivery.Confirmations)
            .WithOne()
            .HasForeignKey(confirmation => confirmation.Delivery)
            .HasConstraintName("fk_outbox_confirmations_delivery")
            .OnDelete(DeleteBehavior.Cascade);

        // IDN-LIFE-003a AC4: the worker reads what is due in one query, and a
        // delivery that is closed is not a delivery that is due.
        builder.HasIndex(delivery => delivery.NextAttemptAt)
            .HasDatabaseName("ix_outbox_due")
            .HasFilter("status = 'awaiting-subscribers'");
    }
}
