using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Privacy.Bases;

/// <summary>
/// How the declared lawful bases are stored.
/// </summary>
/// <remarks>
/// Implements PRIV-BASIS-001 and CONV-ENUM-001. A basis is a row and no constrained
/// column, because a jurisdiction declares its own list and the code reads the flags.
/// </remarks>
internal sealed class LawfulBasisConfiguration : IEntityTypeConfiguration<LawfulBasisRow>
{
    /// <summary>The table.</summary>
    public const string Table = "lawful_bases";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LawfulBasisRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table);

        builder.HasKey(basis => basis.Key).HasName("pk_lawful_bases");

        builder.Property(basis => basis.Key).HasColumnName("key");
        builder.Property(basis => basis.Label).HasColumnName("label");
        builder.Property(basis => basis.IsConsent).HasColumnName("is_consent");
        builder.Property(basis => basis.RequiresWrittenConsentForSensitive)
            .HasColumnName("requires_written_consent_for_sensitive");
        builder.Property(basis => basis.RequiresAssessment).HasColumnName("requires_assessment");
        builder.Property(basis => basis.IsObjectable).HasColumnName("is_objectable");
    }
}
