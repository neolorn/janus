using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// How the admitted export operations are counted.
/// </summary>
/// <remarks>Implements OPS-ALERT-006.</remarks>
internal sealed class BulkExportConfiguration : IEntityTypeConfiguration<BulkExportRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BulkExportRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("bulk_exports", table => table.HasCheckConstraint(
            "ck_bulk_exports_actor",
            "(actor IS NULL) <> (principal IS NULL)"));

        builder.HasKey(export => export.Id).HasName("pk_bulk_exports");

        builder.Property(export => export.Id).HasColumnName("id");

        builder.Property(export => export.Actor)
            .HasColumnName("actor")
            .HasConversion(
                actor => actor!.Value.Value,
                value => new SubjectId(value));

        builder.Property(export => export.Principal).HasColumnName("principal");

        builder.Property(export => export.AdmittedAt).HasColumnName("admitted_at");

        // The limit counts one actor's last hour.
        builder.HasIndex(export => new { export.Actor, export.Principal, export.AdmittedAt })
            .HasDatabaseName("ix_bulk_exports_actor");
    }
}
