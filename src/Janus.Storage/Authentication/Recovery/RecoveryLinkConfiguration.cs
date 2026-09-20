using System;
using Janus.Authentication.Recovery;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Recovery;

/// <summary>
/// How a recovery link is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-RECOV-002, AUTH-RECOV-005 and CONV-ENUM-001. One link of each
/// purpose is outstanding per account, which the partial unique index holds rather
/// than a read before a write: asking again replaces what went before, so an older
/// message is never a second way in. A spent link is outside that index, because the
/// enrolment session it became stands until it is finished or lapses.
/// </remarks>
internal sealed class RecoveryLinkConfiguration : IEntityTypeConfiguration<RecoveryLinkRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "recovery_links";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RecoveryLinkRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_recovery_links_token",
                $"octet_length(token) = {Fingerprint.Length}");
            table.HasCheckConstraint(
                "ck_recovery_links_purpose",
                Vocabulary.Admits<RecoveryPurpose>("purpose"));
            table.HasCheckConstraint(
                "ck_recovery_links_expiry",
                "expires_at > issued_at");
            table.HasCheckConstraint(
                "ck_recovery_links_session",
                "session IS NULL OR spent_at IS NOT NULL");
        });

        builder.HasKey(link => link.Token).HasName("pk_recovery_links");

        builder.Property(link => link.Token)
            .HasColumnName("token")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(link => link.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(link => link.Purpose)
            .HasColumnName("purpose")
            .HasConversion(new VocabularyConverter<RecoveryPurpose>());

        builder.Property(link => link.IssuedAt).HasColumnName("issued_at");
        builder.Property(link => link.ExpiresAt).HasColumnName("expires_at");

        builder.Property(link => link.Approver)
            .HasColumnName("approver")
            .HasConversion(approver => approver!.Value.Value, value => new SubjectId(value));

        builder.Property(link => link.MailboxLost).HasColumnName("mailbox_lost");

        builder.Property(link => link.Session)
            .HasColumnName("session")
            .HasConversion(session => session!.Value.Value, value => new EnrolmentSessionId(value));

        builder.Property(link => link.SpentAt).HasColumnName("spent_at");

        builder.HasIndex(link => new { link.Subject, link.Purpose })
            .HasDatabaseName("ux_recovery_links_subject_purpose")
            .HasFilter("spent_at IS NULL")
            .IsUnique();

        builder.HasIndex(link => link.Session)
            .HasDatabaseName("ux_recovery_links_session")
            .HasFilter("session IS NOT NULL")
            .IsUnique();

        builder.HasIndex(link => link.ExpiresAt)
            .HasDatabaseName("ix_recovery_links_expires_at");

        builder.HasIndex(link => link.Approver)
            .HasDatabaseName("ix_recovery_links_approver");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(link => link.Subject)
            .HasConstraintName("fk_recovery_links_subject")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(link => link.Approver)
            .HasConstraintName("fk_recovery_links_approver")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
