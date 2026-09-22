using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Privacy.Records;

/// <summary>
/// How the supplied fields of the records of processing are stored.
/// </summary>
/// <remarks>
/// Implements PRIV-ROPA-001. The check holds the table at one row, so the register
/// cannot read one answer while an endpoint writes another.
/// </remarks>
internal sealed class ComplianceConfiguration : IEntityTypeConfiguration<ComplianceRow>
{
    /// <summary>The table.</summary>
    public const string Table = "compliance_records";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ComplianceRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table => table.HasCheckConstraint(
            "ck_compliance_records_only",
            "id = " + ComplianceRow.Only.ToString(CultureInfo.InvariantCulture)));

        builder.HasKey(record => record.Id).HasName("pk_compliance_records");

        builder.Property(record => record.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(record => record.DataOwner).HasColumnName("data_owner");

        builder.Property(record => record.OrganisationalMeasures)
            .HasColumnName("organisational_measures");

        builder.Property(record => record.AssessmentLinks)
            .HasColumnName("assessment_links")
            .HasColumnType("jsonb");

        builder.Property(record => record.UpdatedAt).HasColumnName("updated_at");
    }
}
