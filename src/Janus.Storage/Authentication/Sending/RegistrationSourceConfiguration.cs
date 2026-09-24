using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// How a registration session is counted against the source that started it.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-008.</remarks>
internal sealed class RegistrationSourceConfiguration
    : IEntityTypeConfiguration<RegistrationSourceRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RegistrationSourceRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("registration_sources");

        builder.HasKey(started => started.Id).HasName("pk_registration_sources");

        builder.Property(started => started.Id).HasColumnName("id");

        builder.Property(started => started.Source)
            .HasColumnName("source")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(started => started.FingerprintVersion).HasColumnName("fingerprint_version");
        builder.Property(started => started.At).HasColumnName("at");

        builder.HasIndex(started => new { started.Source, started.At })
            .HasDatabaseName("ix_registration_sources_source_at");
    }
}
