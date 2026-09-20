using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Recovery;

/// <summary>
/// How one approval of a re-enrolment is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-RECOV-002 and AUTH-RECOV-003. Approver, account and instant are
/// the key: one approver approving one account at one instant is one approval, and
/// the row stands after the link goes out because both day limits count what was
/// spent as well as what still stands.
/// </remarks>
internal sealed class RecoveryApprovalConfiguration
    : IEntityTypeConfiguration<RecoveryApprovalRecord>
{
    /// <summary>The table, which the encrypted column names as its location.</summary>
    public const string Table = "recovery_approvals";

    /// <summary>The column the confirmed channel is held in.</summary>
    public const string ChannelColumn = "enc_channel";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RecoveryApprovalRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table);

        builder.HasKey(approval => new { approval.Subject, approval.Approver, approval.At })
            .HasName("pk_recovery_approvals");

        builder.Property(approval => approval.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(approval => approval.Approver)
            .HasColumnName("approver")
            .HasConversion(approver => approver.Value, value => new SubjectId(value));

        builder.Property(approval => approval.At).HasColumnName("approved_at");
        builder.Property(approval => approval.Channel).HasColumnName(ChannelColumn);
        builder.Property(approval => approval.SpentAt).HasColumnName("spent_at");

        // AUTH-RECOV-002: the approver's own day limit, counted without a scan.
        builder.HasIndex(approval => new { approval.Approver, approval.At })
            .HasDatabaseName("ix_recovery_approvals_approver");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(approval => approval.Subject)
            .HasConstraintName("fk_recovery_approvals_subject")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(approval => approval.Approver)
            .HasConstraintName("fk_recovery_approvals_approver")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
