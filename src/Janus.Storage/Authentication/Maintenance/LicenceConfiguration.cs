using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Maintenance;

/// <summary>
/// How the licences and permits are stored.
/// </summary>
/// <remarks>Implements OPS-MAINT-001.</remarks>
internal sealed class LicenceConfiguration : IEntityTypeConfiguration<LicenceRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LicenceRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("licences");

        builder.HasKey(licence => licence.Id).HasName("pk_licences");

        builder.Property(licence => licence.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new LicenceId(value));

        builder.Property(licence => licence.Kind)
            .HasColumnName("kind")
            .HasConversion(new VocabularyConverter<LicenceKind>());

        builder.Property(licence => licence.Name).HasColumnName("name");

        builder.Property(licence => licence.ExpiresAt).HasColumnName("expires_at");

        builder.Property(licence => licence.RenewedAt).HasColumnName("renewed_at");
    }
}
