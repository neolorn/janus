using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Privacy.Exports;

/// <summary>
/// How the exports an account has taken are stored.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-003 and D-086. The index is on the account and the instant
/// together, because the only question asked of this table is how many rows one
/// account has in the last window.
/// </remarks>
internal sealed class ExportConfiguration : IEntityTypeConfiguration<ExportRecordRow>
{
    /// <summary>The table.</summary>
    public const string Table = "privacy_exports";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ExportRecordRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table);

        builder.HasKey(export => export.Id).HasName("pk_privacy_exports");

        builder.Property(export => export.Id).HasColumnName("id");

        builder.Property(export => export.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(export => export.AssembledAt).HasColumnName("assembled_at");

        builder.HasIndex(export => new { export.Subject, export.AssembledAt })
            .HasDatabaseName("ix_privacy_exports_subject");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(export => export.Subject)
            .HasConstraintName("fk_privacy_exports_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
