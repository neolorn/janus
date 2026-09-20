using System;
using Janus.Core;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Recovery;

/// <summary>
/// How a running loss report is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-RECOV-007 and D-141. One report stands per credential, which the
/// primary key holds: reporting the same credential again does not restart a window
/// somebody is already being notified about.
/// </remarks>
internal sealed class LossReportConfiguration : IEntityTypeConfiguration<LossReportRecord>
{
    /// <summary>The table, which the encrypted column names as its location.</summary>
    public const string Table = "loss_reports";

    /// <summary>The column the token every notice carries is held in.</summary>
    public const string CancelColumn = "enc_cancel";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LossReportRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table => table.HasCheckConstraint(
            "ck_loss_reports_window",
            "invalidates_at > reported_at"));

        builder.HasKey(report => report.Credential).HasName("pk_loss_reports");

        builder.Property(report => report.Credential)
            .HasColumnName("credential")
            .HasConversion(credential => credential.Value, value => new AuthenticatorId(value));

        builder.Property(report => report.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(report => report.Cancel).HasColumnName(CancelColumn);
        builder.Property(report => report.ReportedAt).HasColumnName("reported_at");
        builder.Property(report => report.InvalidatesAt).HasColumnName("invalidates_at");
        builder.Property(report => report.NotifiedAt).HasColumnName("notified_at");
        builder.Property(report => report.AnyDelivered).HasColumnName("any_delivered");
        builder.Property(report => report.HeldAt).HasColumnName("held_at");

        // AUTH-RECOV-007: what the window owes, read without a scan.
        builder.HasIndex(report => report.InvalidatesAt)
            .HasDatabaseName("ix_loss_reports_invalidates_at");

        builder.HasIndex(report => report.NotifiedAt)
            .HasDatabaseName("ix_loss_reports_notified_at");

        builder.HasIndex(report => report.Subject)
            .HasDatabaseName("ix_loss_reports_subject");

        builder.HasOne<AuthenticatorRecord>()
            .WithMany()
            .HasForeignKey(report => report.Credential)
            .HasConstraintName("fk_loss_reports_credential")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(report => report.Subject)
            .HasConstraintName("fk_loss_reports_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
