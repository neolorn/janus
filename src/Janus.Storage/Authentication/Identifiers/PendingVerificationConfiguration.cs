using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Identifiers;

/// <summary>
/// How a verification a live account has outstanding is stored.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-004, REG-IDENT-007 and CONV-ENUM-001. One verification is
/// outstanding per identifier, which the key holds rather than a read before a write,
/// and a link fingerprint answers for one row only.
/// </remarks>
internal sealed class PendingVerificationConfiguration
    : IEntityTypeConfiguration<PendingVerificationRecord>
{
    /// <summary>The table, which the encrypted column names as its location.</summary>
    public const string Table = "identifier_verifications";

    /// <summary>The column the value being proved is held in.</summary>
    public const string StagedColumn = "enc_staged";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PendingVerificationRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_identifier_verifications_link",
                $"link IS NULL OR octet_length(link) = {Fingerprint.Length}");
            table.HasCheckConstraint(
                "ck_identifier_verifications_old_link",
                $"old_link IS NULL OR octet_length(old_link) = {Fingerprint.Length}");

            // REG-IDENT-007: only a replacement displaces an address, so only a
            // replacement has one to ask.
            table.HasCheckConstraint(
                "ck_identifier_verifications_old",
                "is_replacement OR (NOT old_must_confirm AND old_confirmed_at IS NULL "
                    + "AND old_link IS NULL)");
        });

        builder.HasKey(pending => pending.Identifier).HasName("pk_identifier_verifications");

        builder.Property(pending => pending.Identifier)
            .HasColumnName("identifier_id")
            .HasConversion(id => id.Value, value => new IdentifierId(value));

        builder.Property(pending => pending.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(pending => pending.Browser)
            .HasColumnName("browser")
            .HasConversion(session => session!.Value.Value, value => new SessionId(value));

        builder.Property(pending => pending.IsReplacement).HasColumnName("is_replacement");
        builder.Property(pending => pending.OldMustConfirm).HasColumnName("old_must_confirm");
        builder.Property(pending => pending.OldConfirmedAt).HasColumnName("old_confirmed_at");

        builder.Property(pending => pending.OldLink)
            .HasColumnName("old_link")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(pending => pending.Link)
            .HasColumnName("link")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(pending => pending.StagedAt).HasColumnName("staged_at");
        builder.Property(pending => pending.Staged).HasColumnName(StagedColumn);

        builder.HasIndex(pending => pending.Link)
            .HasDatabaseName("ux_identifier_verifications_link")
            .IsUnique()
            .HasFilter("link IS NOT NULL");

        builder.HasIndex(pending => pending.OldLink)
            .HasDatabaseName("ux_identifier_verifications_old_link")
            .IsUnique()
            .HasFilter("old_link IS NOT NULL");

        builder.HasIndex(pending => pending.StagedAt)
            .HasDatabaseName("ix_identifier_verifications_staged_at");

        builder.HasIndex(pending => pending.Subject)
            .HasDatabaseName("ix_identifier_verifications_subject");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(pending => pending.Subject)
            .HasConstraintName("fk_identifier_verifications_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
