using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Privacy.Requests;

/// <summary>
/// How a data subject request is stored.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-001, PRIV-RIGHT-002 and CONV-ENUM-001. The type and the
/// status are constrained columns because the code branches on each value, and one
/// the code does not branch on is refused by the database.
/// </remarks>
internal sealed class PrivacyRequestConfiguration : IEntityTypeConfiguration<PrivacyRequestRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PrivacyRequestRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("privacy_requests", table =>
        {
            table.HasCheckConstraint(
                "ck_privacy_requests_type",
                Vocabulary.Admits<PrivacyRequestType>("type"));
            table.HasCheckConstraint(
                "ck_privacy_requests_status",
                Vocabulary.Admits<PrivacyRequestStatus>("status"));
        });

        builder.HasKey(request => request.Id).HasName("pk_privacy_requests");

        builder.Property(request => request.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new PrivacyRequestId(value));

        builder.Property(request => request.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(request => request.Type)
            .HasColumnName("type")
            .HasConversion(new VocabularyConverter<PrivacyRequestType>());

        builder.Property(request => request.Detail).HasColumnName("detail");
        builder.Property(request => request.ReceivedAt).HasColumnName("received_at");
        builder.Property(request => request.CreatedAt).HasColumnName("created_at");
        builder.Property(request => request.DecisionDue).HasColumnName("decision_due");
        builder.Property(request => request.WarnAt).HasColumnName("warn_at");
        builder.Property(request => request.EscalateAt).HasColumnName("escalate_at");

        builder.Property(request => request.Status)
            .HasColumnName("status")
            .HasConversion(new VocabularyConverter<PrivacyRequestStatus>());

        builder.Property(request => request.DecidedAt).HasColumnName("decided_at");
        builder.Property(request => request.DecisionReason).HasColumnName("decision_reason");
        builder.Property(request => request.Channel).HasColumnName("channel");
        builder.Property(request => request.IdentityConfirmation)
            .HasColumnName("identity_confirmation");
        builder.Property(request => request.WarnedAt).HasColumnName("warned_at");
        builder.Property(request => request.EscalatedAt).HasColumnName("escalated_at");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(request => request.Subject)
            .HasConstraintName("fk_privacy_requests_subject")
            .OnDelete(DeleteBehavior.Restrict);

        // A second request of a type the subject already has open is a duplicate, and
        // that is read on the way in to every submission.
        builder.HasIndex(request => request.Subject)
            .HasDatabaseName("ix_privacy_requests_subject");

        // PRIV-RIGHT-002 AC2: the sweep reads what the clock has reached in one query,
        // and a request that was decided is not one the clock reaches.
        builder.HasIndex(request => request.WarnAt)
            .HasDatabaseName("ix_privacy_requests_open")
            .HasFilter("status = 'open'");
    }
}
